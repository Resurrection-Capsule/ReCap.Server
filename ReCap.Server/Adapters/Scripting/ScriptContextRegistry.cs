namespace ReCap.Server.Adapters.Scripting;

public interface IScriptGameBridge
{
    bool TryGetPosition(uint objectId, out float x, out float y, out float z);
    float GetHitPoints(uint objectId);
    float GetMaxHitPoints(uint objectId);
    bool ObjectExists(uint objectId);
    byte GetTeam(uint objectId);
    void SetTeam(uint objectId, byte team);
    byte GetPlayerId(uint objectId);
    uint GetTargetId(uint objectId);
    bool IsPlayerControlled(uint objectId);
    bool TryGetAttributeValue(uint objectId, int attributeId, out float value);
    IReadOnlyDictionary<int, float>? GetAttributeTable(uint objectId);
    bool TryGetOrientation(uint objectId, out float x, out float y, out float z, out float w);
    void BroadcastAnimationState(uint objectId, uint stateHash);
    void ResetAnimationState(uint objectId);
    IReadOnlyList<uint> QueryObjectsInRadius(float x, float y, float z, float radius, bool damageableOnly);
    float ApplyHeal(uint targetId, float amount);
    void MarkForDelete(uint objectId);
    void SetVisible(uint objectId, bool visible);
    void SetLocomotionGoal(uint objectId, float x, float y, float z, float stopDistance);
    void SetLocomotionTarget(uint objectId, float x, float y, float z);
    void SetFacing(uint objectId, float x, float y, float z);
    void StopLocomotion(uint objectId);
    void SetNavCollision(uint objectId, bool collidable);
    float GetModifiedMoveSpeed(uint objectId);
    bool TryGetGoalDistance(uint objectId, out float distance);
    uint AddAttributeModifier(uint objectId, int attributeId, float value);
    uint EmitEffect(uint objectId, uint serverEventDef, uint initiatorId);
    void EmitServerEvent(uint serverEventDef, uint objectId, uint attackerId, bool critical,
        System.Numerics.Vector3? position, System.Numerics.Vector3? facing);
    uint CreateObject(uint nounId, float x, float y, float z);
    void BroadcastCombatEvent(uint targetId, uint sourceId, float deltaHealth, int integerHpChange, ushort flags);
    void SendCooldownUpdate(uint objectId, uint abilityId, float cooldownSeconds);
    uint CreateModifier(uint targetId, uint casterId, uint modifierGuid, int rank);
    bool RemoveModifier(uint instanceId);
    uint FindModifierByGuid(uint targetId, uint modifierGuid);
    int GetModifierStackCount(uint instanceId);
    int IncrementModifierStack(uint instanceId);
    void ResetModifierDuration(uint instanceId);
    void DispatchTookDamage(uint targetId, uint attackerId, float amount, int descriptors);
    void DispatchDealtDamage(uint attackerId, uint targetId);
    IReadOnlyList<uint> GetAggroTargets(uint agentId);
    bool HasAggroTargets(uint agentId);
    uint GetBestTarget(uint agentId);
    bool InPerceptionCircle(uint agentId, float x, float y, float z, float offset);
    void CastAiAbility(string abilityName, uint self, uint target);
    bool EvaluateAiCondition(string conditionName, IReadOnlyList<(string Name, string Value)> props, uint self, uint target);

    // nAbility.RequestAbility — run an ability programmatically (sub-ability / projectile / pet cast).
    // Deferred to the next tick so it never re-enters the runtime mid-coroutine. Default no-op keeps
    // lightweight test doubles compiling.
    void RequestAbility(uint abilityHash, uint agentId, uint targetId, float x, float y, float z, int rank) { }

    // nLocomotion.TeleportObject — instant reposition + teleport-route replication (0x90). Default
    // no-op keeps lightweight test doubles compiling.
    void TeleportObject(uint objectId, float x, float y, float z, bool face) { }

    // nAbility/nModifier.CallFunctionInContext — resolve a live instance (modifier) to its context
    // (invocation + shared private table ref) so a function can be run bound to it. Default: unknown.
    bool TryGetInstanceContext(uint instanceId, out AbilityInvocation invocation, out int privateTableRef)
    {
        invocation = default;
        privateTableRef = 0;
        return false;
    }
}

public readonly record struct AbilityInvocation(
    uint AgentId, uint TargetId, float CursorX, float CursorY, float CursorZ, int Rank,
    uint AbilityHash = 0, uint InstanceId = 0, bool TargetInRangeAtStart = false,
    uint InitiatorId = 0, int StackCount = 0, int EventType = 0);

// Payload of a modifier [4] event, read by nAbility.GetAbilityEvent{GUID,Float,Int}Data(handle, index).
// Retail carries a typed slot array; we model the slots each real handler actually reads. TookDamage
// (verified: ThornsPassive/QuantumStateBuff): GUID[1]=attacker, Float[2]=amount, Int[3]=descriptors.
public sealed class ModifierEventData
{
    public IReadOnlyDictionary<int, uint> Guids { get; init; } = System.Collections.Immutable.ImmutableDictionary<int, uint>.Empty;
    public IReadOnlyDictionary<int, float> Floats { get; init; } = System.Collections.Immutable.ImmutableDictionary<int, float>.Empty;
    public IReadOnlyDictionary<int, int> Ints { get; init; } = System.Collections.Immutable.ImmutableDictionary<int, int>.Empty;

