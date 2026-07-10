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
            ("PreloadAsset", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&PreloadAsset),
            ("RequestModifier", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&RequestModifier),
            ("GetFirstModifierByGUID", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetFirstModifierByGUID),
            ("AgentHasModifierMatchingGUID", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&AgentHasModifierMatchingGUID),
            ("GetStackCount", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetStackCount),
            ("IncrementStackCount", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&IncrementStackCount),
            ("ResetDuration", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&ResetDuration),
            ("MarkForDelete", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&MarkForDelete),
            ("GetRank", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetRank));

    // nModifier.RequestModifier(targetId, casterId, modifierGuid, [param4=0], [rank]) — retail
    // nAbility::RequestModifier @0x00a408d0 creates the server-side modifier instance and returns its
    // handle (the 0xA2/0xA3/0xA4 wire messages replicate it). We allocate the instance + broadcast
    // 0xA2; the modifier's own Lua tick (DoT/attribute) execution is a later increment. modifierGuid
    // is a lossy-float32-boxed hash — resolve it exactly via the context handle table.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int RequestModifier(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            if (ctx?.GameBridge is not { } bridge || LuaNative.lua_type(L, 1) != LuaNative.LUA_TNUMBER)
            { LuaNative.lua_pushnumber(L, 0f); return 1; }

            var target = (uint)Math.Round((double)LuaNative.lua_tonumber(L, 1));
            var caster = LuaNative.lua_type(L, 2) == LuaNative.LUA_TNUMBER
                ? (uint)Math.Round((double)LuaNative.lua_tonumber(L, 2)) : 0u;
            var guid = ctx.ResolveAssetHash(LuaNative.lua_tonumber(L, 3));
            var rank = LuaNative.lua_type(L, 5) == LuaNative.LUA_TNUMBER
                ? (int)LuaNative.lua_tonumber(L, 5)
                : ctx.GetInvocation(L)?.Rank ?? 0;

            LuaNative.lua_pushnumber(L, bridge.CreateModifier(target, caster, guid, rank));
            return 1;
        }
        catch { LuaNative.lua_pushnumber(L, 0f); return 1; }
    }

    // nModifier.GetFirstModifierByGUID(targetId, modifierGuid) -> instance handle, 0 (kObjIDNone) if absent.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetFirstModifierByGUID(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            if (ctx?.GameBridge is not { } bridge || LuaNative.lua_type(L, 1) != LuaNative.LUA_TNUMBER)
            { LuaNative.lua_pushnumber(L, 0f); return 1; }
            var target = (uint)Math.Round((double)LuaNative.lua_tonumber(L, 1));
            var guid = ctx.ResolveAssetHash(LuaNative.lua_tonumber(L, 2));
            LuaNative.lua_pushnumber(L, bridge.FindModifierByGuid(target, guid));
            return 1;
        }
        catch { LuaNative.lua_pushnumber(L, 0f); return 1; }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int AgentHasModifierMatchingGUID(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            if (ctx?.GameBridge is not { } bridge || LuaNative.lua_type(L, 1) != LuaNative.LUA_TNUMBER)
            { LuaNative.lua_pushboolean(L, 0); return 1; }
            var target = (uint)Math.Round((double)LuaNative.lua_tonumber(L, 1));
            var guid = ctx.ResolveAssetHash(LuaNative.lua_tonumber(L, 2));
            LuaNative.lua_pushboolean(L, bridge.FindModifierByGuid(target, guid) != 0 ? 1 : 0);
            return 1;
        }
        catch { LuaNative.lua_pushboolean(L, 0); return 1; }
    }

    // nModifier.GetStackCount([instanceId]) — with a handle, the stack of that instance; without one,
    // the running modifier's own stack (context slot, == GetMyStackCount).
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetStackCount(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            if (LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER && ctx?.GameBridge is { } bridge)
            {
                var id = (uint)Math.Round((double)LuaNative.lua_tonumber(L, 1));
                LuaNative.lua_pushnumber(L, (float)bridge.GetModifierStackCount(id));
                return 1;
            }
            var inv = ctx?.GetInvocation(L);
            LuaNative.lua_pushnumber(L, inv.HasValue ? (float)inv.Value.StackCount : 0f);
            return 1;
        }
        catch { LuaNative.lua_pushnumber(L, 0f); return 1; }
    }

    // nModifier.IncrementStackCount([instanceId]) — with a handle, that instance; without one, the
    // running modifier's own instance (context slot — the [4] StackModifier handler calls it no-arg).
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int IncrementStackCount(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            if (ctx?.GameBridge is not { } bridge) { LuaNative.lua_pushnumber(L, 0f); return 1; }
            var id = LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER
                ? (uint)Math.Round((double)LuaNative.lua_tonumber(L, 1))
                : ctx.GetInvocation(L)?.InstanceId ?? 0;
            LuaNative.lua_pushnumber(L, id != 0 ? (float)bridge.IncrementModifierStack(id) : 0f);
            return 1;
        }
        catch { LuaNative.lua_pushnumber(L, 0f); return 1; }
    }

    // nModifier.ResetDuration([instanceId]) — restart the modifier's duration (0xA3); no-arg = the
    // running modifier's own instance (the [4] StackModifier handler calls it no-arg after stacking).
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int ResetDuration(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            if (ctx?.GameBridge is not { } bridge) return 0;
            var id = LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER
                ? (uint)Math.Round((double)LuaNative.lua_tonumber(L, 1))
                : ctx.GetInvocation(L)?.InstanceId ?? 0;
            if (id != 0) bridge.ResetModifierDuration(id);
        }
        catch { }
        return 0;
    }

    // nModifier.MarkForDelete(instanceId) — tear down the instance and replicate 0xA4 ModifierDeleted.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int MarkForDelete(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            if (ctx?.GameBridge is { } bridge && LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER)
                bridge.RemoveModifier((uint)Math.Round((double)LuaNative.lua_tonumber(L, 1)));
        }
        catch { }
        return 0;
    }

    // nModifier.GetRank() — the running modifier's cast rank (context slot, == nAbility.GetRank).
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetRank(nint L)
    {
        try { var inv = ScriptContextRegistry.Get(L)?.GetInvocation(L); LuaNative.lua_pushnumber(L, inv.HasValue ? (float)inv.Value.Rank : 0f); return 1; }
        catch { LuaNative.lua_pushnumber(L, 0f); return 1; }
    }

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
            if (string.IsNullOrEmpty(name)) LuaNative.lua_pushnumber(L, 0f);
            else LuaApiModule.PushHash(L, ScriptVfs.Hash(name));
            return 1;
        }
        catch { LuaNative.lua_pushnumber(L, 0f); return 1; }
    }
}
