using System.Linq;
using ReCap.Server.Adapters.Scripting;
using ReCap.Server.Adapters.Scripting.Native;
using ReCap.Server.Domain.Gameplay;
using AI = ReCap.Server.Domain.Gameplay.AI;

namespace ReCap.Server.Services.Scripting;

public sealed class GameScriptContext : IScriptGameBridge, IDisposable
{
    private readonly Game _game;
    private readonly LuaRuntime _runtime;
    private readonly LuaCoroutineScheduler _scheduler;
    private readonly ScriptRegistry _registry;
    private readonly AI.AggroSystem _aggro;
    private double _clockSeconds;
    private uint _nextAbilityInstanceId;
    // lua_State is not thread-safe: Tick runs on the game loop, InvokeAbility on the RakNet
    // packet thread — serialize every entry into the runtime.
    private readonly Lock _luaGate = new();

    public GameScriptContext(Game game, ScriptEngine engine)
    {
        _game = game;
        _aggro = new AI.AggroSystem(_game.Objects);
        _runtime = engine.CreateBootedRuntime($"game-{game.Id}");
        var state = ScriptContextRegistry.Get(_runtime.L)!;
        state.GameBridge = this;
        _scheduler = state.Scheduler!;
        _registry = state.Registry;
    }

    public ScriptRegistry Registry => _registry;
    internal LuaRuntime Runtime => _runtime;
    public LuaCoroutineScheduler Scheduler => _scheduler;

    public void Tick()
    {
        lock (_luaGate)
        {
            _clockSeconds += 0.05;
            _scheduler.Tick(_clockSeconds);
        }
    }

    // Spawn a coroutine for the named ability's tick function.
    // Tick is called with (self, agentId, targetId, cursorX, cursorY, cursorZ, rank).
    // Context getters (nAbility.GetAgentID etc.) also resolve via per-thread invocation slot.
    // Retail contract (LUA_ABILITY_TICK_CONTRACT.md): 1-param ticks use getters; 9-param ticks
    // use positional args. Both work: Lua ignores extra caller args and getters always match.
    public bool InvokeAbility(uint abilityHash, uint agentId, uint targetId, float cursorX, float cursorY, float cursorZ, int rank)
    {
        lock (_luaGate)
        {
            var entry = _registry.Find(ScriptKind.Ability, abilityHash);
            if (entry is null || !entry.HasTick) return false;
            if (_scheduler.HasThreadForObject(agentId)) return false;
            // Cooldown gate (retail): refuse the cast while this (agent, ability) is still cooling
            // down — stamped by PayCooldownAndMana. Stops the AI enemy re-firing its attack every tick.
            if (ScriptContextRegistry.Get(_runtime.L) is { } state && state.IsOnCooldown(agentId, abilityHash, _scheduler.Now))
                return false;

            var L = _runtime.L;
            // TargetInRangeAtStart mirrors the client's cached at-cast flag (@0x00a410a0 reads a
            // byte stamped at ability start, not a live range test): true when the cast carried a
            // live target. Range-vs-distance refinement needs the ability's range prop (later).
            var targetInRange = targetId != 0 && _game.Objects.Objects.ContainsKey(targetId);
            var invocation = new AbilityInvocation(agentId, targetId, cursorX, cursorY, cursorZ, rank,
                AbilityHash: abilityHash,
                InstanceId: ++_nextAbilityInstanceId,
                TargetInRangeAtStart: targetInRange);

            LuaNative.lua_rawgeti(L, LuaNative.LUA_REGISTRYINDEX, entry.TableRef);
            LuaNative.lua_getfield(L, -1, "tick");
            LuaNative.lua_insert(L, -2);

            // Push (self, agentId, targetId, cursorX, cursorY, cursorZ, rank) = 7 args + fn = 8 on stack
            LuaNative.lua_pushnumber(L, (float)agentId);
            LuaNative.lua_pushnumber(L, (float)targetId);
            LuaNative.lua_pushnumber(L, cursorX);
            LuaNative.lua_pushnumber(L, cursorY);
            LuaNative.lua_pushnumber(L, cursorZ);
            LuaNative.lua_pushnumber(L, (float)rank);

            // fn + self(table) + 6 number args = 8 total values on top
            var threadL = _scheduler.SpawnFromStack(L, agentId, 8,
                beforeFirstResume: threadState =>
                {
                    var ctx = ScriptContextRegistry.Get(_runtime.L);
                    ctx?.SetInvocation(threadState, invocation);
                });

            LuaNative.lua_settop(L, 0);
            return threadL != 0;
        }
    }

