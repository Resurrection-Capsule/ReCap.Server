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
            ("PlayAnimationSequence", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&PlayAnimationSequence),
            ("GetAbilityEventType", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetAbilityEventType),
            ("GetAbilityEventGUIDData", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetAbilityEventGUIDData),
            ("GetAbilityEventFloatData", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetAbilityEventFloatData),
            ("GetAbilityEventIntData", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetAbilityEventIntData),
            ("CheckDescriptors_AnyMatch", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&CheckDescriptorsAnyMatch),
            ("ResetAbilityCooldown", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&ResetAbilityCooldown),
            ("RemoveCooldownTime", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&RemoveCooldownTime),
            ("ScaleCooldownTime", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&ScaleCooldownTime),
            ("AddCooldownTime", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&AddCooldownTime),
            ("RequestAbility", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&RequestAbility),
            ("CallFunctionInContext", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&CallFunctionInContext),
            ("ReleaseAgent", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&ReleaseAgent));
    }

    private static uint ReadObjectIdArg(nint L, int idx)
        => (uint)Math.Round((double)LuaNative.lua_tonumber(L, idx));

    // Cooldown-cluster natives (Ghidra 2026-07-13: Reset@0x00a41b00, Remove@0x00a42e10,
    // Scale@0x00a42f20, Add@0x00a42b30). Retail keys cooldowns per (ability, initiator, rank) on the
    // object's cooldown component (obj+0x29c); we model per (agent, abilityHash) on the context's
    // deadline map, so the initiator/rank args are accepted-and-ignored. Every mutation rebroadcasts
    // 0xC1 (relative: start=0 → client re-stamps end = now + duration), duration 0 = ready now.

    // ResetAbilityCooldown(agent, abilityId, initiatorId, rank) — clear that one ability → ready.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int ResetAbilityCooldown(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            if (ctx is null || LuaNative.lua_gettop(L) < 2) return 0;
            var agent = ReadObjectIdArg(L, 1);
            var ability = ctx.ResolveAssetHash(LuaNative.lua_tonumber(L, 2));
            ctx.ClearCooldown(agent, ability);
            ctx.GameBridge?.SendCooldownUpdate(agent, ability, 0f);
        }
        catch { }
        return 0;
    }

    // RemoveCooldownTime(agent, [abilityId]) — 2 args clears one, 1 arg clears ALL cooldowns on agent.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int RemoveCooldownTime(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            if (ctx is null || LuaNative.lua_gettop(L) < 1) return 0;
            var agent = ReadObjectIdArg(L, 1);
            if (LuaNative.lua_gettop(L) == 2)
            {
                var ability = ctx.ResolveAssetHash(LuaNative.lua_tonumber(L, 2));
                ctx.ClearCooldown(agent, ability);
                ctx.GameBridge?.SendCooldownUpdate(agent, ability, 0f);
            }
            else
            {
                foreach (var ability in ctx.CooldownAbilities(agent))
                {
                    ctx.ClearCooldown(agent, ability);
                    ctx.GameBridge?.SendCooldownUpdate(agent, ability, 0f);
                }
            }
        }
        catch { }
        return 0;
    }

    // ScaleCooldownTime(agent, scale, [abilityId]) — scale remaining by `scale`; 3 args one, 2 args ALL.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int ScaleCooldownTime(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            if (ctx is null || LuaNative.lua_gettop(L) < 2) return 0;
            var agent = ReadObjectIdArg(L, 1);
            var scale = (float)LuaNative.lua_tonumber(L, 2);
            var now = ctx.Scheduler?.Now ?? 0d;
            var targets = LuaNative.lua_gettop(L) >= 3
                ? [ctx.ResolveAssetHash(LuaNative.lua_tonumber(L, 3))]
                : ctx.CooldownAbilities(agent);
            foreach (var ability in targets)
            {
                var remaining = ctx.CooldownRemaining(agent, ability, now);
                if (remaining <= 0d) continue;
                var scaled = remaining * scale;
                ctx.StampCooldown(agent, ability, now + scaled);
                ctx.GameBridge?.SendCooldownUpdate(agent, ability, (float)scaled);
            }
        }
        catch { }
        return 0;
    }

    // AddCooldownTime(agent, abilityId, seconds) — extend remaining cooldown by `seconds`.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int AddCooldownTime(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            if (ctx is null || LuaNative.lua_gettop(L) < 3) return 0;
            var agent = ReadObjectIdArg(L, 1);
            var ability = ctx.ResolveAssetHash(LuaNative.lua_tonumber(L, 2));
            var seconds = (float)LuaNative.lua_tonumber(L, 3);
            var now = ctx.Scheduler?.Now ?? 0d;
            var extended = ctx.CooldownRemaining(agent, ability, now) + seconds;
            ctx.StampCooldown(agent, ability, now + extended);
            ctx.GameBridge?.SendCooldownUpdate(agent, ability, (float)extended);
        }
        catch { }
        return 0;
    }

    private static int EventSlotIndex(nint L) =>
        LuaNative.lua_type(L, 2) == LuaNative.LUA_TNUMBER ? (int)Math.Round((double)LuaNative.lua_tonumber(L, 2)) : 0;

    // A modifier's [4] event handler reads the event type (nAbilityEventFlags) via
    // GetAbilityEventType(eventHandle). We stash the type in the invocation's EventType slot when firing
    // the handler; the opaque handle arg is ignored. Only StackModifier (32) is wired so far.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetAbilityEventType(nint L)
    {
        try
        {
            var inv = ScriptContextRegistry.Get(L)?.GetInvocation(L);
            LuaNative.lua_pushnumber(L, inv.HasValue ? (float)inv.Value.EventType : 0f);
            return 1;
        }
        catch { LuaNative.lua_pushnumber(L, 0f); return 1; }
    }

    // GetAbilityEventGUIDData/FloatData/IntData(eventHandle, index) — typed payload slots of the current
    // [4] event (TookDamage: GUID[1]=attacker, Float[2]=amount, Int[3]=descriptors). 0 when absent.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetAbilityEventGUIDData(nint L)
    {
        try
        {
            var evt = ScriptContextRegistry.Get(L)?.GetModifierEvent(L);
            var value = evt?.Guids.GetValueOrDefault(EventSlotIndex(L)) ?? 0u;
            LuaNative.lua_pushnumber(L, value); // object ids are small — exact as a Lua float
            return 1;
        }
        catch { LuaNative.lua_pushnumber(L, 0f); return 1; }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetAbilityEventFloatData(nint L)
    {
        try
        {
            var evt = ScriptContextRegistry.Get(L)?.GetModifierEvent(L);
            LuaNative.lua_pushnumber(L, evt?.Floats.GetValueOrDefault(EventSlotIndex(L)) ?? 0f);
            return 1;
        }
        catch { LuaNative.lua_pushnumber(L, 0f); return 1; }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetAbilityEventIntData(nint L)
    {
        try
        {
            var evt = ScriptContextRegistry.Get(L)?.GetModifierEvent(L);
            LuaNative.lua_pushnumber(L, evt?.Ints.GetValueOrDefault(EventSlotIndex(L)) ?? 0);
            return 1;
        }
        catch { LuaNative.lua_pushnumber(L, 0f); return 1; }
    }

    // CheckDescriptors_AnyMatch(flags, descriptors) -> bool: whether any bit of `flags` is set in the
    // event's descriptors bitmask (e.g. IsMelee). Pure bitwise AND != 0.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int CheckDescriptorsAnyMatch(nint L)
    {
        try
        {
            var flags = LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER ? (uint)(long)LuaNative.lua_tonumber(L, 1) : 0u;
            var descriptors = LuaNative.lua_type(L, 2) == LuaNative.LUA_TNUMBER ? (uint)(long)LuaNative.lua_tonumber(L, 2) : 0u;
            LuaNative.lua_pushboolean(L, (flags & descriptors) != 0 ? 1 : 0);
            return 1;
        }
        catch { LuaNative.lua_pushboolean(L, 0); return 1; }
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
        // Retail @0x00a43470 reads the invoking ability's cooldown from its def (abilityDef+0x60,
        // populated from the ability's `cooldown` prop) and stamps it. We stamp a per-(agent,ability)
        // deadline that InvokeAbility honours, so the ability can't re-fire until it cools down — the
        // gate that stops the AI enemy (and a spamming player) from attacking every tick. Mana state
        // is not modelled yet; the cooldown is the load-bearing half. 0 returns (retail arity).
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            if (ctx?.GetInvocation(L) is { AbilityHash: not 0 } inv)
            {
                var cooldown = ctx.Registry.Find(ScriptKind.Ability, inv.AbilityHash)?.CooldownForRank(inv.Rank) ?? 0f;
                if (cooldown > 0f)
                {
                    ctx.StampCooldown(inv.AgentId, inv.AbilityHash, (ctx.Scheduler?.Now ?? 0d) + cooldown);
                    ctx.GameBridge?.SendCooldownUpdate(inv.AgentId, inv.AbilityHash, cooldown);
                }
            }
        }
        catch { /* cooldown is best-effort; never fail the cast */ }
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
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            LuaNative.lua_pushnumber(L, ctx?.GetAnimationSequenceIndex(L) ?? 0);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }

    // Reads the invoking ability's animationSequence table (entries: {hit, release,
    // animationstate=PreloadAnimation hash}), rotates the Sequence index per (agent, ability)
    // and broadcasts the chosen animationstate (0xA5). GetAnimationSequenceIndex returns the
    // same index within the cast so timing reads (GetShotTiming → [idx+1].hit) line up.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int PlayAnimationSequence(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            var inv = ctx?.GetInvocation(L);
            if (ctx?.GameBridge is null || inv is null || inv.Value.AbilityHash == 0) return 0;

            var entry = ctx.Registry.Find(ScriptKind.Ability, inv.Value.AbilityHash);
            if (entry is null) return 0;

            var top = LuaNative.lua_gettop(L);
            LuaNative.lua_rawgeti(L, LuaNative.LUA_REGISTRYINDEX, entry.TableRef);
            LuaNative.lua_getfield(L, -1, "animationSequence");
            if (LuaNative.lua_type(L, -1) != 5)
            {
                LuaNative.lua_settop(L, top);
                return 0;
            }

            var count = (int)LuaNative.lua_objlen(L, -1);
            if (count <= 0)
            {
                LuaNative.lua_settop(L, top);
                return 0;
            }

            var index = ctx.NextAnimationSequenceIndex(L, inv.Value.AgentId, inv.Value.AbilityHash, count);
            LuaNative.lua_rawgeti(L, -1, index + 1);
            LuaNative.lua_getfield(L, -1, "animationstate");
            var state = LuaNative.lua_type(L, -1) == 3
                ? (uint)Math.Round((double)LuaNative.lua_tonumber(L, -1))
                : 0u;
            LuaNative.lua_settop(L, top);

            if (state != 0)
                ctx.GameBridge.BroadcastAnimationState(inv.Value.AgentId, state);
            return 0;
        }
        catch
        {
            return 0;
        }
    }

    // RequestAbility(abilityGuid, agent, target, x, y, z, [rank=1], [flag], [instanceId]) — run an
    // ability programmatically (Ghidra @0x00a42bf0 → FUN_009e62a0). Retail defaults rank to 1 when the
    // 7th arg is absent; the extra flag/instanceId args are cast-instance bookkeeping we don't model.
    // The request is deferred to the next tick (bridge queue) to avoid re-entering the runtime.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int RequestAbility(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            if (ctx?.GameBridge is null || LuaNative.lua_gettop(L) < 6) return 0;
            var ability = ctx.ResolveAssetHash(LuaNative.lua_tonumber(L, 1));
            var agent = ReadObjectIdArg(L, 2);
            var target = ReadObjectIdArg(L, 3);
            var x = (float)LuaNative.lua_tonumber(L, 4);
            var y = (float)LuaNative.lua_tonumber(L, 5);
            var z = (float)LuaNative.lua_tonumber(L, 6);
            var rank = LuaNative.lua_gettop(L) >= 7 ? (int)Math.Round((double)LuaNative.lua_tonumber(L, 7)) : 1;
            ctx.GameBridge.RequestAbility(ability, agent, target, x, y, z, rank);
        }
        catch { }
        return 0;
    }

    // CallFunctionInContext(instanceHandle, fn, ...args) — run `fn` bound to the instance's context
    // (its invocation + shared private table) so in-fn getters (GetMyAgentID/GetPrivateTable) resolve
    // to that instance; returns fn's results. Ghidra @0x00a41c20 (shared nAbility/nModifier): resolves
    // the instance, fetches its thread (instance+0x2c) and calls fn in that thread's context. We run it
    // on the CALLER's thread with the instance's context swapped in for the call, then restored — the
    // called methods (SetStrafed/PromoteAlly/UpdateLaserZone) are synchronous, so a protected call is
    // sufficient (a yield across the C boundary would error and be swallowed, matching best-effort).
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    internal static int CallFunctionInContext(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            if (ctx?.GameBridge is null) return 0;
            if (LuaNative.lua_gettop(L) < 2 || LuaNative.lua_type(L, 2) != LuaNative.LUA_TFUNCTION) return 0;

            var handle = (uint)Math.Round((double)LuaNative.lua_tonumber(L, 1));
            if (!ctx.GameBridge.TryGetInstanceContext(handle, out var invocation, out var privateRef))
                return 0;

            var hadInv = ctx.GetInvocation(L);
            var hadShared = ctx.TryGetSharedPrivateTable(L, out var oldShared);
            ctx.SetInvocation(L, invocation);
            if (privateRef != 0) ctx.BindSharedPrivateTable(L, privateRef);

            // Stack: [handle, fn, arg3..argN] — pcall consumes fn+args, pushes results above the handle.
            var nargs = LuaNative.lua_gettop(L) - 2;
            var status = LuaNative.lua_pcall(L, nargs, LuaNative.LUA_MULTRET, 0);

            if (hadInv.HasValue) ctx.SetInvocation(L, hadInv.Value); else ctx.RemoveInvocation(L);
            if (hadShared) ctx.BindSharedPrivateTable(L, oldShared); else ctx.UnbindSharedPrivateTable(L);

            if (status != 0)
            {
                try { Util.Logging.Log.Lua.Warn($"CallFunctionInContext error: {LuaNative.ToManagedString(L, -1)}"); } catch { }
                LuaNative.lua_settop(L, 1); // drop the error, keep only the handle
                return 0;
            }
            return LuaNative.lua_gettop(L) - 1; // result count (values sit above the handle)
        }
        catch { return 0; }
    }

    // Recognized no-op (state mutation impl-time-DEFERRED, needs nAbility::ReleaseAgent decompile).
    // Registering it removes it from stub telemetry so the harvest ratchet drops.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int ReleaseAgent(nint L) => 0;

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
