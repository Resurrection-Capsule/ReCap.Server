using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

public static unsafe class NAbilityContextModule
{
    public static void Register(nint L)
    {
        LuaApiModule.RegisterNamespace(L, "nAbility",
            ("GetAgentID", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetAgentID),
            ("GetTargetID", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetTargetID),
            ("GetTargetPosition", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetTargetPosition),
            ("GetRank", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetRank),
            ("GetAbilityInstanceID", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetAbilityInstanceID),
            ("GetAgentAttributeSnapshot", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetAgentAttributeSnapshot),
            ("PayCooldownAndMana", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&PayCooldownAndMana),
            ("TargetInRangeAtStart", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&TargetInRangeAtStart),
            ("GetAnimationSequenceIndex", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetAnimationSequenceIndex),
            ("PlayAnimationSequence", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&PlayAnimationSequence));
    }

    // Retail contracts (Ghidra 2026-06-06, VERIFIED_FACTS C3 addendum):
    // GetAbilityInstanceID @0x00a403e0    — 0 args, 1 number (per-cast instance id, 0 ok).
    // GetAgentAttributeSnapshot @0x00a417a0 — 0-1 args (optional agent id), 1 number = opaque
    //   snapshot HANDLE consumed by nAttribute.GetAttributeValue_FromSnapshot.
    // PayCooldownAndMana @0x00a43470      — 0 args, 0 RETURNS (no success bool; mana clamps to 0).
    // TargetInRangeAtStart @0x00a410a0    — 0-1 args, 1 boolean (flag cached at cast start).
    // GetAnimationSequenceIndex @0x00a41ce0 — 0 args, 1 number (sequence index, 0 default).
    // PlayAnimationSequence @0x00a41d20   — 0 args, 0 RETURNS (anim broadcast = Simulation phase).

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetAbilityInstanceID(nint L)
    {
        try
        {
            var inv = ScriptContextRegistry.Get(L)?.GetInvocation(L);
            LuaNative.lua_pushnumber(L, inv.HasValue ? (float)inv.Value.InstanceId : 0f);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetAgentAttributeSnapshot(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            if (ctx?.GameBridge is null)
            {
                LuaNative.lua_pushnumber(L, 0f);
                return 1;
            }

            var agentId = ReadOptionalObjectIdArg(L) ?? ctx.GetInvocation(L)?.AgentId ?? 0;
            var attributes = agentId != 0 ? ctx.GameBridge.GetAttributeTable(agentId) : null;
            if (attributes is null)
            {
                LuaNative.lua_pushnumber(L, 0f);
                return 1;
            }

            var handle = ctx.StoreAttributeSnapshot(new Dictionary<int, float>(attributes));
            LuaNative.lua_pushnumber(L, handle);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int PayCooldownAndMana(nint L)
    {
        // Retail: stamps cooldown start + deducts mana clamped to 0, no return value.
        // Server mana/cooldown state lands with the combat phase; arity contract holds now.
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int TargetInRangeAtStart(nint L)
    {
        try
        {
            var inv = ScriptContextRegistry.Get(L)?.GetInvocation(L);
            LuaNative.lua_pushboolean(L, inv is { TargetInRangeAtStart: true } ? 1 : 0);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushboolean(L, 0);
            return 1;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetAnimationSequenceIndex(nint L)
    {
        LuaNative.lua_pushnumber(L, 0f);
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int PlayAnimationSequence(nint L)
    {
        return 0;
    }

    private static uint? ReadOptionalObjectIdArg(nint L)
    {
        if (LuaNative.lua_gettop(L) < 1 || LuaNative.lua_type(L, 1) != 3) return null;
        var id = (uint)Math.Round((double)LuaNative.lua_tonumber(L, 1));
        return id == 0 ? null : id;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetAgentID(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            var inv = ctx?.GetInvocation(L);
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
    private static int GetTargetID(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            var inv = ctx?.GetInvocation(L);
            LuaNative.lua_pushnumber(L, inv.HasValue ? (float)inv.Value.TargetId : 0f);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetTargetPosition(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            var inv = ctx?.GetInvocation(L);
            if (inv.HasValue)
            {
                LuaNative.lua_pushnumber(L, inv.Value.CursorX);
                LuaNative.lua_pushnumber(L, inv.Value.CursorY);
                LuaNative.lua_pushnumber(L, inv.Value.CursorZ);
            }
            else
            {
                LuaNative.lua_pushnumber(L, 0f);
                LuaNative.lua_pushnumber(L, 0f);
                LuaNative.lua_pushnumber(L, 0f);
            }
            return 3;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            LuaNative.lua_pushnumber(L, 0f);
            LuaNative.lua_pushnumber(L, 0f);
            return 3;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetRank(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            var inv = ctx?.GetInvocation(L);
            LuaNative.lua_pushnumber(L, inv.HasValue ? (float)inv.Value.Rank : 0f);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }
}
