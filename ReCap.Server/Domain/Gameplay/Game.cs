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
    private readonly Dictionary<byte, uint[]> _deckObjectIds = new();
    private readonly Dictionary<byte, IReadOnlyList<SquadCreature>> _playerSquads = new();
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

    // C++ Server::OnDebugPing Dungeon → mGame.SwapCharacter(player, 1): deck index 1 is deployed.
    private const int DeployedDeckIndex = 1;

    // Marker NPC/prop spawn — now noun-name filtered (obelisks + director enemies only),
    // so it no longer spawns the design markers that crashed the client. Toggle off to
    // isolate the deck-HUD path during crash testing.
    private const bool SpawnLevelMarkers = true;

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
                Log.Game.Info($"Resolved squad {packet.SquadId}: [{string.Join(", ", squad.Select(c => $"0x{c.Noun:X8} gs={c.GearScore:F0}"))}]");

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

    // Create + broadcast a single non-player level object (obelisk / enemy). Mirrors the
    // C++ ObjectManager::Create + SendObjectCreate path. Per C++: team/playerControlled stay
    // at their defaults (0 / false); only marker-created objects (obelisks) carry the marker
    // scale, enemies created from a bare noun keep scale 1.
    private void SpawnLevelNoun(RakNetClient client, uint noun, Vector3 pos, float scale)
    {
        var objId = _nextObjectId++;
        Objects.Spawn(objId, noun, pos, scale, team: 0, playerControlled: false);

        var objData = new SporelabsObject
        {
            Position = pos,
            Team = 0,
            PlayerControlled = false,
            Visible = true,
            HasCollision = true,
            Scale = scale,
            MarkerScale = scale
        };
        // C++ marker object block = {Team, Visible, HasCollision} → 59B ObjectCreate.
        foreach (byte bit in new byte[] { 0, 16, 17 }) objData.SetDataBit(bit);

        client.SendPacket(new ObjectCreatePacket
        {
            ObjectId = objId,
            CreateData = new GameObjectCreateData
            {
                Noun = noun,
                Position = pos,
                Scale = scale,
                Team = 0,
                HasCollision = true,
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

        // C++ deploys deck index 1; clamp for squads smaller than that.
        var deployIndex = Math.Min(DeployedDeckIndex, deckObjectIds.Length - 1);
        _playerCharacterObjectIds[player.Slot] = deckObjectIds[deployIndex];

        Log.Game.Info($"OnPlayerStart: spawned {squad.Count} hero objects; deploying deck={deployIndex} objId={deckObjectIds[deployIndex]}");

        SwapCharacter(client, player, deployIndex, deckObjectIds[deployIndex]);
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
