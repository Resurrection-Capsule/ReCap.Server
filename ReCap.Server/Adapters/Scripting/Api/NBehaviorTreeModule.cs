using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

public static unsafe class NBehaviorTreeModule
{
    public static void Register(nint L)
    {
        LuaApiModule.RegisterNamespace(L, "nBehaviorTree",
            ("GetMyObjectID", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetMyObjectID),
            ("GetTargetObjectID", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetTargetObjectID));
    }

    // Retail contracts (Ghidra): GetMyObjectID @0x009fad20 / GetTargetObjectID @0x009fad60 read
    // Simulation::GetThreadContext self/target — identical to nAbility.GetAgentID/GetTargetID.

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetMyObjectID(nint L)
    {
        try
        {
            var inv = ScriptContextRegistry.Get(L)?.GetInvocation(L);
            LuaNative.lua_pushnumber(L, inv.HasValue ? (float)inv.Value.AgentId : 0f);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetTargetObjectID(nint L)
    {
        try
        {
            var inv = ScriptContextRegistry.Get(L)?.GetInvocation(L);
            LuaNative.lua_pushnumber(L, inv.HasValue ? (float)inv.Value.TargetId : 0f);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }
}
