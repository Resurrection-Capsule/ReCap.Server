using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

public static unsafe class NTimeManagerModule
{
    // Game.Update drives ScriptContext.Tick at a fixed 50ms cadence (Game loop contract).
    public const float SimTimeDeltaSeconds = 0.05f;

    public static void Register(nint L) =>
        LuaApiModule.RegisterNamespace(L, "nTimeManager",
            ("GetSimTimeDt", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetSimTimeDt),
            ("IsSimTimePaused", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&IsSimTimePaused));

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetSimTimeDt(nint L)
    {
        LuaNative.lua_pushnumber(L, SimTimeDeltaSeconds);
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int IsSimTimePaused(nint L)
    {
        LuaNative.lua_pushboolean(L, 0);
        return 1;
    }
}
