using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using Org.BouncyCastle.Tls;
using ReCap.Server.Adapters.Blaze.Component;
using ReCap.Server.Adapters.Blaze.Ssl;

namespace ReCap.Server.Adapters.Blaze;

public class Client
{
    private static readonly Dictionary<(ushort, ushort), DateTime> _lastLogTime = new();
    private static readonly TimeSpan _logThrottle = TimeSpan.FromSeconds(30);
    private static readonly HashSet<(ushort component, ushort command)> _throttledPackets = new()
    {
        (0x7802, 0x19), // UserSessions -> updateUserSessionClientData
    };

    private byte[] ReceiveBuffer { get; }

    private TcpClient TcpClient { get; }
    private TlsServerProtocol? SslStream { get; }
    private Stream CommStream { get; set; }

    public BlazeServer Server { get; }
    [MemberNotNullWhen(true, nameof(SslStream))]
    public bool IsSecure => Server.IsSecure;
    public IPEndPoint EndPoint { get; } = null!;
    public Action? OnDisconnect { get; set; }

    public ulong UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string AuthToken { get; set; }
    public UserSessionExtendedData ExtendedData { get; } = new();

    public Client(BlazeServer server, TcpClient tcpClient)
    {
        ReceiveBuffer = ArrayPool<byte>.Shared.Rent(0x10000);

        Server = server;
        TcpClient = tcpClient;
        CommStream = TcpClient.GetStream();

        if (TcpClient.Client.RemoteEndPoint is IPEndPoint remoteEndPoint)
            EndPoint = new IPEndPoint(remoteEndPoint.Address, remoteEndPoint.Port);

        if (Server.IsSecure)
        {
            SslStream = new TlsServerProtocol(TcpClient.GetStream());

            Task.Run(Authenticate);
        }
        else
            Task.Run(Receive);
    }

    ~Client()
    {
        ArrayPool<byte>.Shared.Return(ReceiveBuffer);
    }

    public void Notify(Tdf? value, ushort component, ushort command)
    {
        var packet = new Packet(0, component, command, 0, 0, PacketType.Notification, PacketOptions.None);

        if (value is not null)
            packet.SetContent(value);

        SendPacket(packet);
    }

    public void RespondTo(Packet request, Tdf? response = null, uint error = 0)
    {
        var packet = new Packet(request.Id, request.Component, request.Command, error, 0, error == 0 ? PacketType.Reply : PacketType.ErrorReply, PacketOptions.None);

        if (response is not null)
            packet.SetContent(response);

        SendPacket(packet);
    }

    private void SendPacket(Packet packet)
    {
        if (!IsThrottled(packet.Component, packet.Command))
            Log($"Sending {packet.ToString(Server.GetComponentAndCommandName(packet.Component, packet.Command, packet.Type == PacketType.Notification))}...");

        packet.WriteTo(CommStream);

        CommStream.Flush();
    }

    public void Disconnect()
    {
        Log($"Disconnecting...");

        CommStream.Close();

        Server.Disconnect(this);

        OnDisconnect?.Invoke();
    }

    private async void Authenticate()
    {
        if (!Server.IsSecure || SslStream is null)
            return;

        var connTls = new Ssl3TlsServer(Server.Crypto, Server.Cert, Server.Key);

        try
        {
            SslStream.Accept(connTls);

            CommStream = SslStream.Stream;
        }
        catch (Exception ex)
        {
            ReCap.Server.Util.Logging.Log.Blaze.Error(ex, "Failed to accept connection");

            SslStream.Flush();
            SslStream.Close();

            connTls.Cancel();

            return;
        }

        _ = Task.Run(Receive);
    }

    private async void Receive()
    {
        var writeOffset = 0;

        while (TcpClient.Connected)
        {
            int bytesRead;

            try
            {
                bytesRead = await CommStream.ReadAsync(ReceiveBuffer.AsMemory(writeOffset, ReceiveBuffer.Length - writeOffset));
            }
            catch (IOException)
            {
                break;
            }

            if (bytesRead <= 0)
                break;

            writeOffset += bytesRead;

            if (writeOffset <= 0)
                continue;

            try
            {
                while (writeOffset >= Packet.SmallestValidHeaderSize)
                {
                    using var ms = new MemoryStream(ReceiveBuffer, 0, writeOffset, false);

                    var packet = Packet.Parse(ms);
                    if (packet is null)
                        break; // Need more data for a complete packet

                    var totalPacketLength = (int)ms.Position;

                    if (!IsThrottled(packet.Component, packet.Command))
                    {
                        Log($"Incoming packet: {packet.ToString(Server.GetComponentAndCommandName(packet.Component, packet.Command, packet.Type == PacketType.Notification))}");
                    }

                    Server.HandlePacket(this, packet);

                    // Shift extra read bytes to the beginning of the buffer, if any
                    if (writeOffset > totalPacketLength)
                        Array.Copy(ReceiveBuffer, totalPacketLength, ReceiveBuffer, 0, writeOffset - totalPacketLength);

                    writeOffset -= totalPacketLength;
                }
            }
            catch (Exception e)
            {
                Log($"Exception while handling incoming packet! Exception: {e}");
                Disconnect();
                return;
            }

            CommStream.Flush();
        }

        Disconnect();
    }

    private static bool IsThrottled(ushort component, ushort command)
    {
        if (!_throttledPackets.Contains((component, command)))
            return false;

        var key = (component, command);
        var now = DateTime.UtcNow;

        lock (_lastLogTime)
        {
            if (_lastLogTime.TryGetValue(key, out var last) && now - last < _logThrottle)
                return true;

            _lastLogTime[key] = now;
        }

        return false;
    }

    private void Log(string message) => ReCap.Server.Util.Logging.Log.Blaze.Debug($"[{Server.Name} {EndPoint}] {message}");
}

