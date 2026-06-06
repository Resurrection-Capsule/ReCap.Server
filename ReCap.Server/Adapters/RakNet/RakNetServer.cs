using System.Net;
using RakNexus.Network;
using RakNexus.Protocol;
using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Adapters.RakNet.Packets;
using ReCap.Server.Adapters.Blaze.Component.GameManager;
using ReCap.Server.Config;
using ReCap.Server.Services;
using ReCap.Server.Util.Logging;

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
        Log.RakNet.Info($"Peer 0x{session.Guid.G:X16} connected {session.Address}");

        if (Clients.ContainsKey(session.Guid.G))
        {
            Log.RakNet.Error($"Peer 0x{session.Guid.G:X16} already has an assigned client");
            return;
        }

        session.PacketReceived += packet => OnSessionReceiveRaw(session, packet);
        session.OnNewIncomingConnection += () => OnSessionOnNewIncomingConnection(session);
        session.Disconnected += reason => OnSessionDisconnected(session);

        Clients.Add(session.Guid.G, new RakNetClient(session));
    }

    private void OnSessionDisconnected(RakNetSession session)
    {
        Log.RakNet.Info($"Peer 0x{session.Guid.G:X16} disconnected {session.Address}");

        if (!Clients.Remove(session.Guid.G))
            Log.RakNet.Error($"Peer 0x{session.Guid.G:X16} had no assigned client");
    }

    private void OnSessionOnNewIncomingConnection(RakNetSession session) => SendPacket(session, new ConnectedPacket());

    private void OnSessionReceiveRaw(RakNetSession session, Packet rakPacket)
    {
        var data = rakPacket.Data;
        var packetType = (PacketType)data[0];

        if (Log.RakNet.IsEnabled(Serilog.Events.LogEventLevel.Verbose))
            Log.RakNet.Verbose(PacketTrace.Received(packetType.ToString(), data, session.Address.ToString()));

        var packet = PacketActivator.CreateInstance(data);
        if (packet is null)
        {
            Log.RakNet.Warn($"Peer 0x{session.Guid.G:X16} sent an unhandled packet ({packetType}); skipping");
            return;
        }

        if (!Clients.TryGetValue(session.Guid.G, out var client))
        {
            Log.RakNet.Error($"No client for peer 0x{session.Guid.G:X16}, but received a packet ({packetType})");
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

        Log.RakNet.Error($"Peer 0x{session.Guid.G:X16} has no game, but received a packet ({packetType}) intended for a game");
    }

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _ = Task.Run(() => Listener.StartAsync(), stoppingToken);

        IsRunning = true;

        Log.RakNet.Info("Started listening");

        try
        {
            while (IsRunning)
            {
                var games = gameService.GetAllGames();
                foreach (var game in games)
                {
                    // One game's failure must not kill the update loop for every game —
                    // ExecuteAsync runs fire-and-forget, so an escaped exception dies silently
                    // and every scheduled coroutine stalls forever (2026-06-06 Pummel gate).
                    try
                    {
                        game.Update();
                    }
                    catch (Exception ex)
                    {
                        Log.RakNet.Error($"Game {game.Id} Update failed: {ex}");
                    }
                }
                await Task.Delay(50, stoppingToken);
            }
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.RakNet.Fatal($"Game update loop died: {ex}");
        }

        Listener.Stop();

        Log.RakNet.Info("Stopped listening");
    }

    public void SendPacket(ulong guid, IRakNetPacket packet, PacketReliability reliability = PacketReliability.RELIABLE_ORDERED)
    {
        if (Clients.TryGetValue(guid, out var client))
            SendPacket(client.Session, packet, reliability);
        else
            Log.RakNet.Error($"No session for id {guid}; unable to send {packet.Type}");
    }

    public static void SendPacket(RakNetClient client, IRakNetPacket packet, PacketReliability reliability = PacketReliability.RELIABLE_ORDERED)
        => SendPacket(client.Session, packet, reliability);

    private static readonly LogThrottle _gameStateThrottle = new(20);

    public static void SendPacket(RakNetSession session, IRakNetPacket packet, PacketReliability reliability = PacketReliability.RELIABLE_ORDERED)
    {
        using var ms = new MemoryStream();

        ms.WriteByte((byte)packet.Type);

        packet.WriteTo(ms);
        var bytes = ms.ToArray();

        if (Log.RakNet.IsEnabled(Serilog.Events.LogEventLevel.Verbose))
        {
            if (packet.Type == PacketType.GameState)
            {
                if (_gameStateThrottle.ShouldLog())
                    Log.RakNet.Verbose(PacketTrace.Sent($"GameState (×{_gameStateThrottle.Count})", bytes));
            }
            else
            {
                Log.RakNet.Verbose(PacketTrace.Sent(packet.Type.ToString(), bytes));
            }
        }

        session.Send(bytes, PacketPriority.MEDIUM_PRIORITY, reliability, 0, 0);
    }
}
