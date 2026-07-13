using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

public static unsafe class NGameObjectModule
{
    public static void Register(nint L)
    {
        LuaApiModule.RegisterNamespace(L, "nGameObject",
            ("GetPosition", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetPosition),
            ("GetHitPoints", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetHitPoints),
            ("GetMaxHitPoints", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetMaxHitPoints),
            ("IsAlive", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&IsAlive),
            ("GetTeam", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetTeam),
            ("GetTargetID", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetTargetID),
            ("GetWeaponDamage", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetWeaponDamage),
            ("GetObjectDirection", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetObjectDirection),
            ("GetCenterPoint", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetCenterPoint),
            ("GetOrientation", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetOrientation),
            ("GetFacing", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetFacing),
            ("GetFootprintRadius", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetFootprintRadius),
            ("ValidateHostileTarget", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&ValidateHostileTarget),
            ("ValidateFriendlyTarget", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&ValidateFriendlyTarget),
            ("SetAnimationState", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&SetAnimationState),
            ("HealDamage", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&HealDamage),
            ("TakeDamage", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&TakeDamage),
            ("MarkForDelete", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&MarkForDelete),
            ("SetIsVisible", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&SetIsVisible),
            ("SetStealthType", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&SetStealthType),
            ("SetTeam", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&SetTeam),
            ("IsModifierActive", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&IsModifierActive),
            ("ResetAnimationState", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&ResetAnimationState),
            ("SetAttributeSnapshot", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&SetAttributeSnapshot),
            ("GetAttributeSnapshot", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetAttributeSnapshot),
            ("SetTargetPosition", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&SetTargetPosition),
            ("SetNavCollision", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&SetNavCollision),
            ("GetModifiedMoveSpeed", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetModifiedMoveSpeed),
            ("AddEffect", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&AddEffect),
            ("GetObjectDistance", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetObjectDistance),
            ("GetOwnerID", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetOwnerID),
            ("SetOwnerID", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&SetOwnerID),
            ("SetTargetID", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&SetTargetID),
            ("SetOrientation", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&SetOrientation),
            ("AddAggroForObject", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&AddAggroForObject),
            ("AlertObject", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&AlertObject),
            ("GetNPCType", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetNPCType));
    }

    private static uint Oid(nint L, int i) => (uint)Math.Round((double)LuaNative.lua_tonumber(L, i));

    // Tier-2 nGameObject cluster (Ghidra 2026-07-13).

