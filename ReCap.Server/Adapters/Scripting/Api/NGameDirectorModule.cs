using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

// nGameDirector — the AI director. Only GetKillPercent is demanded by the ported scripts (objectives
// read it for kill-based progress/status). Spawn-direction control is not modelled.
public static unsafe class NGameDirectorModule
{
    public static void Register(nint L) =>
        LuaApiModule.RegisterNamespace(L, "nGameDirector",
            ("GetKillPercent", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetKillPercent));

    // GetKillPercent() @0x00a00400 -> float 0..1: spawned combatant enemies defeated / total.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetKillPercent(nint L)
    {
        LuaNative.lua_pushnumber(L, ScriptContextRegistry.Get(L)?.GameBridge?.GetKillPercent() ?? 0f);
        return 1;
    }
}
