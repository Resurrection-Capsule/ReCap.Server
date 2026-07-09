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
            // "ability" descriptor). `cooldown` (schema id 38, seconds) gates re-cast — read it here so
            // PayCooldownAndMana can stamp it. Non-abilities simply have no cooldown key (0).
            var cooldown = TableNumberField(L, 2, "cooldown");
            LuaNative.lua_pushvalue(L, 2);
            var tableRef = LuaNative.luaL_ref(L, LuaNative.LUA_REGISTRYINDEX);
            context.Registry.TryAdd(kind, new ScriptEntry(name, hash, tableRef, hasTick, hasActivate, hasDeactivate, cooldown));
            return 0;
        }
        catch (Exception ex)
        {
            try { Util.Logging.Log.Lua.Error($"[registrar] {kind} failed: {ex.Message}"); } catch { }
            return 0;
        }
    }

    private static bool TableHasFunction(nint L, int tableIndex, string field)
    {
        LuaNative.lua_getfield(L, tableIndex, field);
        var isFn = LuaNative.lua_type(L, -1) == LuaNative.LUA_TFUNCTION;
        LuaNative.lua_settop(L, -2);
        return isFn;
    }

    private static float TableNumberField(nint L, int tableIndex, string field)
    {
        LuaNative.lua_getfield(L, tableIndex, field);
        var value = LuaNative.lua_type(L, -1) == LuaNative.LUA_TNUMBER ? LuaNative.lua_tonumber(L, -1) : 0f;
        LuaNative.lua_settop(L, -2);
        return value;
    }
}
