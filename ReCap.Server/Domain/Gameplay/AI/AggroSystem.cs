using System.Linq;
using System.Numerics;

namespace ReCap.Server.Domain.Gameplay.AI;

// Server-side aggro/perception, ticked once per game frame from ObjectManager.Update. Mirrors the
// client's per-agent perception + aggro-list maintenance (nAgent::InPerceptionCircle @0x00a059a0,
// AddAggroForObject @0x009fd690). Slice rule: a hostile player-controlled object inside an agent's
// perception circle is on its aggro list; targets that die or leave perception are pruned.
public sealed class AggroSystem
{
    private readonly ObjectManager _om;
    public AggroSystem(ObjectManager om) => _om = om;

    public static bool InPerceptionCircle(Vector3 agentPos, Vector3 point, float radius, float offset = 0f)
        => radius >= Vector3.Distance(agentPos, point) - offset;

    public bool IsValidTarget(uint id)
        => _om.Objects.TryGetValue(id, out var o) && !o.Dead && o.Health > 0f;

    public uint BestTargetFor(GameObject agent)
        => agent.Agent is null ? 0u : agent.Agent.GetBestTarget(IsValidTarget);

    public void Tick()
    {
        foreach (var agent in _om.Objects.Values)
        {
            var bb = agent.Agent;
            if (bb is null || agent.Dead) continue;

            foreach (var other in _om.Objects.Values)
            {
                if (!other.PlayerControlled || other.Dead || other.Team == agent.Team) continue;
                if (InPerceptionCircle(agent.Position, other.Position, bb.PerceptionRadius))
                    bb.AddAggro(other.ObjectId);
            }

            foreach (var entry in bb.AggroList.ToArray())
            {
                if (!_om.Objects.TryGetValue(entry.ObjectId, out var t) || t.Dead ||
                    !InPerceptionCircle(agent.Position, t.Position, bb.PerceptionRadius))
                    bb.RemoveAggro(entry.ObjectId);
            }
        }
    }
}
