using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

public static unsafe class NObjectManagerModule
{
    public static void Register(nint L)
    {
        LuaApiModule.RegisterNamespace(L, "nObjectManager",
            ("IsValidObject", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&IsValidObject));
    }

    // Retail @0x009fb160: 1 arg (object id), 1 boolean — pure existence check, no side effects.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int IsValidObject(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            var valid = LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER
                && bridge is not null
                && bridge.ObjectExists((uint)Math.Round((double)LuaNative.lua_tonumber(L, 1)));
            LuaNative.lua_pushboolean(L, valid ? 1 : 0);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushboolean(L, 0);
            return 1;
        }
    }
}
