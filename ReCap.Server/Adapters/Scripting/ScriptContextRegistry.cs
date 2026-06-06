namespace ReCap.Server.Adapters.Scripting;

public interface IScriptGameBridge
{
    bool TryGetPosition(uint objectId, out float x, out float y, out float z);
    float GetHitPoints(uint objectId);
    float GetMaxHitPoints(uint objectId);
    bool ObjectExists(uint objectId);
    byte GetTeam(uint objectId);
    uint GetTargetId(uint objectId);
}

public readonly record struct AbilityInvocation(uint AgentId, uint TargetId, float CursorX, float CursorY, float CursorZ, int Rank);

public sealed class ScriptStateContext
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<nint, AbilityInvocation> _invocations = new();

    public required ScriptRegistry Registry { get; init; }
    public LuaCoroutineScheduler? Scheduler { get; set; }
    public IScriptGameBridge? GameBridge { get; set; }

    public void SetInvocation(nint threadL, AbilityInvocation invocation) => _invocations[threadL] = invocation;

    public AbilityInvocation? GetInvocation(nint callerL)
    {
        if (_invocations.TryGetValue(callerL, out var inv)) return inv;
        return null;
    }

    internal void RemoveInvocation(nint threadL) => _invocations.TryRemove(threadL, out _);
}

public static class ScriptContextRegistry
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<nint, ScriptStateContext> _byState = new();

    public static void Register(nint L, ScriptStateContext context) => _byState[L] = context;
    public static void Unregister(nint L) => _byState.TryRemove(L, out _);
    public static ScriptStateContext? Get(nint L) => _byState.TryGetValue(L, out var c) ? c : null;
}
