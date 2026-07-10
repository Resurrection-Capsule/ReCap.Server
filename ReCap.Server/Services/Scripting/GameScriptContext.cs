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

    // CooldownUpdate 0xC1 (UI swirl). Relative form: start=0 → client stamps end = its now + duration.
    // Cooldown is authored in seconds (matches our scheduler); the client clock is milliseconds, so
    // scale. global=0 leaves the client's global-cooldown flag untouched.
    public void SendCooldownUpdate(uint objectId, uint abilityId, float cooldownSeconds) =>
        _game.BroadcastCooldownUpdate(new ReCap.Server.Adapters.RakNet.Packets.CooldownUpdatePacket
        {
            ObjectId = objectId,
            AbilityId = abilityId,
            Duration = (ulong)(cooldownSeconds * 1000f),
            Start = 0,
            GlobalCooldown = 0,
        });

    // Modifier lifecycle (nModifier.RequestModifier / MarkForDelete / GetFirstModifierByGUID).
    // Create allocates a server-side instance and replicates it via 0xA2 ModifierCreated. The 37B
    // layout's uncertain fields (overdrive/stackCount, timestamp, source obj) are left at the safe
    // C++-lead defaults; icon comes from modifierGuid (+0x04). DoT/attribute tick execution deferred.
    public uint CreateModifier(uint targetId, uint casterId, uint modifierGuid, int rank)
    {
        var entry = _registry.Find(ScriptKind.Modifier, modifierGuid);

        // Priority-dispel pre-pass (retail ApplyStackPolicyAndCreate @0x009e6595, gated on the new
        // modifier's requiresAgent): applying an agent modifier dispels existing modifiers on the target
        // that are flagged deactivateOnInterrupt and rank at or below the new modifierPriority — the
        // cross-def CC/interrupt system. Runs before the same-def stack policy switch.
        if (entry is { RequiresAgent: true })
            ApplyPriorityDispel(targetId, modifierGuid, entry.ModifierPriority, entry.ActivationType);

        // Stack policy (activationType, def+0x190): retail runs this switch BEFORE creating — it may
        // drop existing same-def instances, reject the request, or reuse/refresh an existing one. Apply
        // the removes/refresh here (so [3] deactivate + 0xA3/0xA4 replicate), then create only if asked.
        var decision = _game.Modifiers.ResolveStackPolicy(targetId, casterId, modifierGuid, entry?.ActivationType ?? 0);
        foreach (var removeId in decision.RemoveInstanceIds) RemoveModifier(removeId);
        if (decision.RefreshInstanceId != 0) RefreshModifierDuration(decision.RefreshInstanceId);
        if (decision.StackEventInstanceId != 0) FireStackModifierEvent(decision.StackEventInstanceId);
        if (!decision.ShouldCreate) return decision.ReturnInstanceId;

        var instance = _game.Modifiers.Create(targetId, casterId, modifierGuid, rank);
        _game.BroadcastModifierCreated(new ReCap.Server.Adapters.RakNet.Packets.ModifierCreatedPacket
        {
            TargetId = targetId,
            ModifierGuid = modifierGuid,
            InstanceId = instance.InstanceId,
            StackCount = (uint)instance.StackCount,
        });

        // Retail (nModifier_CreateInstance @0x009e5c50): the modifier's index [2] runs as a persistent
        // per-instance coroutine (the DoT/aura loop, resumed each frame), then index [1] activate runs
        // once. Both — and [3] deactivate on removal — share the instance's private table (retail
        // instance+0x170) so state set in activate is visible to tick/deactivate. [4] event handler
        // (bitmask def+0x1a4) is deferred. Removal is script-driven via nModifier.MarkForDelete / expiry.
        lock (_luaGate)
        {
            if (entry is not null)
            {
                var sharedPrivateRef = EnsureModifierPrivateTable(instance);
                instance.ThreadHandle = SpawnModifierIndex(entry, 2, ModifierInvocation(instance), sharedPrivateRef);
                SpawnModifierIndex(entry, 1, ModifierInvocation(instance), sharedPrivateRef);
            }
        }
        return instance.InstanceId;
    }

    // The instance-scoped private table (retail instance+0x170), created once and shared by the
    // modifier's [1]/[2]/[3] index coroutines. Owned by the instance — freed in RemoveModifier, not by
    // the scheduler when an individual index coroutine ends. Caller holds _luaGate.
    private int EnsureModifierPrivateTable(Domain.Gameplay.ModifierInstance instance)
    {
        if (instance.PrivateTableRef != 0) return instance.PrivateTableRef;
        var L = _runtime.L;
        LuaNative.lua_createtable(L, 0, 0);
        instance.PrivateTableRef = LuaNative.luaL_ref(L, LuaNative.LUA_REGISTRYINDEX);
        return instance.PrivateTableRef;
    }

    // Remove existing modifiers on the target that a new agent modifier outranks (retail dispel loop
    // @0x009e65d9): skip existing that outrank the newcomer (existing.modifierPriority > new), skip
    // those not flagged deactivateOnInterrupt, and skip a same-def UniqueIrreplaceable (activationType
    // 6, handled by the stack switch). DEFERRED edges (need unmodelled state): the "new is too weak ->
    // reject" gate (FUN_009e3be0), the channelled-modifier lock (component+0x23c), and the
    // existing-mid-dispatch (+0x16c) reject.
    private void ApplyPriorityDispel(uint targetId, uint newGuid, int newPriority, int newActivationType)
    {
        foreach (var existing in _game.Modifiers.ModifiersOn(targetId))
        {
            if (_registry.Find(ScriptKind.Modifier, existing.ModifierGuid) is not { } e) continue;
            if (e.ModifierPriority > newPriority) continue;
            if (!e.DeactivateOnInterrupt) continue;
            if (existing.ModifierGuid == newGuid && newActivationType == 6) continue;
            RemoveModifier(existing.InstanceId);
        }
    }

    // Stack-policy case 7 (refresh): recompute the modifier's duration start and replicate 0xA3 so the
    // client restarts its buff timer. StartTime mirrors the 0xA2 create convention (relative epoch, 0);
    // the exact wire timestamp is pending a capture, but the record patch (stack count kept) is faithful.
    private void RefreshModifierDuration(uint instanceId)
    {
        if (_game.Modifiers.Get(instanceId) is not { } instance) return;
        _game.BroadcastModifierUpdated(new ReCap.Server.Adapters.RakNet.Packets.ModifierUpdatedPacket
        {
            TargetId = instance.TargetId,
            InstanceId = instanceId,
            StartTime = 0,
            StackCount = (uint)instance.StackCount,
        });
    }

    public bool RemoveModifier(uint instanceId)
    {
        if (_game.Modifiers.Get(instanceId) is not { } instance) return false;
        var targetId = instance.TargetId;

        lock (_luaGate)
        {
            // Retail runs index [3] deactivate once on removal (sharing the instance table), then tears
            // down the tick thread and frees the private table.
            if (_registry.Find(ScriptKind.Modifier, instance.ModifierGuid) is { } entry)
                SpawnModifierIndex(entry, 3, ModifierInvocation(instance), instance.PrivateTableRef);
            if (instance.ThreadHandle != 0)
                _scheduler.StopThread(instance.ThreadHandle);
            if (instance.PrivateTableRef != 0)
            {
                LuaNative.luaL_unref(_runtime.L, LuaNative.LUA_REGISTRYINDEX, instance.PrivateTableRef);
                instance.PrivateTableRef = 0;
            }
        }

        if (!_game.Modifiers.Remove(instanceId)) return false;
        _game.BroadcastModifierDeleted(new ReCap.Server.Adapters.RakNet.Packets.ModifierDeletedPacket
        {
            TargetId = targetId,
            InstanceId = instanceId,
        });
        return true;
    }

    private static Adapters.Scripting.AbilityInvocation ModifierInvocation(Domain.Gameplay.ModifierInstance instance) =>
        new(AgentId: instance.TargetId, TargetId: 0, CursorX: 0, CursorY: 0, CursorZ: 0, Rank: instance.Rank,
            AbilityHash: instance.ModifierGuid, InstanceId: instance.InstanceId,
            InitiatorId: instance.CasterId, StackCount: instance.StackCount);

    // Spawn modTable[index] (numeric — modifiers use [1]/[2]/[3]/[4], NOT a named field like abilities)
    // as a detached coroutine (objectId 0 = no per-object gate; a target can carry many modifiers plus
    // its own ability thread). Returns the thread handle, or 0 if the index is not a function.
    private nint SpawnModifierIndex(ScriptEntry entry, int index, Adapters.Scripting.AbilityInvocation invocation,
        int sharedPrivateRef = 0, Action<nint>? onComplete = null, Adapters.Scripting.ModifierEventData? eventData = null)
    {
        var L = _runtime.L;
        LuaNative.lua_rawgeti(L, LuaNative.LUA_REGISTRYINDEX, entry.TableRef);
        LuaNative.lua_rawgeti(L, -1, index);
        if (LuaNative.lua_type(L, -1) != LuaNative.LUA_TFUNCTION)
        {
            LuaNative.lua_settop(L, 0);
            return 0;
        }
        LuaNative.lua_remove(L, -2); // drop the table, leave the function on top
        return _scheduler.SpawnFromStack(L, objectId: 0, valuesOnTop: 1,
            beforeFirstResume: t =>
            {
                var c = ScriptContextRegistry.Get(_runtime.L);
                c?.SetInvocation(t, invocation);
                if (sharedPrivateRef != 0) c?.BindSharedPrivateTable(t, sharedPrivateRef);
                if (eventData is not null) c?.SetModifierEvent(t, eventData);
            },
            onComplete: onComplete);
    }

    public uint FindModifierByGuid(uint targetId, uint modifierGuid) =>
        _game.Modifiers.FindByGuid(targetId, modifierGuid)?.InstanceId ?? 0u;

    public int GetModifierStackCount(uint instanceId) =>
        _game.Modifiers.Get(instanceId)?.StackCount ?? 0;

    // nModifier.IncrementStackCount: bump the stack and replicate it via 0xA3 (KeepDuration sentinel so
    // the pure stack bump does not disturb the client's buff timer — ResetDuration handles that).
    public int IncrementModifierStack(uint instanceId)
    {
        if (_game.Modifiers.Get(instanceId) is not { } instance) return 0;
        instance.StackCount++;
        _game.BroadcastModifierUpdated(new ReCap.Server.Adapters.RakNet.Packets.ModifierUpdatedPacket
        {
            TargetId = instance.TargetId,
            InstanceId = instanceId,
            StartTime = ReCap.Server.Adapters.RakNet.Packets.ModifierUpdatedPacket.KeepDuration,
            StackCount = (uint)instance.StackCount,
        });
        return instance.StackCount;
    }

    // nModifier.ResetDuration: restart the modifier's duration and replicate via 0xA3.
    public void ResetModifierDuration(uint instanceId) => RefreshModifierDuration(instanceId);

    // Stacks (activationType 4/5) re-application: run the existing instance's [4] with a StackModifier
    // event (retail nModifier_RunReapplyEvent @0x009e6060 fires index [4] event code 32); the Lua bumps
    // the stack + resets duration. DEFERRED: honoring a false [4] return (retail removes the instance)
    // and the combat-driven events (DealtDamage/TookDamage) — only the StackModifier reapply is wired.
    private void FireStackModifierEvent(uint instanceId)
    {
        if (_game.Modifiers.Get(instanceId) is { } instance)
            FireModifierEvent(instance, 32); // nAbilityEventFlags.StackModifier
    }

    // Fire a modifier's index [4] event handler for one event type (retail dispatch FUN_008f5060 with
    // index 4). The handler reads the type via nAbility.GetAbilityEventType and returns a bool; retail
    // removes the instance when it returns false (nModifier_RunReapplyEvent @0x009e6060). A modifier
    // with no [4] (or a [4] that yields) keeps the instance.
    private void FireModifierEvent(Domain.Gameplay.ModifierInstance instance, int eventType,
        Adapters.Scripting.ModifierEventData? eventData = null)
    {
        if (_registry.Find(ScriptKind.Modifier, instance.ModifierGuid) is not { } entry) return;
        var handlerRan = false;
        var keep = true;
        lock (_luaGate)
        {
            var invocation = ModifierInvocation(instance) with { EventType = eventType };
            SpawnModifierIndex(entry, 4, invocation, instance.PrivateTableRef,
                onComplete: t => { handlerRan = true; keep = LuaNative.lua_toboolean(t, -1) != 0; },
                eventData: eventData);
        }
        if (handlerRan && !keep) RemoveModifier(instance.InstanceId);
    }

    // Combat-event dispatch: fire the [4] event on every modifier on `objectId` whose handledEvents
    // bitmask includes `eventType` (retail fires index [4] on the object's modifier list). Guarded
    // against re-entrancy so a thorns [4] that deals damage back doesn't recurse into another dispatch.
    private bool _inCombatEventDispatch;
    private void DispatchCombatEvent(uint objectId, int eventType, Adapters.Scripting.ModifierEventData data)
    {
        if (_inCombatEventDispatch) return;
        var mods = _game.Modifiers.ModifiersOn(objectId);
        if (mods.Count == 0) return;
        _inCombatEventDispatch = true;
        try
        {
            foreach (var m in mods)
            {
                if (_game.Modifiers.Get(m.InstanceId) is null) continue; // a prior handler removed it
                if (_registry.Find(ScriptKind.Modifier, m.ModifierGuid) is not { HandledEvents: var flags }
                    || (flags & eventType) == 0) continue;
                FireModifierEvent(m, eventType, data);
            }
        }
        finally { _inCombatEventDispatch = false; }
    }

    // nAbilityEventFlags.TookDamage (1): the target took damage — fire [4] on its subscribed modifiers
    // (thorns retaliation, on-hit procs). Attacker/amount/descriptors match the handlers' slot reads.
    public void DispatchTookDamage(uint targetId, uint attackerId, float amount, int descriptors) =>
        DispatchCombatEvent(targetId, 1, Adapters.Scripting.ModifierEventData.TookDamage(attackerId, amount, descriptors));

    // nAbilityEventFlags.DealtDamage (8192): the attacker dealt damage — fire [4] on ITS subscribed
    // modifiers (on-hit procs); the handler reads GUID[1] = the victim it hit.
    public void DispatchDealtDamage(uint attackerId, uint targetId) =>
        DispatchCombatEvent(attackerId, 8192, Adapters.Scripting.ModifierEventData.DealtDamage(targetId));

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
