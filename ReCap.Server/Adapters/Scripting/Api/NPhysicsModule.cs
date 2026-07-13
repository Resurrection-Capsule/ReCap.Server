using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

// nPhysics (Ghidra registrar @0x00a04420, 8 methods). ReCap has no server-side physics engine, so the
// collision/impulse calls are server-side flags or faithful no-ops. Line-of-sight is always clear (no
// occluders modelled) and terrain queries return 0 (no terrain heightfield). SetObjectAsCollidable maps
// onto the existing nav-collision flag; ForceClientUpdate nudges a locomotion re-replicate.
public static unsafe class NPhysicsModule
{
    public static void Register(nint L) =>
        LuaApiModule.RegisterNamespace(L, "nPhysics",
            ("AddPhysicsForObject", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&NoOp),
            ("RemovePhysicsForObject", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&NoOp),
            ("ApplyImpulse", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&NoOp),
            ("IsDynamic", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&IsDynamic),
            ("SetObjectAsCollidable", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&SetObjectAsCollidable),
            ("ForceClientUpdate", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&ForceClientUpdate),
            ("IsInLineOfSight", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&IsInLineOfSight),
            ("DistanceToTerrain", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&DistanceToTerrain));

    private static uint Oid(nint L, int i) => (uint)Math.Round((double)LuaNative.lua_tonumber(L, i));

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int NoOp(nint L) => 0;

    // IsDynamic(obj) — we have no dynamic rigid bodies; report static (false).
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int IsDynamic(nint L) { LuaNative.lua_pushboolean(L, 0); return 1; }

    // SetObjectAsCollidable(obj, collidable) @0x009faca0 — toggle nav collision (existing flag).
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SetObjectAsCollidable(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            if (bridge is not null && LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER)
                bridge.SetNavCollision(Oid(L, 1), LuaNative.lua_toboolean(L, 2) != 0);
        }
        catch { }
        return 0;
    }

    // ForceClientUpdate(obj) — flag a fresh locomotion replicate.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int ForceClientUpdate(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            if (bridge is not null && LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER) bridge.ForceClientUpdate(Oid(L, 1));
        }
        catch { }
        return 0;
    }

    // IsInLineOfSight(obj, target|x,y,z) @0x00a04210 — no occluders modelled -> always clear.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int IsInLineOfSight(nint L) { LuaNative.lua_pushboolean(L, 1); return 1; }

    // DistanceToTerrain(px,py,pz, dx,dy,dz, maxDist) @0x00a04330 — no terrain heightfield -> 0.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int DistanceToTerrain(nint L) { LuaNative.lua_pushnumber(L, 0f); return 1; }
}
