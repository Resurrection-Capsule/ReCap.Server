using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

public static unsafe class NThreadDataModule
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<nint, int> _privateTableRefs = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<nint, System.Collections.Concurrent.ConcurrentDictionary<int, double>> _guidSlots = new();

    public static void Register(nint L) =>
        LuaApiModule.RegisterNamespace(L, "nThreadData",
            ("GetPrivateTable", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetPrivateTable),
            ("SetGUID", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&SetGuid));

    // Per-thread private Lua table (registry-backed). Retail GetPrivateTable does not create;
    // with CreatePrivateTable out of scope we create-on-demand so scripts that store into it work.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetPrivateTable(nint L)
    {
        try
        {
            if (_privateTableRefs.TryGetValue(L, out var existing))
            {
                LuaNative.lua_rawgeti(L, LuaNative.LUA_REGISTRYINDEX, existing);
                if (LuaNative.lua_type(L, -1) == LuaNative.LUA_TTABLE) return 1;
                LuaNative.lua_settop(L, -2);
            }
            LuaNative.lua_createtable(L, 0, 0);
            LuaNative.lua_pushvalue(L, -1);
            var reference = LuaNative.luaL_ref(L, LuaNative.LUA_REGISTRYINDEX);
            _privateTableRefs[L] = reference;
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
            if (LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER
                && LuaNative.lua_type(L, 2) == LuaNative.LUA_TNUMBER)
            {
                var slot = (int)Math.Round((double)LuaNative.lua_tonumber(L, 1));
                var guid = (double)LuaNative.lua_tonumber(L, 2);
                var slots = _guidSlots.GetOrAdd(L, _ => new());
                slots[slot] = guid;
            }
        }
        catch { }
        return 0;
    }
}
