namespace ReCap.Server.Domain.Gameplay.AI;

// Mirrors the client cAgentBlackboard (GameObject+0x2b0, Ghidra 2026-07-09): the aggro list
// (+0x228/0x22c, pre-prioritized) + per-object perception radius. GetBestTarget returns the first
// still-valid entry in list order (client nAgent::SelectFirstValidAggroTarget @0x009e8dd0), NOT a
// nearest-scan. Present only on AI-agent objects; absence == the obj+0x2b0==0 "not an agent" gate.
public sealed class AgentBlackboard
{
    private readonly List<AggroEntry> _aggro = new();

    public float PerceptionRadius { get; set; }

    public IReadOnlyList<AggroEntry> AggroList => _aggro;
    public bool HasTargets => _aggro.Count > 0;

    public void AddAggro(uint objectId, float threat = 0f)
    {
        var i = _aggro.FindIndex(e => e.ObjectId == objectId);
        if (i >= 0) _aggro[i] = _aggro[i] with { Threat = _aggro[i].Threat + threat };
        else _aggro.Add(new AggroEntry(objectId, threat));
    }

    public void RemoveAggro(uint objectId) => _aggro.RemoveAll(e => e.ObjectId == objectId);

    public uint GetBestTarget(Func<uint, bool> isValid)
    {
        foreach (var entry in _aggro)
            if (isValid(entry.ObjectId)) return entry.ObjectId;
        return 0;
    }
}

public readonly record struct AggroEntry(uint ObjectId, float Threat);
