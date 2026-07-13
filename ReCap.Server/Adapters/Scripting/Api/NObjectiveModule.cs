using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

// nObjective (Ghidra registrar @0x00a0c3d0, 21 methods). RegisterObjective is in RegistrarModule; this
// module implements the rest: the per-(target,index) objective data store (Set/Get Int/Float/GUID,
// +FromSPID target-resolving variants) and the objective-event system (Create/Send/Destroy + typed
// slots), which reuses the ability-event builder — objective handlers read event data BY HANDLE.
public static unsafe class NObjectiveModule
{
    public static void Register(nint L) =>
        LuaApiModule.RegisterNamespace(L, "nObjective",
            ("SetObjectiveIntData", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&SetObjectiveIntData),
            ("SetObjectiveIntDataFromSPID", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&SetObjectiveIntData),
            ("GetObjectiveIntData", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetObjectiveIntData),
            ("SetObjectiveFloatData", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&SetObjectiveFloatData),
            ("SetObjectiveFloatDataFromSPID", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&SetObjectiveFloatData),
            ("GetObjectiveFloatData", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetObjectiveFloatData),
            ("SetObjectiveGUIDData", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&SetObjectiveGuidData),
            ("SetObjectiveGUIDDataFromSPID", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&SetObjectiveGuidData),
            ("GetObjectiveGUIDData", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetObjectiveGuidData),
            ("CreateObjectiveEvent", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&CreateObjectiveEvent),
            ("SendObjectiveEvent", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&SendObjectiveEvent),
            ("DestroyObjectiveEvent", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&DestroyObjectiveEvent),
            ("SetObjectiveEventIntData", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&SetObjectiveEventIntData),
            ("GetObjectiveEventIntData", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetObjectiveEventIntData),
            ("SetObjectiveEventFloatData", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&SetObjectiveEventFloatData),
            ("GetObjectiveEventFloatData", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetObjectiveEventFloatData),
            ("SetObjectiveEventGUIDData", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&SetObjectiveEventGuidData),
            ("GetObjectiveEventGUIDData", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetObjectiveEventGuidData),
            ("CallFunctionInContext", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&NAbilityContextModule.CallFunctionInContext),
            ("GetRegisteredDestructibles", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetRegisteredDestructibles));

