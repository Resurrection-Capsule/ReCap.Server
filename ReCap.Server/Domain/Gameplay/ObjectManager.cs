using System.Collections.Concurrent;
using System.Numerics;
using AssetData.Parser.Model;
using ReCap.Server.Services;
using ReCap.Server.Services.Assets;

namespace ReCap.Server.Domain.Gameplay;

[Flags]
public enum ObjectDirtyFlags : uint
{
    None       = 0,
    Locomotion = 1 << 0,
}

public sealed class GameObject
{
    public required uint ObjectId { get; init; }
    public required uint NounId { get; init; }
    public Vector3 Position { get; set; }
    // C++ Object::Initialize sets Orientation via SetOrientation; default Identity.
    public Quaternion Orientation { get; set; } = Quaternion.Identity;
    public float Scale { get; set; } = 1f;
    public byte Team { get; set; }
    public byte PlayerId { get; set; }
    public bool PlayerControlled { get; set; }
    public float Health { get; set; }
    public float MaxHealth { get; set; }
    // Set once when HP first reaches <=0 (C++ Object death, OnObjectDeath). Guards against firing the
    // death sink twice and excludes the corpse from targeting queries before it is swept.
    public bool Dead { get; set; }
    public uint TargetId { get; set; }
    // kAttribute id → value. CONFIRMED ids (Ghidra GetAttributeValue @0x009feca0 switch sites):
    // 0=Strength 1=Dexterity 2=Mind 4=MaxHealth. Remaining ids of the 116-wide domain are
    // unverified — reads of unknown ids return 0 and are debug-logged for harvesting.
    public Dictionary<int, float> Attributes { get; } = new();
    public AssetValue? AIDefinition { get; set; }
    // Mirrors C++ Locomotion::GoalFlags. Default 0x020 = stop/teleport bit (set by Locomotion::Stop() in ctor).
    public uint GoalFlags { get; set; } = 0x020;
    // Locomotion goal state (mirrors C++ Locomotion component). GoalPosition drives the 0x95
    // smooth-move broadcast; TargetPosition is the homing target (SetTargetPosition); Facing is
    // set by TurnToFace (server-side only in Wave 2). NavCollisionDisabled mirrors client obj+0x284
    // (SetNavCollision writes the inverted collidable flag; server-side pathfinding state, no wire).
    public Vector3 GoalPosition { get; set; }
    public Vector3 TargetPosition { get; set; }
    public Vector3 Facing { get; set; }
    // DEFERRED: real per-noun move speed comes from the LocomotionTuning asset (Ghidra
    // LocomotionTuning @0x00f798e0), not yet parsed. Default is a placeholder tuning value used only
    // by GetModifiedMoveSpeed's wind-down estimate; replace when tuning is parsed.
    public float MoveSpeed { get; set; } = DefaultMoveSpeed;
    public bool NavCollisionDisabled { get; set; }
    // Wave-2 stop-distance for the arrival estimate (WaitForNearGoal); not on the wire.
    public float DesiredStopDistance { get; set; }

    public const float DefaultMoveSpeed = 5.0f;
    public const float DefaultPerceptionRadius = 15f;
    public ObjectDirtyFlags DirtyFlags { get; set; }
    // Non-null iff this object is an AI agent (mirrors cAgentBlackboard at client obj+0x2b0).
    public AI.AgentBlackboard? Agent { get; set; }
}

public sealed class ObjectManager
{
    private readonly AssetDatabase? _db;
    // Spawns/removes arrive on the RakNet packet thread (PopulateLevel, OnPlayerStart, Lua
    // MarkForDelete during InvokeAbility) while the game loop enumerates in FlushObjectUpdates —
    // a plain Dictionary threw "Collection was modified" and killed Update (2026-06-06 15:51 log).
    private readonly ConcurrentDictionary<uint, GameObject> _objects = new();
    private readonly AI.AggroSystem _aggro;
    // Same cross-thread hazard as _objects above: AttachController/Remove run on the RakNet
    // thread while Update enumerates on the game-loop thread.
    private readonly ConcurrentDictionary<uint, AI.AIController> _controllers = new();

    public IReadOnlyDictionary<uint, GameObject> Objects => _objects;

    public ObjectManager(AssetDatabase? db)
    {
        _db = db;
        _aggro = new AI.AggroSystem(this);
    }

    public void AttachController(uint objectId, AI.AIController controller) => _controllers[objectId] = controller;