    public static ModifierEventData TookDamage(uint attacker, float amount, int descriptors) => new()
    {
        Guids = new Dictionary<int, uint> { [1] = attacker },
        Floats = new Dictionary<int, float> { [2] = amount },
        Ints = new Dictionary<int, int> { [3] = descriptors },
    };

    // DealtDamage fires on the ATTACKER; the only real consumer (QuantumStateBuff) reads GUID[1] = the
    // victim it hit (slots 2/6 are read but unused, left absent). On-hit procs record that target.
    public static ModifierEventData DealtDamage(uint target) => new()
    {
        Guids = new Dictionary<int, uint> { [1] = target },
    };
}

public sealed class ScriptStateContext
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<nint, AbilityInvocation> _invocations = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<uint, IReadOnlyDictionary<int, float>> _attributeSnapshots = new();
    private uint _nextSnapshotHandle;

    public required ScriptRegistry Registry { get; init; }
    public LuaCoroutineScheduler? Scheduler { get; set; }
    public IScriptGameBridge? GameBridge { get; set; }

    public void SetInvocation(nint threadL, AbilityInvocation invocation) => _invocations[threadL] = invocation;

    // Per-thread modifier [4] event payload (read by GetAbilityEvent*Data). Set when firing the handler,
    // cleared with the invocation on thread release.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<nint, ModifierEventData> _modifierEvents = new();
    public void SetModifierEvent(nint threadL, ModifierEventData data) => _modifierEvents[threadL] = data;
    public ModifierEventData? GetModifierEvent(nint callerL) => _modifierEvents.TryGetValue(callerL, out var e) ? e : null;

    public AbilityInvocation? GetInvocation(nint callerL)
    {
        if (_invocations.TryGetValue(callerL, out var inv)) return inv;
        return null;
    }

    internal void RemoveInvocation(nint threadL)
    {
        _invocations.TryRemove(threadL, out _);
        _animSequenceCurrent.TryRemove(threadL, out _);
        _modifierEvents.TryRemove(threadL, out _);
    }

    // nAbilityAnimationSelection.Sequence rotation: one counter per (agent, ability) across
    // casts; the index chosen by PlayAnimationSequence is read back within the same cast by
    // GetAnimationSequenceIndex (client ctx[0x164] behavior).
    private readonly System.Collections.Concurrent.ConcurrentDictionary<(uint Agent, uint Ability), int> _animSequenceCounter = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<nint, int> _animSequenceCurrent = new();

    public int NextAnimationSequenceIndex(nint threadL, uint agentId, uint abilityHash, int count)
    {
        var next = _animSequenceCounter.AddOrUpdate((agentId, abilityHash), 0, (_, prev) => prev + 1);
        var index = count > 0 ? next % count : 0;
        _animSequenceCurrent[threadL] = index;
        return index;
    }

    public int GetAnimationSequenceIndex(nint threadL) => _animSequenceCurrent.GetValueOrDefault(threadL, 0);

    // Retail snapshot contract (client GetAgentAttributeSnapshot @0x00a417a0 /
    // GetAttributeValue_FromSnapshot @0x009fede0): the snapshot is an opaque numeric HANDLE
    // referencing a frozen attribute array; values read back raw, no modifier recompute.
    public uint StoreAttributeSnapshot(IReadOnlyDictionary<int, float> attributes)
    {
        var handle = System.Threading.Interlocked.Increment(ref _nextSnapshotHandle);
        _attributeSnapshots[handle] = attributes;
        return handle;
    }

    public bool TryGetAttributeSnapshot(uint handle, out IReadOnlyDictionary<int, float> attributes)
        => _attributeSnapshots.TryGetValue(handle, out attributes!);

    // Per-object cast-time attribute snapshot (projectiles carry the caster's snapshot handle).
    private readonly System.Collections.Concurrent.ConcurrentDictionary<uint, uint> _objectSnapshots = new();
    public void SetObjectSnapshot(uint objectId, uint snapshotHandle) => _objectSnapshots[objectId] = snapshotHandle;
    public bool TryGetObjectSnapshot(uint objectId, out uint snapshotHandle) => _objectSnapshots.TryGetValue(objectId, out snapshotHandle);

    // nThreadData per-thread stores (registry-ref + GUID slots), lifecycle-bound to the
    // owning coroutine; cleaned by LuaCoroutineScheduler.Release to avoid leaking registry
    // refs / cross-object bleed if the native allocator reuses a thread pointer.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<nint, int> _privateTableRefs = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<nint, System.Collections.Concurrent.ConcurrentDictionary<int, double>> _guidSlots = new();

    public void SetPrivateTableRef(nint threadL, int reference) => _privateTableRefs[threadL] = reference;

    public bool TryGetPrivateTableRef(nint threadL, out int reference) => _privateTableRefs.TryGetValue(threadL, out reference);

    public bool TryTakePrivateTableRef(nint threadL, out int reference) => _privateTableRefs.TryRemove(threadL, out reference);

    // Instance-scoped private table binding (retail modifier instance+0x170): a modifier's [1]/[2]/[3]
    // index coroutines all resolve nThreadData.GetPrivateTable to the SAME table instead of a per-thread
    // one, so state written in activate is visible to tick/deactivate. The ref is owned by the modifier
    // instance (freed on removal), so a thread finishing only unbinds — it must NOT unref the table.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<nint, int> _sharedPrivateByThread = new();
    public void BindSharedPrivateTable(nint threadL, int reference) => _sharedPrivateByThread[threadL] = reference;
    public bool TryGetSharedPrivateTable(nint threadL, out int reference) => _sharedPrivateByThread.TryGetValue(threadL, out reference);
    public void UnbindSharedPrivateTable(nint threadL) => _sharedPrivateByThread.TryRemove(threadL, out _);

    public void SetGuidSlot(nint threadL, int slot, double guid) =>
        _guidSlots.GetOrAdd(threadL, _ => new()).AddOrUpdate(slot, guid, (_, _) => guid);

    public void RemoveThreadData(nint threadL)
    {
        _guidSlots.TryRemove(threadL, out _);
        _privateTableRefs.TryRemove(threadL, out _);
    }

    // nObjectManager.AttachTriggerVolume records: sphere (objectId, radius) + captured Lua callback
    // registry refs. Trigger FIRING (onEnter/exit/stay) is deferred — no server-side collision system;
    // the refs are held for a future firing pass and freed by lua_close on game teardown.
    private uint _nextTriggerHandle;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<uint, (uint ObjectId, float Radius, int[] Callbacks)> _triggerVolumes = new();
    public uint RegisterTriggerVolume(uint objectId, float radius, int[] callbackRefs)
    {
        var handle = System.Threading.Interlocked.Increment(ref _nextTriggerHandle);
        _triggerVolumes[handle] = (objectId, radius, callbackRefs);
        return handle;
    }
    public int TriggerVolumeCount => _triggerVolumes.Count;

    // nUtil.GetAsset/SPID box a 32-bit FNV hash as a Lua number, but LUA_NUMBER is float32 in this
    // build (24-bit mantissa) so hashes above ~16.7M lose precision on the round-trip through Lua.
    // Register the boxed-float → exact hash at production so a consumer that reads the same float
    // back (nEvent.Notify → ServerEventDef, spawn nouns) recovers the exact hash instead of a
    // rounded one that never resolves an asset. Same float32 on both sides ⇒ exact key match.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<float, uint> _assetHashByBoxed = new();
    public float RegisterAssetHash(uint hash)
    {
        var boxed = (float)hash;
        _assetHashByBoxed[boxed] = hash;
        return boxed;
    }
    public uint ResolveAssetHash(float boxed)
        => _assetHashByBoxed.TryGetValue(boxed, out var hash) ? hash : (uint)boxed;

    // Per-(object, ability) cooldown deadline in scheduler seconds. Stamped by
    // nAbilityContext.PayCooldownAndMana (from the ability's `cooldown` prop) and checked by
    // GameScriptContext.InvokeAbility, which refuses a cast while the ability is still cooling down —
    // the retail gate that stops an AI enemy (or a spamming player) from re-firing every tick.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<(uint Object, uint Ability), double> _cooldownReadyAt = new();
    public void StampCooldown(uint objectId, uint abilityHash, double readyAtSeconds)
        => _cooldownReadyAt[(objectId, abilityHash)] = readyAtSeconds;
    public bool IsOnCooldown(uint objectId, uint abilityHash, double nowSeconds)
        => _cooldownReadyAt.TryGetValue((objectId, abilityHash), out var ready) && nowSeconds < ready;

    // Clear the deadline so the ability is ready again (nAbility.Reset/RemoveCooldownTime → 0xC1 dur 0).
    public void ClearCooldown(uint objectId, uint abilityHash)
        => _cooldownReadyAt.TryRemove((objectId, abilityHash), out _);

    // Every ability currently cooling down on an object (RemoveCooldownTime/ScaleCooldownTime, no id).
    public IReadOnlyList<uint> CooldownAbilities(uint objectId)
        => _cooldownReadyAt.Keys.Where(k => k.Object == objectId).Select(k => k.Ability).ToList();

    // Remaining cooldown in seconds (0 if none / already ready) — for Scale/AddCooldownTime.
    public double CooldownRemaining(uint objectId, uint abilityHash, double nowSeconds)
        => _cooldownReadyAt.TryGetValue((objectId, abilityHash), out var ready)
            ? Math.Max(0d, ready - nowSeconds) : 0d;
}

public static class ScriptContextRegistry
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<nint, ScriptStateContext> _byState = new();

    public static void Register(nint L, ScriptStateContext context) => _byState[L] = context;
    public static void Unregister(nint L) => _byState.TryRemove(L, out _);
    public static ScriptStateContext? Get(nint L) => _byState.TryGetValue(L, out var c) ? c : null;
}
