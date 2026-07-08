using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

public static unsafe class NThreadModule
{
    public static void Register(nint L)
    {
        LuaApiModule.RegisterNamespace(L, "nThread",
            ("CreateThreadForObject", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&CreateThreadForObject),
            ("Sleep", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&Sleep),
            ("WaitForever", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&WaitForever),
            ("WaitForXSeconds", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&WaitForXSeconds),
            ("WaitUntilTime", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&WaitUntilTime),
            ("WakeUp", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&WakeUp),
            ("WaitForHitpointsAbove", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&WaitForHitpointsAbove),
            ("WaitForFadeOutInXSeconds", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&WaitForFadeOutInXSeconds),
            ("WaitForNearGoal", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&WaitForNearGoal),
            ("WaitForJumpComplete", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&WaitForJumpComplete));
    }

    // DEFERRED (no server-side movement integration): resume on a time estimate, not a live position
    // predicate. WaitForNearGoal args 3-5 (-1,10,true) roles unconfirmed; arg4 treated as timeout cap.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int WaitForNearGoal(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            var scheduler = ctx?.Scheduler;
            var bridge = ctx?.GameBridge;
            if (scheduler is not null && bridge is not null && LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER)
            {
                var objId = (uint)Math.Round((double)LuaNative.lua_tonumber(L, 1));
                var threshold = LuaNative.lua_type(L, 2) == LuaNative.LUA_TNUMBER ? (float)LuaNative.lua_tonumber(L, 2) : 0f;
                var timeout = LuaNative.lua_type(L, 4) == LuaNative.LUA_TNUMBER ? (double)LuaNative.lua_tonumber(L, 4) : 10.0;
                var remaining = bridge.TryGetGoalDistance(objId, out var dist) ? Math.Max(0f, dist - threshold) : 0f;
                var speed = Math.Max(0.01f, bridge.GetModifiedMoveSpeed(objId));
                var estimate = Math.Min(remaining / speed, timeout);
                scheduler.RegisterYield(L, sleeping: false, wakeAt: scheduler.Now + estimate);
            }
            else return 0;
        }
        catch (Exception ex)
        {
            try { Util.Logging.Log.Lua.Error($"[nThread] WaitForNearGoal failed: {ex.Message}"); } catch { }
            return 0;
        }
        return LuaNative.lua_yield(L, 0);
    }

    // DEFERRED default jump duration (real source: jump anim/locomotion tuning, not yet parsed).
    private const double DefaultJumpDurationSeconds = 0.6;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int WaitForJumpComplete(nint L)
    {
        try
        {
            var scheduler = ScriptContextRegistry.Get(L)?.Scheduler;
            scheduler?.RegisterYield(L, sleeping: false, wakeAt: scheduler.Now + DefaultJumpDurationSeconds);
        }
        catch (Exception ex)
        {
            try { Util.Logging.Log.Lua.Error($"[nThread] WaitForJumpComplete failed: {ex.Message}"); } catch { }
            return 0;
        }
        return LuaNative.lua_yield(L, 0);
    }

    // catalog §Mechanical: yield until GetHitPoints(objId) > threshold OR timeout (0 = no timeout).
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int WaitForHitpointsAbove(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            var scheduler = ctx?.Scheduler;
            var bridge = ctx?.GameBridge;
            if (scheduler is not null && bridge is not null && LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER)
            {
                var objId = (uint)Math.Round((double)LuaNative.lua_tonumber(L, 1));
                var threshold = LuaNative.lua_type(L, 2) == LuaNative.LUA_TNUMBER ? (float)LuaNative.lua_tonumber(L, 2) : 0f;
                var timeout = LuaNative.lua_type(L, 3) == LuaNative.LUA_TNUMBER ? (double)LuaNative.lua_tonumber(L, 3) : 0.0;
                double? wakeAt = timeout > 0 ? scheduler.Now + timeout : null;
                scheduler.RegisterYield(L, sleeping: false, wakeAt: wakeAt, wakeWhen: () => bridge.GetHitPoints(objId) > threshold);
            }
            else return 0;
        }
        catch (Exception ex)
        {
            try { Util.Logging.Log.Lua.Error($"[nThread] WaitForHitpointsAbove failed: {ex.Message}"); } catch { }
            return 0;
        }
        return LuaNative.lua_yield(L, 0);
    }

    // catalog §Mechanical: plain timed yield tied to corpse fade window.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int WaitForFadeOutInXSeconds(nint L)
    {
        try
        {
            var scheduler = ScriptContextRegistry.Get(L)?.Scheduler;
            var seconds = LuaNative.lua_type(L, 2) == LuaNative.LUA_TNUMBER
                ? Math.Max(0.0, (double)LuaNative.lua_tonumber(L, 2)) : 0.0;
            scheduler?.RegisterYield(L, sleeping: false, wakeAt: scheduler.Now + seconds);
        }
        catch (Exception ex)
        {
            try { Util.Logging.Log.Lua.Error($"[nThread] WaitForFadeOutInXSeconds failed: {ex.Message}"); } catch { }
            return 0;
        }
        return LuaNative.lua_yield(L, 0);
    }

    // Retail @0x00a02280: arg1 = absolute SIM time (seconds, clamped >= 0); optional args 2-3
    // (cast-speed object + overdrive scale) deferred. Yields until the scheduler clock reaches it.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int WaitUntilTime(nint L)
    {
        try
        {
            var scheduler = ScriptContextRegistry.Get(L)?.Scheduler;
            var rawTime = LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER
                ? (double)LuaNative.lua_tonumber(L, 1)
                : 0.0;
            var wakeAt = Math.Max(0.0, rawTime);
            scheduler?.RegisterYield(L, sleeping: false, wakeAt: Math.Max(scheduler.Now, wakeAt));
        }
        catch (Exception ex)
        {
            try { Util.Logging.Log.Lua.Error($"[nThread] WaitUntilTime failed: {ex.Message}"); } catch { }
            return 0;
        }
        return LuaNative.lua_yield(L, 0);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int CreateThreadForObject(nint L)
    {
        try
        {
            var scheduler = ScriptContextRegistry.Get(L)?.Scheduler;
            if (scheduler is null) return 0;
            if (LuaNative.lua_type(L, 1) != LuaNative.LUA_TNUMBER) return 0;
            if (LuaNative.lua_type(L, 2) != LuaNative.LUA_TFUNCTION) return 0;
            var objectId = (uint)Math.Round((double)LuaNative.lua_tonumber(L, 1));
            if (scheduler.HasThreadForObject(objectId)) return 0;
            var argCount = LuaNative.lua_gettop(L) - 2;
            scheduler.Spawn(L, objectId, 2, argCount);
        }
        catch (Exception ex)
        {
            try { Util.Logging.Log.Lua.Error($"[nThread] CreateThreadForObject failed: {ex.Message}"); } catch { }
        }
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Sleep(nint L)
    {
        try
        {
            var scheduler = ScriptContextRegistry.Get(L)?.Scheduler;
            scheduler?.RegisterYield(L, sleeping: true, wakeAt: null);
        }
        catch (Exception ex)
        {
            try { Util.Logging.Log.Lua.Error($"[nThread] Sleep failed: {ex.Message}"); } catch { }
            return 0;
        }
        return LuaNative.lua_yield(L, 0);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int WaitForever(nint L)
    {
        try
        {
            var scheduler = ScriptContextRegistry.Get(L)?.Scheduler;
            scheduler?.RegisterYield(L, sleeping: true, wakeAt: null);
        }
        catch (Exception ex)
        {
            try { Util.Logging.Log.Lua.Error($"[nThread] WaitForever failed: {ex.Message}"); } catch { }
            return 0;
        }
        return LuaNative.lua_yield(L, 0);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int WaitForXSeconds(nint L)
    {
        try
        {
            var scheduler = ScriptContextRegistry.Get(L)?.Scheduler;
            var rawSeconds = LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER
                ? (double)LuaNative.lua_tonumber(L, 1)
                : 0.0;
            var seconds = Math.Max(0.0, rawSeconds);
            scheduler?.RegisterYield(L, sleeping: false, wakeAt: (scheduler.Now + seconds));
        }
        catch (Exception ex)
        {
            try { Util.Logging.Log.Lua.Error($"[nThread] WaitForXSeconds failed: {ex.Message}"); } catch { }
            return 0;
        }
        return LuaNative.lua_yield(L, 0);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int WakeUp(nint L)
    {
        try
        {
            var scheduler = ScriptContextRegistry.Get(L)?.Scheduler;
            if (scheduler is null) return 0;
            if (LuaNative.lua_type(L, 1) != LuaNative.LUA_TNUMBER) return 0;
            var objectId = (uint)Math.Round((double)LuaNative.lua_tonumber(L, 1));
            scheduler.WakeObject(objectId);
        }
        catch (Exception ex)
        {
            try { Util.Logging.Log.Lua.Error($"[nThread] WakeUp failed: {ex.Message}"); } catch { }
        }
        return 0;
    }
}