    // GetObjectDistance(a, b) @0x00a060c0 — edge-to-edge distance (center minus both radii, floored 0).
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetObjectDistance(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            if (bridge is not null && LuaNative.lua_gettop(L) >= 2
                && bridge.TryGetObjectDistance(Oid(L, 1), Oid(L, 2), out var dist))
            {
                LuaNative.lua_pushnumber(L, dist);
                return 1;
            }
        }
        catch { }
        return 0;
    }

    // GetOwnerID(obj) @0x009fd910 — owner object id (obj+0x50); 0 if none.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetOwnerID(nint L)
    {
        try { LuaNative.lua_pushnumber(L, ScriptContextRegistry.Get(L)?.GameBridge?.GetOwnerId(Oid(L, 1)) ?? 0u); }
        catch { LuaNative.lua_pushnumber(L, 0f); }
        return 1;
    }

    // SetOwnerID(obj, ownerId) @0x009fd950.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SetOwnerID(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            if (bridge is not null && LuaNative.lua_gettop(L) >= 2) bridge.SetOwnerId(Oid(L, 1), Oid(L, 2));
        }
        catch { }
        return 0;
    }

    // SetTargetID(obj, targetId) @0x009fce30 — set the object's combat target (kObjIDNone = 0 clears).
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SetTargetID(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            if (bridge is not null && LuaNative.lua_gettop(L) >= 2) bridge.SetTargetId(Oid(L, 1), Oid(L, 2));
        }
        catch { }
        return 0;
    }

    // SetOrientation(obj, ...) @0x00a081c0. Corpus uses two forms: 5 args = full quaternion
    // (x,y,z,w, e.g. from GetOrientation), and 2 args = yaw angle (radians) about world-up — only ever
    // called with 0 (reset). The 4-arg direction form is unused in the corpus and left unhandled.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SetOrientation(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            if (bridge is null) return 0;
            var id = Oid(L, 1);
            var top = LuaNative.lua_gettop(L);
            if (top >= 5)
                bridge.SetOrientation(id,
                    (float)LuaNative.lua_tonumber(L, 2), (float)LuaNative.lua_tonumber(L, 3),
                    (float)LuaNative.lua_tonumber(L, 4), (float)LuaNative.lua_tonumber(L, 5));
            else if (top == 2)
            {
                var half = (float)LuaNative.lua_tonumber(L, 2) * 0.5f; // yaw about world-up Y
                bridge.SetOrientation(id, 0f, MathF.Sin(half), 0f, MathF.Cos(half));
            }
        }
        catch { }
        return 0;
    }

    // AddAggroForObject(agent, target, amount, [reason]) @0x009fd690 — raise the agent's threat toward
    // target. No-op if the object is not an AI agent.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int AddAggroForObject(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            if (bridge is not null && LuaNative.lua_gettop(L) >= 3)
                bridge.AddAggroForObject(Oid(L, 1), Oid(L, 2), (float)LuaNative.lua_tonumber(L, 3));
        }
        catch { }
        return 0;
    }

    // AlertObject(agent, target) @0x009fd750 — make the agent notice the target (aggro with no extra
    // threat). No-op if the object is not an AI agent.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int AlertObject(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            if (bridge is not null && LuaNative.lua_gettop(L) >= 2)
                bridge.AddAggroForObject(Oid(L, 1), Oid(L, 2), 0f);
        }
        catch { }
        return 0;
    }

    // GetNPCType(obj) @0x009fdf20 — the object's nNPCType (from the noun, cached at spawn); -1 if none.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetNPCType(nint L)
    {
        try { LuaNative.lua_pushnumber(L, ScriptContextRegistry.Get(L)?.GameBridge?.GetNpcType(Oid(L, 1)) ?? -1); }
        catch { LuaNative.lua_pushnumber(L, -1f); }
        return 1;
    }

    // Retail contracts (Ghidra 2026-06-06): HealDamage @0x009fd020 — args (sourceId, targetId,
    // amount, amount2, [flags], [applyMods], [forceDead]) → 2 returns (actualHeal float,
    // isCrit bool); clamps to MaxHP, revives from 0, emits damage/sim events (server events =
    // Simulation phase). MarkForDelete @0x009fd370 (obj+0x5d=1, swept next tick, 0 ret).
    // SetIsVisible @0x009fd660 (obj+0x5f, 0 ret, no net msg). SetStealthType @0x009fddb0
    // (combatComponent+0x556, 0 ret).

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int HealDamage(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            if (bridge is null || LuaNative.lua_type(L, 2) != LuaNative.LUA_TNUMBER)
            {
                LuaNative.lua_pushnumber(L, 0f);
                LuaNative.lua_pushboolean(L, 0);
                return 2;
            }
            var sourceId = LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER
                ? (uint)Math.Round((double)LuaNative.lua_tonumber(L, 1)) : 0u;
            var targetId = (uint)Math.Round((double)LuaNative.lua_tonumber(L, 2));
            var amount = LuaNative.lua_type(L, 3) == LuaNative.LUA_TNUMBER ? LuaNative.lua_tonumber(L, 3) : 0f;
            var applied = bridge.ApplyHeal(targetId, amount);

            // Retail HealDamage @0x009fd020 emits the combat event (0xBA) that drives the client's
            // floating number + hit reaction. The client parser (ClientNet::OnGmsCombatEvent →
            // FUN_004e6190) gates the display block on deltaHealth > 0, so deltaHealth carries the
            // POSITIVE MAGNITUDE of the change (matches C++ TakeDamage/Heal: mDeltaHealth = |amount|,
            // mIntegerHpChange = (int)mDeltaHealth). A signed delta lands in the wrong branch and shows
            // nothing. applied is the signed HP delta here; take its magnitude for the wire.
            var magnitude = MathF.Abs(applied);
            if (magnitude > 0f)
            {
                try { bridge.BroadcastCombatEvent(targetId, sourceId, magnitude, (int)MathF.Round(magnitude), 0); }
                catch { /* feedback packet is best-effort; never fail the cast */ }
            }

            LuaNative.lua_pushnumber(L, applied);
            LuaNative.lua_pushboolean(L, 0);
            return 2;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            LuaNative.lua_pushboolean(L, 0);
            return 2;
        }
    }

    // Caller contract (melee tick disasm, CALL 36 12 4 = 11 args / 3 returns):
    // TakeDamage(snapshotHandle, targetId, damageTable{min,max}, damageType, damageSource,
    // coefficient, descriptors, damageMultiplier, dirX, dirY, dirZ) → (true, damageDealt, isCrit).
    // Engine (C++ Object.cpp:1377): baseDamage = Random(min,max); crit from snapshot; subtract HP.
    // arg3 MUST be a table — a number raises a Lua error. Coefficient/multiplier/damageType/
    // damageSource/descriptors/defense are accepted but unscaled in the reference (spec L-1/L-2).
    // Missing target → 0 Lua return values (nil gate).
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int TakeDamage(nint L)
    {
        bool argError = false;
        uint targetId = 0;
        float min = 0f, max = 0f;
        IReadOnlyDictionary<int, float>? snapshot = null;
        IScriptGameBridge? bridge = null;
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            bridge = ctx?.GameBridge;
            if (bridge is null || LuaNative.lua_type(L, 2) != LuaNative.LUA_TNUMBER)
                return 0;
            targetId = (uint)System.Math.Round((double)LuaNative.lua_tonumber(L, 2));
            if (!bridge.ObjectExists(targetId))
                return 0;
            if (LuaNative.lua_type(L, 3) != LuaNative.LUA_TTABLE)
            {
                argError = true;
            }
            else
            {
                LuaNative.lua_rawgeti(L, 3, 1);
                min = (float)LuaNative.lua_tonumber(L, -1);
                LuaNative.lua_settop(L, -2);
                LuaNative.lua_rawgeti(L, 3, 2);
                max = (float)LuaNative.lua_tonumber(L, -1);
                LuaNative.lua_settop(L, -2);
                if (LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER)
                {
                    var handle = (uint)System.Math.Round((double)LuaNative.lua_tonumber(L, 1));
                    if (handle != 0) ctx!.TryGetAttributeSnapshot(handle, out snapshot);
                }
            }
        }
        catch
        {
            return 0;
        }

        if (argError)
        {
            LuaNative.lua_pushstring(L, "Expected a table for damage range in TakeDamage!");
            return LuaNative.lua_error(L);
        }

        var damage = RollDamage(min, max);
        var isCrit = ApplyCrit(snapshot, ref damage);
        float dealt = 0f;
        try { dealt = damage > 0f ? -bridge!.ApplyHeal(targetId, -damage) : 0f; }
        catch { dealt = 0f; }

        // Floating damage number + combat log (0xBA). Client gates the display on deltaHealth > 0, so
        // it carries the POSITIVE magnitude (C++ mDeltaHealth = |damage|, mIntegerHpChange = (int)it);
        // flags left 0 (the crit bit is not yet Ghidra-confirmed — isCrit is already returned to Lua).
        if (dealt > 0f)
        {
            try { bridge!.BroadcastCombatEvent(targetId, 0, dealt, (int)MathF.Round(dealt), 0); }
            catch { /* feedback packet is best-effort; never fail the cast */ }

            // TookDamage event: fire the target's subscribed modifier [4] handlers (thorns/on-hit procs).
            // Attacker = the caster running this cast; descriptors = arg 7 (IsMelee etc.).
            try
            {
                var attacker = ScriptContextRegistry.Get(L)?.GetInvocation(L)?.AgentId ?? 0;
                var descriptors = LuaNative.lua_type(L, 7) == LuaNative.LUA_TNUMBER ? (int)(long)LuaNative.lua_tonumber(L, 7) : 0;
                bridge!.DispatchTookDamage(targetId, attacker, dealt, descriptors);
                if (attacker != 0) bridge!.DispatchDealtDamage(attacker, targetId);
            }
            catch { /* event dispatch is best-effort; never fail the cast */ }
        }

        LuaNative.lua_pushboolean(L, 1);
        LuaNative.lua_pushnumber(L, dealt);
        LuaNative.lua_pushboolean(L, isCrit ? 1 : 0);
        return 3;
    }

    // Crit attrs (C++ Object::CheckCritical, Attributes.h): AutoCrit=19, CriticalRating=10,
    // CriticalDamageIncrease=22. Reference TakeDamage applies NO coefficient/multiplier/defense
    // scaling (spec divergence L-1/L-2) — damage is the rolled range modified only by crit.
    private static float RollDamage(float min, float max)
    {
        if (max <= min) return min;
        return min + System.Random.Shared.NextSingle() * (max - min);
    }

    private static bool ApplyCrit(IReadOnlyDictionary<int, float>? snapshot, ref float damage)
    {
        if (snapshot is null) return false;
        bool crit;
        if (snapshot.TryGetValue(19, out var autoCrit) && autoCrit > 0f)
        {
            crit = true;
        }
        else
        {
            snapshot.TryGetValue(10, out var criticalRating);
            crit = System.Random.Shared.NextSingle() < criticalRating / 100f;
        }
        if (crit)
        {
            snapshot.TryGetValue(22, out var critIncrease);
            damage *= critIncrease + 1f;
        }
        return crit;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int MarkForDelete(nint L)
    {
        try
        {
            if (LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER)
                ScriptContextRegistry.Get(L)?.GameBridge?.MarkForDelete(ReadId(L));
            return 0;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SetIsVisible(nint L)
    {
        try
        {
            if (LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER)
                ScriptContextRegistry.Get(L)?.GameBridge?.SetVisible(ReadId(L), LuaNative.lua_toboolean(L, 2) != 0);
            return 0;
        }
        catch
        {
            return 0;
        }
    }

    // kAttribute ids 101/102 (source enum, part-attribute idx 0x65/0x66 — VERIFIED_FACTS).
    private const int MinWeaponDamageAttr = 101;
    private const int MaxWeaponDamageAttr = 102;

    // GetWeaponDamage(agentId) → {[1]=min,[2]=max} table (disasm template_ability_melee GetDamage
    // 2026-06-07: result is consumed as result[1]/result[2]). Reads the object's weapon-damage
    // attributes; absent → 0.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetWeaponDamage(nint L)
    {
        float min = 0f, max = 0f;
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            if (bridge is not null && LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER)
            {
                var id = ReadId(L);
                bridge.TryGetAttributeValue(id, MinWeaponDamageAttr, out min);
                bridge.TryGetAttributeValue(id, MaxWeaponDamageAttr, out max);
            }
        }
        catch
        {
            min = max = 0f;
        }
        LuaNative.lua_createtable(L, 2, 0);
        LuaNative.lua_pushnumber(L, min);
        LuaNative.lua_rawseti(L, -2, 1);
        LuaNative.lua_pushnumber(L, max);
        LuaNative.lua_rawseti(L, -2, 2);
        return 1;
    }

    // SetTeam(objectId, team) → 0 returns; server-side team reassignment (charm/conversion).
    // Targeting validation (ValidateHostileTarget) reads team server-side; client visual team
    // reflection lands with the object-stream phase.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SetTeam(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            if (bridge is not null
                && LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER
                && LuaNative.lua_type(L, 2) == LuaNative.LUA_TNUMBER)
            {
                var id = (uint)System.Math.Round((double)LuaNative.lua_tonumber(L, 1));
                var team = (byte)System.Math.Round((double)LuaNative.lua_tonumber(L, 2));
                bridge.SetTeam(id, team);
            }
            return 0;
        }
        catch
        {
            return 0;
        }
    }

    // IsModifierActive(objectId, modifierId) → 1 bool. v1: modifier tracking not built → false.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int IsModifierActive(nint L)
    {
        LuaNative.lua_pushboolean(L, 0);
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SetStealthType(nint L)
    {
        // Stealth component state lands with the Simulation phase; arity contract (0 returns) holds.
        return 0;
    }

    // GetObjectDirection(fromId, toId) → 3 floats: normalized direction from→to (melee tick
    // disasm CALL 36 3 4 = 2 args/3 returns; feeds hitEffect "facing"). Missing object → (0,0,0).
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetObjectDirection(nint L)
    {
        float dx = 0f, dy = 0f, dz = 0f;
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            if (bridge is not null
                && LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER
                && LuaNative.lua_type(L, 2) == LuaNative.LUA_TNUMBER)
            {
                var from = (uint)System.Math.Round((double)LuaNative.lua_tonumber(L, 1));
                var to = (uint)System.Math.Round((double)LuaNative.lua_tonumber(L, 2));
                if (bridge.TryGetPosition(from, out var fx, out var fy, out var fz)
                    && bridge.TryGetPosition(to, out var tx, out var ty, out var tz))
                {
                    dx = tx - fx; dy = ty - fy; dz = tz - fz;
                    var len = System.MathF.Sqrt(dx * dx + dy * dy + dz * dz);
                    if (len > 1e-6f) { dx /= len; dy /= len; dz /= len; }
                    else { dx = 0f; dy = 0f; dz = 0f; }
                }
            }
        }
        catch
        {
            dx = 0f; dy = 0f; dz = 0f;
        }
        LuaNative.lua_pushnumber(L, dx);
        LuaNative.lua_pushnumber(L, dy);
        LuaNative.lua_pushnumber(L, dz);
        return 3;
    }

    // Retail contracts (Ghidra 2026-06-06): GetCenterPoint @0x009fb7f0 (3 floats, lua error on
    // missing); GetOrientation @0x00a02bb0 (4 floats XYZW); GetFacing @0x009fb9a0 (3 floats,
    // forward derived from orientation); GetFootprintRadius @0x009fbb20 (1 float, 0 on null);
    // ValidateHostileTarget @0x009fd3c0 / ValidateFriendlyTarget @0x009fd460 (2-3 args, 1 bool);
    // SetAnimationState @0x009fc000 (2 args, state id string→FNV or number, 0 returns,
    // NETWORK broadcast — server sends 0xA5 to all clients).

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetCenterPoint(nint L)
    {
        float x = 0, y = 0, z = 0;
        bool found = false;
        try
        {
            if (LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER)
            {
                var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
                found = bridge is not null && bridge.TryGetPosition(ReadId(L), out x, out y, out z);
            }
        }
        catch
        {
            found = false;
        }
        if (!found)
        {
            LuaNative.lua_pushstring(L, "Could not find object!");
            return LuaNative.lua_error(L);
        }
        LuaNative.lua_pushnumber(L, x);
        LuaNative.lua_pushnumber(L, y);
        LuaNative.lua_pushnumber(L, z);
        return 3;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetOrientation(nint L)
    {
        float x = 0, y = 0, z = 0, w = 1;
        bool found = false;
        try
        {
            if (LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER)
            {
                var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
                found = bridge is not null && bridge.TryGetOrientation(ReadId(L), out x, out y, out z, out w);
            }
        }
        catch
        {
            found = false;
        }
        if (!found)
        {
            LuaNative.lua_pushstring(L, "Could not find object!");
            return LuaNative.lua_error(L);
        }
        LuaNative.lua_pushnumber(L, x);
        LuaNative.lua_pushnumber(L, y);
        LuaNative.lua_pushnumber(L, z);
        LuaNative.lua_pushnumber(L, w);
        return 4;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetFacing(nint L)
    {
        float x = 0, y = 0, z = 0, w = 1;
        bool found = false;
        try
        {
            if (LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER)
            {
                var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
                found = bridge is not null && bridge.TryGetOrientation(ReadId(L), out x, out y, out z, out w);
            }
        }
        catch
        {
            found = false;
        }
        if (!found)
        {
            LuaNative.lua_pushstring(L, "Could not find object!");
            return LuaNative.lua_error(L);
        }
        // Forward = orientation applied to the +Y unit axis (Spore-engine convention, Z-up).
        // UNCONFIRMED axis choice — only affects FX direction until verified on the wire.
        var forward = System.Numerics.Vector3.Transform(
            new System.Numerics.Vector3(0f, 1f, 0f),
            new System.Numerics.Quaternion(x, y, z, w));
        Util.Logging.Log.Lua.Debug(
            $"[arc] GetFacing quat=({x:F2},{y:F2},{z:F2},{w:F2}) → fwd=({forward.X:F2},{forward.Y:F2},{forward.Z:F2})");
        LuaNative.lua_pushnumber(L, forward.X);
        LuaNative.lua_pushnumber(L, forward.Y);
        LuaNative.lua_pushnumber(L, forward.Z);
        return 3;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetFootprintRadius(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            var exists = LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER
                && bridge is not null && bridge.ObjectExists(ReadId(L));
            // Noun physics footprint not parsed yet; 0.5 = generic creature radius placeholder.
            LuaNative.lua_pushnumber(L, exists ? 0.5f : 0f);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int ValidateHostileTarget(nint L) => ValidateTarget(L, hostile: true);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int ValidateFriendlyTarget(nint L) => ValidateTarget(L, hostile: false);

    // Caller contract (template_ability_melee tick disasm): arg1 is the source TEAM number
    // (scripts pass nGameObject.GetTeam(agent)), arg2 the target object id — NOT two object ids.
    private static int ValidateTarget(nint L, bool hostile)
    {
        try
        {
            if (LuaNative.lua_type(L, 1) != LuaNative.LUA_TNUMBER || LuaNative.lua_type(L, 2) != LuaNative.LUA_TNUMBER)
            {
                LuaNative.lua_pushboolean(L, 0);
                return 1;
            }
            var sourceTeam = (byte)Math.Round((double)LuaNative.lua_tonumber(L, 1));
            var targetId = (uint)Math.Round((double)LuaNative.lua_tonumber(L, 2));
            var allowDead = LuaNative.lua_gettop(L) >= 3 && LuaNative.lua_toboolean(L, 3) != 0;
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;

            var valid = bridge is not null
                && bridge.ObjectExists(targetId)
                && (allowDead || bridge.GetHitPoints(targetId) > 0f)
                && (hostile
                    ? sourceTeam != bridge.GetTeam(targetId)
                    : sourceTeam == bridge.GetTeam(targetId));
            LuaNative.lua_pushboolean(L, valid ? 1 : 0);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushboolean(L, 0);
            return 1;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SetAnimationState(nint L)
    {
        try
        {
            if (LuaNative.lua_type(L, 1) != LuaNative.LUA_TNUMBER) return 0;
            var objectId = ReadId(L);
            uint state = LuaNative.lua_type(L, 2) switch
            {
                3 => (uint)Math.Round((double)LuaNative.lua_tonumber(L, 2)),
                4 => AssetData.Parser.WireHash.Fnv1a(LuaNative.ToManagedString(L, 2) ?? string.Empty),
                _ => 0u,
            };
            if (state != 0)
                ScriptContextRegistry.Get(L)?.GameBridge?.BroadcastAnimationState(objectId, state);
            return 0;
        }
        catch
        {
            return 0;
        }
    }

    // ResetAnimationState(objId) = undo death-anim (catalog §Mechanical): broadcast state 0.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int ResetAnimationState(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            if (bridge is not null && LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER)
                bridge.ResetAnimationState((uint)Math.Round((double)LuaNative.lua_tonumber(L, 1)));
        }
        catch { }
        return 0;
    }

    // nGameObject.GetAttributeSnapshot(obj) -> snapshot handle: the object's stored cast-time snapshot
    // if one was set (e.g. a projectile that carries its caster's), else a fresh snapshot of the
    // object's live attributes. Used as SetAttributeSnapshot(newObj, GetAttributeSnapshot(srcObj)).
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetAttributeSnapshot(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            if (ctx is null || LuaNative.lua_type(L, 1) != LuaNative.LUA_TNUMBER)
            { LuaNative.lua_pushnumber(L, 0f); return 1; }
            var objId = (uint)Math.Round((double)LuaNative.lua_tonumber(L, 1));
            if (ctx.TryGetObjectSnapshot(objId, out var existing))
            { LuaNative.lua_pushnumber(L, existing); return 1; }
            var table = ctx.GameBridge?.GetAttributeTable(objId);
            var handle = table is null ? 0u : ctx.StoreAttributeSnapshot(new Dictionary<int, float>(table));
            LuaNative.lua_pushnumber(L, handle);
            return 1;
        }
        catch { LuaNative.lua_pushnumber(L, 0f); return 1; }
    }

    // Projectile carries the caster's cast-time snapshot handle (catalog §Mechanical).
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SetAttributeSnapshot(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            if (ctx is not null
                && LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER
                && LuaNative.lua_type(L, 2) == LuaNative.LUA_TNUMBER)
            {
                var objId = (uint)Math.Round((double)LuaNative.lua_tonumber(L, 1));
                var handle = (uint)Math.Round((double)LuaNative.lua_tonumber(L, 2));
                ctx.SetObjectSnapshot(objId, handle);
            }
        }
        catch { }
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SetTargetPosition(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            if (bridge is not null
                && LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER
                && LuaNative.lua_type(L, 2) == LuaNative.LUA_TNUMBER
                && LuaNative.lua_type(L, 3) == LuaNative.LUA_TNUMBER
                && LuaNative.lua_type(L, 4) == LuaNative.LUA_TNUMBER)
            {
                bridge.SetLocomotionTarget(
                    (uint)Math.Round((double)LuaNative.lua_tonumber(L, 1)),
                    (float)LuaNative.lua_tonumber(L, 2), (float)LuaNative.lua_tonumber(L, 3), (float)LuaNative.lua_tonumber(L, 4));
            }
        }
        catch { }
        return 0;
    }

    // Ghidra nGameObject::SetNavCollision@0x009fe7c0: server-side pathfinding flag (inverted), no wire.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SetNavCollision(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            if (bridge is not null && LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER)
            {
                var collidable = LuaNative.lua_toboolean(L, 2) != 0;
                bridge.SetNavCollision((uint)Math.Round((double)LuaNative.lua_tonumber(L, 1)), collidable);
            }
        }
        catch { }
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetModifiedMoveSpeed(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            var speed = bridge is not null && LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER
                ? bridge.GetModifiedMoveSpeed((uint)Math.Round((double)LuaNative.lua_tonumber(L, 1)))
                : 0f;
            LuaNative.lua_pushnumber(L, speed);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }

    // nGameObject.AddEffect(objId, serverEventDefHandle, [initiatorId]) -> effect-instance handle;
    // emits 0x9B attached FX (catalog §Modifier/FX). Effect handle is for a future RemoveEffect (deferred).
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int AddEffect(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            if (bridge is null || LuaNative.lua_type(L, 1) != LuaNative.LUA_TNUMBER || LuaNative.lua_type(L, 2) != LuaNative.LUA_TNUMBER)
            {
                LuaNative.lua_pushnumber(L, 0f);
                return 1;
            }
            var objId = (uint)Math.Round((double)LuaNative.lua_tonumber(L, 1));
            var effect = (uint)Math.Round((double)LuaNative.lua_tonumber(L, 2));
            var initiator = LuaNative.lua_type(L, 3) == LuaNative.LUA_TNUMBER ? (uint)Math.Round((double)LuaNative.lua_tonumber(L, 3)) : 0u;
            LuaNative.lua_pushnumber(L, bridge.EmitEffect(objId, effect, initiator));
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }

    private static uint ReadId(nint L) =>
        (uint)Math.Round((double)LuaNative.lua_tonumber(L, 1));

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetPosition(nint L)
    {
        float x = 0, y = 0, z = 0;
        bool found = false;
        try
        {
            if (LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER)
            {
                var id = ReadId(L);
                var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
                found = bridge is not null && bridge.TryGetPosition(id, out x, out y, out z);
            }
        }
        catch
        {
            found = false;
        }
        if (!found)
        {
            LuaNative.lua_pushstring(L, "Could not find object!");
            return LuaNative.lua_error(L);
        }
        LuaNative.lua_pushnumber(L, x);
        LuaNative.lua_pushnumber(L, y);
        LuaNative.lua_pushnumber(L, z);
        return 3;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetHitPoints(nint L)
    {
        try
        {
            if (LuaNative.lua_type(L, 1) != LuaNative.LUA_TNUMBER) { LuaNative.lua_pushnumber(L, 0f); return 1; }
            var id = ReadId(L);
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            LuaNative.lua_pushnumber(L, bridge?.GetHitPoints(id) ?? 0f);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetMaxHitPoints(nint L)
    {
        try
        {
            if (LuaNative.lua_type(L, 1) != LuaNative.LUA_TNUMBER) { LuaNative.lua_pushnumber(L, 0f); return 1; }
            var id = ReadId(L);
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            LuaNative.lua_pushnumber(L, bridge?.GetMaxHitPoints(id) ?? 0f);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int IsAlive(nint L)
    {
        try
        {
            if (LuaNative.lua_type(L, 1) != LuaNative.LUA_TNUMBER) { LuaNative.lua_pushboolean(L, 0); return 1; }
            var id = ReadId(L);
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            var hp = bridge?.GetHitPoints(id) ?? 0f;
            LuaNative.lua_pushboolean(L, hp > 0f ? 1 : 0);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushboolean(L, 0);
            return 1;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetTeam(nint L)
    {
        try
        {
            if (LuaNative.lua_type(L, 1) != LuaNative.LUA_TNUMBER) return 0;
            var id = ReadId(L);
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            if (bridge is null || !bridge.ObjectExists(id)) return 0;
            LuaNative.lua_pushnumber(L, (float)bridge.GetTeam(id));
            return 1;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetTargetID(nint L)
    {
        try
        {
            if (LuaNative.lua_type(L, 1) != LuaNative.LUA_TNUMBER) { LuaNative.lua_pushnumber(L, 0f); return 1; }
            var id = ReadId(L);
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            LuaNative.lua_pushnumber(L, bridge is not null ? (float)bridge.GetTargetId(id) : 0f);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }
}
