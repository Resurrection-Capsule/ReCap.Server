namespace ReCap.Server.Domain.Gameplay;

// A live modifier (buff/debuff) instance attached to a target object. Mirrors the client record the
// 0xA2 ModifierCreated packet builds in object+0x2b4, keyed by InstanceId. The DoT/attribute tick
// coroutine that runs the modifier's Lua is a later increment; this holds the create/query/delete
// lifecycle so RequestModifier returns a usable handle and 0xA2/0xA4 replicate to the client.
public sealed class ModifierInstance
{
    public required uint InstanceId { get; init; }
    public required uint TargetId { get; init; }
    public required uint CasterId { get; init; }
    public required uint ModifierGuid { get; init; }
    public int Rank { get; init; }
    public int StackCount { get; set; } = 1;
    public uint DurationMs { get; set; } = 0xFFFFFFFF;

    // The per-instance Lua coroutine running the modifier's tick (index [2]); 0 = none (tickless
    // modifier or unresolved script). Stopped when the modifier is removed.
    public nint ThreadHandle { get; set; }
}

// The outcome of the stack-policy switch: which existing instances to remove first, whether to create
// a fresh one, and — for reuse/refresh policies — the existing instance to hand back (RefreshOldest
// also asks the caller to send 0xA3 on it). Reject = create nothing, return handle 0.
public readonly record struct StackDecision(
    bool ShouldCreate,
    uint ReturnInstanceId,
    uint RefreshInstanceId,
    IReadOnlyList<uint> RemoveInstanceIds)
{
    public static StackDecision Create => new(true, 0, 0, []);
    public static StackDecision Reject => new(false, 0, 0, []);
    public static StackDecision Existing(uint id) => new(false, id, 0, []);
    public static StackDecision CreateAfterRemoving(IEnumerable<uint> ids) => new(true, 0, 0, ids.ToList());
    public static StackDecision RefreshOldest(uint keepId, IEnumerable<uint> removeIds) =>
        new(false, keepId, keepId, removeIds.ToList());
}

public sealed class ModifierSystem
{
    private readonly Dictionary<uint, ModifierInstance> _byId = [];
    private readonly Dictionary<uint, List<ModifierInstance>> _byTarget = [];
    private uint _nextInstanceId;

    public ModifierInstance Create(uint targetId, uint casterId, uint modifierGuid, int rank)
    {
        var instance = new ModifierInstance
        {
            InstanceId = ++_nextInstanceId,
            TargetId = targetId,
            CasterId = casterId,
            ModifierGuid = modifierGuid,
            Rank = rank,
        };
        _byId[instance.InstanceId] = instance;
        if (!_byTarget.TryGetValue(targetId, out var list))
            _byTarget[targetId] = list = [];
        list.Add(instance);
        return instance;
    }

    public ModifierInstance? Get(uint instanceId) => _byId.GetValueOrDefault(instanceId);

    // First modifier on a target with the given def guid (nModifier.GetFirstModifierByGUID contract).
    public ModifierInstance? FindByGuid(uint targetId, uint modifierGuid) =>
        _byTarget.TryGetValue(targetId, out var list)
            ? list.FirstOrDefault(m => m.ModifierGuid == modifierGuid)
            : null;

    // Same-def instances on a target, insertion order (oldest first) — the list retail's stack-policy
    // switch iterates (FUN_009e4f50, this = target+0x29c modifier component, filtered by def guid).
    public IReadOnlyList<ModifierInstance> FindAllByGuid(uint targetId, uint modifierGuid) =>
        _byTarget.TryGetValue(targetId, out var list)
            ? list.Where(m => m.ModifierGuid == modifierGuid).ToList()
            : [];

    // Resolve the stack policy (ability schema activationType, def+0x190) against the target's existing
    // same-def modifiers, WITHOUT mutating — the caller applies the removes/refresh/create so the Lua
    // [3] deactivate + 0xA3/0xA4 replication stay on the script side. Mirrors retail FUN_009e6420's
    // switch(*(def+0x190)) cases 0-7 (default = reject). Verified 2026-07-10 (Ghidra jump table @0x9e6978).
    public StackDecision ResolveStackPolicy(uint targetId, uint casterId, uint modifierGuid, int activationType)
    {
        var same = FindAllByGuid(targetId, modifierGuid);
        switch (activationType)
        {
            case 0: // stack — always create a new instance alongside any existing
                return StackDecision.Create;
            case 1: // replace-all — remove every same-def instance, then create
                return StackDecision.CreateAfterRemoving(same.Select(m => m.InstanceId));
            case 2: // replace-per-caster — remove this caster's instance, then create
                return same.FirstOrDefault(m => m.CasterId == casterId) is { } prior
                    ? StackDecision.CreateAfterRemoving([prior.InstanceId])
                    : StackDecision.Create;
            case 3: // reject-per-initiator — if this caster already has one, no new instance
                return same.Any(m => m.CasterId == casterId) ? StackDecision.Reject : StackDecision.Create;
            case 4: // unique-per-caster — reuse this caster's existing instance if present
            case 5:
                return same.FirstOrDefault(m => m.CasterId == casterId) is { } existing
                    ? StackDecision.Existing(existing.InstanceId)
                    : StackDecision.Create;
            case 6: // single — if any same-def instance exists on the target, reject
                return same.Count > 0 ? StackDecision.Reject : StackDecision.Create;
            case 7: // refresh — keep the oldest, drop the rest, refresh its duration (0xA3), reuse it
                return same.Count == 0
                    ? StackDecision.Create
                    : StackDecision.RefreshOldest(same[0].InstanceId, same.Skip(1).Select(m => m.InstanceId));
            default: // retail default arm: skip create, no handle
                return StackDecision.Reject;
        }
    }

    public bool Remove(uint instanceId)
    {
        if (!_byId.Remove(instanceId, out var instance)) return false;
        if (_byTarget.TryGetValue(instance.TargetId, out var list))
        {
            list.Remove(instance);
            if (list.Count == 0) _byTarget.Remove(instance.TargetId);
        }
        return true;
    }
}
