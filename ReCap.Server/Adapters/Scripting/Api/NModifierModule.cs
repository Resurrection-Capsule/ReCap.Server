using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

// nModifier context getters read the same per-thread invocation slot as nAbility — retail
// ModifierInvocation == AbilityInvocation (GetMyStackCount decompiles as nAbility::GetMyStackCount
// @0x00a41600; RegisterModifier == RegisterAbility @0x00a43040). PreloadAsset == nAbility.PreloadAsset.
public static unsafe class NModifierModule
{
    public static void Register(nint L) =>
        LuaApiModule.RegisterNamespace(L, "nModifier",
            ("GetMyAgentID", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetMyAgentID),
            ("GetMyInitiatorID", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetMyInitiatorID),
            ("GetMyStackCount", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetMyStackCount),
            ("GetInitiatorAttributeSnapshot", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetInitiatorAttributeSnapshot),
            ("PreloadAsset", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&PreloadAsset));

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetMyAgentID(nint L)
    {
        try { var inv = ScriptContextRegistry.Get(L)?.GetInvocation(L); LuaNative.lua_pushnumber(L, inv.HasValue ? (float)inv.Value.AgentId : 0f); return 1; }
        catch { LuaNative.lua_pushnumber(L, 0f); return 1; }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetMyInitiatorID(nint L)
    {
        try { var inv = ScriptContextRegistry.Get(L)?.GetInvocation(L); LuaNative.lua_pushnumber(L, inv.HasValue ? (float)inv.Value.InitiatorId : 0f); return 1; }
        catch { LuaNative.lua_pushnumber(L, 0f); return 1; }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetMyStackCount(nint L)
    {
        try { var inv = ScriptContextRegistry.Get(L)?.GetInvocation(L); LuaNative.lua_pushnumber(L, inv.HasValue ? (float)inv.Value.StackCount : 0f); return 1; }
        catch { LuaNative.lua_pushnumber(L, 0f); return 1; }
    }

    // Mirrors nAbility.GetAgentAttributeSnapshot but freezes the INITIATOR's attributes.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetInitiatorAttributeSnapshot(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            var initiatorId = ctx?.GetInvocation(L)?.InitiatorId ?? 0;
            var attributes = ctx?.GameBridge is { } b && initiatorId != 0 ? b.GetAttributeTable(initiatorId) : null;
            if (ctx is null || attributes is null) { LuaNative.lua_pushnumber(L, 0f); return 1; }
            LuaNative.lua_pushnumber(L, ctx.StoreAttributeSnapshot(new Dictionary<int, float>(attributes)));
            return 1;
        }
        catch { LuaNative.lua_pushnumber(L, 0f); return 1; }
    }

    // nModifier.PreloadAsset(name, [ownerTag]) -> FNV hash handle (== nAbility.PreloadAsset, C3).
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int PreloadAsset(nint L)
    {
        try
        {
            var name = LuaNative.lua_type(L, 1) == LuaNative.LUA_TSTRING ? LuaNative.ToManagedString(L, 1) : null;
            var hash = string.IsNullOrEmpty(name) ? 0u : ScriptVfs.Hash(name);
            LuaNative.lua_pushnumber(L, hash);
            return 1;
        }
        catch { LuaNative.lua_pushnumber(L, 0f); return 1; }
    }
}
