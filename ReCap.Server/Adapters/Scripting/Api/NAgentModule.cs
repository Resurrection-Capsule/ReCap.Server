using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

// nAgent natives (client LuaFunctions::nAgent @0x00a05a90): aggro list + perception over AgentBlackboard.
public static unsafe class NAgentModule
{
    public static void Register(nint L)
    {
        LuaApiModule.RegisterNamespace(L, "nAgent",
            ("GetTargetsOnAggroList", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetTargetsOnAggroList),
            ("HasTargetsOnAggroList", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&HasTargetsOnAggroList),
            ("GetBestTarget", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetBestTarget),
            ("InPerceptionCircle", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&InPerceptionCircle));
    }

    private static uint ReadId(nint L, int idx) => (uint)Math.Round((double)LuaNative.lua_tonumber(L, idx));

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetTargetsOnAggroList(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            var ids = bridge is not null && LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER
                ? bridge.GetAggroTargets(ReadId(L, 1)) : [];
            LuaNative.lua_createtable(L, ids.Count, 0);
            var table = LuaNative.lua_gettop(L);
            for (int i = 0; i < ids.Count; i++)
            {
                LuaNative.lua_pushnumber(L, i + 1);
                LuaNative.lua_pushnumber(L, ids[i]);
                LuaNative.lua_rawset(L, table);
            }
            return 1;
        }
        catch { LuaNative.lua_createtable(L, 0, 0); return 1; }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int HasTargetsOnAggroList(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            bool has = bridge is not null && LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER && bridge.HasAggroTargets(ReadId(L, 1));
            LuaNative.lua_pushboolean(L, has ? 1 : 0);
            return 1;
        }
        catch { LuaNative.lua_pushboolean(L, 0); return 1; }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetBestTarget(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            uint best = bridge is not null && LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER ? bridge.GetBestTarget(ReadId(L, 1)) : 0u;
            LuaNative.lua_pushnumber(L, best);
            return 1;
        }
        catch { LuaNative.lua_pushnumber(L, 0); return 1; }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int InPerceptionCircle(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            bool inside = false;
            if (bridge is not null && LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER)
            {
                float x = (float)LuaNative.lua_tonumber(L, 2), y = (float)LuaNative.lua_tonumber(L, 3), z = (float)LuaNative.lua_tonumber(L, 4);
                float offset = LuaNative.lua_gettop(L) >= 5 ? (float)LuaNative.lua_tonumber(L, 5) : 0f;
                inside = bridge.InPerceptionCircle(ReadId(L, 1), x, y, z, offset);
            }
            LuaNative.lua_pushboolean(L, inside ? 1 : 0);
            return 1;
        }
        catch { LuaNative.lua_pushboolean(L, 0); return 1; }
    }
}
