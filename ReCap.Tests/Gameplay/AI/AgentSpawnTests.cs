using System.Numerics;
using ReCap.Server.Domain.Gameplay;

namespace ReCap.Tests.Gameplay.AI;

public class AgentSpawnTests
{
    [Fact]
    public void PlayerControlled_GetsNoAgentBlackboard()
    {
        var om = new ObjectManager(null);
        var hero = om.Spawn(1, 100, Vector3.Zero, 1f, team: 1, playerControlled: true);
        Assert.Null(hero.Agent);
    }

    [Fact]
    public void NpcWithoutResolvedAiDefinition_GetsNoAgentBlackboard()
    {
        var om = new ObjectManager(null); // null db -> AIDefinition stays null
        var npc = om.Spawn(2, 200, Vector3.Zero, 1f, team: 2, playerControlled: false);
        Assert.Null(npc.Agent);
    }
}
