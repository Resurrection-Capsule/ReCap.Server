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
            ("TurnToFaceTargetObject", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&TurnToFaceTargetObject));

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
