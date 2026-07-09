using ReCap.Server.Adapters.Scripting;

namespace ReCap.Server.Domain.Gameplay.AI;

public sealed class BridgeAiActions(IScriptGameBridge bridge) : IAiActions
{
    public void CastAbility(string abilityName, uint self, uint target) =>
        bridge.CastAiAbility(abilityName, self, target);

    public bool ConditionPasses(string conditionName, IReadOnlyList<(string Name, string Value)> props, uint self, uint target) =>
        bridge.EvaluateAiCondition(conditionName, props, self, target);
}
