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

    // Resolves the player's chosen squad (account, 1-based squadId) into the creatures
    // they own and saved in that deck. Set by GameService; mirrors C++ user->GetSquadById.
    public Func<AccountModel, int, IReadOnlyList<SquadCreature>>? SquadResolver { get; set; }

    private DateTime _lastTick = DateTime.UtcNow;

    private int PlayersConnected = 0;
    private bool ReadyForStart = false;
    private uint _nextObjectId = 1;
    private readonly Dictionary<byte, uint> _playerCharacterObjectIds = new();
    private readonly Dictionary<uint, LocomotionData> _objectLocomotion = new();
    private readonly Dictionary<byte, uint[]> _deckObjectIds = new();
    private readonly Dictionary<byte, IReadOnlyList<SquadCreature>> _playerSquads = new();
    public GameState State { get; private set; } = GameState.Initializing;

    // Mirrors C++ teleportMovement (Server.cpp:76). Default false = smooth movement (0x91
    // flags 0x001; own-hero walk is client-side, enabled by D-017/D-017b ClassAttributes).
    // CLI --teleport-movement re-enables the D-015 snap contract (0x90 + 0x91 flags 0x21)
    // as a fallback for creatures whose data lacks locomotion speeds.
    public static bool TeleportMovement { get; set; } = false;

    public IEnumerable<ulong> GetPlayerIds() => Players.Where(p => !p.Value.IsBot).Select(p => p.Value.Id);

    public void Update()
    {
        var now = DateTime.UtcNow;
        var delta = (now - _lastTick).TotalSeconds;
        _lastTick = now;
        Objects.Update(delta);
        FlushObjectUpdates();

        foreach (var player in Players.Values)
        {
            if (player.Client == null) continue;

            var gameState = new GameStatePacket
            {
                GameTime = (ulong)(DateTime.UtcNow - StartTime).TotalMilliseconds,
                TimeElapsed = (ulong)(DateTime.UtcNow - StartTime).TotalMilliseconds,
                State = State,
                // C++ Server.cpp:1369 sends Blaze::GameType::Chain here every tick; 0 is invalid
                // (enum starts at 1) and misdirects the client UI state machine.
                GameType = (uint)LabsGameType.Chain
            };
            player.Client.SendPacket(gameState);

            SendLabsPlayerUpdate(player.Client);

            // C++ Instance::Update (50ms tick) sends ObjectiveUpdated for objective 0
            // (FinishLevelQuickly) every tick with value = elapsed seconds. The objectives HUD
            // popup (cObjectivePopup) appears to need this per-frame feed. Medal=Gold(4).
            if (State == GameState.Dungeon)
            {
                player.Client.SendPacket(new ObjectiveUpdatedPacket
                {
                    ObjectiveId = ObjectivesInitForLevelPacket.ObjectiveIds[0],
                    ClientId = player.Slot,
                    Medal = 4,
                    Voiceover = _objectiveVoiceover,
                    Value = (uint)Math.Max(0, (now - StartTime).TotalSeconds)
                });
            }
        }
    }

    private static readonly uint _objectiveVoiceover =
        ObjectivesInitForLevelPacket.FnvHash("vo_ship_obelisk_accessed");

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

    private void BroadcastToAllPlayers(IRakNetPacket packet)
    {
        foreach (var player in Players.Values)
            player.Client?.SendPacket(packet);
    }

    // Flush per-object dirty flags accumulated since the last tick. Mirrors C++ ObjectManager::Update
    // → Instance::SendObjectUpdate: routes via GoalFlags to ObjectTeleport (0x020 bit) or
    // LocomotionDataUpdate, then clears flags. Called once per 50ms tick after Objects.Update().
    private void FlushObjectUpdates()
    {
        foreach (var obj in Objects.Objects.Values)
        {
            if (obj.DirtyFlags == ObjectDirtyFlags.None) continue;

            if ((obj.DirtyFlags & ObjectDirtyFlags.Locomotion) != 0 && !obj.PlayerControlled)
            {
                IRakNetPacket locomotionPacket = (obj.GoalFlags & 0x020) != 0
                    ? new ObjectTeleportPacket { ObjectId = obj.ObjectId, Position = obj.Position, Orientation = obj.Orientation }
                    : new LocomotionDataUpdatePacket { ObjectId = obj.ObjectId, Locomotion = new LocomotionData { GoalFlags = obj.GoalFlags } };
                BroadcastToAllPlayers(locomotionPacket);
            }

            obj.DirtyFlags = ObjectDirtyFlags.None;
        }
    }

    // C++ Server::OnDebugPing Dungeon → mGame.SwapCharacter(player, 1): deck index 1 is deployed.
    private const int DeployedDeckIndex = 1;

    // Resolve the player's selected squad from their persisted deck (never hardcoded).
    private IReadOnlyList<SquadCreature> ResolveSquadForPlayer(Player player, int squadId)
    {
        if (SquadResolver != null && Clients.TryGetValue(player.Id, out var account))
        {
            var squad = SquadResolver(account, squadId);
            if (squad.Count > 0)
                return squad;
            Log.Game.Warn($"Squad {squadId} resolved empty for account {player.Id}");
        }
        return Array.Empty<SquadCreature>();
    }

    private static void FillSquadCharacters(LabsPlayerData playerData, IReadOnlyList<SquadCreature> squad)
    {
        for (int i = 0; i < 3; i++)
        {
            if (i < squad.Count)
            {
                var c = squad[i];
                playerData.Characters[i] = new LabsCharacterData
                {
                    Version = c.Version,
                    NounId = c.Noun,
                    AssetId = 0,
                    CreatureType = c.CreatureType,
                    DeployCooldown = 0,
                    AbilityPoints = 10,
                    AbilityRanks = new uint[] { 1, 1, 1, 1, 1, 1, 1, 1, 1 },
                    Health = c.MaxHealth,
                    MaxHealth = c.MaxHealth,
                    Mana = c.MaxMana,
                    MaxMana = c.MaxMana,
                    GearScore = c.GearScore,
                    GearScoreFlattened = c.GearScoreFlattened
                };
            }
            else
            {
                // Empty squad slot: noun 0, mirrors C++ SetSquad when a creature is missing.
                playerData.Characters[i] = new LabsCharacterData
                {
                    Version = 0,
                    NounId = 0,
                    AssetId = 0,
                    CreatureType = 0,
                    AbilityRanks = new uint[] { 1, 1, 1, 1, 1, 1, 1, 1, 1 }
                };
            }
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
                // C++ PrepareGameStart -> Player::SetSquad(user->GetSquadById(squadId)):
                // resolve the chosen squad from the player's persisted deck.
                var squad = ResolveSquadForPlayer(player, (int)packet.SquadId);
                _playerSquads[player.Slot] = squad;
                Log.Game.Info($"Resolved squad {packet.SquadId}: [{string.Join(", ", squad.Select(c => $"0x{c.Noun:X8} gs={c.GearScore:F0} hp={c.MaxHealth:F0} spd={c.NonCombatSpeed:F2}/{c.CombatSpeed:F2}"))}]");

                if (player.PlayerData != null)
                {
                    FillSquadCharacters(player.PlayerData, squad);
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

    // C++ ObjectManager::Create(marker) + SendObjectCreate: the object carries the marker
    // transform and markerId (ObjectManager.cpp:206-212). Wire-verified 93B shape:
    // createData all-10 + object-reflection {6,7,8,17,22}.
    private void SpawnWorldObject(RakNetClient client, uint noun, AssetValue marker, float scale, bool hasCollision)
    {
        var pos = marker.FindByName("pos").AsVector3();
        var rot = marker.FindByName("rotDegrees").AsVector3();
        var markerId = marker.FindByName("markerId").AsUInt32();

        var objId = _nextObjectId++;
        Objects.Spawn(objId, noun, pos, scale, team: 0, playerControlled: false);

        var objData = new SporelabsObject
        {
            Position = pos,
            Orientation = Quaternion.Identity,
            Scale = scale,
            HasCollision = hasCollision,
            MarkerScale = 1f,
            SourceMarkerKeyMarkerId = markerId
        };
        foreach (byte bit in new byte[] { 6, 7, 8, 17, 22 }) objData.SetDataBit(bit);

        client.SendPacket(new ObjectCreatePacket
        {
            ObjectId = objId,
            CreateData = new GameObjectCreateData
            {
                Noun = noun,
                Position = pos,
                RotXDegrees = rot.X,
                RotYDegrees = rot.Y,
                RotZDegrees = rot.Z,
                Scale = scale,
                Team = 0,
                HasCollision = hasCollision,
                PlayerControlled = false
            },
            ObjectData = objData
        });
    }

    private void OnPlayerStart(RakNetClient client)
    {
        var player = GetPlayerByClient(client);
        if (player == null) return;

        client.SendPacket(ObjectivesInitForLevelPacket.CreateDefault());

        // C++ Instance::OnPlayerStart sends an ObjectiveUpdated (0xB8) for each objective right
        // after the init (Instance.cpp:399-401). The per-tick obj-0 update is in Update().
        // Medal = Gold(4) per the hardcoded objective set; clientId = player slot.
        for (uint i = 0; i < ObjectivesInitForLevelPacket.ObjectiveIds.Length; i++)
            client.SendPacket(new ObjectiveUpdatedPacket
            {
                ObjectiveId = ObjectivesInitForLevelPacket.ObjectiveIds[i],
                ClientId = player.Slot,
                Medal = 4,
                Voiceover = 0,
                Value = 1
            });

        var squad = _playerSquads.TryGetValue(player.Slot, out var s) ? s : Array.Empty<SquadCreature>();
        if (squad.Count == 0)
        {
            Log.Game.Warn($"Player slot {player.Slot} has no resolved squad; nothing to deploy");
            return;
        }

        var spawnPos = ResolveSpawnPosition() ?? new Vector3(44.0f, 0.47f, 17.5f);

        // C++ Instance::OnPlayerStart force-creates ALL squad character objects (the GetActiveObjects
        // ObjectCreate loop) BEFORE one is deployed via SwapCharacter. PlayerCharacterDeploy references
        // a hero objId; with no preceding ObjectCreate the client can't bind the deck-HUD.
        // WIRE-VERIFIED field sets (cpp_loopback hero objId=1, DIVERGENCE_LEDGER D-009):
        //   ObjectCreate object-reflection = {0 Team, 1 PlayerControlled, 3 PlayerIdx, 17 HasCollision}
        //     (C++ hero = 59B; NOT 93B — 93B is the enemy variant). Position is NOT in the create
        //     reflection; C++ leaves createData.position zero and positions the hero via ObjectUpdate.
        //   ObjectUpdate object-reflection = {6 Position, 16 Visible} (C++ hero = 21B).
        var deckObjectIds = new uint[squad.Count];
        for (int i = 0; i < squad.Count; i++)
        {
            var charObjId = _nextObjectId++;
            deckObjectIds[i] = charObjId;
            var noun = squad[i].Noun;
            Objects.Spawn(charObjId, noun, spawnPos, 1.0f, team: 1, playerControlled: true);

            var createObj = new SporelabsObject
            {
                Team = 1,
                PlayerControlled = true,
                PlayerIdx = player.Slot,
                HasCollision = true,
                Scale = 1.0f,
                MarkerScale = 1.0f
            };
            foreach (byte bit in new byte[] { 0, 1, 3, 17 }) createObj.SetDataBit(bit);

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
                ObjectData = createObj
            });

            var moveObj = new SporelabsObject { Position = spawnPos, Visible = true };
            foreach (byte bit in new byte[] { 6, 16 }) moveObj.SetDataBit(bit);
            client.SendPacket(new ObjectUpdatePacket { ObjectId = charObjId, ObjectData = moveObj });

            float maxHp = squad[i].MaxHealth > 0 ? squad[i].MaxHealth : 200f;
            float maxMp = squad[i].MaxMana > 0 ? squad[i].MaxMana : 200f;
            client.SendPacket(new CombatantDataUpdatePacket { ObjectId = charObjId, HitPoints = maxHp, ManaPoints = maxMp });

            // C++ Object setup attribute set (Object.cpp:608-662, base ClassAttributes >0 only
            // + SetWeaponDamage(1,5)); wire-verified vs cpp_loopback 0x96 hero msgs #607/612/617.
            var attrs = new AttributeDataUpdatePacket { ObjectId = charObjId };
            var c = squad[i];
            if (c.Strength > 0f) attrs.Set(AttributeDataUpdatePacket.Strength, c.Strength);
            if (c.Dexterity > 0f) attrs.Set(AttributeDataUpdatePacket.Dexterity, c.Dexterity);
            if (c.Mind > 0f) attrs.Set(AttributeDataUpdatePacket.Mind, c.Mind);
            attrs.Set(AttributeDataUpdatePacket.MaxHealth, maxHp);
            attrs.Set(AttributeDataUpdatePacket.MaxMana, maxMp);
            if (c.PhysicalDefense > 0f) attrs.Set(AttributeDataUpdatePacket.PhysicalDefense, c.PhysicalDefense);
            if (c.EnergyDefense > 0f) attrs.Set(AttributeDataUpdatePacket.EnergyDefense, c.EnergyDefense);
            if (c.CriticalRating > 0f) attrs.Set(AttributeDataUpdatePacket.CriticalRating, c.CriticalRating);
            if (c.NonCombatSpeed > 0f) attrs.Set(AttributeDataUpdatePacket.NonCombatSpeed, c.NonCombatSpeed);
            if (c.CombatSpeed > 0f) attrs.Set(AttributeDataUpdatePacket.CombatSpeed, c.CombatSpeed);
            attrs.Set(AttributeDataUpdatePacket.AttackSpeedScale, 1f);
            attrs.Set(AttributeDataUpdatePacket.CooldownScale, 1f);
            attrs.Set(AttributeDataUpdatePacket.InvisibleToSecurityTeleporters, 1f);
            attrs.Set(AttributeDataUpdatePacket.MinWeaponDamage, 1f);
            attrs.Set(AttributeDataUpdatePacket.MaxWeaponDamage, 5f);
            client.SendPacket(attrs);
        }
        _deckObjectIds[player.Slot] = deckObjectIds;

        PopulateLevel(client);

        // C++ deploys deck index 1; clamp for squads smaller than that.
        var deployIndex = Math.Min(DeployedDeckIndex, deckObjectIds.Length - 1);
        _playerCharacterObjectIds[player.Slot] = deckObjectIds[deployIndex];

        Log.Game.Info($"OnPlayerStart: spawned {squad.Count} hero objects; deploying deck={deployIndex} objId={deckObjectIds[deployIndex]}");

        SwapCharacter(client, player, deployIndex, deckObjectIds[deployIndex]);
    }

    // C++ Instance::OnPlayerStart populates the world in three name-filtered passes
    // (Instance.cpp:318-391); every other marker (design blocks, spawn points, lights, VFX,
    // paths) stays server-side and is never sent to the client. No InteractableDataUpdate at
    // load — C++ sends 0x98 only on runtime loot drops (Instance.cpp:403-410 vs 663-667).
    private static readonly uint HealthObeliskNoun = DbpfReader.FnvHash("prefab_health_obelisk.Noun");
    private static readonly uint BossObeliskNoun = DbpfReader.FnvHash("prefab_boss_obelisk.Noun");
    // C++ TriggerVolume ctor (ObjectManager.cpp:13): teleporter markers reach the client as a
    // hidden SecurityTeleporter — scale 0, no collision — not as the marker's own noun.
    private static readonly uint SecurityTeleporterNoun = DbpfReader.FnvHash("SecurityTeleporter.noun");
    private static readonly uint[] DirectorSpawnPointNouns =
    {
        DbpfReader.FnvHash("SpawnPoint_Director.Noun"),
        DbpfReader.FnvHash("SpawnPoint_DirectorA.Noun"),
        DbpfReader.FnvHash("SpawnPoint_DirectorB.Noun"),
        DbpfReader.FnvHash("SpawnPoint_DirectorWanderer.Noun")
    };

    private IEnumerable<AssetValue> MarkersOf(string markersetName)
    {
        if (Assets?.GetMarkerSetByName(markersetName)?.FindByName("markers") is not ArrayValue markers)
            yield break;
        foreach (var marker in markers.Items)
            yield return marker;
    }

    private void PopulateLevel(RakNetClient client)
    {
        if (Assets is null || string.IsNullOrEmpty(Chain.LevelName)) return;
        var level = Chain.LevelName;

        int obelisks = 0, teleporters = 0, enemies = 0;

        // Obelisks: "<level>_obelisk_1" markers carrying exactly the two obelisk nouns (Instance.cpp:318-329).
        foreach (var marker in MarkersOf($"{level}_obelisk_1"))
        {
            var noun = DbpfReader.FnvHash(marker.FindByName("nounDef").AsString());
            if (noun != HealthObeliskNoun && noun != BossObeliskNoun) continue;

            var scale = marker.FindByName("scale").AsFloat();
            SpawnWorldObject(client, noun, marker, scale <= 0f ? 1f : scale, hasCollision: true);
            obelisks++;
        }

        // Teleporters: only "_design"/"_design_spawners" markers with real teleporter component
        // data (Instance.cpp:331-343). Binary markersets serialize componentData on every marker,
        // so presence means a non-empty teleporter node — never the node itself.
        foreach (var setName in new[] { $"{level}_design", $"{level}_design_spawners" })
        {
            foreach (var marker in MarkersOf(setName))
            {
                var teleporter = marker.FindByName("componentData").FindByName("teleporter");
                if (teleporter is null || teleporter.Children.Count == 0) continue;

                SpawnWorldObject(client, SecurityTeleporterNoun, marker, scale: 0f, hasCollision: false);
                teleporters++;
            }
        }

        // Director enemies: one per AI-wander markerset, noun drawn from the level's enemy bank —
        // never the SpawnPoint marker's own noun (Instance.cpp:345-391, including the one-per-set cap).
        var enemyBank = Chain.EnemyNouns.Where(n => n != 0).ToArray();
        string[][] wanderSetNames =
        {
            new[] { $"{level}_AI_Wander" },
            new[] { $"{level}_AI_WandererA", $"{level}_AI_Wanderers_A" },
            new[] { $"{level}_AI_WandererB", $"{level}_AI_Wanderers_B" },
            new[] { $"{level}_AI_WandererC", $"{level}_AI_Wanderers_C" }
        };
        foreach (var candidates in wanderSetNames)
        {
            if (enemyBank.Length == 0) break;
            foreach (var marker in candidates.SelectMany(MarkersOf))
            {
                var markerNoun = DbpfReader.FnvHash(marker.FindByName("nounDef").AsString());
                if (!DirectorSpawnPointNouns.Contains(markerNoun)) continue;

                var enemyNoun = enemyBank[Random.Shared.Next(enemyBank.Length)];
                SpawnWorldObject(client, enemyNoun, marker, scale: 1f, hasCollision: true);
                enemies++;
                break;
            }
        }

        Log.Game.Info($"PopulateLevel({level}): {obelisks} obelisks, {teleporters} teleporter triggers, {enemies} director enemies");
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
            // Movement (C++ OnActionCommandMsgs Server.cpp:715-731). TeleportMovement=true:
            // D-015 verified contract — 0x90 snap + 0x91 flags 0x21. False: smooth path —
            // flags 0x001 only; the own-hero walk is client-side (CLIENT_MOVEMENT_CONTRACT.md).
            var (goalFlags, gx, gy, gz) = packet.ReadMovementData();
            var goal = new Vector3(gx, gy, gz);
            var locomotion = GetObjectLocomotion(packet.ObjectId);
            locomotion.SetGoalPosition(goal);
            locomotion.PartialGoalPosition = new Vector3(packet.PosX, packet.PosY, packet.PosZ);
            locomotion.GoalFlags |= goalFlags;

            if (TeleportMovement)
            {
                locomotion.GoalFlags |= 0x020;
                sender.SendPacket(new ObjectTeleportPacket { ObjectId = packet.ObjectId, Position = goal, Orientation = default });
            }
            sender.SendPacket(new ObjectPlayerMovePacket { ObjectId = packet.ObjectId, Locomotion = locomotion });
            Log.Game.Debug($"Move obj=0x{packet.ObjectId:X} -> ({gx:F1},{gy:F1},{gz:F1}) flags=0x{locomotion.GoalFlags:X}{(TeleportMovement ? "" : " (no teleport)")}");
        }
        else if (packet.CommandType == 4)
        {
            // C++ StopMovement → Locomotion::Stop (flags=0x020, GoalPosition preserved) → tick
            // sends 0x91 for player-controlled objects (Instance.cpp:967-970).
            var locomotion = GetObjectLocomotion(packet.ObjectId);
            locomotion.Stop();
            sender.SendPacket(new ObjectPlayerMovePacket { ObjectId = packet.ObjectId, Locomotion = locomotion });
        }
        else if (packet.CommandType == 5)
        {
            // C++ SwitchCharacter: read u32 creatureIndex → Player::SwapCharacter (deck index +
            // PlayerBits) → broadcast PlayerCharacterDeploy → LPU (Server.cpp:750-755,
            // Instance.cpp:523-539, Player.cpp:234-241).
            var player = GetPlayerByClient(sender);
            if (player == null) return;

            var creatureIndex = (int)packet.ReadSwitchIndex();
            if (!_deckObjectIds.TryGetValue(player.Slot, out var deckIds) ||
                creatureIndex < 0 || creatureIndex >= deckIds.Length)
            {
                Log.Game.Warn($"SwitchCharacter: invalid index {creatureIndex}");
                return;
            }

            _playerCharacterObjectIds[player.Slot] = deckIds[creatureIndex];
            SwapCharacter(sender, player, creatureIndex, deckIds[creatureIndex]);
        }
    }

    private LocomotionData GetObjectLocomotion(uint objectId)
    {
        if (!_objectLocomotion.TryGetValue(objectId, out var locomotion))
        {
            locomotion = new LocomotionData();
            _objectLocomotion[objectId] = locomotion;
        }
        return locomotion;
    }
}
