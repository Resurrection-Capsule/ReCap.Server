using AssetData.Parser.Model;
using ReCap.Server.Domain.Gameplay.AI;

namespace ReCap.Tests.Gameplay.AI;

public class AIControllerTests
{
    private sealed class RecordingActions : IAiActions
    {
        public HashSet<(string, string)> TrueConditions { get; } = new();
        public List<uint> Casts { get; } = new();
        public List<(uint self, uint target)> Moves { get; } = new();
        public bool ConditionMet(string ns, string name, uint self, uint target) => TrueConditions.Contains((ns, name));
        public void CastAbility(uint ability, uint self, uint target) => Casts.Add(ability);
        public void MoveToward(uint self, uint target) => Moves.Add((self, target));
    }

    [Fact]
    public void Tick_TransitionsOnCondition_ThenCastsGambitAbility()
    {
        var actions = new RecordingActions();
        var ai = SyntheticAi.TwoNodeGambit();
        var ctrl = new AIController(ai, actions);

        ctrl.Tick(selfId: 2, targetId: 1);
        Assert.Empty(actions.Casts);
        Assert.Equal(0, ctrl.CurrentNode);

        actions.TrueConditions.Add(("ai", "InRange"));
        ctrl.Tick(2, 1);
        Assert.Equal(1, ctrl.CurrentNode);

        actions.TrueConditions.Add(("ai", "HasTarget"));
        ctrl.Tick(2, 1);
        Assert.Contains(999u, actions.Casts);
    }

    [Fact]
    public void Tick_UnknownDefinitionShape_NoOpsWithoutThrowing()
    {
        var actions = new RecordingActions();
        var empty = new StructValue { Name = "AIDefinition", TypeName = "cAIDefinition" };
        var ctrl = new AIController(empty, actions);

        var exception = Record.Exception(() => ctrl.Tick(1, 2));

        Assert.Null(exception);
        Assert.Empty(actions.Casts);
        Assert.Empty(actions.Moves);
    }

    private static class SyntheticAi
    {
        public static AssetValue TwoNodeGambit()
        {
            var condition0 = new StructValue { Name = "mpConditionData", TypeName = "cAICondition" };
            condition0.Add(new StringValue { Name = "namespace", Value = "ai" });
            condition0.Add(new StringValue { Name = "name", Value = "InRange" });

            var output0 = new ArrayValue { Name = "output", ElementType = "int" };
            output0.Add(new NumberValue { Name = "0", Value = 1 });

            var node0 = new StructValue { Name = "0", TypeName = "cAINode" };
            node0.Add(condition0);
            node0.Add(output0);

            var gambitCondition = new StructValue { Name = "condition", TypeName = "cAICondition" };
            gambitCondition.Add(new StringValue { Name = "namespace", Value = "ai" });
            gambitCondition.Add(new StringValue { Name = "name", Value = "HasTarget" });

            var gambit = new StructValue { Name = "mpPhaseData", TypeName = "cAIGambit" };
            gambit.Add(gambitCondition);
            gambit.Add(new NumberValue { Name = "ability", Value = 999 });

            var node1 = new StructValue { Name = "1", TypeName = "cAINode" };
            node1.Add(gambit);

            var nodes = new ArrayValue { Name = "ainode", ElementType = "cAINode" };
            nodes.Add(node0);
            nodes.Add(node1);

            var definition = new StructValue { Name = "AIDefinition", TypeName = "cAIDefinition" };
            definition.Add(nodes);
            return definition;
        }
    }
}
