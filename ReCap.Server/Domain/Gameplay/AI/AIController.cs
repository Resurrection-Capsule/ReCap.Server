using AssetData.Parser.Model;
using ReCap.Server.Services.Assets;

namespace ReCap.Server.Domain.Gameplay.AI;

public interface IAiActions
{
    bool ConditionMet(string ns, string name, uint self, uint target);
    void CastAbility(uint gambitAbility, uint self, uint target);
    void MoveToward(uint self, uint target);
}

// Walks an AIDefinition node graph (ainode[]: mpConditionData decides transitions via output edges;
// mpPhaseData carries gambits = condition->ability). Operates on the generic AssetValue shape so it
// needs no game data to unit-test; real condition namespace::names + phase encodings are exercised
// in-game after the harvest (Task 8). Tolerant: unknown/absent shapes -> no-op, never throws.
public sealed class AIController
{
    private readonly AssetValue _def;
    private readonly IAiActions _actions;
    public int CurrentNode { get; private set; }

    public AIController(AssetValue aiDefinition, IAiActions actions)
    {
        _def = aiDefinition;
        _actions = actions;
    }

    public void Tick(uint selfId, uint targetId)
    {
        var nodes = _def.FindByName("ainode");
        var node = NodeAt(nodes, CurrentNode);
        if (node is null) return;

        if (Condition(node.FindByName("mpConditionData"), selfId, targetId))
        {
            var outputs = node.FindByName("output");
            var next = FirstInt(outputs);
            if (next is int n && NodeAt(nodes, n) is not null) { CurrentNode = n; return; }
        }

        var phase = node.FindByName("mpPhaseData");
        foreach (var gambit in Gambits(phase))
        {
            if (!Condition(gambit.FindByName("condition"), selfId, targetId)) continue;
            var ability = gambit.FindByName("ability").AsUInt32();
            if (ability != 0) { _actions.CastAbility(ability, selfId, targetId); return; }
        }

        if (targetId != 0) _actions.MoveToward(selfId, targetId);
    }

    private bool Condition(AssetValue? conditionData, uint self, uint target)
    {
        if (conditionData is null) return false;
        var ns = conditionData.FindByName("namespace").AsString();
        var name = conditionData.FindByName("name").AsString();
        return !string.IsNullOrEmpty(ns) && !string.IsNullOrEmpty(name) && _actions.ConditionMet(ns, name, self, target);
    }

    private static AssetValue? NodeAt(AssetValue? nodes, int index)
        => nodes is ArrayValue a && index >= 0 && index < a.Items.Count ? a.Items[index] : null;

    private static IEnumerable<AssetValue> Gambits(AssetValue? phase)
        => phase is ArrayValue a ? a.Items : phase is not null ? new[] { phase } : System.Array.Empty<AssetValue>();

    private static int? FirstInt(AssetValue? arr)
        => arr is ArrayValue a && a.Items.Count > 0 ? (int)a.Items[0].AsUInt32() : null;
}
