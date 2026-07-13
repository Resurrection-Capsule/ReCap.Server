using ReCap.Server.Adapters.Blaze.Component.GameManager;
using ReCap.Server.Adapters.Persistence;
using ReCap.Server.Adapters.Scripting;
using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Services.Scripting;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

// nObjective (Ghidra registrar @0x00a0c3d0): the objective data store (Set/Get Int/Float/GUID) and the
// objective-event system (Create/Send/Destroy + typed slots), dispatched to registered objectives'
// HandleEvent ([2]) by handledEvents. Driven through the real GameScriptContext.
public class NObjectiveModuleTests
{
    private static GameScriptContext MakeContext(out Game game)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "recap-obj-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var engine = new ScriptEngine(new ScriptVfs(new PackageMounts(tempDir)));
        game = new Game(1, GameType.Matched, null);
        var ctx = new GameScriptContext(game, engine);
        game.ScriptContext = ctx;
        return ctx;
    }

    [Fact]
    public void ObjectiveIntData_RoundTripsByTargetAndIndex()
    {
        using var ctx = MakeContext(out _);
        ctx.Runtime.Execute(LuaFixtures.Compile("""
            nObjective.SetObjectiveIntData(255, 0, 95, true, false)
            nObjective.SetObjectiveIntData(255, 1, 42, false, false)
            """), "set");

        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return nObjective.GetObjectiveIntData(255, 0) == 95")));
        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return nObjective.GetObjectiveIntData(255, 1) == 42")));
        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return nObjective.GetObjectiveIntData(255, 9) == 0"))); // unset
    }

    [Fact]
    public void ObjectiveFloatAndGuidData_RoundTrip()
    {
        using var ctx = MakeContext(out _);
        ctx.Runtime.Execute(LuaFixtures.Compile("""
            nObjective.SetObjectiveFloatData(255, 0, 0.75)
            nObjective.SetObjectiveGUIDData(255, 0, 12345)
            """), "set");

        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile(
            "return math.abs(nObjective.GetObjectiveFloatData(255, 0) - 0.75) < 0.001")));
        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return nObjective.GetObjectiveGUIDData(255, 0) == 12345")));
    }

    [Fact]
    public void ObjectiveEvent_DispatchesToMatchingObjectiveHandleEvent_WithPayloadByHandle()
    {
        using var ctx = MakeContext(out _);
        // An objective subscribed to event type 2 records the type + the guid slot the handler reads.
        ctx.Runtime.Execute(LuaFixtures.Compile("""
            SeenType, SeenGuid = -1, -1
            nObjective.RegisterObjective("recap_obj_probe", {
                handledEvents = 2,
                [2] = function(evType, evHandle)
                        SeenType = evType
                        SeenGuid = nObjective.GetObjectiveEventGUIDData(evHandle, 1)
                      end,
            })
            NoFire = 0
            nObjective.RegisterObjective("recap_obj_other", {
                handledEvents = 1,
                [2] = function(evType, evHandle) NoFire = NoFire + 1 end,
            })
            """), "reg");
        ctx.ActivateObjectives(new[] { "recap_obj_probe", "recap_obj_other" }); // events dispatch to active objectives

        ctx.Runtime.Execute(LuaFixtures.Compile("""
            local ev = nObjective.CreateObjectiveEvent(2)
            nObjective.SetObjectiveEventGUIDData(ev, 1, 777)
            nObjective.SendObjectiveEvent(ev)
            nObjective.DestroyObjectiveEvent(ev)
            """), "send");

        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return SeenType == 2 and SeenGuid == 777")));
        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return NoFire == 0"))); // wrong subscription doesn't fire
        Assert.Equal(0, ctx.Scheduler.ErrorCount);
    }

    [Fact]
    public void DestroyObjectiveEvent_FreesTheBuilder()
    {
        using var ctx = MakeContext(out _);
        // After destroy, a send on the same handle finds no event and dispatches nothing.
        ctx.Runtime.Execute(LuaFixtures.Compile("""
            Fired = 0
            nObjective.RegisterObjective("recap_obj_free", {
                handledEvents = 4,
                [2] = function() Fired = Fired + 1 end,
            })
            """), "reg");
        ctx.ActivateObjectives(new[] { "recap_obj_free" }); // active, so only the destroy keeps it from firing
        ctx.Runtime.Execute(LuaFixtures.Compile("""
            local ev = nObjective.CreateObjectiveEvent(4)
            nObjective.DestroyObjectiveEvent(ev)
            nObjective.SendObjectiveEvent(ev)
            """), "t");

        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return Fired == 0")));
    }

    [Fact]
    public void GetRegisteredDestructibles_IsZeroWhenUntracked()
    {
        using var ctx = MakeContext(out _);
        Assert.True(ctx.Runtime.EvalBool(LuaFixtures.Compile("return nObjective.GetRegisteredDestructibles() == 0")));
    }
}
