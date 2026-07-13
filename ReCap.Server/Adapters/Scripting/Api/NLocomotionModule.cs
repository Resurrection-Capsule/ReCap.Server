using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

public static unsafe class NLocomotionModule
{
    public static void Register(nint L) =>
        LuaApiModule.RegisterNamespace(L, "nLocomotion",
            ("Stop", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&Stop),
            ("SlideToPoint", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&SlideToPoint),
            ("MoveToCircleEdge", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&MoveToCircleEdge),
            ("TurnToFace", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&TurnToFace),
            ("TeleportObject", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&TeleportObject),
            ("MoveToObject", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&MoveToObject),
            ("MoveToPointExact", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&MoveToPointExact),
            ("MoveToPointWithinRange", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&MoveToPointWithinRange),
            ("TurnToFaceTargetObject", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&TurnToFaceTargetObject),
            ("JumpInDirection", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&JumpInDirection));

    private static uint Id(nint L, int i) => (uint)Math.Round((double)LuaNative.lua_tonumber(L, i));

    // TeleportObject(obj, x, y, z, [face]) @0x00a04590 — instant reposition + teleport-route replication.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int TeleportObject(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            if (bridge is not null && Num(L, 1, out var id) && Num(L, 2, out var x) && Num(L, 3, out var y) && Num(L, 4, out var z))
            {
                var face = LuaNative.lua_gettop(L) >= 5 && LuaNative.lua_toboolean(L, 5) != 0;
                bridge.TeleportObject((uint)Math.Round(id), x, y, z, face);
            }
        }
        catch { }
        return 0;
    }

    // MoveToObject(obj, targetObj, [extraRange=0], [face]) @0x009fadc0 — pathfind toward a live object,
    // stopping `extraRange` short of it; returns has-locomotion. We aim at the target's current position
    // (the AI re-issues per gambit tick, so tracking a moving target falls out of re-evaluation).
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int MoveToObject(nint L)
    {
        var ok = false;
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            if (bridge is not null && Num(L, 1, out var id) && Num(L, 2, out var tid)
                && bridge.TryGetPosition(Id(L, 2), out var tx, out var ty, out var tz))
            {
                var extra = Num(L, 3, out var r) ? r : 0f;
                bridge.SetLocomotionGoal(Id(L, 1), tx, ty, tz, extra);
                ok = bridge.ObjectExists(Id(L, 1));
            }
        }
        catch { }
        LuaNative.lua_pushboolean(L, ok ? 1 : 0);
        return 1;
    }

    // MoveToPointExact(obj, x, y, z, [face]) @0x00a048c0 — pathfind to an exact point (no stop distance);
    // returns has-locomotion.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int MoveToPointExact(nint L)
    {
        var ok = false;
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            if (bridge is not null && Num(L, 1, out var id) && Num(L, 2, out var x) && Num(L, 3, out var y) && Num(L, 4, out var z))
            {
                bridge.SetLocomotionGoal(Id(L, 1), x, y, z, 0f);
                ok = bridge.ObjectExists(Id(L, 1));
            }
        }
        catch { }
        LuaNative.lua_pushboolean(L, ok ? 1 : 0);
        return 1;
    }

    // MoveToPointWithinRange(obj, x, y, z, range, [face]) @0x00a04ad0 — blocking move: aim the goal at
    // the point (stopping `range` short) and yield until arrival. Retail installs a live arrival
    // predicate; we resume on a time estimate (as WaitForNearGoal/MoveTowardObject). Yields; no return
    // on the resume path (returns has-locomotion false only when it cannot start).
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int MoveToPointWithinRange(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            var scheduler = ctx?.Scheduler;
            var bridge = ctx?.GameBridge;
            if (scheduler is null || bridge is null
                || !Num(L, 1, out var idf) || !Num(L, 2, out var x) || !Num(L, 3, out var y)
                || !Num(L, 4, out var z) || !Num(L, 5, out var range))
            { LuaNative.lua_pushboolean(L, 0); return 1; }

            var id = (uint)Math.Round(idf);
            bridge.SetLocomotionGoal(id, x, y, z, range);
            var remaining = bridge.TryGetGoalDistance(id, out var dist) ? Math.Max(0f, dist - range) : 0f;
            var speed = Math.Max(0.01f, bridge.GetModifiedMoveSpeed(id));
            scheduler.RegisterYield(L, sleeping: false, wakeAt: scheduler.Now + Math.Min(remaining / speed, 10.0));
        }
        catch
        {
            LuaNative.lua_pushboolean(L, 0);
            return 1;
        }
        return LuaNative.lua_yield(L, 0);
    }

    // JumpInDirection(obj, dir{x,y,z}, ...) @0x00a04750 — a ballistic jump driven by the physics
    // component (obj+0xb0, 12 args of arc/speed tuning). ReCap has no server-side physics, so this is a
    // faithful no-op that reports has-locomotion; real jump arcs are deferred with the physics model.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int JumpInDirection(nint L)
    {
        var ok = false;
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            ok = bridge is not null && Num(L, 1, out var id) && bridge.ObjectExists((uint)Math.Round(id));
        }
        catch { }
        LuaNative.lua_pushboolean(L, ok ? 1 : 0);
        return 1;
    }

    // TurnToFaceTargetObject(obj, targetObj, [immediate]) @0x009fb130 — orient obj toward another object.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int TurnToFaceTargetObject(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            if (bridge is not null && Num(L, 1, out var id) && Num(L, 2, out var tid)
                && bridge.TryGetPosition(Id(L, 1), out var ox, out var oy, out var oz)
                && bridge.TryGetPosition(Id(L, 2), out var tx, out var ty, out var tz))
                bridge.SetFacing(Id(L, 1), tx - ox, ty - oy, tz - oz);
        }
        catch { }
        return 0;
    }

    private static bool Num(nint L, int i, out float v)
    {
        if (LuaNative.lua_type(L, i) == LuaNative.LUA_TNUMBER) { v = (float)LuaNative.lua_tonumber(L, i); return true; }
        v = 0f; return false;
    }

    // Ghidra nLocomotion::SlideToPoint@0x00a046b0: (objId,x,y,z,speed) -> sets positional goal + speed.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SlideToPoint(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            if (bridge is not null && Num(L, 1, out var id) && Num(L, 2, out var x) && Num(L, 3, out var y) && Num(L, 4, out var z))
                bridge.SetLocomotionGoal((uint)Math.Round(id), x, y, z, 0f);
        }
        catch { }
        return 0;
    }

    // Ghidra nLocomotion::MoveToCircleEdge@0x00a049c0: (objId,x,y,z,radius,[faceGoal]) -> goal with
    // stop-distance=radius; returns 1 bool (had-locomotion). faceGoal flag deferred (0x800 face-on-arrival).
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int MoveToCircleEdge(nint L)
    {
        var ok = false;
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            if (bridge is not null && Num(L, 1, out var id) && Num(L, 2, out var x) && Num(L, 3, out var y) && Num(L, 4, out var z) && Num(L, 5, out var radius))
            {
                bridge.SetLocomotionGoal((uint)Math.Round(id), x, y, z, radius);
                ok = true;
            }
        }
        catch { }
        LuaNative.lua_pushboolean(L, ok ? 1 : 0);
        return 1;
    }

    // Ghidra nLocomotion::TurnToFace@0x00a05300: (objId,x,y,z,[immediate]) -> SetFacing path.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int TurnToFace(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            if (bridge is not null && Num(L, 1, out var id) && Num(L, 2, out var x) && Num(L, 3, out var y) && Num(L, 4, out var z))
                bridge.SetFacing((uint)Math.Round(id), x, y, z);
        }
        catch { }
        return 0;
    }

    // Ghidra Locomotion::Stop@0x00a1a150: (objId) -> GoalFlags=0x020.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Stop(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            if (bridge is not null && Num(L, 1, out var id))
                bridge.StopLocomotion((uint)Math.Round(id));
        }
        catch { }
        return 0;
    }
}
