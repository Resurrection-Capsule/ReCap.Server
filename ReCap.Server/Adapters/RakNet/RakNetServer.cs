using System.Net;
// using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharpRakNet.Network;
using SharpRakNet.Protocol.Raknet;
using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Adapters.RakNet.Packets;
using ReCap.Server.Adapters.Blaze.Component.GameManager;
using ReCap.Server.Config;
using ReCap.Server.Services;
using ReCap.Server.Utils;

namespace ReCap.Server.Adapters.RakNet;

public class RakNetServer
{
    private AccountService accountService;
    private GameService gameService;

    public Dictionary<ulong, RakNetClient> Clients { get; } = new();    // RakNet Guid -> Client
    public Dictionary<ulong, Game> Games { get; } = new();              // GameId -> Game
    public Dictionary<ulong, ulong> GameAssigments { get; } = new();    // UserId -> GameId

    public RaknetListener Listener { get; }
    public bool IsRunning { get; private set; }
    public ulong GameCounter { get; private set; } = 0x0080000000000001;

    public RakNetServer(SqliteConfig newSqliteConfig, string name, IPAddress hostAddress, int port, bool isSecure, string hostname)
    {
        accountService = new AccountService(newSqliteConfig);
        gameService = new GameService();

        Listener = new RaknetListener(new IPEndPoint(hostAddress, port))
        {
            SessionConnected = OnSessionConnected,
            SessionDisconnected = OnSessionDisconnected
        };
    }

    private void OnSessionConnected(RaknetSession session)
    {
        Logger.info($"RakNet: Peer 0x{session.Guid} connected {session.PeerEndPoint}!");

        if (Clients.ContainsKey(session.Guid))
        {
            Logger.error($"RakNet: Peer 0x{session.Guid} already has an assigned client!");
            return;
        }

        session.SessionReceiveRaw += OnSessionReceiveRaw;
        session.SessionOnNewIncomingConnection += OnSessionOnNewIncomingConnection;

        Clients.Add(session.Guid, new RakNetClient(session));
    }

    private void OnSessionDisconnected(RaknetSession session)
    {
        Logger.info($"RakNet: Peer 0x{session.Guid} disconnected {session.PeerEndPoint}!");

        if (Clients.TryGetValue(session.Guid, out var client))
        {
            // TODO: signal it to the game?

            session.SessionReceiveRaw -= OnSessionReceiveRaw;
            session.SessionOnNewIncomingConnection -= OnSessionOnNewIncomingConnection;

            Clients.Remove(session.Guid);
        }
        else
            Logger.error($"RakNet: Peer 0x{session.Guid} had no assigned client!");
    }

    private void OnSessionOnNewIncomingConnection(RaknetSession session) => SendPacket(session, new ConnectedPacket());

    private bool OnSessionReceiveRaw(RaknetSession session, byte[] data)
    {
        var packetType = (PacketType)data[0];

        if (true) // packetType != PacketType.ClockSync
        {
            Logger.info($"RakNet: Receiving {packetType} packet from {session.PeerEndPoint}! Data: {BitConverter.ToString(data)}!");
        }

        var packet = PacketActivator.CreateInstance(data);
        if (packet is null)
        {
            Logger.error($"RakNet: Peer 0x{session.Guid} has sent an unhandled packet ({packetType})! Skipping...");

            return false;
        }

        if (!Clients.TryGetValue(session.Guid, out var client))
        {
            Logger.error($"RakNet: No client was found for peer 0x{session.Guid}, but received a packet ({packetType})!");

            return false;
        }

        switch (packet)
        {
            case HelloPlayerRequestPacket helloPlayerRequestPacket:
                client.UserId = helloPlayerRequestPacket.UserId;
                client.PlaygroupId = helloPlayerRequestPacket.PlaygroupId;
                
                var account = accountService.getAccountById(client.UserId);
                var game = gameService.GetGameByPlayer(account);
                if (game == null)
                {
                    game = gameService.CreateGame();
                    gameService.AddPlayerToGame(game.Id, account);
                }

                // Clients.Remove(session.Guid);

                // session.Disconnect();

                return true;
        }

        if (client.Game is not null)
        {
            client.Game.HandlePacket(client, packet);
            return true;
        }

        Logger.error($"RakNet: Peer 0x{session.Guid} has no game, but received a packet ({packetType}) intended for a game!");

        return false;
    }

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Listener.BeginListener();

        IsRunning = true;

        Logger.info($"[RakNet]: Started listening on {Listener.Socket.Socket.Client.LocalEndPoint}!");

        try
        {
            // Definitely the wrong way to do this but I don't have a lot of experience with C# multithreading...
            while (IsRunning)
            {
                var games = gameService.GetAllGames();
                foreach (var game in games)
                {
                    game.Update();
                }
                await Task.Delay(100, stoppingToken);
            }
        }
        catch (TaskCanceledException)
        {
        }

        Listener.StopListener();

        Logger.info("RakNet: Stopped listening!");
    }

    public void SendPacket(ulong guid, IRakNetPacket packet, Reliability reliability = Reliability.ReliableOrdered)
    {
        if (Clients.TryGetValue(guid, out var client))
            SendPacket(client.Session, packet, reliability);
        else
            Logger.error($"No session found for session id: {guid}, unable to send packet {packet.Type} to it!");
    }

    public static void SendPacket(RakNetClient client, IRakNetPacket packet, Reliability reliability = Reliability.ReliableOrdered)
        => SendPacket(client.Session, packet, reliability);
    
    public static void SendPacket(RaknetSession session, IRakNetPacket packet, Reliability reliability = Reliability.ReliableOrdered)
    {
        using var ms = new MemoryStream();

        ms.WriteByte((byte)packet.Type);

        packet.WriteTo(ms);

        session.Sendq.Insert(reliability, ms.ToArray());
    }
}
