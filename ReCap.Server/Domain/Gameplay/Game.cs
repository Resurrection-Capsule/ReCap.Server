using System.Numerics;
using AssetData.Parser;
using ReCap.Server.Adapters.RakNet;
using ReCap.Server.Adapters.RakNet.Packets;
using ReCap.Server.Domain.Gameplay.Objects;

using ReCap.Server.Adapters.Blaze.Component.GameManager;
using ReCap.Server.Models;
using ReCap.Server.Services;

namespace ReCap.Server.Domain.Gameplay;

public class Game(ulong id, GameType gameType, AssetDatabase? assetDatabase = null) : IGame
{
    public Dictionary<ulong, byte> ExpectedPlayers { get; } = new();
    public Dictionary<byte, Player> Players { get; } = new();
    public Dictionary<byte, Bot> Bots { get; } = new();

    public Dictionary<ulong, AccountModel> Clients { get; } = new();
    public Dictionary<byte, AccountModel> ClientsBySlot { get; } = new();

    public DateTime StartTime { get; } = DateTime.UtcNow;
    public ulong Id { get; } = id;
    public int MaxPlayers { get; }
    public double GameClock = 999999999;
    public AssetDatabase? Assets { get; } = assetDatabase;
    public ChainData Chain { get; } = new();

    private int PlayersConnected = 0;
    private bool ReadyForStart = false;
    private uint _nextObjectId = 1;
    private readonly Dictionary<byte, uint> _playerCharacterObjectIds = new();
    public GameState State { get; private set; } = GameState.Initializing;

    public IEnumerable<ulong> GetPlayerIds() => Players.Where(p => !p.Value.IsBot).Select(p => p.Value.Id);

    public void Update()
    {
        foreach (var player in Players.Values)
        {
            if (player.Client == null) continue;

            var gameState = new GameStatePacket
            {
                GameTime = (ulong)(DateTime.UtcNow - StartTime).TotalMilliseconds,
                TimeElapsed = (ulong)(DateTime.UtcNow - StartTime).TotalMilliseconds,
                State = State,
                GameType = 0
            };
            player.Client.SendPacket(gameState);

            SendLabsPlayerUpdate(player.Client);
        }
    }

    public bool SetupPlayer(ulong playerId, byte slot)
    {
        if (ExpectedPlayers.ContainsKey(playerId))
            return true;

        ExpectedPlayers.Add(playerId, slot);
        return true;
    }

    public void SetupBot(byte slot)
    {
        Bots.Add(slot, new(0, slot));
    }

