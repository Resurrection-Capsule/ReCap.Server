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

    // Registry ref of the instance-scoped private Lua table (retail instance+0x170) shared across the
    // modifier's index [1] activate / [2] tick / [3] deactivate invocations; 0 = not yet created.
    // Owned by the instance — freed on removal, NOT when an individual index coroutine finishes.
    public int PrivateTableRef { get; set; }
}

// The outcome of the stack-policy switch: which existing instances to remove first, whether to create
// a fresh one, and — for reuse/refresh policies — the existing instance to hand back (RefreshOldest
// also asks the caller to send 0xA3 on it). Reject = create nothing, return handle 0.
public readonly record struct StackDecision(
    bool ShouldCreate,
    uint ReturnInstanceId,
    uint RefreshInstanceId,
    IReadOnlyList<uint> RemoveInstanceIds,
    uint StackEventInstanceId = 0)
{
    public static StackDecision Create => new(true, 0, 0, []);
    public static StackDecision Reject => new(false, 0, 0, []);
    public static StackDecision Existing(uint id) => new(false, id, 0, []);
    // Stacks (activationType 4/5): reuse the existing instance and fire its [4] StackModifier event
    // (retail nModifier_RunReapplyEvent @0x009e6060) so its Lua bumps the stack + resets duration.
    public static StackDecision StackOntoExisting(uint id) => new(false, id, 0, [], id);
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
        // Enum values verified against GlobalDefinitions.lua nActivationType 2026-07-10.
        var same = FindAllByGuid(targetId, modifierGuid);
        switch (activationType)
        {
            case 0: // Default — always create a new instance alongside any existing
                return StackDecision.Create;
            case 1: // Unique — remove every same-def instance, then create
                return StackDecision.CreateAfterRemoving(same.Select(m => m.InstanceId));
            case 2: // CasterUnique — remove this caster's instance, then create
                return same.FirstOrDefault(m => m.CasterId == casterId) is { } prior
                    ? StackDecision.CreateAfterRemoving([prior.InstanceId])
                    : StackDecision.Create;
            case 3: // CasterUniqueIrreplaceable — if this caster already has one, no new instance (SproutPoison)
                return same.Any(m => m.CasterId == casterId) ? StackDecision.Reject : StackDecision.Create;
            case 4: // Stacks — reuse this caster's existing instance and fire its [4] StackModifier event
            case 5: // StacksAndCasterUnique — (bumps stack + resets duration in Lua), else create fresh
                return same.FirstOrDefault(m => m.CasterId == casterId) is { } existing
                    ? StackDecision.StackOntoExisting(existing.InstanceId)
                    : StackDecision.Create;
            case 6: // UniqueIrreplaceable — if any same-def instance exists on the target, reject
                return same.Count > 0 ? StackDecision.Reject : StackDecision.Create;
            case 7: // UniqueResets — keep the oldest, drop the rest, reset its duration (0xA3), reuse it
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