    public GameObject Spawn(uint objectId, uint nounId, Vector3 position, float scale, byte team, bool playerControlled)
    {
        float maxHealth = 0f;
        float strength = 0f, dexterity = 0f, mind = 0f;
        float maxMana = 0f, physDefense = 0f, energyDefense = 0f, critical = 0f;
        float nonCombatSpeed = 0f, combatSpeed = 0f;
        AssetValue? aiDef = null;
        AssetValue? noun = null;

        if (_db is not null)
        {
            var attrs = _db.ResolveClassAttributesForCreature(nounId);
            if (attrs is not null)
            {
                // C++ Object::Initialize non-player path reads ClassAttributes baseHealth
                // (Object.cpp:677-680); the player path gets MaxHealth from Character save
                // data instead — our PlayerClass maxHealth read approximates that at gs=0.
                maxHealth = playerControlled
                    ? attrs.FindByName("maxHealth").AsFloat()
                    : attrs.FindByName("baseHealth").AsFloat();
                strength = attrs.FindByName("baseStrength").AsFloat();
                dexterity = attrs.FindByName("baseDexterity").AsFloat();
                mind = attrs.FindByName("baseMind").AsFloat();
                // Full ClassAttributes base block for NPCs → combatant attribute ids, so the object can
                // be replicated as a complete combatant to the client (C++ fills these for every object
                // with stats). Same fields the hero squad reads; also feeds server-side combat later.
                if (!playerControlled)
                {
                    maxMana = attrs.FindByName("maxMana").AsFloat();
                    if (maxMana <= 0f) maxMana = attrs.FindByName("baseMana").AsFloat();
                    physDefense = attrs.FindByName("basePhysicalDefense").AsFloat();
                    energyDefense = attrs.FindByName("baseEnergyDefense").AsFloat();
                    critical = attrs.FindByName("baseCritical").AsFloat();
                    nonCombatSpeed = attrs.FindByName("baseNonCombatSpeed").AsFloat();
                    combatSpeed = attrs.FindByName("baseCombatSpeed").AsFloat();
                }
            }

            noun = _db.GetNoun(nounId);
            if (noun is not null)
            {
                var aiRef = (noun.FindByName("aiDefinition") as StringValue)?.Value;
                if (!string.IsNullOrEmpty(aiRef))
                    aiDef = _db.GetAssetByName(aiRef);
            }
        }

        var obj = new GameObject
        {
            ObjectId = objectId,
            NounId = nounId,
            Position = position,
            Scale = scale,
            Team = team,
            PlayerControlled = playerControlled,
            Health = maxHealth,
            MaxHealth = maxHealth,
            AIDefinition = aiDef
        };
        obj.Attributes[0] = strength;
        obj.Attributes[1] = dexterity;
        obj.Attributes[2] = mind;
        obj.Attributes[4] = maxHealth;
        if (!playerControlled)
        {
            obj.Attributes[5] = maxMana;          // MaxMana
            obj.Attributes[7] = physDefense;      // PhysicalDefense
            obj.Attributes[9] = energyDefense;    // EnergyDefense
            obj.Attributes[10] = critical;        // CriticalRating
            obj.Attributes[11] = nonCombatSpeed;  // NonCombatSpeed
            obj.Attributes[12] = combatSpeed;     // CombatSpeed
        }
        // C++ Object::Initialize player path SetWeaponDamage(1,5) base (Object.cpp:608-662, mirrored
        // on the wire at Game.cs MinWeaponDamage/MaxWeaponDamage); melee GetDamage reads these
        // (kAttribute 101/102) when the agent is player-controlled. NPC weapon damage = parts, deferred.
        if (playerControlled)
        {
            obj.Attributes[101] = 1f;
            obj.Attributes[102] = 5f;
        }

        // Mirrors C++ Object::Initialize (Object.cpp:560-562): only nouns with hasLocomotion=true
        // get CreateLocomotionData → UpdateLocomotion dirty → ObjectTeleport on first tick.
        // TriggerVolumes bypass Initialize entirely (no locomotion). Static nouns default to false.
        if (!playerControlled && noun?.FindByName("hasLocomotion").AsBool() == true)
            obj.DirtyFlags = ObjectDirtyFlags.Locomotion;

        // AI agent = non-player noun with a resolved AIDefinition (the client's obj+0x2b0 gate).
        if (!playerControlled && aiDef is not null)
            obj.Agent = new AI.AgentBlackboard { PerceptionRadius = GameObject.DefaultPerceptionRadius };

        _objects[objectId] = obj;
        return obj;
    }

    public bool Remove(uint objectId)
    {
        _controllers.TryRemove(objectId, out _);
        return _objects.TryRemove(objectId, out _);
    }

    // True when the AssetDatabase can resolve this noun — used to gate the 0x8C ObjectCreate announce
    // for Lua-spawned objects (if the server resolves it, the client can too; unresolvable → no wire).
    public bool NounResolves(uint nounId) => _db?.GetNoun(nounId) is not null;

    public void Update(double deltaSeconds)
    {
        if (_objects.Count == 0) return;

        _aggro.Tick();
        foreach (var (id, controller) in _controllers.ToArray())
        {
            if (!_objects.TryGetValue(id, out var agent) || agent.Dead || agent.Agent is null) continue;
            try
            {
                controller.Tick(id, _aggro.BestTargetFor(agent));
            }
            catch (Exception ex)
            {
                ReCap.Server.Util.Logging.Log.Game.Error($"AI controller {id} tick failed: {ex}");
            }
        }
    }
}
