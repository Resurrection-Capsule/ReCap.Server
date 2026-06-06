using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

public static unsafe class NGameObjectModule
{
    public static void Register(nint L)
    {
        LuaApiModule.RegisterNamespace(L, "nGameObject",
            ("GetPosition", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetPosition),
            ("GetHitPoints", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetHitPoints),
            ("GetMaxHitPoints", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetMaxHitPoints),
            ("IsAlive", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&IsAlive),
            ("GetTeam", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetTeam),
            ("GetTargetID", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetTargetID));
    }

    private static uint ReadId(nint L) =>
        (uint)Math.Round((double)LuaNative.lua_tonumber(L, 1));

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetPosition(nint L)
    {
        float x = 0, y = 0, z = 0;
        bool found = false;
        try
        {
            if (LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER)
            {
                var id = ReadId(L);
                var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
                found = bridge is not null && bridge.TryGetPosition(id, out x, out y, out z);
            }
        }
        catch
        {
            found = false;
        }
        if (!found)
        {
            LuaNative.lua_pushstring(L, "Could not find object!");
            return LuaNative.lua_error(L);
        }
        LuaNative.lua_pushnumber(L, x);
        LuaNative.lua_pushnumber(L, y);
        LuaNative.lua_pushnumber(L, z);
        return 3;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetHitPoints(nint L)
    {
        try
        {
            if (LuaNative.lua_type(L, 1) != LuaNative.LUA_TNUMBER) { LuaNative.lua_pushnumber(L, 0f); return 1; }
            var id = ReadId(L);
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            LuaNative.lua_pushnumber(L, bridge?.GetHitPoints(id) ?? 0f);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetMaxHitPoints(nint L)
    {
        try
        {
            if (LuaNative.lua_type(L, 1) != LuaNative.LUA_TNUMBER) { LuaNative.lua_pushnumber(L, 0f); return 1; }
            var id = ReadId(L);
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            LuaNative.lua_pushnumber(L, bridge?.GetMaxHitPoints(id) ?? 0f);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int IsAlive(nint L)
    {
        try
        {
            if (LuaNative.lua_type(L, 1) != LuaNative.LUA_TNUMBER) { LuaNative.lua_pushboolean(L, 0); return 1; }
            var id = ReadId(L);
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            var hp = bridge?.GetHitPoints(id) ?? 0f;
            LuaNative.lua_pushboolean(L, hp > 0f ? 1 : 0);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushboolean(L, 0);
            return 1;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetTeam(nint L)
    {
        try
        {
            if (LuaNative.lua_type(L, 1) != LuaNative.LUA_TNUMBER) return 0;
            var id = ReadId(L);
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            if (bridge is null || !bridge.ObjectExists(id)) return 0;
            LuaNative.lua_pushnumber(L, (float)bridge.GetTeam(id));
            return 1;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetTargetID(nint L)
    {
        try
        {
            if (LuaNative.lua_type(L, 1) != LuaNative.LUA_TNUMBER) { LuaNative.lua_pushnumber(L, 0f); return 1; }
            var id = ReadId(L);
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            LuaNative.lua_pushnumber(L, bridge is not null ? (float)bridge.GetTargetId(id) : 0f);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }
}
