using System.Collections.Concurrent;
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
    public GameType Type { get; } = gameType;
    public int MaxPlayers { get; }
    public double GameClock = 999999999;
    public AssetDatabase? Assets { get; } = assetDatabase;
    public ChainData Chain { get; } = new();
    public ObjectManager Objects { get; } = new(assetDatabase);
    public GameScheduler Scheduler { get; } = new();

    // Resolves the player's chosen squad (account, 1-based squadId) into the creatures
    // they own and saved in that deck. Set by GameService; mirrors C++ user->GetSquadById.
    public Func<AccountModel, int, IReadOnlyList<SquadCreature>>? SquadResolver { get; set; }

    private DateTime _lastTick = DateTime.UtcNow;

    // Inbound gameplay packets are enqueued from the RakNet receive thread and drained here on the
    // game-loop thread at the top of every Update(). This makes the whole game simulation — packet
    // handling, object/AI ticks, and all Lua — run single-threaded on the game loop. It removes the
    // cross-thread lock-ordering deadlock between GameScriptContext._luaGate (held by the game loop
    // while a broadcast calls RakNexus session.Send → ReliabilityLayer._syncLock) and the receive
    // thread (holding ReliabilityLayer._syncLock while dispatching → HandlePacket → InvokeAbility →
    // _luaGate). RakNexus already delivers reliably ordered, so FIFO drain preserves order.
    private readonly ConcurrentQueue<(RakNetClient Client, IRakNetPacket Packet)> _inbound = new();

    public void EnqueueInbound(RakNetClient client, IRakNetPacket packet) => _inbound.Enqueue((client, packet));

    public int PendingInboundCount => _inbound.Count;

    private void DrainInbound()
    {
        while (_inbound.TryDequeue(out var item))
        {
            try
            {
                HandlePacket(item.Client, item.Packet);
            }
            catch (Exception ex)
            {
                Log.Game.Error($"HandlePacket({item.Packet.Type}) failed: {ex}");
            }
        }
    }

    private uint _nextObjectId = 1;
    private readonly Dictionary<byte, uint> _playerCharacterObjectIds = new();
    private readonly Dictionary<uint, LocomotionData> _objectLocomotion = new();
    private readonly Dictionary<uint, int> _interactableTimesUsed = new();
    private readonly Dictionary<byte, uint[]> _deckObjectIds = new();
    private readonly Dictionary<byte, IReadOnlyList<SquadCreature>> _playerSquads = new();
    public GameState State { get; private set; } = GameState.Initializing;

    public Services.Scripting.GameScriptContext? ScriptContext { get; set; }

    // Mirrors C++ teleportMovement (Server.cpp:76). Default false = smooth movement (0x91
    // flags 0x001; own-hero walk is client-side, enabled by D-017/D-017b ClassAttributes).
    // CLI --teleport-movement re-enables the D-015 snap contract (0x90 + 0x91 flags 0x21)
    // as a fallback for creatures whose data lacks locomotion speeds.
    public static bool TeleportMovement { get; set; } = false;

    public IEnumerable<ulong> GetPlayerIds() => Players.Where(p => !p.Value.IsBot).Select(p => p.Value.Id);

    public void Update()
    {
        DrainInbound();

        var now = DateTime.UtcNow;
        var delta = (now - _lastTick).TotalSeconds;
        _lastTick = now;
        Objects.Update(delta);
        Scheduler.Tick(delta);
        ScriptContext?.Tick();
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
        player.Client?.SendPacket(helloPlayer);
    }

    private void OnPartyMergeComplete(Player player)
    {
        player.Client?.SendPacket(new PartyMergeCompletePacket());
    }

    private void OnPlayerJoined(Player joiningPlayer)
    {
        var playerJoinedPacket = new PlayerJoinedPacket(joiningPlayer.Slot);
        var otherPlayerJoinedPacket = new PlayerJoinedPacket();

        foreach (var player in Players)
        {
            if (player.Value.Slot != joiningPlayer.Slot)
                player.Value.Client?.SendPacket(playerJoinedPacket);

            otherPlayerJoinedPacket.Slot = player.Value.Slot;
            joiningPlayer.Client?.SendPacket(otherPlayerJoinedPacket);
        }

        foreach (var bot in Bots)
        {
            otherPlayerJoinedPacket.Slot = bot.Value.Slot;
            joiningPlayer.Client?.SendPacket(otherPlayerJoinedPacket);
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
                IRakNetPacket locomotionPacket;
                if ((obj.GoalFlags & 0x020) != 0)
                    locomotionPacket = new ObjectTeleportPacket { ObjectId = obj.ObjectId, Position = obj.Position, Orientation = obj.Orientation };
                else if ((obj.GoalFlags & 0x001) != 0)
                    // 0x95 smooth-move channel (client OnGmsLocomotionDataUnreliableUpdate @0x0053e600).
                    locomotionPacket = new LocomotionDataUnreliableUpdatePacket { ObjectId = obj.ObjectId, GoalPosition = obj.GoalPosition };
                else
                    locomotionPacket = new LocomotionDataUpdatePacket { ObjectId = obj.ObjectId, Locomotion = new LocomotionData { GoalFlags = obj.GoalFlags } };
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

            case CrystalDragMessagePacket crystalDrag:
                HandleCrystalDrag(sender, crystalDrag);
                break;

            case LootDropMessagePacket lootDrop:
                // Unknown layout (C++ Server.cpp:1093 is a hexdump stub) — capture for mapping.
                Log.Game.Info($"LootDropMessage ({lootDrop.RawData.Length}B): {Convert.ToHexString(lootDrop.RawData)}");
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

    // C++ ObjectManager::Create(marker) + SendObjectCreate. Wire-verified vs cpp_loopback
    // (zelems_1, decoded 2026-06-05, tools/scratch/decode_creates.py):
    //   marker objects (SecurityTeleporter 93B) = createData all-10 with ROT ZEROS
    //     + object-reflection {6,7,8,17,22} (markerId sent);
    //   enemies (81B) = same createData shape (scale=1, team=0, hasColl=1)
    //     + object-reflection {6,7} ONLY — no scale/collision/markerId on the wire.
    // The capture leaves createData rot zeroed even for rotated markers — marker rotDegrees
    // are NOT propagated; orientation rides the reflection quat.
    private void SpawnWorldObject(RakNetClient client, uint noun, AssetValue marker, float scale, bool hasCollision, bool bindMarker = true)
    {
        var pos = marker.FindByName("pos").AsVector3();
        var markerId = marker.FindByName("markerId").AsUInt32();

        var objId = _nextObjectId++;
        var enemy = Objects.Spawn(objId, noun, pos, scale, team: 0, playerControlled: false);
        if (enemy.Agent is not null && enemy.AIDefinition is not null && Assets is not null && ScriptContext is not null)
            Objects.AttachController(enemy.ObjectId,
                new AI.AIController(enemy.AIDefinition, Assets.GetAssetByName,
                    new AI.BridgeAiActions(ScriptContext)));

        var objData = new SporelabsObject
        {
            Position = pos,
            Orientation = Quaternion.Identity
        };
        objData.SetDataBit(6);
        objData.SetDataBit(7);
        if (bindMarker)
        {
            objData.Scale = scale;
            objData.HasCollision = hasCollision;
            objData.SourceMarkerKeyMarkerId = markerId;
            objData.SetDataBit(8);
            objData.SetDataBit(17);
            objData.SetDataBit(22);
        }

        client.SendPacket(new ObjectCreatePacket
        {
            ObjectId = objId,
            CreateData = new GameObjectCreateData
            {
                Noun = noun,
                Position = pos,
                Scale = scale,
                Team = 0,
                HasCollision = hasCollision,
                PlayerControlled = false
            },
            ObjectData = objData
        });
        Log.Game.Debug($"WorldObject obj=0x{objId:X} noun=0x{noun:X8} at ({pos.X:F0},{pos.Y:F0},{pos.Z:F0}){(bindMarker ? $" marker=0x{markerId:X8}" : " (enemy shape)")}");
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

            // bindMarker:false EXPERIMENT (D-023): capture has zero obelisk samples; the only
            // render-verified world-object shape is the bare enemy {6,7}. Marker-bound obelisks
            // ({6,7,8,17,22}) stayed invisible at point-blank range (scaldron_4 gate 2026-06-05).
            var scale = marker.FindByName("scale").AsFloat();
            SpawnWorldObject(client, noun, marker, scale <= 0f ? 1f : scale, hasCollision: true, bindMarker: false);
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
                RegisterTeleporterTrigger(marker, teleporter);
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
                SpawnWorldObject(client, enemyNoun, marker, scale: 1f, hasCollision: true, bindMarker: false);
                enemies++;
                break;
            }
        }

        Log.Game.Info($"PopulateLevel({level}): {obelisks} obelisks, {teleporters} teleporter triggers, {enemies} director enemies");
    }

    // Server-side teleporter activation (D-023). C++ never implemented trigger volumes
    // (asset schema only, no onEnter handling) — walking into a teleporter did nothing there
    // either. Contract used here is the D-015 client-verified teleport pair (0x90 pos +
    // 0x91 flags|=0x20). The hero position comes from the client itself: every ActionCommand
    // header carries the current hero pos, streamed ~5×/s while walking.
    private readonly record struct TeleporterTrigger(
        Vector3 Position, float Radius, uint DestinationMarkerId, uint OnEnterEvent, uint OnExitEvent);
    private readonly List<TeleporterTrigger> _teleporterTriggers = new();
    private readonly Dictionary<uint, DateTime> _teleportCooldowns = new();
    private Dictionary<uint, Vector3>? _markerPositionsById;

    private void RegisterTeleporterTrigger(AssetValue marker, AssetValue teleporter)
    {
        var destination = teleporter.FindByName("destinationMarkerId").AsUInt32();
        if (destination == 0) return;

        // TriggerVolumeDef dims (AssetCatalog.cpp:225-248): sphereRadius, else box half-extent.
        // events.onEnterEvent/onExitEvent = .ServerEventDef keys (FX, data-driven) — played via
        // ServerEvent 0x9B (client contract: ServerEventPacket.cs).
        var volume = teleporter.FindByName("triggerVolume");
        var radius = volume?.FindByName("sphereRadius").AsFloat() ?? 0f;
        if (radius <= 0f)
        {
            var boxW = volume?.FindByName("boxWidth").AsFloat() ?? 0f;
            var boxL = volume?.FindByName("boxLength").AsFloat() ?? 0f;
            radius = MathF.Max(boxW, boxL) / 2f;
        }
        if (radius <= 0f) radius = 4f;

        var events = volume?.FindByName("events");
        var onEnter = events?.FindByName("onEnterEvent").AsUInt32() ?? 0;
        var onExit = events?.FindByName("onExitEvent").AsUInt32() ?? 0;
        if (onEnter != 0 || onExit != 0)
            Log.Game.Debug($"Teleporter trigger events: enter=0x{onEnter:X8} exit=0x{onExit:X8}");

        _teleporterTriggers.Add(new TeleporterTrigger(
            marker.FindByName("pos").AsVector3(), radius, destination, onEnter, onExit));
    }

    private Vector3? ResolveMarkerPosition(uint markerId)
    {
        if (Assets is null) return null;
        if (_markerPositionsById is null)
        {
            _markerPositionsById = new Dictionary<uint, Vector3>();
            foreach (var set in Assets.MarkerSets.Values)
            {
                if (set.FindByName("markers") is not ArrayValue markers) continue;
                foreach (var marker in markers.Items)
                    _markerPositionsById.TryAdd(marker.FindByName("markerId").AsUInt32(), marker.FindByName("pos").AsVector3());
            }
        }
        return _markerPositionsById.TryGetValue(markerId, out var pos) ? pos : null;
    }

    private void CheckTeleporterTriggers(RakNetClient client, uint objectId, Vector3 heroPos)
    {
        if (_teleporterTriggers.Count == 0) return;
        if (_teleportCooldowns.TryGetValue(objectId, out var until) && DateTime.UtcNow < until) return;

        foreach (var trigger in _teleporterTriggers)
        {
            if (Vector3.DistanceSquared(heroPos, trigger.Position) > trigger.Radius * trigger.Radius) continue;

            if (ResolveMarkerPosition(trigger.DestinationMarkerId) is not Vector3 destination)
            {
                Log.Game.Warn($"Teleporter dest marker 0x{trigger.DestinationMarkerId:X8} unresolved");
                return;
            }

            _teleportCooldowns[objectId] = DateTime.UtcNow.AddSeconds(3);

            // Data-driven teleport FX (trigger volume onEnter/onExit .ServerEventDef keys):
            // enter at the source on the hero, exit at the destination after the snap.
            if (trigger.OnEnterEvent != 0)
                client.SendPacket(new ServerEventPacket { ServerEventDef = trigger.OnEnterEvent, ObjectId = objectId });

            var locomotion = GetObjectLocomotion(objectId);
            locomotion.SetGoalPosition(destination);
            locomotion.GoalFlags |= 0x020;
            client.SendPacket(new ObjectTeleportPacket { ObjectId = objectId, Position = destination, Orientation = default });
            client.SendPacket(new ObjectPlayerMovePacket { ObjectId = objectId, Locomotion = locomotion });

            if (trigger.OnExitEvent != 0)
                client.SendPacket(new ServerEventPacket { ServerEventDef = trigger.OnExitEvent, ObjectId = objectId });

            Log.Game.Info($"Teleporter: obj=0x{objectId:X} ({heroPos.X:F0},{heroPos.Y:F0},{heroPos.Z:F0}) -> ({destination.X:F0},{destination.Y:F0},{destination.Z:F0}) fx=({trigger.OnEnterEvent:X8}/{trigger.OnExitEvent:X8})");
            return;
        }
    }

    // C++ Instance::SwapCharacter: set current deck index, broadcast PlayerCharacterDeploy,
    // send LPU. Without it the client never binds an active hero and fades out.
    // Client slot semantics (Ghidra BuildAbilityCommandStruct @0x004d8a80 + slot resolver
    // FUN_009c7180): type byte = 8 - (slot < 5); slots 0-4 = the CURRENT hero's PlayerClass
    // ability fields [basic, special1, special2, special3, passive]; slots 6/7/8 (type 8)
    // recurse into the squad character (client charIdx 4) — not modeled yet, ack-only.
    private void InvokeAbilityCommand(RakNetClient sender, byte commandType, uint slotIndex, uint targetId, Vector3 cursor, int rank)
    {
        var player = GetPlayerByClient(sender);
        if (player is null || ScriptContext is null) return;

        if (commandType == 8)
        {
            Log.Game.Info($"UseSquadAbility slot={slotIndex}: squad character not modeled yet — ack only");
            return;
        }

        if (!_playerSquads.TryGetValue(player.Slot, out var squad) || squad.Count == 0)
        {
            Log.Game.Warn($"Ability cast: no resolved squad for player slot={player.Slot}");
            return;
        }

        int deckIndex = player.PlayerData?.CurrentDeckIndex ?? 0;
        if (deckIndex >= squad.Count) deckIndex = 0;
        var creature = squad[deckIndex];

        var abilitySlots = Assets?.ResolveAbilitySlots(creature.Noun);
        if (abilitySlots is null)
        {
            Log.Game.Warn($"Ability cast: no PlayerClass ability slots for noun=0x{creature.Noun:X8}");
            return;
        }
        if (slotIndex >= abilitySlots.Length || abilitySlots[slotIndex] == 0)
        {
            Log.Game.Warn($"Ability cast: empty ability slot {slotIndex} for noun=0x{creature.Noun:X8}");
            return;
        }

        var abilityHash = abilitySlots[slotIndex];
        var heroObjectId = _playerCharacterObjectIds.GetValueOrDefault(player.Slot);
        var entry = ScriptContext.Registry.Find(Adapters.Scripting.ScriptKind.Ability, abilityHash);
        var invoked = ScriptContext.InvokeAbility(abilityHash, heroObjectId, targetId, cursor.X, cursor.Y, cursor.Z, rank);
        var outcome = invoked ? "invoked"
            : entry is null ? "refused: not in registry"
            : !entry.HasTick ? "refused: no tick in __index chain (channeled/vtable family — pending dispatch contract)"
            : "refused: agent thread busy";
        Log.Game.Info($"Cast slot={slotIndex} ability='{entry?.Name ?? "?"}' hash=0x{abilityHash:X8} " +
                      $"agent={heroObjectId} target=0x{targetId:X} rank={rank} → {outcome}");
    }

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
        Log.Game.Debug($"ActionCommand: type={packet.CommandType} stamp={packet.CommandStamp} obj=0x{packet.ObjectId:X} pos=({packet.PosX:F1},{packet.PosY:F1},{packet.PosZ:F1})");

        // The 0x9C common header carries the hero's live position + orientation — adopt them so
        // Lua facing/cone tests (GetFacing → CircleIntersectsArc) see the real heading.
        if (Objects.Objects.TryGetValue(packet.ObjectId, out var heroObject))
        {
            heroObject.Position = new Vector3(packet.PosX, packet.PosY, packet.PosZ);
            heroObject.Orientation = new Quaternion(packet.OriX, packet.OriY, packet.OriZ, packet.OriW);
        }

        if (packet.CommandType is 3 or 4 or 5 or 10)
            ClearActiveEmote(sender, packet.ObjectId);

        CheckTeleporterTriggers(sender, packet.ObjectId, new Vector3(packet.PosX, packet.PosY, packet.PosZ));

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
        else if (packet.CommandType == 6)
        {
            // Overdrive (client emit @0x004ebb50, payload u8 heroSlot) — not in the C++ ref.
            // v1: ack with response type=2 so the client command lock clears; the overdrive
            // effect itself is Simulation-phase work.
            SendActionCommandResponse(sender, packet.CommandStamp);
            Log.Game.Info($"Overdrive slot={packet.ReadOverdriveSlot()} (stub)");
        }
        else if (packet.CommandType is 7 or 8)
        {
            // UseCharacterAbility / UseSquadAbility (44B ActionCommandAbilityData,
            // C++ Types.h:855, client emit @0x004d8a80). Response stays type=2 —
            // clears the client's 3s pending lock (FUN_004d9150) without arming the
            // case-1 ability path (which drives locomotion/FX from response fields;
            // that arming is Simulation-phase work).
            var (targetId, cursorPos, targetPos, index, rank, userData) = packet.ReadAbilityData();
            SendActionCommandResponse(sender, packet.CommandStamp);
            Log.Game.Debug($"Ability type={packet.CommandType} slot={index} rank={rank} target=0x{targetId:X} " +
                           $"cursor=({cursorPos.X:F1},{cursorPos.Y:F1},{cursorPos.Z:F1}) " +
                           $"targetPos=({targetPos.X:F1},{targetPos.Y:F1},{targetPos.Z:F1}) userData=0x{userData:X}");
            InvokeAbilityCommand(sender, packet.CommandType, index, targetId, cursorPos, rank);
        }
        else if (packet.CommandType == 9)
        {
            // CatalystPickup (20B ActionCommandCatalystData, C++ Types.h:847, client emit
            // @0x0044ece0). C++ routes to Instance::InteractWithObject (Server.cpp:942-947).
            // v1: same interact bookkeeping as type 11 + lock-clearing ack; loot phase pending.
            var (targetId, position) = packet.ReadCatalystData();
            RecordInteraction(sender, targetId);
            SendActionCommandResponse(sender, packet.CommandStamp);
            Log.Game.Info($"CatalystPickup obj=0x{targetId:X} pos=({position.X:F1},{position.Y:F1},{position.Z:F1})");
        }
        else if (packet.CommandType == 10)
        {
            // Cancel (C++ Server.cpp:950-955 → Instance::CancelAction, which only stops the
            // object's Lua action thread — none here yet). Emote clear already handled above.
            Log.Game.Debug($"Cancel obj=0x{packet.ObjectId:X}");
        }
        else if (packet.CommandType == 11)
        {
            // C++ UseInteractableObject (Server.cpp case 11 → Instance::InteractWithObject,
            // Instance.cpp:541-578): TimesUsed++ on the interactable, then per noun type
            // (Loot/Crystal pickups; default = dev-shortcut DropLoot — loot phase pending).
            // v1: track TimesUsed + send 0x98 so the client sees the obelisk state change.
            var targetId = packet.ReadInteractableObjectId();
            RecordInteraction(sender, targetId);
            SendActionCommandResponse(sender, packet.CommandStamp);
        }
        else if (packet.CommandType is 12 or 13)
        {
            // C++ Dance/Taunt (Server.cpp:966-977): SendAnimationState with the emote anim hash,
            // overlay=false, scale=1 (Instance.cpp:990, defaults Instance.h:202).
            // C++ alone is incomplete: the client arms a 3s pending-command lock when it sends
            // type 12/13 (FUN_004d9150 → block+4, deadline block+0x10) and hard-rejects movement
            // clicks until ActionCommandResponse type=2 echoes the command stamp (FUN_004d9ba0
            // case 2 → clears lock + resets anim). Response must go FIRST so the emote anim
            // sent after it survives the case-2 anim reset.
            SendActionCommandResponse(sender, packet.CommandStamp);
            var emoteState = packet.CommandType == 12 ? EmoteDanceState : EmoteTauntState;
            SendAnimationState(sender, packet.ObjectId, emoteState);
            _activeEmotes[packet.ObjectId] = emoteState;
        }
    }

    private static readonly uint EmoteDanceState = ObjectivesInitForLevelPacket.FnvHash("emote_dance_all");
    private static readonly uint EmoteTauntState = ObjectivesInitForLevelPacket.FnvHash("emote_taunt_all");

    private readonly Dictionary<uint, uint> _activeEmotes = new();

    // Looping emotes (emote_*_all have no end, anim flag +0x70=0 in FUN_004ddde0) never
    // terminate client-side; retail-like cancel = clear the scripted anim when the player
    // issues the next move/stop/switch/cancel command.
    private void ClearActiveEmote(RakNetClient client, uint objectId)
    {
        if (!_activeEmotes.Remove(objectId)) return;
        SendAnimationState(client, objectId, 0);
    }

    // Response type=2 (FUN_004d9ba0 case 2): clears the client pending-command lock when
    // SyncStamp echoes ActionCommandMsgs byte +0x01. UserData=0xFFFFFFFF (<0) skips the
    // client's DAT_0143fe3c write (FUN_004e2030).
    private void SendActionCommandResponse(RakNetClient client, byte stamp)
    {
        client.SendPacket(new ActionCommandResponsePacket
        {
            SyncStamp = stamp,
            ResponseType = 2,
            UserData = 0xFFFFFFFF
        });
    }

    // C++ OnCrystalDragMessage (Server.cpp:1026-1091). The catalyst grid only gains content
    // via loot pickups (Instance.cpp:1103) — none exist before the loot phase, so every drag
    // resolves exactly like C++ with an empty grid: reject (type 3, slot echo → the client
    // snaps the crystal back, ClientNet::OnGmsCrystalMessage @0x0053f700 reads fixed 29B).
    // MoveType 2 grid-swap + 0 drop-to-world land with the loot phase (need Player catalysts
    // + CrystalBits LPU updates + DropCatalyst world loot, Player.cpp:374-391).
    private void HandleCrystalDrag(RakNetClient sender, CrystalDragMessagePacket packet)
    {
        Log.Game.Debug($"CrystalDrag slot={packet.CrystalSlot} move={packet.MoveType} newSlot={packet.NewSlot}");
        sender.SendPacket(new CrystalMessagePacket
        {
            MoveType = 3,
            Slot = packet.CrystalSlot,
            NewSlot = 0
        });
    }

    private void RecordInteraction(RakNetClient client, uint targetId)
    {
        var timesUsed = _interactableTimesUsed.GetValueOrDefault(targetId) + 1;
        _interactableTimesUsed[targetId] = timesUsed;

        client.SendPacket(new InteractableDataUpdatePacket
        {
            ObjectId = targetId,
            TimesUsed = timesUsed,
            UsesAllowed = 0,
            Ability = 0
        });
        Log.Game.Info($"Interact obj=0x{targetId:X} timesUsed={timesUsed}");
    }

    private void SendAnimationState(RakNetClient client, uint objectId, uint state, bool overlay = false, float scale = 1f)
    {
        client.SendPacket(new SetAnimationStatePacket
        {
            ObjectId = objectId,
            State = state,
            Timestamp = (ulong)(DateTime.UtcNow - StartTime).TotalMilliseconds,
            Overlay = overlay,
            Scale = scale
        });
    }

    // Object death sink (D-025). Fired from the HP-mutation point (GameScriptContext.ApplyHeal) the
    // first time an object reaches <=0 hp. Despawns the object: removes it server-side so it stops
    // being a valid target (ends the corpse re-target loop) and tells every client to delete it via
    // ObjectDelete 0x8E. The client handler (OnGmsObjectDelete @0x0053ddc0) removes immediately — no
    // death animation is driven by this message. Loot/objective hooks land with the loot phase.
    public void OnObjectDeath(uint objectId)
    {
        if (!Objects.Objects.TryGetValue(objectId, out var obj)) return;
        Log.Game.Info($"[death] object={objectId} noun=0x{obj.NounId:X8} despawned");
        BroadcastToAllPlayers(new ObjectDeletePacket { ObjectIds = [objectId] });
        Objects.Remove(objectId);
    }

    // nGameObject.AddEffect → 0x9B ServerEvent (client OnGmsServerEvent @0x0053ec80). Attached FX
    // recipe {6 ServerEventDef, 7 ObjectId} (+ AttackerId when an initiator is given).
    public void BroadcastServerEvent(ServerEventPacket packet) => BroadcastToAllPlayers(packet);

    // CombatEvent (0xBA) — floating damage/heal numbers + combat log; one per damage instance.
    public void BroadcastCombatEvent(CombatEventPacket packet) => BroadcastToAllPlayers(packet);

    // CooldownUpdate (0xC1) — ability-button cooldown swirl; sent when an ability with cooldown fires.
    public void BroadcastCooldownUpdate(CooldownUpdatePacket packet) => BroadcastToAllPlayers(packet);

    // Modifier (buff/debuff) lifecycle 0xA2/0xA4 — RequestModifier creates, MarkForDelete/expiry removes.
    public ModifierSystem Modifiers { get; } = new();
    public void BroadcastModifierCreated(ModifierCreatedPacket packet) => BroadcastToAllPlayers(packet);
    public void BroadcastModifierUpdated(ModifierUpdatedPacket packet) => BroadcastToAllPlayers(packet);
    public void BroadcastModifierDeleted(ModifierDeletedPacket packet) => BroadcastToAllPlayers(packet);

    // nObjectManager.CreateObject: allocate id, spawn server-side, and announce via the existing 0x8C
    // ObjectCreate (client OnGmsObjectCreate @0x0053f550 — reflection envelope, tolerates optional
    // fields) using the wire-verified enemy-shape field set {6,7}. Announce is gated on the noun
    // resolving server-side (unresolvable noun → server-side only, no wire) to avoid a client
    // noun-load crash. Returns the new object id.
    // DEFERRED: visible spawn is effectively inert for real nouns until a lossless asset-handle path
    // exists — nUtil.GetAsset returns (float)FNV and LUA_NUMBER=float (24-bit mantissa) drops hashes
    // above ~16.7M, so NounResolves fails and the spawn stays server-side-only (invisible, never a
    // crash). Fix needs an integer handle table, not a float-boxed hash.
    public uint SpawnScriptObject(uint nounId, Vector3 position)
    {
        var objId = _nextObjectId++;
        Objects.Spawn(objId, nounId, position, 1f, team: 0, playerControlled: false);
        if (Objects.NounResolves(nounId))
        {
            var objData = new SporelabsObject { Position = position, Orientation = Quaternion.Identity };
            objData.SetDataBit(6);
            objData.SetDataBit(7);
            BroadcastToAllPlayers(new ObjectCreatePacket
            {
                ObjectId = objId,
                CreateData = new GameObjectCreateData
                {
                    Noun = nounId,
                    Position = position,
                    Scale = 1f,
                    Team = 0,
                    HasCollision = false,
                    PlayerControlled = false
                },
                ObjectData = objData
            });
        }
        return objId;
    }

    // Lua nGameObject.SetAnimationState (client @0x009fc000 broadcasts via SporeNet message).
    public void BroadcastAnimationState(uint objectId, uint state)
    {
        Log.Game.Info($"[lua] SetAnimationState obj={objectId} state=0x{state:X8}");
        foreach (var player in Players.Values)
            if (player.Client is { } client)
                SendAnimationState(client, objectId, state);
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

    internal bool TryGetObjectPosition(uint objectId, out Vector3 pos)
    {
        if (!Objects.Objects.TryGetValue(objectId, out var obj)) { pos = default; return false; }
        if (_playerCharacterObjectIds.ContainsValue(objectId) &&
            _objectLocomotion.TryGetValue(objectId, out var loco))
        {
            pos = loco.PartialGoalPosition != default ? loco.PartialGoalPosition : obj.Position;
            return true;
        }
        pos = obj.Position;
        return true;
    }
}