    public void Dispose() => _runtime.Dispose();

    public bool TryGetPosition(uint objectId, out float x, out float y, out float z)
    {
        if (_game.TryGetObjectPosition(objectId, out var p)) { x = p.X; y = p.Y; z = p.Z; return true; }
        x = y = z = 0f;
        return false;
    }

    public float GetHitPoints(uint objectId) =>
        _game.Objects.Objects.TryGetValue(objectId, out var o) ? o.Health : 0f;

    public float GetMaxHitPoints(uint objectId) =>
        _game.Objects.Objects.TryGetValue(objectId, out var o) ? o.MaxHealth : 0f;

    public bool ObjectExists(uint objectId) => _game.Objects.Objects.ContainsKey(objectId);

    public byte GetTeam(uint objectId) =>
        _game.Objects.Objects.TryGetValue(objectId, out var o) ? o.Team : (byte)0;

    public void SetTeam(uint objectId, byte team)
    {
        if (_game.Objects.Objects.TryGetValue(objectId, out var o)) o.Team = team;
    }

    // Ghidra nPlayer::GetPlayerIdForObject@0x009ff410: object id -> player id (byte obj+0x55).
    public byte GetPlayerId(uint objectId) =>
        _game.Objects.Objects.TryGetValue(objectId, out var o) ? o.PlayerId : (byte)0;

    public bool IsPlayerControlled(uint objectId) =>
        _game.Objects.Objects.TryGetValue(objectId, out var o) && o.PlayerControlled;

    public uint GetTargetId(uint objectId) =>
        _game.Objects.Objects.TryGetValue(objectId, out var o) ? o.TargetId : 0u;

    public bool TryGetAttributeValue(uint objectId, int attributeId, out float value)
    {
        value = 0f;
        return _game.Objects.Objects.TryGetValue(objectId, out var o)
            && o.Attributes.TryGetValue(attributeId, out value);
    }

    public IReadOnlyDictionary<int, float>? GetAttributeTable(uint objectId) =>
        _game.Objects.Objects.TryGetValue(objectId, out var o) ? o.Attributes : null;

    public bool TryGetOrientation(uint objectId, out float x, out float y, out float z, out float w)
    {
        if (_game.Objects.Objects.TryGetValue(objectId, out var o))
        {
            x = o.Orientation.X; y = o.Orientation.Y; z = o.Orientation.Z; w = o.Orientation.W;
            return true;
        }
        x = y = z = 0f; w = 1f;
        return false;
    }

    public void BroadcastAnimationState(uint objectId, uint stateHash) =>
        _game.BroadcastAnimationState(objectId, stateHash);

    // ResetAnimationState = undo death-anim (catalog §Mechanical): broadcast state 0.
    public void ResetAnimationState(uint objectId) => _game.BroadcastAnimationState(objectId, 0u);

    // Client wrapper @0x00a0aed0: QueryObjectsInRadius capped at 256, no alive gate; the table
    // filter (nSporeLabs.damageableObjectTypes whitelist) is approximated server-side as
    // "has a combatant" (MaxHealth > 0) until per-noun type ids are parsed.
    public IReadOnlyList<uint> QueryObjectsInRadius(float x, float y, float z, float radius, bool damageableOnly)
    {
        var center = new System.Numerics.Vector3(x, y, z);
        var radiusSq = radius * radius;
        var result = new List<uint>();
        foreach (var (id, obj) in _game.Objects.Objects)
        {
            if (damageableOnly && (obj.MaxHealth <= 0f || obj.Dead)) continue;
            if (System.Numerics.Vector3.DistanceSquared(center, obj.Position) > radiusSq) continue;
            result.Add(id);
            if (result.Count >= 256) break;
        }
        return result;
    }

