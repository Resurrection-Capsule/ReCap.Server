using AssetData.Parser.Model;
using ReCap.Server.Services.Assets;

namespace ReCap.Server.Domain.Gameplay.AI;

public interface IAiActions
{
    void CastAbility(string abilityName, uint self, uint target);
    bool ConditionPasses(string conditionName, IReadOnlyList<(string Name, string Value)> props, uint self, uint target);
}

// Real AIDefinition shape (AI_SLICE_HARVEST.md, ZelemBasicMelee sample):
// ainode[0].mpPhaseData is an asset-REFERENCE (StringValue name) resolved to a Phase asset.
// Phase.gambit[] is a prioritizedList: first gambit whose condition passes (absent = unconditional)
// wins and its ability is cast. Tolerant: any null/missing/wrong-type shape -> no-op, never throws.
public sealed class AIController
{
    private readonly AssetValue _def;
    private readonly Func<string, AssetValue?> _resolveAsset;
    private readonly IAiActions _actions;

    public AIController(AssetValue aiDefinition, Func<string, AssetValue?> resolveAsset, IAiActions actions)
    {
        _def = aiDefinition;
        _resolveAsset = resolveAsset;
        _actions = actions;
    }

    public void Tick(uint selfId, uint targetId)
    {
        // Aggro-driven: no valid target on the aggro list ⇒ idle. The slice contract is
        // "aggro → best target → cast" (AI_SLICE_HARVEST.md); a basic-melee gambit is meaningless
        // without a target, and firing it every tick made enemies flail attacks at their own
        // position (target=0). Conditionless idle/patrol phases are a later, richer-enemy concern.
        if (targetId == 0) return;

        if (_def.FindByName("ainode") is not ArrayValue nodes || nodes.Items.Count == 0) return;
        var node = nodes.Items[0];

        var phaseName = (node.FindByName("mpPhaseData") as StringValue)?.Value;
        if (string.IsNullOrEmpty(phaseName)) return;

        var phase = _resolveAsset(phaseName);
        if (phase is null) return;

        if (phase.FindByName("gambit") is not ArrayValue gambits) return;

        foreach (var gambit in gambits.Items)
        {
            var conditionName = (gambit.FindByName("condition") as StringValue)?.Value;
            bool passes;
            if (string.IsNullOrEmpty(conditionName))
            {
                passes = true;
            }
            else
            {
                var props = ReadProps(gambit.FindByName("conditionProps"));
                passes = _actions.ConditionPasses(conditionName, props, selfId, targetId);
            }

            if (!passes) continue;

            var abilityName = (gambit.FindByName("ability") as StringValue)?.Value;
            if (!string.IsNullOrEmpty(abilityName))
            {
                _actions.CastAbility(abilityName, selfId, targetId);
            }
            return;
        }
    }

    private static IReadOnlyList<(string Name, string Value)> ReadProps(AssetValue? propsNode)
    {
        if (propsNode is not ArrayValue arr || arr.Items.Count == 0)
            return System.Array.Empty<(string, string)>();

        var result = new List<(string, string)>(arr.Items.Count);
        foreach (var item in arr.Items)
        {
            var name = (item.FindByName("name") as StringValue)?.Value;
            var value = (item.FindByName("value") as StringValue)?.Value;
            if (!string.IsNullOrEmpty(name)) result.Add((name, value ?? string.Empty));
        }
        return result;
    }
}
