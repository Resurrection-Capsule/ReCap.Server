namespace ReCap.Server.Adapters.Scripting;

public sealed class ScriptStateContext
{
    public required ScriptRegistry Registry { get; init; }
    public object? Scheduler { get; set; } // T5 retypes to LuaCoroutineScheduler
    public object? GameBridge { get; set; }
}

public static class ScriptContextRegistry
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<nint, ScriptStateContext> _byState = new();

    public static void Register(nint L, ScriptStateContext context) => _byState[L] = context;
    public static void Unregister(nint L) => _byState.TryRemove(L, out _);
    public static ScriptStateContext? Get(nint L) => _byState.TryGetValue(L, out var c) ? c : null;
}