    public float ApplyHeal(uint targetId, float amount)
    {
        if (!_game.Objects.Objects.TryGetValue(targetId, out var obj) || obj.MaxHealth <= 0f) return 0f;
        var before = obj.Health;
        obj.Health = Math.Clamp(obj.Health + amount, 0f, obj.MaxHealth);
        ReCap.Server.Util.Logging.Log.Game.Info(
            $"[lua] HealDamage target={targetId} amount={amount:F1} hp {before:F1}→{obj.Health:F1}");
        var delta = obj.Health - before;
        // C++ Object::SetHealth: HP reaching <=0 triggers OnObjectDeath (once). Despawns the corpse so
        // it stops being a valid target — without this the melee tick keeps re-finding the dead object.
        if (obj.Health <= 0f && !obj.Dead)
        {
            obj.Dead = true;
            _game.OnObjectDeath(targetId);
        }
        return delta;
    }

    public void MarkForDelete(uint objectId) => _game.Objects.Remove(objectId);

    public void SetVisible(uint objectId, bool visible)
    {
        // GameObject visibility flag lands with the object-stream phase; accept silently for now.
    }

    public void SetLocomotionGoal(uint objectId, float x, float y, float z, float stopDistance)
    {
        if (!_game.Objects.Objects.TryGetValue(objectId, out var o)) return;
        // Mirror C++ Locomotion::SetGoalPosition (GoalFlags=0x001, clears stale target/facing).
        o.GoalPosition = new System.Numerics.Vector3(x, y, z);
        o.TargetPosition = System.Numerics.Vector3.Zero;
        o.Facing = System.Numerics.Vector3.Zero;
        o.GoalFlags = 0x001;
        o.DirtyFlags |= ObjectDirtyFlags.Locomotion;
        // stopDistance retained server-side for the arrival estimate only (0x95 carries goal, not stop dist).
        o.DesiredStopDistance = stopDistance;
    }

    public void SetLocomotionTarget(uint objectId, float x, float y, float z)
    {
        if (!_game.Objects.Objects.TryGetValue(objectId, out var o)) return;
        o.TargetPosition = new System.Numerics.Vector3(x, y, z);
        o.DirtyFlags |= ObjectDirtyFlags.Locomotion;
    }

    public void SetFacing(uint objectId, float x, float y, float z)
    {
        // Server-side only in Wave 2 (visible turn-in-place deferred — see plan).
        if (_game.Objects.Objects.TryGetValue(objectId, out var o))
            o.Facing = new System.Numerics.Vector3(x, y, z);
    }

    public void StopLocomotion(uint objectId)
    {
        if (!_game.Objects.Objects.TryGetValue(objectId, out var o)) return;
        o.TargetPosition = System.Numerics.Vector3.Zero;
        o.Facing = System.Numerics.Vector3.Zero;
        o.GoalFlags = 0x020;
        o.DirtyFlags |= ObjectDirtyFlags.Locomotion;
    }

    public void SetNavCollision(uint objectId, bool collidable)
    {
        // Client SetNavCollision @0x009fe7c0 writes the INVERTED collidable flag; server-side only, no wire.
        if (_game.Objects.Objects.TryGetValue(objectId, out var o))
            o.NavCollisionDisabled = !collidable;
    }

    public float GetModifiedMoveSpeed(uint objectId) =>
        _game.Objects.Objects.TryGetValue(objectId, out var o) ? o.MoveSpeed : 0f;

    public bool TryGetGoalDistance(uint objectId, out float distance)
    {
        if (_game.Objects.Objects.TryGetValue(objectId, out var o))
        {
            distance = System.Numerics.Vector3.Distance(o.Position, o.GoalPosition);
            return true;
        }
        distance = 0f;
        return false;
    }

    private uint _nextAttrModHandle;
    // DEFERRED: retail layers attribute modifiers (recomputed by GetAttributeValue); ReCap applies an
    // additive delta directly to GameObject.Attributes (add-vs-mult unverified) and records the reversal
    // for a future RemoveAttributeModifier (not yet demanded). Handle 0 = object missing.
    public uint AddAttributeModifier(uint objectId, int attributeId, float value)
    {
        if (!_game.Objects.Objects.TryGetValue(objectId, out var o)) return 0;
        o.Attributes[attributeId] = (o.Attributes.TryGetValue(attributeId, out var cur) ? cur : 0f) + value;
        return ++_nextAttrModHandle;
    }

