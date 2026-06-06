namespace ReCap.Server.Adapters.Scripting;

public enum ScriptKind { Ability, Modifier, Affix, Condition, Objective }

public sealed record ScriptEntry(string Name, uint Hash, int TableRef, bool HasTick, bool HasActivate, bool HasDeactivate);

public sealed class ScriptRegistry
{
    private readonly Dictionary<(ScriptKind, uint), ScriptEntry> _entries = [];

    public bool TryAdd(ScriptKind kind, ScriptEntry entry)
    {
        if (_entries.ContainsKey((kind, entry.Hash))) return false;
        _entries[(kind, entry.Hash)] = entry;
        return true;
    }

    public ScriptEntry? Find(ScriptKind kind, uint hash) =>
        _entries.TryGetValue((kind, hash), out var e) ? e : null;

    public int Count(ScriptKind kind) => _entries.Keys.Count(k => k.Item1 == kind);

    public IEnumerable<ScriptEntry> AllWithTick(ScriptKind kind) =>
        _entries.Where(kv => kv.Key.Item1 == kind && kv.Value.HasTick).Select(kv => kv.Value);

    public IEnumerable<ScriptEntry> AllEntries(ScriptKind kind) =>
        _entries.Where(kv => kv.Key.Item1 == kind).Select(kv => kv.Value);
}
