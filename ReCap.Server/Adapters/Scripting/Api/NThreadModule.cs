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
            ("WakeUp", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&WakeUp));
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
