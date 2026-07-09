using AssetData.Parser.Model;
using ReCap.Server.Domain.Gameplay.AI;

namespace ReCap.Tests.Gameplay.AI;

public class AIControllerTests
{
    private sealed class RecordingActions : IAiActions
    {
        public List<string> Casts { get; } = new();
        public HashSet<string> FailingConditions { get; } = new();

        public void CastAbility(string abilityName, uint self, uint target) => Casts.Add(abilityName);

        public bool ConditionPasses(string conditionName, IReadOnlyList<(string Name, string Value)> props, uint self, uint target)
            => !FailingConditions.Contains(conditionName);
    }

    [Fact]
    public void Tick_ResolvesPhaseRef_CastsUnconditionalGambitAbility()
    {
        var actions = new RecordingActions();
        var phase = SyntheticAi.SingleUnconditionalGambitPhase("ZelemBasicMeleeAttack");
        var def = SyntheticAi.Definition("P");
        var ctrl = new AIController(def, name => name == "P" ? phase : null, actions);

        ctrl.Tick(selfId: 2, targetId: 1);

        Assert.Contains("ZelemBasicMeleeAttack", actions.Casts);
    }

    [Fact]
    public void Tick_NoAggroTarget_DoesNotCast()
    {
        var actions = new RecordingActions();
        var phase = SyntheticAi.SingleUnconditionalGambitPhase("ZelemBasicMeleeAttack");
        var def = SyntheticAi.Definition("P");
        var ctrl = new AIController(def, name => name == "P" ? phase : null, actions);

        ctrl.Tick(selfId: 2, targetId: 0);

        Assert.Empty(actions.Casts);
    }

    [Fact]
    public void Tick_PrioritizedList_FallsThroughToUnconditionalGambitWhenFirstFails()
    {
        var actions = new RecordingActions();
        actions.FailingConditions.Add("Distance");

        var gambit0 = new StructValue { Name = "0", TypeName = "cGambitDefinition" };
        gambit0.Add(new StringValue { Name = "condition", Value = "Distance" });
        var conditionProps = new ArrayValue { Name = "conditionProps", ElementType = "cAssetProperty" };
        var prop = new StructValue { Name = "0", TypeName = "cAssetProperty" };
        prop.Add(new StringValue { Name = "name", Value = "Distance" });
        prop.Add(new StringValue { Name = "value", Value = "10" });
        conditionProps.Add(prop);
        gambit0.Add(conditionProps);
        gambit0.Add(new StringValue { Name = "ability", Value = "RangedAttack" });

        var gambit1 = new StructValue { Name = "1", TypeName = "cGambitDefinition" };
        gambit1.Add(new StringValue { Name = "ability", Value = "Fallback" });

        var gambits = new ArrayValue { Name = "gambit", ElementType = "cGambitDefinition" };
        gambits.Add(gambit0);
        gambits.Add(gambit1);

        var phase = new StructValue { Name = "Phase", TypeName = "cPhase" };
        phase.Add(gambits);

        var def = SyntheticAi.Definition("P");
        var ctrl = new AIController(def, name => name == "P" ? phase : null, actions);

        ctrl.Tick(selfId: 2, targetId: 1);

        Assert.DoesNotContain("RangedAttack", actions.Casts);
        Assert.Contains("Fallback", actions.Casts);
    }

    [Fact]
    public void Tick_UnknownDefinitionShape_NoOpsWithoutThrowing()
    {
        var actions = new RecordingActions();
        var empty = new StructValue { Name = "AIDefinition", TypeName = "cAIDefinition" };
        var ctrl = new AIController(empty, _ => null, actions);

        var exception = Record.Exception(() => ctrl.Tick(1, 2));

        Assert.Null(exception);
        Assert.Empty(actions.Casts);
    }

    [Fact]
    public void Tick_PhaseRefDoesNotResolve_NoOpsWithoutThrowing()
    {
        var actions = new RecordingActions();
        var def = SyntheticAi.Definition("Missing");
        var ctrl = new AIController(def, _ => null, actions);

        var exception = Record.Exception(() => ctrl.Tick(1, 2));

        Assert.Null(exception);
        Assert.Empty(actions.Casts);
    }

    private static class SyntheticAi
    {
        public static AssetValue Definition(string phaseRefName)
        {
            var node = new StructValue { Name = "0", TypeName = "cAINode" };
            node.Add(new StringValue { Name = "mpPhaseData", Value = phaseRefName });

            var nodes = new ArrayValue { Name = "ainode", ElementType = "cAINode" };
            nodes.Add(node);

            var definition = new StructValue { Name = "AIDefinition", TypeName = "cAIDefinition" };
            definition.Add(nodes);
            return definition;
        }

        public static AssetValue SingleUnconditionalGambitPhase(string abilityName)
        {
            var gambit = new StructValue { Name = "0", TypeName = "cGambitDefinition" };
            gambit.Add(new StringValue { Name = "ability", Value = abilityName });

            var gambits = new ArrayValue { Name = "gambit", ElementType = "cGambitDefinition" };
            gambits.Add(gambit);

            var phase = new StructValue { Name = "Phase", TypeName = "cPhase" };
            phase.Add(gambits);
            return phase;
        }
    }
}
