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

    [Fact]
    public void WaitForHitpointsAboveResumesWhenHpCrossesThreshold()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        var ctx = ScriptContextRegistry.Get(rt.L)!;
        var bridge = new MutableHpBridge();
        ctx.GameBridge = bridge;
        var scheduler = ctx.Scheduler!;

        // Spawn a coroutine for object 10 that waits until its HP rises above 50 (no timeout).
        rt.Execute(LuaFixtures.Compile("""
            nThread.CreateThreadForObject(10, function()
                nThread.WaitForHitpointsAbove(10, 50, 0)
            end)
            """), "wfa");

        scheduler.Tick(1.0);
        Assert.True(scheduler.HasThreadForObject(10)); // hp 40 <= 50 -> parked
        bridge.Hp = 60f;
        scheduler.Tick(2.0);
        Assert.False(scheduler.HasThreadForObject(10)); // hp 60 > 50 -> resumed
    }

    private sealed class MutableHpBridge : IScriptGameBridge
    {
        public float Hp = 40f;
        public float GetHitPoints(uint id) => Hp;
        public float GetMaxHitPoints(uint id) => 100f;
        public bool ObjectExists(uint id) => true;
        public bool TryGetPosition(uint id, out float x, out float y, out float z) { x = y = z = 0f; return true; }
        public byte GetTeam(uint id) => 0;
        public void SetTeam(uint id, byte t) { }
        public uint GetTargetId(uint id) => 0;
        public bool IsPlayerControlled(uint id) => false;
        public byte GetPlayerId(uint id) => 0;
        public bool TryGetAttributeValue(uint id, int a, out float v) { v = 0f; return false; }
        public IReadOnlyDictionary<int, float>? GetAttributeTable(uint id) => null;
        public bool TryGetOrientation(uint id, out float x, out float y, out float z, out float w) { x = y = z = 0f; w = 1f; return true; }
        public void BroadcastAnimationState(uint id, uint s) { }
        public void ResetAnimationState(uint id) { }
        public IReadOnlyList<uint> QueryObjectsInRadius(float x, float y, float z, float r, bool d) => [];
        public float ApplyHeal(uint id, float a) => 0f;
        public void MarkForDelete(uint id) { }
        public void SetVisible(uint id, bool v) { }
        public void SetLocomotionGoal(uint id, float x, float y, float z, float stop) { }
        public void SetLocomotionTarget(uint id, float x, float y, float z) { }
        public void SetFacing(uint id, float x, float y, float z) { }
        public void StopLocomotion(uint id) { }
        public void SetNavCollision(uint id, bool collidable) { }
        public float GetModifiedMoveSpeed(uint id) => 10f;
        public bool TryGetGoalDistance(uint id, out float d) { d = 20f; return true; }
        public uint AddAttributeModifier(uint id, int a, float v) => 0;
        public uint EmitEffect(uint id, uint fx, uint init) => 0;
        public void EmitServerEvent(uint fx, uint id, uint attacker, bool critical, System.Numerics.Vector3? position, System.Numerics.Vector3? facing) { }
        public uint CreateObject(uint n, float x, float y, float z) => 0;
        public void BroadcastCombatEvent(uint target, uint source, float delta, int hp, ushort flags) { }
        public void SendCooldownUpdate(uint objectId, uint abilityId, float cooldownSeconds) { }
        public IReadOnlyList<uint> GetAggroTargets(uint a) => System.Array.Empty<uint>();
        public bool HasAggroTargets(uint a) => false;
        public uint GetBestTarget(uint a) => 0;
        public bool InPerceptionCircle(uint a, float x, float y, float z, float o) => false;
        public void CastAiAbility(string abilityName, uint self, uint target) { }
        public bool EvaluateAiCondition(string conditionName, IReadOnlyList<(string Name, string Value)> props, uint self, uint target) => false;
    }

    // WaitUntilTime(t) is a RELATIVE wait of t seconds from the call — the retail impl (@0x00a02280)
    // stores t as a duration and the wake predicate (@0x00a02230) fires when now-startTime >= t. The
    // ability tick passes small hit-time offsets (e.g. 0.26); treating them as absolute sim time made
    // the wait a no-op once the game clock passed 0.26, so basic-attack coroutines finished in one tick
    // and the AI re-cast every 50ms.
    [Fact]
    public void WaitUntilTimeWaitsRelativeToNowNotAbsoluteSimTime()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        var scheduler = ScriptContextRegistry.Get(rt.L)!.Scheduler!;
        scheduler.Tick(10.0); // sim clock already well past the 0.26 hit-time offset

        rt.Execute(LuaFixtures.Compile("""
            nThread.CreateThreadForObject(10, function()
                nThread.WaitUntilTime(0.26)
            end)
            """), "wut");

        scheduler.Tick(10.1);
        Assert.True(scheduler.HasThreadForObject(10));  // 0.26s not elapsed -> parked
        scheduler.Tick(10.4);
        Assert.False(scheduler.HasThreadForObject(10)); // 0.26s elapsed -> resumed
    }

    [Fact]
    public void WaitForNearGoalResumesAfterEstimatedTravelTime()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        var ctx = ScriptContextRegistry.Get(rt.L)!;
        ctx.GameBridge = new MutableHpBridge(); // goalDistance 20, moveSpeed 10 → ~2s travel
        var scheduler = ctx.Scheduler!;
        rt.Execute(LuaFixtures.Compile("""
            nThread.CreateThreadForObject(10, function()
                nThread.WaitForNearGoal(10, 0, -1, 10, true)
            end)
            """), "wng");

        scheduler.Tick(1.0);
        Assert.True(scheduler.HasThreadForObject(10));  // ~2s estimate not elapsed
        scheduler.Tick(3.5);
        Assert.False(scheduler.HasThreadForObject(10)); // elapsed → resumed
    }
}
