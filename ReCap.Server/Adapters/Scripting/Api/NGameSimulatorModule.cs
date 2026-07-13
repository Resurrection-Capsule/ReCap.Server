using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

public static unsafe class NGameSimulatorModule
{
    public static void Register(nint L) =>
        LuaApiModule.RegisterNamespace(L, "nGameSimulator",
            ("GetGameTime", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetGameTime),
            ("IsChainGame", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&IsChainGame));

    // IsChainGame() @0x00a008c0 → bool: whether this is a campaign/chain game.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int IsChainGame(nint L)
    {
        LuaNative.lua_pushboolean(L, (ScriptContextRegistry.Get(L)?.GameBridge?.IsChainGame() ?? false) ? 1 : 0);
        return 1;
    }

    // GetGameTime() → 1 number: the simulation clock in seconds (scheduler tick clock; same base
    // as nThread.WaitForXSeconds so script duration comparisons stay consistent).
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetGameTime(nint L)
    {
        var now = ScriptContextRegistry.Get(L)?.Scheduler?.Now ?? 0.0;
        LuaNative.lua_pushnumber(L, (float)now);
        return 1;
    }
}