    public bool AttachPlayer(AccountModel account, RakNetClient client)
    {
        if (!ExpectedPlayers.TryGetValue(account.Id, out var slot))
            return false;

        ExpectedPlayers.Remove(account.Id);

        Clients.Add(account.Id, account);

        var player = new Player(account.Id, slot)
        {
            Client = client
        };

        Players.Add(slot, player);

        ushort crystalBits8 = 0;
        for (int i = 0; i < 8; i++) crystalBits8 |= (ushort)(LabsPlayerUpdatePacket.CrystalBits << i);
        player.SetUpdateBits((ushort)(LabsPlayerUpdatePacket.PlayerBits | crystalBits8));

        OnHelloPlayer(player);
        OnPartyMergeComplete(player);
        SendLabsPlayerUpdate(player.Client);

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

    private void OnPartyMergeComplete(Player player)
    {
        player.Client.SendPacket(new PartyMergeCompletePacket());
    }

    private void OnPlayerJoined(Player joiningPlayer)
    {
        var playerJoinedPacket = new PlayerJoinedPacket(joiningPlayer.Slot);
        var otherPlayerJoinedPacket = new PlayerJoinedPacket();

        foreach (var player in Players)
        {
            if (player.Value.Slot != joiningPlayer.Slot)
                player.Value.Client.SendPacket(playerJoinedPacket);

            otherPlayerJoinedPacket.Slot = player.Value.Slot;
            joiningPlayer.Client.SendPacket(otherPlayerJoinedPacket);
        }

        foreach (var bot in Bots)
        {
            otherPlayerJoinedPacket.Slot = bot.Value.Slot;
            joiningPlayer.Client.SendPacket(otherPlayerJoinedPacket);
        }
    }

    private Player? GetPlayerByClient(RakNetClient client)
    {
        foreach (var p in Players.Values)
            if (p.Client == client) return p;
        return null;
    }

    private void SendLabsPlayerUpdate(RakNetClient client)
    {
        var player = GetPlayerByClient(client);
        if (player == null) return;

        if (player.PlayerData == null)
        {
            player.PlayerData = CreatePlayerData(player);
            player.PlayerData.SetInitialDataBits();
        }

        var pd = player.PlayerData;
        pd.Status = player.GameStatus;
        pd.StatusProgress = player.GameStatusProgress;

        if (pd._needsStatusUpdate)
        {
            pd.SetDataBit(7);
            pd.SetDataBit(8);
            pd._needsStatusUpdate = false;
        }

        var updateBits = player.UpdateBits;
        if (updateBits == 0) return;

        var updatePacket = new LabsPlayerUpdatePacket
        {
            PlayerId = player.Slot,
            UpdateBits = updateBits,
            PlayerData = pd
        };

        client.SendPacket(updatePacket);
        pd.ResetDataBits();
        player.ResetUpdateBits();
    }

    private static void FillSquadCharacters(LabsPlayerData playerData)
    {
        uint[] creatureNouns = { 1667741389u, 749013658u, 3591937345u };
        uint[] creatureTypes = { 2u, 0u, 3u };
        for (int i = 0; i < 3; i++)
        {
            playerData.Characters[i] = new LabsCharacterData
            {
                Version = 1,
                NounId = creatureNouns[i],
                AssetId = 0,
                CreatureType = creatureTypes[i],
                DeployCooldown = 0,
                AbilityPoints = 10,
                AbilityRanks = new uint[] { 1, 1, 1, 1, 1, 1, 1, 1, 1 },
                Health = 200f,
                MaxHealth = 200f,
                Mana = 200f,
                MaxMana = 200f,
                GearScore = 300f,
                GearScoreFlattened = 300f
            };
        }
    }

    private LabsPlayerData CreatePlayerData(Player player)
    {
        var playerData = new LabsPlayerData
        {
            DataSetup = false,
            CurrentDeckIndex = 0,
            QueuedDeckIndex = 0,
            PlayerIndex = player.Slot,
            Team = 1,
            PlayerOnlineId = player.Id,
            Status = player.GameStatus,
            StatusProgress = player.GameStatusProgress,
            CurrentCreatureId = 0,
            EnergyPoints = 0,
            IsCharged = true,
            DNA = 0,
            LockCamera = false,
            LockedOverdrive = false,
            LockedCrystals = false,
            LockedAbilityMin = 0xFF,
            LockedDeckIndexMin = 0xFF,
            DeckScore = 500,
            AvatarLevel = 30,
            AvatarXP = 0f,
            ChainProgression = 10
        };

        for (int i = 0; i < 8; i++)
        {
            playerData.Catalysts[i] = new LabsCatalystData
            {
                NounId = 0x02FB89EB,
                Rarity = 2
            };
        }

        return playerData;
    }

    public void HandlePacket(RakNetClient sender, IRakNetPacket packet)
    {
        switch (packet)
        {
            case DebugPingPacket:
                HandleDebugPing(sender);
                break;

            case ChainPlayerMsgsPacket chainPlayerMsgs:
                HandleChainPlayerMsgs(sender, chainPlayerMsgs);
                break;

            case PlayerStatusUpdatePacket playerStatusUpdate:
                HandlePlayerStatusUpdate(sender, playerStatusUpdate);
                break;

            case ActionCommandMsgsPacket actionCommand:
                HandleActionCommand(sender, actionCommand);
                break;
        }
    }

    private void HandleDebugPing(RakNetClient sender)
    {
        Console.WriteLine($"[Game] OnDebugPing (State={State})");

        switch (State)
        {
            case GameState.Initializing:
                State = GameState.ChainVoting;
                break;

            case GameState.ChainVoting:
                sender.SendPacket(new ChainVoteMsgsPacket { Value = 0, ChainData = Chain });
                break;

            case GameState.PreDungeon:
                break;

            case GameState.Dungeon:
                sender.SendPacket(new DirectorStatePacket());
                sender.SendPacket(new QuickGameMsgsPacket());
                OnPlayerStart(sender);
                break;
        }
    }

    private void HandleChainPlayerMsgs(RakNetClient sender, ChainPlayerMsgsPacket packet)
    {
        Console.WriteLine($"[Game] OnChainPlayerMsgs({packet.ByteCount}): {packet.Value}");

        if (packet.ByteCount == 1)
        {
            if (packet.Value == 0)
            {
                if (Assets != null) Chain.PopulateFromLevel(Assets);
                sender.SendPacket(new ChainVoteMsgsPacket { Value = 0, ChainData = Chain });
                sender.SendPacket(new ChainVoteMsgsPacket { Value = 1, SecondsUntilDeployment = 30f });
                Console.WriteLine("[Game] Sent ChainVoteMessages");
            }
            else if (packet.Value == 2)
            {
                sender.SendPacket(new ChainVoteMsgsPacket { Value = 2, StayInParty = false });
            }
        }
        else if (packet.ByteCount == 6)
        {
            State = GameState.PreDungeon;

            if (Assets != null) Chain.PopulateFromLevel(Assets);

            var prepareStart = new GamePrepareForStartPacket(Chain.Level, Chain.MarkerSet, 1, Chain.LevelIndex);
            sender.SendPacket(prepareStart);
            Console.WriteLine($"[Game] Sent GamePrepareForStart (Level=0x{Chain.Level:X8}, LevelIndex={Chain.LevelIndex}, SquadId={packet.SquadId})");

            var player = GetPlayerByClient(sender);
            if (player != null)
            {
                if (player.PlayerData != null)
                {
                    FillSquadCharacters(player.PlayerData);
                    player.PlayerData.SetDataBit(1);
                    player.PlayerData.SetDataBit(2);
                    player.PlayerData.SetDataBit(3);
                    player.PlayerData.SetDataBit(23);
                }
                player.SetUpdateBits((ushort)(LabsPlayerUpdatePacket.PlayerBits | LabsPlayerUpdatePacket.CharacterMask));
                SendLabsPlayerUpdate(sender);
            }
        }
    }

    private void HandlePlayerStatusUpdate(RakNetClient sender, PlayerStatusUpdatePacket packet)
    {
        Console.WriteLine($"[Game] OnPlayerStatusUpdate: {packet.Status} (State={State})");

        var player = GetPlayerByClient(sender);
        if (player != null)
        {
            player.GameStatus = packet.Status;
            player.GameStatusProgress = packet.Progress;
            player.SetUpdateBits(LabsPlayerUpdatePacket.PlayerBits);
            if (player.PlayerData != null)
                player.PlayerData._needsStatusUpdate = true;
        }

        if (packet.Status == 0x08)
        {
            State = GameState.Dungeon;
            sender.SendPacket(new GameStartPacket(Chain.LevelIndex));
            sender.SendPacket(new DebugPingPacket());
        }

        SendLabsPlayerUpdate(sender);
    }

    private void OnPlayerStart(RakNetClient client)
    {
        var player = GetPlayerByClient(client);
        if (player == null) return;

        if (Assets != null)
        {
            var markers = Assets.GetLevelMarkers($"{Chain.LevelName}.level");
            foreach (var marker in markers)
            {
                var nounDef = marker["nounDef"]?.AsUInt32() ?? 0;
                if (nounDef == 0) continue;

                var markerPos = marker["pos"]?.AsVector3() ?? Vector3.Zero;
                var markerScale = marker["scale"]?.AsFloat() ?? 1.0f;

                var markerObjId = _nextObjectId++;
                var npcPacket = new ObjectCreatePacket
                {
                    ObjectId = markerObjId,
                    CreateData = new GameObjectCreateData
                    {
                        Noun = nounDef,
                        Position = markerPos,
                        Scale = markerScale,
                        Team = 2,
                        HasCollision = true,
                        PlayerControlled = false
                    },
                    ObjectData = new SporelabsObject
                    {
                        Position = markerPos,
                        Team = 2,
                        PlayerControlled = false,
                        Visible = true,
                        HasCollision = true,
                        Scale = markerScale,
                        MarkerScale = markerScale
                    }
                };
                client.SendPacket(npcPacket);
            }
        }

        var spawnPos = new Vector3(44.0f, 0.47f, 17.5f);
        uint creatureNoun = 1667741389u;

        var objectId = _nextObjectId++;
        _playerCharacterObjectIds[player.Slot] = objectId;

        var createPacket = new ObjectCreatePacket
        {
            ObjectId = objectId,
            CreateData = new GameObjectCreateData
            {
                Noun = creatureNoun,
                Position = spawnPos,
                Scale = 1.0f,
                Team = 1,
                HasCollision = true,
                PlayerControlled = true
            },
            ObjectData = new SporelabsObject
            {
                Position = spawnPos,
                Team = 1,
                PlayerControlled = true,
                PlayerIdx = player.Slot,
                Visible = true,
                HasCollision = true,
                Scale = 1.0f,
                MarkerScale = 1.0f
            }
        };
        client.SendPacket(createPacket);
        Console.WriteLine($"[Game] Spawned hero objectId={objectId} noun=0x{creatureNoun:X} at ({spawnPos.X},{spawnPos.Y},{spawnPos.Z})");

        client.SendPacket(new PlayerCharacterDeployPacket(player.Slot, objectId));
    }

    private void HandleActionCommand(RakNetClient sender, ActionCommandMsgsPacket packet)
    {
        Console.WriteLine($"[Game] ActionCommand: type={packet.CommandType} obj=0x{packet.ObjectId:X} pos=({packet.PosX:F1},{packet.PosY:F1},{packet.PosZ:F1})");

        if (packet.CommandType == 3)
        {
            var movePacket = new ObjectPlayerMovePacket
            {
                ObjectId = packet.ObjectId,
                Locomotion = new LocomotionData
                {
                    GoalFlags = 0x001,
                    GoalPosition = new Vector3(packet.PosX, packet.PosY, packet.PosZ),
                    AllowedStopDistance = 0,
                    DesiredStopDistance = 0
                }
            };
            sender.SendPacket(movePacket);
        }
        else if (packet.CommandType == 4)
        {
            var movePacket = new ObjectPlayerMovePacket
            {
                ObjectId = packet.ObjectId,
                Locomotion = new LocomotionData
                {
                    GoalFlags = 0x020
                }
            };
            sender.SendPacket(movePacket);
        }
        else if (packet.CommandType == 5)
        {
            Console.WriteLine($"[Game] Switch character requested");
        }
    }
}
