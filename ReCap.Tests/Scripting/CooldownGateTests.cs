using ReCap.Server.Adapters.Scripting;

namespace ReCap.Tests.Scripting;

// The per-(object, ability) cooldown deadline that PayCooldownAndMana stamps and
// GameScriptContext.InvokeAbility honours to stop an ability re-firing every tick.
public class CooldownGateTests
{
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
