using System.Numerics;
using ReCap.Server.Adapters.Blaze.Component.GameManager;
using ReCap.Server.Adapters.Persistence;
using ReCap.Server.Adapters.Scripting;
using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Services.Scripting;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

// The remaining Tier-1/2 deferred natives, now implemented (Ghidra 2026-07-13):
// GetNPCType, GetAbilityAttributeValue, the CreateAbilityEvent/SendAbilityEvent chain,
// MoveToPointWithinRange (blocking yield), JumpInDirection (physics no-op).
public class DeferredNativesTests
{
    private static GameScriptContext MakeContext(out Game game)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "recap-defn-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var engine = new ScriptEngine(new ScriptVfs(new PackageMounts(tempDir)));
        game = new Game(1, GameType.Matched, null);
        var ctx = new GameScriptContext(game, engine);
        game.ScriptContext = ctx;
        return ctx;
    }

    [Fact]
    public void GetNPCType_ReturnsCachedTypeElseMinusOne()
    {
        using var ctx = MakeContext(out var game);
        game.Objects.Spawn(10, 0, Vector3.Zero, 1f, 2, false);         // no noun data -> -1
        game.Objects.Spawn(20, 0, new Vector3(1, 0, 0), 1f, 1, true);
        game.Objects.Objects[20].NpcType = 3;                          // e.g. a boss type

        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return nGameObject.GetNPCType(10) == -1")));
        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return nGameObject.GetNPCType(20) == 3")));
    }

    [Fact]
    public void GetAbilityAttributeValue_NoAssetDb_ReturnsZeroHarmlessly()
    {
        using var ctx = MakeContext(out var game);
        game.Objects.Spawn(10, 0, Vector3.Zero, 1f, 2, false);
        // No AssetDatabase in this harness, so the ability's scalingAttribute can't resolve -> 0, no throw.
        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return nAbility.GetAbilityAttributeValue(10, 12345) == 0")));
    }

    [Fact]
    public void CreateAndSendAbilityEvent_FiresSubscribedModifierIndex4()
    {
        using var ctx = MakeContext(out var game);
        // A modifier on target 10 subscribed to event type 4 records that its [4] fired with that type.
        ctx.Runtime.Execute(LuaFixtures.Compile("""
            EvSeen = -1
            nModifier.RegisterModifier("recap_absevt_probe", {
                handledEvents = 4,
                [2] = function() nThread.WaitForever() end,
                [4] = function(ev) EvSeen = nAbility.GetAbilityEventType(ev) return true end,
            })
            NoFire = 0
            nModifier.RegisterModifier("recap_absevt_other", {
                handledEvents = 1,
                [4] = function(ev) NoFire = NoFire + 1 return true end,
            })
            """), "reg");
        ctx.CreateModifier(10, 20, ScriptVfs.Hash("recap_absevt_probe"), 0);
        ctx.CreateModifier(10, 20, ScriptVfs.Hash("recap_absevt_other"), 0);

        ctx.Runtime.Execute(LuaFixtures.Compile("""
            local ev = nAbility.CreateAbilityEvent(4)
            nAbility.SendAbilityEvent(ev, 10)
            """), "send");

        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return EvSeen == 4")));
        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return NoFire == 0"))); // wrong subscription doesn't fire
        Assert.Equal(0, ctx.Scheduler.ErrorCount);
    }

    [Fact]
    public void SendAbilityEvent_ReusableAcrossTargets()
    {
        using var ctx = MakeContext(out var game);
        ctx.Runtime.Execute(LuaFixtures.Compile("""
            Hits = 0
            nModifier.RegisterModifier("recap_absevt_multi", {
                handledEvents = 8,
                [2] = function() nThread.WaitForever() end,
                [4] = function(ev) Hits = Hits + 1 return true end,
            })
            """), "reg");
        ctx.CreateModifier(10, 0, ScriptVfs.Hash("recap_absevt_multi"), 0);
        ctx.CreateModifier(11, 0, ScriptVfs.Hash("recap_absevt_multi"), 0);

        // One event object dispatched to two targets (gravitystorm loop pattern).
        ctx.Runtime.Execute(LuaFixtures.Compile("""
            local ev = nAbility.CreateAbilityEvent(8)
            nAbility.SendAbilityEvent(ev, 10)
            nAbility.SendAbilityEvent(ev, 11)
            """), "send");

        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return Hits == 2")));
    }

    [Fact]
    public void MoveToPointWithinRange_AimsGoalAndParksUntilArrival()
    {
        using var ctx = MakeContext(out var game);
        game.Objects.Spawn(10, 0, Vector3.Zero, 1f, 2, false); // speed 5

        ctx.Runtime.Execute(LuaFixtures.Compile("""
            nThread.CreateThreadForObject(10, function() nLocomotion.MoveToPointWithinRange(10, 12, 0, 0, 2) end)
            """), "move");

        Assert.Equal(new Vector3(12, 0, 0), game.Objects.Objects[10].GoalPosition);
        Assert.True(ctx.Scheduler.HasThreadForObject(10)); // parked until arrival

        for (var i = 0; i < 60; i++) ctx.Tick(); // (12-2)/5 = 2.0s < 3.0s
        Assert.False(ctx.Scheduler.HasThreadForObject(10));
    }

    [Fact]
    public void JumpInDirection_IsHarmlessAndReportsHasLocomotion()
    {
        using var ctx = MakeContext(out var game);
        game.Objects.Spawn(10, 0, Vector3.Zero, 1f, 2, false);

        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile(
            "return nLocomotion.JumpInDirection(10, 1, 0, 0, 5, 0, 0, 0, 0, 0, 0, 0) == true")));
        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return nLocomotion.JumpInDirection(999) == false")));
    }
}
