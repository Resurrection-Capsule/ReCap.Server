using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

public static unsafe class NMathUtilModule
{
    public static void Register(nint L) =>
        LuaApiModule.RegisterNamespace(L, "nMathUtil",
            ("TransformVector", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&TransformVector));

    // Retail @0x00a06bb0: 7 args (vx, vy, vz, qx, qy, qz, qw), 3 float returns —
    // rotates the vector by the quaternion. Pure math, no game state.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int TransformVector(nint L)
    {
        try
        {
            var v = new Vector3(LuaNative.lua_tonumber(L, 1), LuaNative.lua_tonumber(L, 2), LuaNative.lua_tonumber(L, 3));
            var q = new Quaternion(LuaNative.lua_tonumber(L, 4), LuaNative.lua_tonumber(L, 5),
                                   LuaNative.lua_tonumber(L, 6), LuaNative.lua_tonumber(L, 7));
            var r = Vector3.Transform(v, q);
            LuaNative.lua_pushnumber(L, r.X);
            LuaNative.lua_pushnumber(L, r.Y);
            LuaNative.lua_pushnumber(L, r.Z);
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
}
