using System.Net;
using RakNexus.Network;
using RakNexus.Protocol;
using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Adapters.RakNet.Packets;
using ReCap.Server.Adapters.Blaze.Component.GameManager;
using ReCap.Server.Config;
using ReCap.Server.Services;
using ReCap.Server.Util;

namespace ReCap.Server.Adapters.RakNet;

public class RakNetServer
{
    private AccountService accountService;
    private GameService gameService;
    private AssetDatabase? assetDatabase;

    public Dictionary<ulong, RakNetClient> Clients { get; } = new();
    public Dictionary<ulong, Game> Games { get; } = new();
    public Dictionary<ulong, ulong> GameAssigments { get; } = new();

    public RakNetListener Listener { get; }
    public bool IsRunning { get; private set; }
    public ulong GameCounter { get; private set; } = 0x0080000000000001;

    public RakNetServer(SqliteConfig newSqliteConfig, string name, IPAddress hostAddress, int port, bool isSecure, string hostname, AssetDatabase? assetDatabase = null, GameService? sharedGameService = null)
    {
        accountService = new AccountService(newSqliteConfig);
        gameService = sharedGameService ?? new GameService();
        this.assetDatabase = assetDatabase;

        Listener = new RakNetListener(port);
        Listener.SessionConnected += OnSessionConnected;
    }

    private void OnSessionConnected(RakNetSession session)
    {
        Logger.info($"RakNet: Peer 0x{session.Guid.G:X16} connected {session.Address}!");

        if (Clients.ContainsKey(session.Guid.G))
        {
            Logger.error($"RakNet: Peer 0x{session.Guid.G:X16} already has an assigned client!");
            return;
        }

        session.PacketReceived += packet => OnSessionReceiveRaw(session, packet);
        session.OnNewIncomingConnection += () => OnSessionOnNewIncomingConnection(session);
        session.Disconnected += reason => OnSessionDisconnected(session);

        Clients.Add(session.Guid.G, new RakNetClient(session));
    }

    private void OnSessionDisconnected(RakNetSession session)
    {
        Logger.info($"RakNet: Peer 0x{session.Guid.G:X16} disconnected {session.Address}!");

        if (!Clients.Remove(session.Guid.G))
            Logger.error($"RakNet: Peer 0x{session.Guid.G:X16} had no assigned client!");
    }

    private void OnSessionOnNewIncomingConnection(RakNetSession session) => SendPacket(session, new ConnectedPacket());

    private void OnSessionReceiveRaw(RakNetSession session, Packet rakPacket)
    {
        var data = rakPacket.Data;
        var packetType = (PacketType)data[0];

        Logger.info($"RakNet: Receiving {packetType} packet from {session.Address}! Data: {BitConverter.ToString(data)}!");

        var packet = PacketActivator.CreateInstance(data);
        if (packet is null)
        {
            Logger.error($"RakNet: Peer 0x{session.Guid.G:X16} has sent an unhandled packet ({packetType})! Skipping...");
            return;
        }

        if (!Clients.TryGetValue(session.Guid.G, out var client))
        {
            Logger.error($"RakNet: No client was found for peer 0x{session.Guid.G:X16}, but received a packet ({packetType})!");
            return;
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

                client.Game = game;
                game.AttachPlayer(account, client);
                return;
        }

        if (client.Game is not null)
        {
            client.Game.HandlePacket(client, packet);
            return;
        }

        Logger.error($"RakNet: Peer 0x{session.Guid.G:X16} has no game, but received a packet ({packetType}) intended for a game!");
    }

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _ = Task.Run(() => Listener.StartAsync(), stoppingToken);

        IsRunning = true;

        Logger.info($"[RakNet]: Started listening!");

        try
        {
            while (IsRunning)
            {
                var games = gameService.GetAllGames();
                foreach (var game in games)
                {
                    game.Update();
                }
                await Task.Delay(50, stoppingToken);
            }
        }
        catch (TaskCanceledException)
        {
        }

        Listener.Stop();

        Logger.info("RakNet: Stopped listening!");
    }

    public void SendPacket(ulong guid, IRakNetPacket packet, PacketReliability reliability = PacketReliability.RELIABLE_ORDERED)
    {
        if (Clients.TryGetValue(guid, out var client))
            SendPacket(client.Session, packet, reliability);
        else
            Logger.error($"No session found for session id: {guid}, unable to send packet {packet.Type} to it!");
    }

    public static void SendPacket(RakNetClient client, IRakNetPacket packet, PacketReliability reliability = PacketReliability.RELIABLE_ORDERED)
        => SendPacket(client.Session, packet, reliability);

    public static void SendPacket(RakNetSession session, IRakNetPacket packet, PacketReliability reliability = PacketReliability.RELIABLE_ORDERED)
    {
        using var ms = new MemoryStream();

        ms.WriteByte((byte)packet.Type);

        packet.WriteTo(ms);

        {
            var bytes = ms.ToArray();
            string hex;
            if (bytes.Length <= 32)
            {
                hex = BitConverter.ToString(bytes);
            }
            else if (packet.Type == PacketType.LabsPlayerUpdate || packet.Type == PacketType.GamePrepareForStart || packet.Type == PacketType.ChainVoteMsgs)
            {
                hex = BitConverter.ToString(bytes) + $" ({bytes.Length}B)";
            }
            else
            {
                hex = BitConverter.ToString(bytes, 0, 32) + $"...({bytes.Length}B)";
            }
            Logger.info($"RakNet: Sent {packet.Type} [{hex}]");
        }

        session.Send(ms.ToArray(), PacketPriority.MEDIUM_PRIORITY, reliability, 0, 0);
    }
}
