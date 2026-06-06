using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

public static unsafe class PreloadModule
{
    public static void Register(nint L) =>
        LuaApiModule.RegisterNamespace(L, "nAbility",
            ("PreloadAsset", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&PreloadAsset),
            ("PreloadAnimation", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&PreloadAnimation),
            ("PreloadModifier", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&PreloadModifier));

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int PreloadAsset(nint L)
    {
        try
        {
            if (LuaNative.lua_type(L, 1) == LuaNative.LUA_TSTRING)
            {
                var name = LuaNative.ToManagedString(L, 1);
                LuaNative.lua_pushnumber(L, name is null ? 0f : (float)ScriptVfs.Hash(name));
            }
            else
            {
                var n = LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER
                    ? (float)System.Math.Round((double)LuaNative.lua_tonumber(L, 1))
                    : 0f;
                LuaNative.lua_pushnumber(L, n);
            }
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int PreloadAnimation(nint L)
    {
        try
        {
            if (LuaNative.lua_type(L, 1) == LuaNative.LUA_TSTRING)
            {
                var name = LuaNative.ToManagedString(L, 1);
                LuaNative.lua_pushnumber(L, name is null ? 0f : (float)ScriptVfs.Hash(name));
            }
            else
            {
                var n = LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER
                    ? (float)System.Math.Round((double)LuaNative.lua_tonumber(L, 1))
                    : 0f;
                LuaNative.lua_pushnumber(L, n);
            }
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int PreloadModifier(nint L)
    {
        try
        {
            var n = LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER
                ? (float)System.Math.Round((double)LuaNative.lua_tonumber(L, 1))
                : 0f;
            LuaNative.lua_pushnumber(L, n);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }
}
