using ReCap.Server.Domain.Gameplay.AI;

namespace ReCap.Tests.Gameplay.AI;

public class AgentBlackboardTests
{
    [Fact]
    public void AddAggro_IsIdempotentPerObject_AndAccumulatesThreat()
    {
        var bb = new AgentBlackboard();
        bb.AddAggro(10, 5f);
        bb.AddAggro(10, 3f);
        bb.AddAggro(20, 1f);
        Assert.Equal(2, bb.AggroList.Count);
        Assert.Equal(8f, bb.AggroList.Single(e => e.ObjectId == 10).Threat);
        Assert.True(bb.HasTargets);
    }

    [Fact]
    public void GetBestTarget_ReturnsFirstValidInListOrder()
    {
        var bb = new AgentBlackboard();
        bb.AddAggro(10);
        bb.AddAggro(20);
        // 10 is invalid (dead/gone) -> best target is the next valid, 20.
        Assert.Equal(20u, bb.GetBestTarget(id => id == 20));
        Assert.Equal(0u, bb.GetBestTarget(_ => false));
    }

    [Fact]
    public void RemoveAggro_DropsTarget()
    {
        var bb = new AgentBlackboard();
        bb.AddAggro(10);
        bb.RemoveAggro(10);
        Assert.False(bb.HasTargets);
    }
}
