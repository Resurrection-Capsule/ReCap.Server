using ReCap.Server.Adapters.RakNet;
using ReCap.Server.Adapters.RakNet.Packets;

using ReCap.Server.Adapters.Blaze.Component.GameManager;
using ReCap.Server.Models;

namespace ReCap.Server.Domain.Gameplay;

public class Game(ulong id, GameType gameType) : IGame
{
    public Dictionary<ulong, byte> ExpectedPlayers { get; } = new();
    public Dictionary<ulong, Player> Players { get; } = new();
    public Dictionary<byte, Bot> Bots { get; } = new();

    public Dictionary<ulong, AccountModel> Clients { get; } = new();
    public Dictionary<byte, AccountModel> ClientsBySlot { get; } = new();

    public DateTime StartTime { get; } = DateTime.UtcNow;
    public ulong Id { get; }
    public int MaxPlayers { get; }
    public double GameClock = 999999999;
    private int PlayersConnected = 0;
    private bool ReadyForStart = false;
    public GameState State { get; private set; } = GameState.Initializing;

    public IEnumerable<ulong> GetPlayerIds() => Players.Where(p => !p.Value.IsBot).Select(p => p.Value.Id);

    public void Update()
    {
        switch (State)
        {
            case GameState.Initializing:
                break;
            case GameState.InGame:
                break;
        }
    }

    public bool SetupPlayer(ulong playerId, byte slot)
    {
        if (Players.TryGetValue(slot, out var player))
            return player.Id == playerId;

        if (playerId > 10)
            Players.Add(slot, new Player(playerId, slot));
        else
            Bots.Add(slot, new Bot(playerId, slot));

        return true;
    }

    public void SetupBot(byte slot)
    {
        Bots.Add(slot, new(0, slot));
    }

    public bool AttachPlayer(AccountModel account)
    {
        if (!ExpectedPlayers.TryGetValue(account.Id, out var slot))
            return false;

        ExpectedPlayers.Remove(account.Id);

        Clients.Add(account.Id, account);

        var player = new Player(account.Id, slot);

        Players.Add(slot, player);

        OnHelloPlayer(player);
        OnPlayerJoined(player);
        OnPrepareForStart(player);

        return true;
    }


    private void OnHelloPlayer(Player player)
    {
        var helloPlayer = new HelloPlayerPacket
        {
            PlayerType = 0,
            GameplayIndex = player.Slot
        };
        player.Client.SendPacket(helloPlayer);
    }

    private void OnPlayerJoined(Player joiningPlayer)
    {
        var playerJoinedPacket = new PlayerJoinedPacket(joiningPlayer.Slot);
        var otherPlayerJoinedPacket = new PlayerJoinedPacket();

        foreach (var player in Players)
        {
            // Notify others of the new player joining
            if (player.Value.Slot != joiningPlayer.Slot)
                player.Value.Client.SendPacket(playerJoinedPacket);

            // Notify the joining player about others, who already joined
            otherPlayerJoinedPacket.Slot = player.Value.Slot;

            joiningPlayer.Client.SendPacket(otherPlayerJoinedPacket);
        }

        foreach (var bot in Bots)
        {
            otherPlayerJoinedPacket.Slot = bot.Value.Slot;

            joiningPlayer.Client.SendPacket(otherPlayerJoinedPacket);
        }    
    }

    private void OnPrepareForStart(Player player)
    {
        var prepareForStart = new GamePrepareForStartPacket(player.Slot)
        {
            unk2 = 4,
            pLevelAsset = 0x946D41FE,
            markerSet1 = 0x6B9636C0,
            markerSet2 = 0x6B9636C0,
            unk6 = 0
        };
        player.Client.SendPacket(prepareForStart);
    }


    public void HandlePacket(RakNetClient sender, IRakNetPacket packet)
    {
        // switch (packet)
        // {
        //     case PlayerStatusUpdatePacket playerStatusUpdatePacket:
        //         if (playerStatusUpdatePacket.Status == 0x80)
        //         {
        //             sender.SendPacket(new EnterPreGameFlowPacket());
        //             GameClock = (int)(DateTime.UtcNow - StartTime).TotalMilliseconds + 25000;
        //             State = GameState.ShaperSelect;
        //         }
        //         break;
        // }
    }
}
