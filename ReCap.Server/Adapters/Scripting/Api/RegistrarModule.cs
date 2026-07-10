using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

public static unsafe class RegistrarModule
{
    public static void Register(nint L)
    {
        LuaApiModule.RegisterNamespace(L, "nAbility",
            ("RegisterAbility", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&RegisterAbility));
        LuaApiModule.RegisterNamespace(L, "nModifier",
            ("RegisterModifier", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&RegisterModifier));
        LuaApiModule.RegisterNamespace(L, "nAffix",
            ("RegisterAffix", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&RegisterAffix));
        LuaApiModule.RegisterNamespace(L, "nCondition",
            ("RegisterCondition", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&RegisterCondition));
        LuaApiModule.RegisterNamespace(L, "nObjective",
            ("RegisterObjective", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&RegisterObjective));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int RegisterAbility(nint L) => RegisterEntry(L, ScriptKind.Ability);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int RegisterModifier(nint L) => RegisterEntry(L, ScriptKind.Modifier);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int RegisterAffix(nint L) => RegisterEntry(L, ScriptKind.Affix);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int RegisterCondition(nint L) => RegisterEntry(L, ScriptKind.Condition);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int RegisterObjective(nint L) => RegisterEntry(L, ScriptKind.Objective);

    private static int RegisterEntry(nint L, ScriptKind kind)
    {
        try
        {
            if (LuaNative.lua_type(L, 1) != LuaNative.LUA_TSTRING || LuaNative.lua_type(L, 2) != LuaNative.LUA_TTABLE)
                return 0;
            var name = LuaNative.ToManagedString(L, 1);
            var context = ScriptContextRegistry.Get(L);
            if (name is null || context is null) return 0;
            var hash = ScriptVfs.Hash(name);
            if (context.Registry.Find(kind, hash) is not null) return 0;
            var hasTick = TableHasFunction(L, 2, "tick");
            var hasActivate = TableHasFunction(L, 2, "activate");
            var hasDeactivate = TableHasFunction(L, 2, "deactivate");
            // Ability props table carries the data the client def is populated from (AssetTypeRegistry
            // "ability" descriptor). `cooldown` (seconds) gates re-cast — read it here so
            // PayCooldownAndMana can stamp it. It is a ranked-value table ({1.5,1.25,1}) or a scalar;
            // capture the whole thing so the cast's rank picks the right value. Non-abilities have none.
            var cooldowns = TableRankedField(L, 2, "cooldown");
            // activationType (ability schema int, def+0x190) is the modifier stack policy the retail
            // create path switches on (nAbility::RequestModifier -> FUN_009e6420): 0 stack, 1 replace-all,
            // 2 replace-per-caster, 3 reject-per-initiator, 4/5 unique-per-caster, 6 single, 7 refresh.
            var activationType = TableIntField(L, 2, "activationType");
            // handledEvents (ability schema int bitmask, def+0x1a4) — which nAbilityEventFlags a
            // modifier's [4] handler subscribes to (e.g. TookDamage/StackModifier). Combat dispatch
            // fires [4] only on modifiers whose bitmask includes the event.
            var handledEvents = TableIntField(L, 2, "handledEvents");
            // Priority-dispel pre-pass fields (ability schema, AssetData.Parser ability.cs offsets):
            // requiresAgent (0x5a) gates the pre-pass; deactivateOnInterrupt (0x17c) marks an existing
            // modifier as dispellable by a >= priority one; modifierPriority (0x178) is the rank.
            var requiresAgent = TableBoolField(L, 2, "requiresAgent");
            var modifierPriority = TableIntField(L, 2, "modifierPriority");
            var deactivateOnInterrupt = TableBoolField(L, 2, "deactivateOnInterrupt");
            LuaNative.lua_pushvalue(L, 2);
            var tableRef = LuaNative.luaL_ref(L, LuaNative.LUA_REGISTRYINDEX);
            context.Registry.TryAdd(kind, new ScriptEntry(name, hash, tableRef, hasTick, hasActivate, hasDeactivate, cooldowns, activationType, handledEvents, requiresAgent, modifierPriority, deactivateOnInterrupt));
            return 0;
        }
        catch (Exception ex)
        {
            try { Util.Logging.Log.Lua.Error($"[registrar] {kind} failed: {ex.Message}"); } catch { }
            return 0;
        }
    }

    // Scalar int field (0 when absent/non-numeric). LUA_NUMBER is float32 here, exact for small ints.
    private static int TableIntField(nint L, int tableIndex, string field)
    {
        LuaNative.lua_getfield(L, tableIndex, field);
        var value = LuaNative.lua_type(L, -1) == LuaNative.LUA_TNUMBER ? (int)LuaNative.lua_tonumber(L, -1) : 0;
        LuaNative.lua_settop(L, -2);
        return value;
    }

    // Bool field (false when absent/nil). Lua truthiness via lua_toboolean.
    private static bool TableBoolField(nint L, int tableIndex, string field)
    {
        LuaNative.lua_getfield(L, tableIndex, field);
        var value = LuaNative.lua_toboolean(L, -1) != 0;
        LuaNative.lua_settop(L, -2);
        return value;
    }

    private static bool TableHasFunction(nint L, int tableIndex, string field)
    {
        LuaNative.lua_getfield(L, tableIndex, field);
        var isFn = LuaNative.lua_type(L, -1) == LuaNative.LUA_TFUNCTION;
        LuaNative.lua_settop(L, -2);
        return isFn;
    }

    // A ranked-value field is either a scalar number or a { } array of per-rank numbers.
    // Returns null when the key is absent/malformed (no cooldown), a 1-element list for a scalar.
    private static IReadOnlyList<float>? TableRankedField(nint L, int tableIndex, string field)
    {
        LuaNative.lua_getfield(L, tableIndex, field);
        IReadOnlyList<float>? result = null;
        var type = LuaNative.lua_type(L, -1);
        if (type == LuaNative.LUA_TNUMBER)
        {
            result = new[] { LuaNative.lua_tonumber(L, -1) };
        }
        else if (type == LuaNative.LUA_TTABLE)
        {
            var n = (int)LuaNative.lua_objlen(L, -1);
            if (n > 0)
            {
                var list = new float[n];
                for (var i = 1; i <= n; i++)
                {
                    LuaNative.lua_rawgeti(L, -1, i);
                    list[i - 1] = LuaNative.lua_type(L, -1) == LuaNative.LUA_TNUMBER ? LuaNative.lua_tonumber(L, -1) : 0f;
                    LuaNative.lua_settop(L, -2);
                }
                result = list;
            }
        }
        LuaNative.lua_settop(L, -2);
        return result;
    }
}
