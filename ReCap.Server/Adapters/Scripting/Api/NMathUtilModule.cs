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
            ("CircleIntersectsArc", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&CircleIntersectsArc),
            ("DistanceToLine", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&DistanceToLine),
            ("RotateVectorByAxisAngle", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&RotateVectorByAxisAngle));

    // DistanceToLine(px,py,pz, ax,ay,az, bx,by,bz) @0x00a06f90 → 1 float: distance from P to the
    // SEGMENT A→B (client FUN_007b6a40 clamps the projection to [0,1]). Pure math.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int DistanceToLine(nint L)
    {
        try
        {
            var p = new Vector3(LuaNative.lua_tonumber(L, 1), LuaNative.lua_tonumber(L, 2), LuaNative.lua_tonumber(L, 3));
            var a = new Vector3(LuaNative.lua_tonumber(L, 4), LuaNative.lua_tonumber(L, 5), LuaNative.lua_tonumber(L, 6));
            var b = new Vector3(LuaNative.lua_tonumber(L, 7), LuaNative.lua_tonumber(L, 8), LuaNative.lua_tonumber(L, 9));
            var ab = b - a;
            var t = Vector3.Dot(p - a, ab);
            var lenSq = Vector3.Dot(ab, ab);
            var closest = t <= 0f ? a : t >= lenSq ? b : a + ab * (t / lenSq);
            LuaNative.lua_pushnumber(L, Vector3.Distance(p, closest));
            return 1;
        }
        catch { LuaNative.lua_pushnumber(L, 0f); return 1; }
    }

    // RotateVectorByAxisAngle(vx,vy,vz, ax,ay,az, angle) @0x00a09640 → 3 floats: rotate V around axis A
    // by `angle` RADIANS (client half-angle scale DAT_00fd896c = 0.5; axis used as-is, not normalized).
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int RotateVectorByAxisAngle(nint L)
    {
        try
        {
            var v = new Vector3(LuaNative.lua_tonumber(L, 1), LuaNative.lua_tonumber(L, 2), LuaNative.lua_tonumber(L, 3));
            var axis = new Vector3(LuaNative.lua_tonumber(L, 4), LuaNative.lua_tonumber(L, 5), LuaNative.lua_tonumber(L, 6));
            var angle = LuaNative.lua_tonumber(L, 7);
            var r = Vector3.Transform(v, Quaternion.CreateFromAxisAngle(axis, angle));
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

            ReCap.Server.Util.Logging.Log.Lua.Debug(
                $"[arc] circle=({cx:F1},{cy:F1} r={cr:F1}) arc=({ax:F1},{ay:F1}) facing=({fx:F2},{fy:F2}) " +
                $"len={length:F1} angle={totalAngle:F2} dist={dist:F1} → {(hit ? "HIT" : "miss")}");
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
