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
            ("SetStealthType", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&SetStealthType));
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
            var targetId = (uint)Math.Round((double)LuaNative.lua_tonumber(L, 2));
            var amount = LuaNative.lua_type(L, 3) == LuaNative.LUA_TNUMBER ? LuaNative.lua_tonumber(L, 3) : 0f;
            var applied = bridge.ApplyHeal(targetId, amount);
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

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SetStealthType(nint L)
    {
        // Stealth component state lands with the Simulation phase; arity contract (0 returns) holds.
        return 0;
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
