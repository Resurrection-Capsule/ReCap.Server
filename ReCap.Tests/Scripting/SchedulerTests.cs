using ReCap.Server.Adapters.Scripting;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

public class SchedulerTests
{
    private static (LuaRuntime rt, LuaCoroutineScheduler sched) Make()
    {
        var rt = LuaRuntime.CreateSandboxedState();
        var sched = ScriptContextRegistry.Get(rt.L)!.Scheduler!;
        return (rt, sched);
    }

    [Fact]
    public void WaitForXSecondsResumesAfterDeadline()
    {
        var (rt, sched) = Make();
        using var _ = rt;
        rt.Execute(LuaFixtures.Compile(
            "nThread.CreateThreadForObject(1, function() nThread.WaitForXSeconds(1.0) Done = true end)"), "t");
        Assert.False(rt.EvalBool(LuaFixtures.Compile("return Done == true")));
        sched.Tick(0.5);
        Assert.False(rt.EvalBool(LuaFixtures.Compile("return Done == true")));
        sched.Tick(1.1);
        Assert.True(rt.EvalBool(LuaFixtures.Compile("return Done == true")));
    }

    [Fact]
    public void SleepOnlyWakesOnWakeUp()
    {
        var (rt, sched) = Make();
        using var _ = rt;
        rt.Execute(LuaFixtures.Compile(
            "nThread.CreateThreadForObject(2, function() nThread.Sleep() Woke = true end)"), "t");
        sched.Tick(100.0);
        Assert.False(rt.EvalBool(LuaFixtures.Compile("return Woke == true")));
        rt.Execute(LuaFixtures.Compile("nThread.WakeUp(2)"), "wake");
        sched.Tick(100.1);
        Assert.True(rt.EvalBool(LuaFixtures.Compile("return Woke == true")));
    }

    [Fact]
    public void OneThreadPerObjectGuard()
    {
        var (rt, sched) = Make();
        using var _ = rt;
        rt.Execute(LuaFixtures.Compile("""
            Count = 0
            nThread.CreateThreadForObject(3, function() Count = Count + 1 nThread.WaitForever() end)
            nThread.CreateThreadForObject(3, function() Count = Count + 100 end)
            """), "t");
        Assert.True(rt.EvalBool(LuaFixtures.Compile("return Count == 1")));
    }

    [Fact]
    public void ErroredCoroutineIsReleasedAndLogged()
    {
        var (rt, sched) = Make();
        using var _ = rt;
        rt.Execute(LuaFixtures.Compile(
            "nThread.CreateThreadForObject(4, function() nThread.WaitForXSeconds(0.1) error('boom') end)"), "t");
        sched.Tick(1.0);
        rt.Execute(LuaFixtures.Compile(
            "nThread.CreateThreadForObject(4, function() Recovered = true end)"), "t2");
        Assert.True(rt.EvalBool(LuaFixtures.Compile("return Recovered == true")));
    }
}
