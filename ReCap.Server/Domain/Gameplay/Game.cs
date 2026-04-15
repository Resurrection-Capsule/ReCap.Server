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
    public Dictionary<ulong, Player> Players { get; } = new();
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
        switch (State)
        {
            case GameState.Initializing:
                break;
            case GameState.PreDungeon:
                break;
            case GameState.Dungeon:
                break;
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

        OnHelloPlayer(player);
        OnPartyMergeComplete(player);
        OnPlayerJoined(player);
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

    private void SendLabsPlayerUpdate(RakNetClient client)
    {
        Player? player = null;
        foreach (var p in Players.Values)
        {
            if (p.Client == client)
            {
                player = p;
                break;
            }
        }

        if (player == null) return;

        var updatePacket = new LabsPlayerUpdatePacket
        {
            PlayerId = player.Slot,
            UpdateBits = LabsPlayerUpdatePacket.PlayerBits | LabsPlayerUpdatePacket.CharacterMask | LabsPlayerUpdatePacket.CrystalMask,
            PlayerData = new LabsPlayerData
            {
                DataSetup = true,
                CurrentDeckIndex = 0,
                QueuedDeckIndex = 0,
                PlayerIndex = player.Slot,
                Team = 1,
                PlayerOnlineId = player.Id,
                Status = 0,
                StatusProgress = 0,
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
            }
        };

        // Add 3 fake characters
        for (int i = 0; i < 3; i++)
        {
            updatePacket.Characters[i] = new LabsCharacterData
            {
                Version = 1,
                NounId = 0x3039C538, // SageBasic
                AssetId = 0,
                CreatureType = 1,
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

        // Add 9 fake catalysts
        for (int i = 0; i < 9; i++)
        {
            updatePacket.Catalysts[i] = new LabsCatalystData
            {
                NounId = i < 8 ? 0x02FB89EB : 0u, // Catalyst_Health
                Rarity = 2
            };
        }

        client.SendPacket(updatePacket);
        Console.WriteLine("[Game] Sent LabsPlayerUpdate");
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
                sender.SendPacket(new ChainVoteMsgsPacket { Value = 1, SecondsUntilDeployment = 30f });
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

            // Apply the actual user level vote index
            Chain.SetLevelByIndex((int)packet.LevelIndex);
            if (Assets != null) Chain.PopulateFromLevel(Assets);

            var prepareStart = new GamePrepareForStartPacket(Chain.Level, Chain.MarkerSet, 1, Chain.LevelIndex);
            sender.SendPacket(prepareStart);

            SendLabsPlayerUpdate(sender);
        }
    }

    private void HandlePlayerStatusUpdate(RakNetClient sender, PlayerStatusUpdatePacket packet)
    {
        Console.WriteLine($"[Game] OnPlayerStatusUpdate: {packet.Status} (State={State})");

        if (packet.Status == 0x08)
        {
            State = GameState.Dungeon;
            sender.SendPacket(new GameStartPacket(0));
            sender.SendPacket(new DebugPingPacket());

            SendLabsPlayerUpdate(sender);
        }
        else
        {
            SendLabsPlayerUpdate(sender);
        }
    }

    private void OnPlayerStart(RakNetClient client)
    {
        Player? player = null;
        foreach (var p in Players.Values)
        {
            if (p.Client == client)
            {
                player = p;
                break;
            }
        }

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
        uint creatureNoun = 0x3039C538;

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
