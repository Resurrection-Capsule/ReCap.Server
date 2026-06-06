using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

public static unsafe class NMathUtilModule
{
    public static void Register(nint L) =>
        LuaApiModule.RegisterNamespace(L, "nMathUtil",
            ("TransformVector", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&TransformVector),
            ("CircleIntersectsArc", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&CircleIntersectsArc));

    // Caller contract (TargetUtils.lua disasm, melee hit-cone test): 9 args
    // (circleX, circleY, circleRadius, arcX, arcY, facingX, facingY, arcLength, totalAngle)
    // on the horizontal X/Y plane (Z is up; caller pre-gates |dz| separately) → 1 bool.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int CircleIntersectsArc(nint L)
    {
        try
        {
            float cx = LuaNative.lua_tonumber(L, 1), cy = LuaNative.lua_tonumber(L, 2);
            float cr = LuaNative.lua_tonumber(L, 3);
            float ax = LuaNative.lua_tonumber(L, 4), ay = LuaNative.lua_tonumber(L, 5);
            float fx = LuaNative.lua_tonumber(L, 6), fy = LuaNative.lua_tonumber(L, 7);
            float length = LuaNative.lua_tonumber(L, 8);
            float totalAngle = LuaNative.lua_tonumber(L, 9);

            var dx = cx - ax;
            var dy = cy - ay;
            var dist = MathF.Sqrt(dx * dx + dy * dy);

            bool hit;
            if (dist - cr > length)
            {
                hit = false;
            }
            else if (dist <= cr)
            {
                hit = true;
            }
            else
            {
                var delta = MathF.Atan2(dy, dx) - MathF.Atan2(fy, fx);
                while (delta > MathF.PI) delta -= 2f * MathF.PI;
                while (delta < -MathF.PI) delta += 2f * MathF.PI;
                var halfAngle = totalAngle * 0.5f;
                var circleSpan = MathF.Asin(Math.Clamp(cr / dist, 0f, 1f));
                hit = MathF.Abs(delta) <= halfAngle + circleSpan;
            }

            LuaNative.lua_pushboolean(L, hit ? 1 : 0);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushboolean(L, 0);
            return 1;
        }
    }

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