    // --- data store (target byte, index, value [, flag1, flag2]) ---
    // FromSPID variants resolve the target from an object/SPID; server-side we key on the target arg
    // directly (byte), so they share the same setter. The two trailing bool flags drive HUD replication
    // (deferred), so the stored value is what matters here.
    private static byte Target(nint L) => (byte)((uint)Math.Round((double)LuaNative.lua_tonumber(L, 1)) & 0xff);
    private static int Index(nint L) => (int)Math.Round((double)LuaNative.lua_tonumber(L, 2));

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SetObjectiveIntData(nint L)
    {
        try
        {
            ScriptContextRegistry.Get(L)?.GameBridge?.SetObjectiveData(Target(L), Index(L),
                (int)Math.Round((double)LuaNative.lua_tonumber(L, 3)), 0f, 0u, ObjectiveDataKind.Int);
        }
        catch { }
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SetObjectiveFloatData(nint L)
    {
        try
        {
            ScriptContextRegistry.Get(L)?.GameBridge?.SetObjectiveData(Target(L), Index(L),
                0, (float)LuaNative.lua_tonumber(L, 3), 0u, ObjectiveDataKind.Float);
        }
        catch { }
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SetObjectiveGuidData(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            ctx?.GameBridge?.SetObjectiveData(Target(L), Index(L), 0, 0f,
                ctx.ResolveAssetHash(LuaNative.lua_tonumber(L, 3)), ObjectiveDataKind.Guid);
        }
        catch { }
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetObjectiveIntData(nint L)
    {
        try { LuaNative.lua_pushnumber(L, ScriptContextRegistry.Get(L)?.GameBridge?.GetObjectiveInt(Target(L), Index(L)) ?? 0); }
        catch { LuaNative.lua_pushnumber(L, 0f); }
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetObjectiveFloatData(nint L)
    {
        try { LuaNative.lua_pushnumber(L, ScriptContextRegistry.Get(L)?.GameBridge?.GetObjectiveFloat(Target(L), Index(L)) ?? 0f); }
        catch { LuaNative.lua_pushnumber(L, 0f); }
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetObjectiveGuidData(nint L)
    {
        try { LuaNative.lua_pushnumber(L, ScriptContextRegistry.Get(L)?.GameBridge?.GetObjectiveGuid(Target(L), Index(L)) ?? 0u); }
        catch { LuaNative.lua_pushnumber(L, 0f); }
        return 1;
    }

    // --- objective events (reuse the ability-event builder; handlers read data by handle) ---
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int CreateObjectiveEvent(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            var type = LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER ? (int)Math.Round((double)LuaNative.lua_tonumber(L, 1)) : 0;
            LuaNative.lua_pushnumber(L, ctx?.CreateAbilityEvent(type) ?? 0u);
            return 1;
        }
        catch { LuaNative.lua_pushnumber(L, 0f); return 1; }
    }

    // SendObjectiveEvent(eventHandle) — dispatch to every registered objective whose handledEvents match.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SendObjectiveEvent(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            if (ctx?.GameBridge is { } bridge && LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER)
            {
                var handle = (uint)Math.Round((double)LuaNative.lua_tonumber(L, 1));
                if (ctx.GetAbilityEvent(handle) is { } ev) bridge.DispatchObjectiveEvent(ev.Type, handle);
            }
        }
        catch { }
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int DestroyObjectiveEvent(nint L)
    {
        try
        {
            if (LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER)
                ScriptContextRegistry.Get(L)?.RemoveAbilityEvent((uint)Math.Round((double)LuaNative.lua_tonumber(L, 1)));
        }
        catch { }
        return 0;
    }

    private static int EventSlot(nint L) =>
        LuaNative.lua_type(L, 2) == LuaNative.LUA_TNUMBER ? (int)Math.Round((double)LuaNative.lua_tonumber(L, 2)) : 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SetObjectiveEventIntData(nint L)
    {
        try
        {
            var b = ScriptContextRegistry.Get(L)?.GetAbilityEvent((uint)Math.Round((double)LuaNative.lua_tonumber(L, 1)));
            if (b is not null) b.Ints[EventSlot(L)] = (int)Math.Round((double)LuaNative.lua_tonumber(L, 3));
        }
        catch { }
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SetObjectiveEventFloatData(nint L)
    {
        try
        {
            var b = ScriptContextRegistry.Get(L)?.GetAbilityEvent((uint)Math.Round((double)LuaNative.lua_tonumber(L, 1)));
            if (b is not null) b.Floats[EventSlot(L)] = (float)LuaNative.lua_tonumber(L, 3);
        }
        catch { }
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SetObjectiveEventGuidData(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            var b = ctx?.GetAbilityEvent((uint)Math.Round((double)LuaNative.lua_tonumber(L, 1)));
            if (b is not null) b.Guids[EventSlot(L)] = ctx!.ResolveAssetHash(LuaNative.lua_tonumber(L, 3));
        }
        catch { }
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetObjectiveEventIntData(nint L)
    {
        try
        {
            var b = ScriptContextRegistry.Get(L)?.GetAbilityEvent((uint)Math.Round((double)LuaNative.lua_tonumber(L, 1)));
            LuaNative.lua_pushnumber(L, b?.Ints.GetValueOrDefault(EventSlot(L)) ?? 0);
        }
        catch { LuaNative.lua_pushnumber(L, 0f); }
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetObjectiveEventFloatData(nint L)
    {
        try
        {
            var b = ScriptContextRegistry.Get(L)?.GetAbilityEvent((uint)Math.Round((double)LuaNative.lua_tonumber(L, 1)));
            LuaNative.lua_pushnumber(L, b?.Floats.GetValueOrDefault(EventSlot(L)) ?? 0f);
        }
        catch { LuaNative.lua_pushnumber(L, 0f); }
        return 1;
    }

    // GetObjectiveEventGUIDData(handle, index) @0x00a017b0 — read the event's guid slot by handle.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetObjectiveEventGuidData(nint L)
    {
        try
        {
            var b = ScriptContextRegistry.Get(L)?.GetAbilityEvent((uint)Math.Round((double)LuaNative.lua_tonumber(L, 1)));
            LuaNative.lua_pushnumber(L, b?.Guids.GetValueOrDefault(EventSlot(L)) ?? 0u);
        }
        catch { LuaNative.lua_pushnumber(L, 0f); }
        return 1;
    }

    // GetRegisteredDestructibles() @0x00a01890 — destructible-ornament tracking not modelled -> 0.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetRegisteredDestructibles(nint L)
    {
        LuaNative.lua_pushnumber(L, ScriptContextRegistry.Get(L)?.GameBridge?.GetRegisteredDestructibles() ?? 0u);
        return 1;
    }
}
