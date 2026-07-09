using AssetData.Parser.Model;
using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Domain.Gameplay.AI;

namespace ReCap.Tests.Gameplay.AI;

public class AiTickIntegrationTests
{
    private sealed class RecordingActions : IAiActions
    {
        public List<string> Casts { get; } = new();

        public void CastAbility(string abilityName, uint self, uint target) => Casts.Add(abilityName);

        public bool ConditionPasses(string conditionName, IReadOnlyList<(string Name, string Value)> props, uint self, uint target)
            => true;
    }

    private static class SyntheticAi
    {
        public static (AssetValue Definition, Func<string, AssetValue?> Resolver) SingleUnconditionalGambit(string abilityName)
        {
            var node = new StructValue { Name = "0", TypeName = "cAINode" };
            node.Add(new StringValue { Name = "mpPhaseData", Value = "P" });

            var nodes = new ArrayValue { Name = "ainode", ElementType = "cAINode" };
            nodes.Add(node);

            var definition = new StructValue { Name = "AIDefinition", TypeName = "cAIDefinition" };
            definition.Add(nodes);

            var gambit = new StructValue { Name = "0", TypeName = "cGambitDefinition" };
            gambit.Add(new StringValue { Name = "ability", Value = abilityName });

            var gambits = new ArrayValue { Name = "gambit", ElementType = "cGambitDefinition" };
            gambits.Add(gambit);

            var phase = new StructValue { Name = "Phase", TypeName = "cPhase" };
            phase.Add(gambits);

            return (definition, name => name == "P" ? phase : null);
        }
    }

    [Fact]
    public void Update_AggrosAndCasts_ForAgentWithHeroInPerception()
    {
        var om = new ObjectManager(null);
        var enemy = om.Spawn(2, 200, System.Numerics.Vector3.Zero, 1f, team: 2, playerControlled: false);
        enemy.Agent = new AgentBlackboard { PerceptionRadius = 15f };
        var hero = om.Spawn(1, 100, new System.Numerics.Vector3(5, 0, 0), 1f, team: 1, playerControlled: true);
        hero.Health = hero.MaxHealth = 100f;

        var actions = new RecordingActions();
        var ai = SyntheticAi.SingleUnconditionalGambit("ZelemBasicMeleeAttack");
        om.AttachController(enemy.ObjectId, new AIController(ai.Definition, ai.Resolver, actions));

        om.Update(0.05);

        Assert.Equal(1u, enemy.Agent.GetBestTarget(id => om.Objects.ContainsKey(id)));
        Assert.Contains("ZelemBasicMeleeAttack", actions.Casts);
    }
}
