using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

public static unsafe class NUtilModule
{
    public static void Register(nint L) =>
        LuaApiModule.RegisterNamespace(L, "nUtil",
            ("SPID", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&Spid),
            ("ToGUID", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&ToGuid));

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Spid(nint L)
    {
        try
        {
            var s = LuaNative.lua_type(L, 1) == LuaNative.LUA_TSTRING
                ? LuaNative.ToManagedString(L, 1)
                : null;
            LuaNative.lua_pushnumber(L, s is null ? 0f : (float)ScriptVfs.Hash(s));
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int ToGuid(nint L)
    {
        try
        {
            var s = LuaNative.lua_type(L, 1) == LuaNative.LUA_TSTRING
                ? LuaNative.ToManagedString(L, 1)
                : null;
            ulong v = 0;
            if (s is not null)
            {
                var hex = s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? s[2..] : s;
                ulong.TryParse(hex, System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out v);
            }
            LuaNative.lua_pushnumber(L, (float)v);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }
}
