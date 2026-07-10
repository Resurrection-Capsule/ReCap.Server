using ReCap.Server.Domain.Gameplay;

namespace ReCap.Tests.Gameplay;

// Pure coverage of the modifier stack policy (activationType / def+0x190) — retail FUN_009e6420's
// switch, verified 2026-07-10 against the Ghidra jump table @0x9e6978. ResolveStackPolicy never
// mutates; it only reports which existing same-def instances to drop/refresh and whether to create.
public class ModifierStackPolicyTests
{
    private const uint Target = 10;
    private const uint OtherTarget = 11;
    private const uint CasterA = 100;
    private const uint CasterB = 200;
    private const uint Guid = 0xABCDEF01;
    private const uint OtherGuid = 0x12345678;

    private static ModifierSystem WithExisting(params (uint caster, uint guid, uint target)[] rows)
    {
        var sys = new ModifierSystem();
        foreach (var (caster, guid, target) in rows) sys.Create(target, caster, guid, 0);
        return sys;
    }

    [Fact]
    public void Stack_AlwaysCreates_NoRemovals()
    {
        var sys = WithExisting((CasterA, Guid, Target), (CasterB, Guid, Target));
        var d = sys.ResolveStackPolicy(Target, CasterA, Guid, 0);
        Assert.True(d.ShouldCreate);
        Assert.Empty(d.RemoveInstanceIds);
        Assert.Equal(0u, d.ReturnInstanceId);
    }

    [Fact]
    public void ReplaceAll_RemovesEverySameDefOnTarget_ThenCreates()
    {
        var sys = WithExisting((CasterA, Guid, Target), (CasterB, Guid, Target), (CasterA, OtherGuid, Target));
        var d = sys.ResolveStackPolicy(Target, CasterA, Guid, 1);
        Assert.True(d.ShouldCreate);
        Assert.Equal(2, d.RemoveInstanceIds.Count); // both same-def instances, not the OtherGuid one
    }

    [Fact]
    public void ReplaceAll_ScopedToTarget_LeavesOtherTargetsAlone()
    {
        var sys = WithExisting((CasterA, Guid, Target), (CasterA, Guid, OtherTarget));
        var d = sys.ResolveStackPolicy(Target, CasterA, Guid, 1);
        Assert.Single(d.RemoveInstanceIds);
    }

    [Fact]
    public void ReplacePerCaster_RemovesOnlyThisCasters_ThenCreates()
    {
        var sys = WithExisting((CasterA, Guid, Target), (CasterB, Guid, Target));
        var d = sys.ResolveStackPolicy(Target, CasterA, Guid, 2);
        Assert.True(d.ShouldCreate);
        Assert.Single(d.RemoveInstanceIds);
    }

    [Fact]
    public void ReplacePerCaster_NoPriorFromCaster_JustCreates()
    {
        var sys = WithExisting((CasterB, Guid, Target));
        var d = sys.ResolveStackPolicy(Target, CasterA, Guid, 2);
        Assert.True(d.ShouldCreate);
        Assert.Empty(d.RemoveInstanceIds);
    }

    [Fact]
    public void RejectPerInitiator_SameCasterHasOne_Rejects()
    {
        var sys = WithExisting((CasterA, Guid, Target));
        var d = sys.ResolveStackPolicy(Target, CasterA, Guid, 3);
        Assert.False(d.ShouldCreate);
        Assert.Equal(0u, d.ReturnInstanceId);
    }

    [Fact]
    public void RejectPerInitiator_DifferentCaster_Creates()
    {
        var sys = WithExisting((CasterB, Guid, Target));
        var d = sys.ResolveStackPolicy(Target, CasterA, Guid, 3);
        Assert.True(d.ShouldCreate);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void UniquePerCaster_ReusesExistingCasterInstance(int activation)
    {
        var sys = WithExisting((CasterB, Guid, Target));
        var existing = sys.FindByGuid(Target, Guid)!.InstanceId;
        var d = sys.ResolveStackPolicy(Target, CasterB, Guid, activation);
        Assert.False(d.ShouldCreate);
        Assert.Equal(existing, d.ReturnInstanceId);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void UniquePerCaster_NoCasterInstance_Creates(int activation)
    {
        var sys = WithExisting((CasterB, Guid, Target));
        var d = sys.ResolveStackPolicy(Target, CasterA, Guid, activation);
        Assert.True(d.ShouldCreate);
    }

    [Fact]
    public void Single_AnyExisting_Rejects()
    {
        var sys = WithExisting((CasterB, Guid, Target));
        var d = sys.ResolveStackPolicy(Target, CasterA, Guid, 6);
        Assert.False(d.ShouldCreate);
        Assert.Equal(0u, d.ReturnInstanceId);
    }

    [Fact]
    public void Single_NoneExisting_Creates()
    {
        var sys = new ModifierSystem();
        var d = sys.ResolveStackPolicy(Target, CasterA, Guid, 6);
        Assert.True(d.ShouldCreate);
    }

    [Fact]
    public void Refresh_KeepsOldest_DropsRest_RefreshesSurvivor()
    {
        var sys = WithExisting((CasterA, Guid, Target), (CasterB, Guid, Target), (CasterA, Guid, Target));
        var oldest = sys.FindAllByGuid(Target, Guid)[0].InstanceId;
        var d = sys.ResolveStackPolicy(Target, CasterA, Guid, 7);
        Assert.False(d.ShouldCreate);
        Assert.Equal(oldest, d.ReturnInstanceId);
        Assert.Equal(oldest, d.RefreshInstanceId);
        Assert.Equal(2, d.RemoveInstanceIds.Count); // the two newer instances pruned
    }

    [Fact]
    public void Refresh_NoneExisting_Creates()
    {
        var sys = new ModifierSystem();
        var d = sys.ResolveStackPolicy(Target, CasterA, Guid, 7);
        Assert.True(d.ShouldCreate);
        Assert.Equal(0u, d.RefreshInstanceId);
    }

    [Fact]
    public void UnknownActivationType_Rejects()
    {
        var sys = new ModifierSystem();
        var d = sys.ResolveStackPolicy(Target, CasterA, Guid, 99);
        Assert.False(d.ShouldCreate);
    }
}
