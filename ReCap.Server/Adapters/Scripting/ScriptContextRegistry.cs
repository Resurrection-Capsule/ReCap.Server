namespace ReCap.Server.Adapters.Scripting;

public interface IScriptGameBridge
{
    bool TryGetPosition(uint objectId, out float x, out float y, out float z);
    float GetHitPoints(uint objectId);
    float GetMaxHitPoints(uint objectId);
    bool ObjectExists(uint objectId);
    byte GetTeam(uint objectId);
    uint GetTargetId(uint objectId);
    bool IsPlayerControlled(uint objectId);
    bool TryGetAttributeValue(uint objectId, int attributeId, out float value);
    IReadOnlyDictionary<int, float>? GetAttributeTable(uint objectId);
    bool TryGetOrientation(uint objectId, out float x, out float y, out float z, out float w);
    void BroadcastAnimationState(uint objectId, uint stateHash);
    IReadOnlyList<uint> QueryObjectsInRadius(float x, float y, float z, float radius, bool damageableOnly);
    float ApplyHeal(uint targetId, float amount);
    void MarkForDelete(uint objectId);
    void SetVisible(uint objectId, bool visible);
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
}

public static class ScriptContextRegistry
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<nint, ScriptStateContext> _byState = new();

    public static void Register(nint L, ScriptStateContext context) => _byState[L] = context;
    public static void Unregister(nint L) => _byState.TryRemove(L, out _);
    public static ScriptStateContext? Get(nint L) => _byState.TryGetValue(L, out var c) ? c : null;
}
