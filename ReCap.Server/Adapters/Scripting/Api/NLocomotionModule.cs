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
            ("TurnToFace", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&TurnToFace));

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
