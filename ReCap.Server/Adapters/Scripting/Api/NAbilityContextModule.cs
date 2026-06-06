using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

public static unsafe class NAbilityContextModule
{
    public static void Register(nint L)
    {
        LuaApiModule.RegisterNamespace(L, "nAbility",
            ("GetAgentID", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetAgentID),
            ("GetTargetID", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetTargetID),
            ("GetTargetPosition", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetTargetPosition),
            ("GetRank", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetRank));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetAgentID(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            var inv = ctx?.GetInvocation(L);
            LuaNative.lua_pushnumber(L, inv.HasValue ? (float)inv.Value.AgentId : 0f);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetTargetID(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            var inv = ctx?.GetInvocation(L);
            LuaNative.lua_pushnumber(L, inv.HasValue ? (float)inv.Value.TargetId : 0f);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetTargetPosition(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            var inv = ctx?.GetInvocation(L);
            if (inv.HasValue)
            {
                LuaNative.lua_pushnumber(L, inv.Value.CursorX);
                LuaNative.lua_pushnumber(L, inv.Value.CursorY);
                LuaNative.lua_pushnumber(L, inv.Value.CursorZ);
            }
            else
            {
                LuaNative.lua_pushnumber(L, 0f);
                LuaNative.lua_pushnumber(L, 0f);
                LuaNative.lua_pushnumber(L, 0f);
            }
            return 3;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            LuaNative.lua_pushnumber(L, 0f);
            LuaNative.lua_pushnumber(L, 0f);
            return 3;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetRank(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            var inv = ctx?.GetInvocation(L);
            LuaNative.lua_pushnumber(L, inv.HasValue ? (float)inv.Value.Rank : 0f);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }
}
