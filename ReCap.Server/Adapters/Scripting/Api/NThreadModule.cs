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
            ("MoveTowardObject", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&MoveTowardObject),
            ("WaitForProjectile", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&WaitForProjectile),
            ("WaitForJumpComplete", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&WaitForJumpComplete));
    }

    private const float ProjectileImpactRadius = 2.0f;

    // nThread.WaitForProjectile(projectileObj, caster, params{mDirection={x,y,z}, mSpeed, mRange}) —
    // the projectile ability yields until its shot lands, then resumes with the hit result the caller
    // branches on (hitObject or nil, then impact x/y/z). We have no projectile physics, so approximate:
    // fly `mRange` along `mDirection` at `mSpeed` (flight time = range/speed), then on wake report the
    // nearest hostile within a small radius of the impact point as the hit (else a miss). Enough for a
    // ranged enemy to actually connect; true collision/homing simulation is deferred.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int WaitForProjectile(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            var scheduler = ctx?.Scheduler;
            var bridge = ctx?.GameBridge;
            if (scheduler is null || bridge is null) return 0;

            var caster = LuaNative.lua_type(L, 2) == LuaNative.LUA_TNUMBER
                ? (uint)Math.Round((double)LuaNative.lua_tonumber(L, 2)) : 0u;

            float speed = 8f, range = 10f, dx = 0f, dy = 0f, dz = 0f;
            if (LuaNative.lua_type(L, 3) == LuaNative.LUA_TTABLE)
            {
                speed = TableNumber(L, 3, "mSpeed", 8f);
                range = TableNumber(L, 3, "mRange", 10f);
                (dx, dy, dz) = TableVector(L, 3, "mDirection");
            }

            bridge.TryGetPosition(caster, out var sx, out var sy, out var sz);
            var len = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
            if (len > 0f) { dx /= len; dy /= len; dz /= len; }
            var impactX = sx + dx * range;
            var impactY = sy + dy * range;
            var impactZ = sz + dz * range;
            var casterTeam = bridge.GetTeam(caster);
            var flight = speed > 0f ? range / speed : 0.3f;

            scheduler.RegisterYield(L, sleeping: false, wakeAt: scheduler.Now + flight, wakeWhen: null,
                resumeValues: t =>
                {
                    uint hitId = 0;
                    foreach (var id in bridge.QueryObjectsInRadius(impactX, impactY, impactZ, ProjectileImpactRadius, true))
                        if (id != caster && bridge.GetTeam(id) != casterTeam) { hitId = id; break; }
                    if (hitId != 0) LuaNative.lua_pushnumber(t, hitId); else LuaNative.lua_pushnil(t);
                    LuaNative.lua_pushnumber(t, impactX);
                    LuaNative.lua_pushnumber(t, impactY);
                    LuaNative.lua_pushnumber(t, impactZ);
                    return 4;
                });
        }
        catch (Exception ex)
        {
            try { Util.Logging.Log.Lua.Error($"[nThread] WaitForProjectile failed: {ex.Message}"); } catch { }
            return 0;
        }
        return LuaNative.lua_yield(L, 0);
    }

    private static float TableNumber(nint L, int tableIndex, string field, float fallback)
    {
        LuaNative.lua_getfield(L, tableIndex, field);
        var v = LuaNative.lua_type(L, -1) == LuaNative.LUA_TNUMBER ? (float)LuaNative.lua_tonumber(L, -1) : fallback;
        LuaNative.lua_settop(L, -2);
        return v;
    }

    private static (float, float, float) TableVector(nint L, int tableIndex, string field)
    {
        LuaNative.lua_getfield(L, tableIndex, field);
        float x = 0f, y = 0f, z = 0f;
        if (LuaNative.lua_type(L, -1) == LuaNative.LUA_TTABLE)
        {
            LuaNative.lua_rawgeti(L, -1, 1); x = (float)LuaNative.lua_tonumber(L, -1); LuaNative.lua_settop(L, -2);
            LuaNative.lua_rawgeti(L, -1, 2); y = (float)LuaNative.lua_tonumber(L, -1); LuaNative.lua_settop(L, -2);
            LuaNative.lua_rawgeti(L, -1, 3); z = (float)LuaNative.lua_tonumber(L, -1); LuaNative.lua_settop(L, -2);
        }
        LuaNative.lua_settop(L, -2);
        return (x, y, z);
    }

    // MoveTowardObject(mover, target, [stopDist], ...) @0x00a03ad0 — blocking chase: point the mover's
    // goal at the target's current position (stopping stopDist short) and yield until arrival. Retail
    // tracks the moving target live; we aim once and resume on a time estimate (same approximation as
    // WaitForNearGoal), which suffices for an enemy closing on the hero per gambit tick. The trailing
    // face/flag args are locomotion tuning we don't model. Returns nothing on the resume path.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int MoveTowardObject(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            var scheduler = ctx?.Scheduler;
            var bridge = ctx?.GameBridge;
            if (scheduler is null || bridge is null
                || LuaNative.lua_type(L, 1) != LuaNative.LUA_TNUMBER || LuaNative.lua_type(L, 2) != LuaNative.LUA_TNUMBER)
            { LuaNative.lua_pushboolean(L, 0); return 1; }

            var mover = (uint)Math.Round((double)LuaNative.lua_tonumber(L, 1));
            var target = (uint)Math.Round((double)LuaNative.lua_tonumber(L, 2));
            var stopDist = LuaNative.lua_type(L, 3) == LuaNative.LUA_TNUMBER ? (float)LuaNative.lua_tonumber(L, 3) : 0f;
            if (!bridge.TryGetPosition(target, out var tx, out var ty, out var tz))
            { LuaNative.lua_pushboolean(L, 0); return 1; }

            bridge.SetLocomotionGoal(mover, tx, ty, tz, stopDist);
            var remaining = bridge.TryGetGoalDistance(mover, out var dist) ? Math.Max(0f, dist - stopDist) : 0f;
            var speed = Math.Max(0.01f, bridge.GetModifiedMoveSpeed(mover));
            scheduler.RegisterYield(L, sleeping: false, wakeAt: scheduler.Now + Math.Min(remaining / speed, 10.0));
        }
        catch (Exception ex)
        {
            try { Util.Logging.Log.Lua.Error($"[nThread] MoveTowardObject failed: {ex.Message}"); } catch { }
            LuaNative.lua_pushboolean(L, 0);
            return 1;
        }
        return LuaNative.lua_yield(L, 0);
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

    // Retail @0x00a02280: arg1 is a RELATIVE duration in seconds (clamped >= 0), NOT an absolute sim
    // time — the impl stores it as a duration and the wake predicate (@0x00a02230) fires when
    // now-castStart >= duration. So yield for `seconds` from now. The ability tick passes small
    // hit-time offsets (e.g. 0.26); the old absolute reading made the wait a no-op once the clock
    // passed 0.26, so basic-attack coroutines finished in one tick and the AI re-cast every 50ms.
    // DEFERRED: cast-speed scaling (÷(1+castSpeed) via arg2 snapshot) and the multi-hit cast-start
    // epoch (each WaitUntilTime relative to the same cast start) — the single-hit slice needs neither.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int WaitUntilTime(nint L)
    {
        try
        {
            var scheduler = ScriptContextRegistry.Get(L)?.Scheduler;
            var seconds = LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER
                ? Math.Max(0.0, (double)LuaNative.lua_tonumber(L, 1))
                : 0.0;
            scheduler?.RegisterYield(L, sleeping: false, wakeAt: scheduler.Now + seconds);
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
