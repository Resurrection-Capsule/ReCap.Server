using ReCap.Server.Adapters.Scripting;
using ReCap.Server.Adapters.Scripting.Native;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

public class SchedulerPredicateTests
{
    [Fact]
    public void PredicateYieldResumesWhenConditionTrue()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        var scheduler = ScriptContextRegistry.Get(rt.L)!.Scheduler!;
        var gate = false;
        var predicate = new Func<bool>(() => gate);

        var thread = scheduler.Spawn(rt.L, objectId: 10, fnIndex: PushSleeperFn(rt.L), argCount: 0);
        scheduler.RegisterYield(thread, sleeping: false, wakeAt: null, wakeWhen: predicate);

        scheduler.Tick(1.0);
        Assert.True(scheduler.HasThreadForObject(10)); // predicate false -> still parked

        gate = true;
        scheduler.Tick(2.0);
        Assert.False(scheduler.HasThreadForObject(10)); // predicate true -> resumed to completion
    }

    [Fact]
    public void PredicateYieldResumesOnTimeoutEvenIfPredicateStaysFalse()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        var scheduler = ScriptContextRegistry.Get(rt.L)!.Scheduler!;
        var predicate = new Func<bool>(() => false);

        var thread = scheduler.Spawn(rt.L, objectId: 11, fnIndex: PushSleeperFn(rt.L), argCount: 0);
        scheduler.RegisterYield(thread, sleeping: false, wakeAt: 5.0, wakeWhen: predicate);

        scheduler.Tick(4.0);
        Assert.True(scheduler.HasThreadForObject(11)); // neither predicate nor timeout ready

        scheduler.Tick(5.0);
        Assert.False(scheduler.HasThreadForObject(11)); // timeout reached -> resumed despite false predicate
    }

    // Pushes onto rt.L's stack a compiled function that yields once via coroutine.yield()
    // then returns, and returns its stack index (top). Spawn() moves it (+following args)
    // into the new coroutine thread.
    private static int PushSleeperFn(nint L)
    {
        var chunk = LuaFixtures.Compile("return function() coroutine.yield() end");
        var status = LuaNative.luaL_loadbuffer(L, chunk, (nuint)chunk.Length, "sleeper");
        if (status != LuaNative.LUA_OK)
            throw new InvalidOperationException($"luaL_loadbuffer failed: {LuaNative.ToManagedString(L, -1)}");
        var callStatus = LuaNative.lua_pcall(L, 0, 1, 0);
        if (callStatus != LuaNative.LUA_OK)
            throw new InvalidOperationException($"chunk exec failed: {LuaNative.ToManagedString(L, -1)}");
        return LuaNative.lua_gettop(L);
    }
}