    private uint _nextEffectHandle;
    public uint EmitEffect(uint objectId, uint serverEventDef, uint initiatorId)
    {
        if (serverEventDef == 0) return 0;
        _game.BroadcastServerEvent(new ReCap.Server.Adapters.RakNet.Packets.ServerEventPacket
        {
            ServerEventDef = serverEventDef,
            ObjectId = objectId,
            AttackerId = initiatorId,
        });
        return ++_nextEffectHandle;
    }

    // nEvent.Notify FX recipe → ServerEvent 0x9B. Ability hit/impact scripts fire two recipe shapes:
    // ATTACHED ({6 ServerEventDef, 7 ObjectId}) when the effect rides an object, and AT-POSITION
    // ({6, 10 Position}, +11 Facing) when it plays at a world point (the melee-hit notify carries
    // {facing, asset, position}, no objectId). Critical (field 5) tints it. ServerEventDef=0
    // (unresolved asset) is a client-side silent skip, so gate it.
    public void EmitServerEvent(uint serverEventDef, uint objectId, uint attackerId, bool critical,
        System.Numerics.Vector3? position, System.Numerics.Vector3? facing)
    {
        if (serverEventDef == 0) return;
        _game.BroadcastServerEvent(new ReCap.Server.Adapters.RakNet.Packets.ServerEventPacket
        {
            ServerEventDef = serverEventDef,
            ObjectId = objectId,
            AttackerId = attackerId,
            Critical = critical,
            Position = objectId == 0 ? position : null,
            Facing = facing,
        });
    }

    public uint CreateObject(uint nounId, float x, float y, float z) =>
        _game.SpawnScriptObject(nounId, new System.Numerics.Vector3(x, y, z));

    // Floating damage/heal number + combat-log entry (wire 0xBA). deltaHealth<0 = damage.
    public void BroadcastCombatEvent(uint targetId, uint sourceId, float deltaHealth, int integerHpChange, ushort flags) =>
        _game.BroadcastCombatEvent(new ReCap.Server.Adapters.RakNet.Packets.CombatEventPacket
        {
            TargetId = targetId,
            SourceId = sourceId,
            DeltaHealth = deltaHealth,
            IntegerHpChange = integerHpChange,
            Flags = flags,
        });

    public IReadOnlyList<uint> GetAggroTargets(uint agentId)
        => _game.Objects.Objects.TryGetValue(agentId, out var o) && o.Agent is { } bb
            ? bb.AggroList.Select(e => e.ObjectId).ToList() : [];

    public bool HasAggroTargets(uint agentId)
        => _game.Objects.Objects.TryGetValue(agentId, out var o) && o.Agent is { HasTargets: true };

    public uint GetBestTarget(uint agentId)
        => _game.Objects.Objects.TryGetValue(agentId, out var o) ? _aggro.BestTargetFor(o) : 0u;

    public bool InPerceptionCircle(uint agentId, float x, float y, float z, float offset)
        => _game.Objects.Objects.TryGetValue(agentId, out var o) && o.Agent is { } bb
           && AI.AggroSystem.InPerceptionCircle(o.Position, new System.Numerics.Vector3(x, y, z), bb.PerceptionRadius, offset);

    public void CastAiAbility(string abilityName, uint self, uint target)
    {
        var hash = AssetData.Parser.WireHash.Fnv1a(abilityName);
        var cursor = _game.Objects.Objects.TryGetValue(target, out var t) ? t.Position : System.Numerics.Vector3.Zero;
        InvokeAbility(hash, self, target, cursor.X, cursor.Y, cursor.Z, rank: 0);
    }

    // Condition-type evaluators (Distance, Closest, ...) are harvest-deferred; the slice enemy
    // (ZelemBasicMelee) uses none. See AI_SLICE_HARVEST.md.
    public bool EvaluateAiCondition(string conditionName, IReadOnlyList<(string Name, string Value)> props, uint self, uint target) => false;
}
