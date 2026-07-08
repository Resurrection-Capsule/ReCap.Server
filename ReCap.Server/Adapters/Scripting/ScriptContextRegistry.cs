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
}

public readonly record struct AbilityInvocation(
    uint AgentId, uint TargetId, float CursorX, float CursorY, float CursorZ, int Rank,
    uint AbilityHash = 0, uint InstanceId = 0, bool TargetInRangeAtStart = false);

public sealed class ScriptStateContext
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<nint, AbilityInvocation> _invocations = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<uint, IReadOnlyDictionary<int, float>> _attributeSnapshots = new();
    private uint _nextSnapshotHandle;

    public required ScriptRegistry Registry { get; init; }
    public LuaCoroutineScheduler? Scheduler { get; set; }
    public IScriptGameBridge? GameBridge { get; set; }

    public void SetInvocation(nint threadL, AbilityInvocation invocation) => _invocations[threadL] = invocation;

    public AbilityInvocation? GetInvocation(nint callerL)
    {
        if (_invocations.TryGetValue(callerL, out var inv)) return inv;
        return null;
    }

    internal void RemoveInvocation(nint threadL)
    {
        _invocations.TryRemove(threadL, out _);
        _animSequenceCurrent.TryRemove(threadL, out _);
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

    public void SetGuidSlot(nint threadL, int slot, double guid) =>
        _guidSlots.GetOrAdd(threadL, _ => new()).AddOrUpdate(slot, guid, (_, _) => guid);

    public void RemoveThreadData(nint threadL)
    {
        _guidSlots.TryRemove(threadL, out _);
        _privateTableRefs.TryRemove(threadL, out _);
    }
}

public static class ScriptContextRegistry
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<nint, ScriptStateContext> _byState = new();

    public static void Register(nint L, ScriptStateContext context) => _byState[L] = context;
    public static void Unregister(nint L) => _byState.TryRemove(L, out _);
    public static ScriptStateContext? Get(nint L) => _byState.TryGetValue(L, out var c) ? c : null;
}
