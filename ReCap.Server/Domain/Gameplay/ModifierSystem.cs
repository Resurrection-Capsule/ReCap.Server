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
