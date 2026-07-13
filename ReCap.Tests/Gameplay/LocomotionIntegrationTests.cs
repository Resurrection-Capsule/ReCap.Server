using System.Numerics;
using ReCap.Server.Domain.Gameplay;

namespace ReCap.Tests.Gameplay;

// Server-authoritative movement: ObjectManager.Update advances a pursuing non-player object toward its
// MoveTarget so distance/range checks work and enemies pursue (not freeze). The 0x95 wire goal carries
// the object's CURRENT integrated position (small step), not the far target, so the client trails
// smoothly instead of teleporting across the map.
public class LocomotionIntegrationTests
{
    private static GameObject Spawn(ObjectManager om, uint id, Vector3 pos)
    {
        var o = om.Spawn(id, 0, pos, 1f, team: 0, playerControlled: false);
        o.MoveSpeed = 5f;
        return o;
    }

    [Fact]
    public void EnemyAdvancesTowardMoveTarget_WireGoalIsCurrentPositionNotFarTarget()
    {
        var om = new ObjectManager(null);
        var o = Spawn(om, 10, Vector3.Zero);
        o.MoveTarget = new Vector3(10, 0, 0);

        om.Update(1.0); // 1s * speed 5 = advance 5 units
        Assert.Equal(5f, om.Objects[10].Position.X, 2);
        // The wire goal (0x95) is the small-step current position, NOT the far MoveTarget — no teleport.
        Assert.Equal(om.Objects[10].Position, om.Objects[10].GoalPosition);
    }

    [Fact]
    public void StopsWithinMeleeRange_AndClearsMoveTarget()
    {
        var om = new ObjectManager(null);
        var o = Spawn(om, 10, Vector3.Zero);
        o.MoveTarget = new Vector3(10, 0, 0);

        for (var i = 0; i < 10; i++) om.Update(1.0);
        Assert.Equal(7.5f, om.Objects[10].Position.X, 1); // stops on the 2.5 melee ring
        Assert.Null(om.Objects[10].MoveTarget);           // and holds (no more pursuit)
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
        Assert.InRange(20f - endX, 2.0f, 3.5f); // stops within melee reach, not on top of the player
    }

    [Fact]
    public void PlayerControlledObjectsAreNotServerMoved()
    {
        var om = new ObjectManager(null);
        var p = om.Spawn(10, 0, Vector3.Zero, 1f, team: 0, playerControlled: true);
        p.MoveSpeed = 5f;
        p.MoveTarget = new Vector3(10, 0, 0);

        om.Update(1.0);
        Assert.Equal(0f, om.Objects[10].Position.X, 2); // client-driven; server does not move it
    }
}
