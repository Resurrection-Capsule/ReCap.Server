using System.Numerics;
using AssetData.Parser;
using AssetData.Parser.Model;
using ReCap.Server.Adapters.RakNet;
using ReCap.Server.Adapters.RakNet.Packets;
using ReCap.Server.Domain.Gameplay.Objects;

using ReCap.Server.Adapters.Blaze.Component.GameManager;
using ReCap.Server.Models;
using ReCap.Server.Services;
using ReCap.Server.Services.Assets;
using ReCap.Server.Util.Logging;

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
    public ObjectManager Objects { get; } = new(assetDatabase);

    private DateTime _lastTick = DateTime.UtcNow;

    private int PlayersConnected = 0;
    private bool ReadyForStart = false;
    private uint _nextObjectId = 1;
    private readonly Dictionary<byte, uint> _playerCharacterObjectIds = new();
    private readonly Dictionary<byte, uint[]> _deckObjectIds = new();
    public GameState State { get; private set; } = GameState.Initializing;

    public IEnumerable<ulong> GetPlayerIds() => Players.Where(p => !p.Value.IsBot).Select(p => p.Value.Id);

    public void Update()
    {
        var now = DateTime.UtcNow;
        var delta = (now - _lastTick).TotalSeconds;
        _lastTick = now;
        Objects.Update(delta);

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

    public void SelectLevel(uint levelIndex)
    {
        Chain.SetLevelByIndex((int)levelIndex);
        Chain.StarLevel = 0;
        Chain.CompletedLevel = false;

        if (Assets != null)
        {
            Chain.PopulateFromLevel(Assets);
            Chain.ResolveMarkerSet(Assets);
        }

        Log.Game.Info($"Level selected: index={levelIndex} name={Chain.LevelName} level=0x{Chain.Level:X8}");
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

    // TODO(robust): squad creatures are hardcoded; should come from the player's
    // saved deck/squad (Blaze) instead of fixed nouns. Tracked separately.
    private static readonly uint[] SquadCreatureNouns = { 1667741389u, 749013658u, 3591937345u };
    private static readonly uint[] SquadCreatureTypes = { 2u, 0u, 3u };

    // C++ Server::OnDebugPing Dungeon → mGame.SwapCharacter(player, 1): deck index 1 is deployed.
    private const int DeployedDeckIndex = 1;

    // Marker-based NPC spawn is too greedy (spawns non-object design markers) and crashes
    // the client. Off until we filter by real spawnable noun types like C++ does.
    private const bool SpawnLevelMarkers = false;

    private void FillSquadCharacters(LabsPlayerData playerData)
    {
        uint[] creatureNouns = SquadCreatureNouns;
        uint[] creatureTypes = SquadCreatureTypes;
        for (int i = 0; i < 3; i++)
        {
            float maxHealth = 200f;
            float maxMana = 200f;

            var attrs = Assets?.ResolveClassAttributesForCreature(creatureNouns[i]);
            if (attrs is not null)
            {
                var h = attrs.FindByName("maxHealth").AsFloat();
                var m = attrs.FindByName("maxMana").AsFloat();
                if (h > 0f) maxHealth = h;
                if (m > 0f) maxMana = m;
            }

            playerData.Characters[i] = new LabsCharacterData
            {
                Version = 1,
                NounId = creatureNouns[i],
                AssetId = 0,
                CreatureType = creatureTypes[i],
                DeployCooldown = 0,
                AbilityPoints = 10,
                AbilityRanks = new uint[] { 1, 1, 1, 1, 1, 1, 1, 1, 1 },
                Health = maxHealth,
                MaxHealth = maxHealth,
                Mana = maxMana,
                MaxMana = maxMana,
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
        Log.Game.Info($"OnDebugPing (State={State})");

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
        Log.Game.Info($"OnChainPlayerMsgs({packet.ByteCount}): {packet.Value}");

        if (packet.ByteCount == 1)
        {
            if (packet.Value == 0)
            {
                if (Assets != null) Chain.PopulateFromLevel(Assets);
                sender.SendPacket(new ChainVoteMsgsPacket { Value = 0, ChainData = Chain });
                sender.SendPacket(new ChainVoteMsgsPacket { Value = 1, SecondsUntilDeployment = 30f });
                Log.Game.Info("Sent ChainVoteMessages");
            }
            else if (packet.Value == 2)
            {
                sender.SendPacket(new ChainVoteMsgsPacket { Value = 2, StayInParty = false });
            }
        }
        else if (packet.ByteCount == 6)
        {
            State = GameState.PreDungeon;

            if (Assets != null)
            {
                Chain.PopulateFromLevel(Assets);
                Chain.ResolveMarkerSet(Assets);
            }

            var prepareStart = new GamePrepareForStartPacket(Chain.Level, Chain.MarkerSet, 1, Chain.LevelIndex);
            sender.SendPacket(prepareStart);
            Log.Game.Info($"Sent GamePrepareForStart (Level=0x{Chain.Level:X8}, LevelIndex={Chain.LevelIndex}, SquadId={packet.SquadId})");

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
        Log.Game.Info($"OnPlayerStatusUpdate: {packet.Status} (State={State})");

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

    private Vector3? ResolveSpawnPosition()
    {
        if (Assets == null) return null;

        // C++ Instance::LoadLevel reads CameraSpawnPoint markers from the level's
        // "<level>_design" markerset (fallback "_design_spawners"). Our markerset keys
        // are Fnv1a of the name WITHOUT the ".Markerset" extension (see LevelLoader).
        var ms = Assets.GetMarkerSetByName($"{Chain.LevelName}_design")
              ?? Assets.GetMarkerSetByName($"{Chain.LevelName}_design_spawners");
        if (ms is null)
        {
            Log.Game.Debug($"spawn: markerset '{Chain.LevelName}_design' not found");
            return null;
        }
        if (ms.FindByName("markers") is not ArrayValue markers)
        {
            Log.Game.Debug($"spawn: markerset '{Chain.LevelName}_design' has no markers array");
            return null;
        }

        // nounDef is a DataType.Asset -> parsed as the asset NAME string (or null).
        foreach (var marker in markers.Items)
        {
            if (string.Equals(marker.FindByName("nounDef").AsString(), "CameraSpawnPoint.Noun", StringComparison.OrdinalIgnoreCase))
                return marker.FindByName("pos").AsVector3();
        }

        var names = markers.Items.Select(m => m.FindByName("nounDef").AsString())
            .Where(s => !string.IsNullOrEmpty(s)).Distinct().Take(10);
        Log.Game.Debug($"spawn: {markers.Items.Count} markers, no CameraSpawnPoint; nounDef names: {string.Join(", ", names)}");
        return null;
    }

    private void OnPlayerStart(RakNetClient client)
    {
        var player = GetPlayerByClient(client);
        if (player == null) return;

        client.SendPacket(ObjectivesInitForLevelPacket.CreateDefault());

        // DISABLED: the marker loop spawns EVERY marker with a nounDef as a team-2 object,
        // including non-spawnable design markers (lights, cameras, decals, water, triggers).
        // Those create invalid objects the client dereferences each frame -> null-deref crash
        // (Exception Report: ACCESS_VIOLATION read 0x0 in per-frame object loop). C++ only
        // spawns specific noun types (obelisks/enemies). Re-enable with proper type filtering.
        if (SpawnLevelMarkers && Assets != null)
        {
            var markers = Assets.GetLevelMarkers(Chain.LevelName);
            foreach (var marker in markers)
            {
                // nounDef is a DataType.Asset -> parsed as the noun NAME string.
                var nounName = marker.FindByName("nounDef").AsString();
                if (string.IsNullOrEmpty(nounName)) continue;
                if (string.Equals(nounName, "CameraSpawnPoint.Noun", StringComparison.OrdinalIgnoreCase)) continue;
                var nounDef = ChainData.FnvHash(nounName);

                var markerPos = marker.FindByName("pos").AsVector3();
                var markerScale = marker.FindByName("scale").AsFloat();
                if (markerScale == 0f) markerScale = 1.0f;

                var markerObjId = _nextObjectId++;
                Objects.Spawn(markerObjId, nounDef, markerPos, markerScale, team: 2, playerControlled: false);
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

        var resolvedSpawn = ResolveSpawnPosition();
        if (resolvedSpawn == null)
            Log.Game.Warn($"No CameraSpawnPoint marker for {Chain.LevelName}; using fallback spawn");
        var spawnPos = resolvedSpawn ?? new Vector3(44.0f, 0.47f, 17.5f);

        // C++ OnPlayerStart force-creates ALL squad character objects so the client
        // can find them on swap; only then is one deployed via SwapCharacter.
        var deckObjectIds = new uint[SquadCreatureNouns.Length];
        for (int i = 0; i < SquadCreatureNouns.Length; i++)
        {
            var charObjId = _nextObjectId++;
            deckObjectIds[i] = charObjId;
            var noun = SquadCreatureNouns[i];
            Objects.Spawn(charObjId, noun, spawnPos, 1.0f, team: 1, playerControlled: true);

            var objData = new SporelabsObject
            {
                Position = spawnPos,
                Team = 1,
                PlayerControlled = true,
                PlayerIdx = player.Slot,
                Visible = true,
                HasCollision = true,
                Scale = 1.0f,
                MarkerScale = 1.0f
            };

            // C++ SendObjectUpdate sequence for a creature: ObjectCreate -> ObjectUpdate
            // -> CombatantData -> AttributeData. The player hero needs all four or the
            // client fades out / crashes on deploy.
            client.SendPacket(new ObjectCreatePacket
            {
                ObjectId = charObjId,
                CreateData = new GameObjectCreateData
                {
                    Noun = noun,
                    Position = spawnPos,
                    Scale = 1.0f,
                    Team = 1,
                    HasCollision = true,
                    PlayerControlled = true
                },
                ObjectData = objData
            });
            client.SendPacket(new ObjectUpdatePacket { ObjectId = charObjId, ObjectData = objData });

            var charData = player.PlayerData?.Characters[i];
            float maxHp = charData?.MaxHealth ?? 200f;
            float maxMp = charData?.MaxMana ?? 200f;

            client.SendPacket(new CombatantDataUpdatePacket { ObjectId = charObjId, HitPoints = maxHp, ManaPoints = maxMp });

            var attrs = new AttributeDataUpdatePacket { ObjectId = charObjId };
            attrs.Set(AttributeDataUpdatePacket.MaxHealth, maxHp);
            attrs.Set(AttributeDataUpdatePacket.MaxMana, maxMp);
            attrs.Set(AttributeDataUpdatePacket.AttackSpeedScale, 1f);
            attrs.Set(AttributeDataUpdatePacket.CooldownScale, 1f);
            attrs.Set(AttributeDataUpdatePacket.InvisibleToSecurityTeleporters, 1f);
            attrs.Set(AttributeDataUpdatePacket.MinWeaponDamage, 1f);
            attrs.Set(AttributeDataUpdatePacket.MaxWeaponDamage, 5f);
            client.SendPacket(attrs);
        }
        _deckObjectIds[player.Slot] = deckObjectIds;
        _playerCharacterObjectIds[player.Slot] = deckObjectIds[DeployedDeckIndex];

        Log.Game.Info($"Spawned squad ({deckObjectIds.Length} chars) at ({spawnPos.X:F1},{spawnPos.Y:F1},{spawnPos.Z:F1}); deploying deck={DeployedDeckIndex}");

        SwapCharacter(client, player, DeployedDeckIndex, deckObjectIds[DeployedDeckIndex]);
    }

    // C++ Instance::SwapCharacter: set current deck index, broadcast PlayerCharacterDeploy,
    // send LPU. Without it the client never binds an active hero and fades out.
    private void SwapCharacter(RakNetClient client, Player player, int deckIndex, uint objectId)
    {
        if (player.PlayerData != null)
        {
            player.PlayerData.CurrentDeckIndex = (byte)deckIndex;
            player.PlayerData.SetDataBit(1);
        }
        player.SetUpdateBits(LabsPlayerUpdatePacket.PlayerBits);
        client.SendPacket(new PlayerCharacterDeployPacket(player.Slot, (uint)deckIndex, objectId));
        SendLabsPlayerUpdate(client);
        Log.Game.Info($"Deployed deck={deckIndex} objectId={objectId}");
    }

    private void HandleActionCommand(RakNetClient sender, ActionCommandMsgsPacket packet)
    {
        Log.Game.Debug($"ActionCommand: type={packet.CommandType} obj=0x{packet.ObjectId:X} pos=({packet.PosX:F1},{packet.PosY:F1},{packet.PosZ:F1})");

        if (packet.CommandType == 3)
        {
            // ActionCommandMovementData: goalPosition + goalFlags. C++ broadcasts 0x91 to move.
            var (goalFlags, gx, gy, gz) = packet.ReadMovementData();
            var movePacket = new ObjectPlayerMovePacket
            {
                ObjectId = packet.ObjectId,
                Locomotion = new LocomotionData
                {
                    GoalFlags = goalFlags,
                    GoalPosition = new Vector3(gx, gy, gz),
                    AllowedStopDistance = 0,
                    DesiredStopDistance = 0
                }
            };
            sender.SendPacket(movePacket);
            Log.Game.Debug($"Move obj=0x{packet.ObjectId:X} -> ({gx:F1},{gy:F1},{gz:F1}) flags=0x{goalFlags:X}");
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
            Log.Game.Debug("Switch character requested");
        }
    }
}
