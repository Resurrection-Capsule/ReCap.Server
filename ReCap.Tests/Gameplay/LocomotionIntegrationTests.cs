using System.Numerics;
using ReCap.Server.Adapters.Blaze.Component.GameManager;
using ReCap.Server.Domain.Gameplay;

namespace ReCap.Tests.Gameplay;

// Server-authoritative movement: ObjectManager.Update advances non-player objects toward their
// locomotion goal so distance/range checks work and enemies actually pursue (not freeze in place).
public class LocomotionIntegrationTests
{
    private static GameObject Spawn(ObjectManager om, uint id, Vector3 pos)
    {
        var o = om.Spawn(id, 0, pos, 1f, team: 0, playerControlled: false);
        o.MoveSpeed = 5f;
        return o;
    }

    [Fact]
    public void EnemyAdvancesTowardGoalEachTick()
    {
        var om = new ObjectManager(null);
        var o = Spawn(om, 10, Vector3.Zero);
        o.GoalPosition = new Vector3(10, 0, 0);
        o.GoalFlags = 0x001; // moving

        om.Update(1.0); // 1s * speed 5 = advance 5 units
        Assert.Equal(5f, om.Objects[10].Position.X, 2);
        om.Update(1.0);
        Assert.Equal(10f, om.Objects[10].Position.X, 2); // reached the goal
    }

    [Fact]
    public void StopsWithinDesiredStopDistance()
    {
        var om = new ObjectManager(null);
        var o = Spawn(om, 10, Vector3.Zero);
        o.GoalPosition = new Vector3(10, 0, 0);
        o.DesiredStopDistance = 2f; // stop 2 units short
        o.GoalFlags = 0x001;

        for (var i = 0; i < 10; i++) om.Update(1.0);
        Assert.Equal(8f, om.Objects[10].Position.X, 2);       // lands on the 2-unit stop ring
        Assert.Equal(0x020u, om.Objects[10].GoalFlags & 0x020u); // and stops
    }

    [Fact]
    public void AggroedEnemyPursuesTargetIntoMeleeRange()
    {
        var om = new ObjectManager(null);
        var enemy = Spawn(om, 10, Vector3.Zero);
        enemy.Agent = new ReCap.Server.Domain.Gameplay.AI.AgentBlackboard { PerceptionRadius = 100f };
        var player = om.Spawn(20, 0, new Vector3(20, 0, 0), 1f, team: 1, playerControlled: true);
        player.Health = 100f; player.MaxHealth = 100f;
        enemy.Agent.AddAggro(20); // enemy has the player on its aggro list

        var startX = om.Objects[10].Position.X;
        for (var i = 0; i < 20; i++) om.Update(0.5); // plenty of time to close 20 units at speed 5
        var endX = om.Objects[10].Position.X;

        Assert.True(endX > startX, "enemy did not pursue the target");
        // Stops within melee reach of the player at x=20, not on top of it.
        Assert.InRange(20f - endX, 2.0f, 3.5f);
    }

    [Fact]
    public void PlayerControlledObjectsAreNotServerMoved()
    {
        var om = new ObjectManager(null);
        var p = om.Spawn(10, 0, Vector3.Zero, 1f, team: 0, playerControlled: true);
        p.MoveSpeed = 5f;
        p.GoalPosition = new Vector3(10, 0, 0);
        p.GoalFlags = 0x001;

        om.Update(1.0);
        Assert.Equal(0f, om.Objects[10].Position.X, 2); // client-driven; server does not move it
    }
}
