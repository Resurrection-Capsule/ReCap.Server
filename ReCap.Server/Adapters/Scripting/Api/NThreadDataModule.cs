using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

public static unsafe class NThreadDataModule
{
    public static void Register(nint L) =>
        LuaApiModule.RegisterNamespace(L, "nThreadData",
            ("GetPrivateTable", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetPrivateTable),
            ("CreatePrivateTable", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&CreatePrivateTable),
            ("SetGUID", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&SetGuid));

    // Create a fresh per-thread private table and return it (objectives' Init and many modifiers do
    // `local pt = nThreadData.CreatePrivateTable(); pt.x = ...`). Was unregistered → hit the stub →
    // returned nil → "attempt to index a nil value" killed every objective's Init. Stores it as the
    // per-thread ref so a later GetPrivateTable (or the objective/modifier shared-table capture) resolves
    // the same table.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int CreatePrivateTable(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            LuaNative.lua_createtable(L, 0, 0);
            LuaNative.lua_pushvalue(L, -1);
            var reference = LuaNative.luaL_ref(L, LuaNative.LUA_REGISTRYINDEX);
            ctx?.SetPrivateTableRef(L, reference);
            return 1;
        }
        catch { LuaNative.lua_pushnil(L); return 1; }
    }

    // Per-thread private Lua table (registry-backed). Retail GetPrivateTable does not create;
    // with CreatePrivateTable out of scope we create-on-demand so scripts that store into it work.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetPrivateTable(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            if (ctx is null)
            {
                LuaNative.lua_pushnil(L);
                return 1;
            }
            // A modifier index coroutine borrows its instance's shared table (retail instance+0x170) —
            // return that so activate/tick/deactivate see one another's state. It is instance-owned, so
            // do not touch the per-thread ref map here.
            if (ctx.TryGetSharedPrivateTable(L, out var sharedRef))
            {
                LuaNative.lua_rawgeti(L, LuaNative.LUA_REGISTRYINDEX, sharedRef);
                if (LuaNative.lua_type(L, -1) == LuaNative.LUA_TTABLE) return 1;
                LuaNative.lua_settop(L, -2);
            }
            if (ctx.TryGetPrivateTableRef(L, out var existing))
            {
                LuaNative.lua_rawgeti(L, LuaNative.LUA_REGISTRYINDEX, existing);
                if (LuaNative.lua_type(L, -1) == LuaNative.LUA_TTABLE) return 1;
                LuaNative.lua_settop(L, -2);
            }
            LuaNative.lua_createtable(L, 0, 0);
            LuaNative.lua_pushvalue(L, -1);
            var reference = LuaNative.luaL_ref(L, LuaNative.LUA_REGISTRYINDEX);
            ctx.SetPrivateTableRef(L, reference);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnil(L);
            return 1;
        }
    }

    // Per-thread indexed GUID slot store (paired GetGUID deferred until demanded).
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SetGuid(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            if (ctx is not null
                && LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER
                && LuaNative.lua_type(L, 2) == LuaNative.LUA_TNUMBER)
            {
                var slot = (int)Math.Round((double)LuaNative.lua_tonumber(L, 1));
                var guid = (double)LuaNative.lua_tonumber(L, 2);
                ctx.SetGuidSlot(L, slot, guid);
            }
        }
        catch { }
        return 0;
    }
}
