using ReCap.Server.Adapters.Scripting;
using ReCap.Server.Adapters.Scripting.Native;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

// The per-(object, ability) cooldown deadline that PayCooldownAndMana stamps and
// GameScriptContext.InvokeAbility honours to stop an ability re-firing every tick.
public class CooldownGateTests
{
    // Ability cooldowns are authored as ranked-value tables ({1.5, 1.25, 1} = rank 0/1/2), not a
    // scalar. The registrar must capture the whole table so PayCooldownAndMana can pick the cast's
    // rank; reading it as a single number yielded 0 and left basic melee attacks un-throttled.
    [Fact]
    public void RegistrarCapturesRankedCooldownTable()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        var ctx = ScriptContextRegistry.Get(rt.L)!;

        rt.Execute(LuaFixtures.Compile("""
            nAbility.RegisterAbility("RankedCd", { cooldown = {1.5, 1.25, 1}, tick = function() end })
            """), "cd");

        var entry = ctx.Registry.Find(ScriptKind.Ability, ScriptVfs.Hash("RankedCd"));
        Assert.NotNull(entry);
        Assert.Equal(1.5f, entry!.CooldownForRank(0));
        Assert.Equal(1.25f, entry.CooldownForRank(1));
        Assert.Equal(1f, entry.CooldownForRank(2));
        Assert.Equal(1f, entry.CooldownForRank(9)); // rank beyond table clamps to the last entry
    }

    [Fact]
    public void ScalarCooldownStillReadsAsRankZero()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        var ctx = ScriptContextRegistry.Get(rt.L)!;

        rt.Execute(LuaFixtures.Compile("""
            nAbility.RegisterAbility("ScalarCd", { cooldown = 3, tick = function() end })
            """), "cd2");

        var entry = ctx.Registry.Find(ScriptKind.Ability, ScriptVfs.Hash("ScalarCd"));
        Assert.NotNull(entry);
        Assert.Equal(3f, entry!.CooldownForRank(0));
        Assert.Equal(3f, entry.CooldownForRank(4));
    }

    [Fact]
    public void IsOnCooldown_BlocksUntilDeadlineThenClears()
    {
        var ctx = new ScriptStateContext { Registry = new ScriptRegistry() };

        Assert.False(ctx.IsOnCooldown(2, 0x1234, nowSeconds: 0d));   // never stamped

        ctx.StampCooldown(2, 0x1234, readyAtSeconds: 5d);
        Assert.True(ctx.IsOnCooldown(2, 0x1234, nowSeconds: 3d));    // still cooling
        Assert.False(ctx.IsOnCooldown(2, 0x1234, nowSeconds: 5d));   // ready at the deadline
        Assert.False(ctx.IsOnCooldown(2, 0x9999, nowSeconds: 3d));   // a different ability is unaffected
        Assert.False(ctx.IsOnCooldown(7, 0x1234, nowSeconds: 3d));   // a different object is unaffected
    }
}
