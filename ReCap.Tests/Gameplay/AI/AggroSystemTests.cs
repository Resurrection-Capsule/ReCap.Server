using System.Numerics;
using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Domain.Gameplay.AI;

namespace ReCap.Tests.Gameplay.AI;

public class AggroSystemTests
{
    [Fact]
    public void InPerceptionCircle_MatchesGhidraSemantics()
    {
        // point within radius+offset of agent -> true (client InPerceptionCircle @0x00a059a0).
        Assert.True(AggroSystem.InPerceptionCircle(Vector3.Zero, new Vector3(3, 0, 4), radius: 5f));
        Assert.False(AggroSystem.InPerceptionCircle(Vector3.Zero, new Vector3(3, 0, 4), radius: 4f));
        Assert.True(AggroSystem.InPerceptionCircle(Vector3.Zero, new Vector3(3, 0, 4), radius: 4f, offset: 1f));
    }

    [Fact]
    public void Tick_AggrosHostilePlayerInPerception_AndPrunesWhenGone()
    {
        var om = new ObjectManager(null);
        var enemy = om.Spawn(2, 200, Vector3.Zero, 1f, team: 2, playerControlled: false);
        enemy.Agent = new AgentBlackboard { PerceptionRadius = 15f };
        var hero = om.Spawn(1, 100, new Vector3(5, 0, 0), 1f, team: 1, playerControlled: true);
        hero.Health = hero.MaxHealth = 100f;

        var sys = new AggroSystem(om);
        sys.Tick();
        Assert.Equal(1u, enemy.Agent.GetBestTarget(id => om.Objects.ContainsKey(id)));

        hero.Position = new Vector3(100, 0, 0); // out of perception
        sys.Tick();
        Assert.False(enemy.Agent.HasTargets);
    }

    [Fact]
    public void Tick_IgnoresSameTeamAndNonPlayers()
    {
        var om = new ObjectManager(null);
        var enemy = om.Spawn(2, 200, Vector3.Zero, 1f, team: 2, playerControlled: false);
        enemy.Agent = new AgentBlackboard { PerceptionRadius = 15f };
        om.Spawn(3, 200, new Vector3(1, 0, 0), 1f, team: 2, playerControlled: false); // ally npc, close
        var sys = new AggroSystem(om);
        sys.Tick();
        Assert.False(enemy.Agent.HasTargets);
    }
}
