namespace ReCap.Server.Adapters.Scripting;

public enum ScriptKind { Ability, Modifier, Affix, Condition, Objective }

// Cooldowns is the ability's ranked-value cooldown table (author writes {1.5, 1.25, 1} = rank 0/1/2,
// or a scalar captured as a 1-element list). Empty = no cooldown. CooldownForRank mirrors the game's
// GetRankedValue: pick the rank index, clamping to the last entry for ranks beyond the table.
public sealed record ScriptEntry(string Name, uint Hash, int TableRef, bool HasTick, bool HasActivate, bool HasDeactivate, IReadOnlyList<float>? Cooldowns = null, int ActivationType = 0, int HandledEvents = 0)
{
    public float CooldownForRank(int rank)
    {
        if (Cooldowns is not { Count: > 0 } cd) return 0f;
        return cd[Math.Clamp(rank, 0, cd.Count - 1)];
    }
}

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
