using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

// nClient (Ghidra registrar @0x00a01bd0, 3 methods) — the retail client itself registers all three
// DrawReticle_* as LuaStub no-ops (targeting-reticle overlay is drawn elsewhere), so the faithful
// server-side implementation is likewise a no-op.
public static unsafe class NClientModule
{
    public static void Register(nint L) =>
        LuaApiModule.RegisterNamespace(L, "nClient",
            ("DrawReticle_Circle", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&NoOp),
            ("DrawReticle_PointBlankCircle", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&NoOp),
            ("DrawReticle_Cone", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&NoOp));

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int NoOp(nint L) => 0;
}
