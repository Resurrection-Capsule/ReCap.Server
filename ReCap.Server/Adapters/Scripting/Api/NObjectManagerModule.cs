using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

public static unsafe class NObjectManagerModule
{
    public static void Register(nint L)
    {
        LuaApiModule.RegisterNamespace(L, "nObjectManager",
            ("IsValidObject", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&IsValidObject),
            ("GetObjectsInRadius", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetObjectsInRadius),
            ("GetObjectsInRadius_SortedByDistance", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetObjectsInRadiusSortedByDistance),
            ("CreateObject", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&CreateObject),
            ("AttachTriggerVolume", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&AttachTriggerVolume));
    }

    // nObjectManager.CreateObject(nounAssetHandle, x, y, z, ...) -> new object id. arg1 = asset handle
    // (nUtil.GetAsset result); orientation/scale/owner args deferred (callers chain SetTeam/etc).
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int CreateObject(nint L)
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
                var noun = (uint)Math.Round((double)LuaNative.lua_tonumber(L, 1));
                var id = bridge.CreateObject(noun,
                    (float)LuaNative.lua_tonumber(L, 2), (float)LuaNative.lua_tonumber(L, 3), (float)LuaNative.lua_tonumber(L, 4));
                LuaNative.lua_pushnumber(L, id);
                return 1;
            }
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }

    // nObjectManager.AttachTriggerVolume(objId, radius, [onEnter], [onExit], [onStay]) -> handle.
    // Sphere-only (Ghidra @0x00a07e00); optional Lua callback closures captured via luaL_ref. Trigger
    // FIRING is deferred (no server-side collision system); refs held for a future pass, freed by lua_close.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int AttachTriggerVolume(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            if (ctx is null || LuaNative.lua_type(L, 1) != LuaNative.LUA_TNUMBER || LuaNative.lua_type(L, 2) != LuaNative.LUA_TNUMBER)
            {
                LuaNative.lua_pushnumber(L, 0f);
                return 1;
            }
            var objId = (uint)Math.Round((double)LuaNative.lua_tonumber(L, 1));
            var radius = (float)LuaNative.lua_tonumber(L, 2);
            var refs = new List<int>();
            for (var i = 3; i <= 5; i++)
                if (LuaNative.lua_type(L, i) == LuaNative.LUA_TFUNCTION)
                {
                    LuaNative.lua_pushvalue(L, i);
                    refs.Add(LuaNative.luaL_ref(L, LuaNative.LUA_REGISTRYINDEX));
                }
            LuaNative.lua_pushnumber(L, ctx.RegisterTriggerVolume(objId, radius, refs.ToArray()));
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }

    // Retail @0x00a0b220: same args/filter as GetObjectsInRadius, result sorted by distance²
    // ascending before the table is built.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetObjectsInRadiusSortedByDistance(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            if (bridge is null)
            {
                LuaNative.lua_createtable(L, 0, 0);
                return 1;
            }

            var x = LuaNative.lua_tonumber(L, 1);
            var y = LuaNative.lua_tonumber(L, 2);
            var z = LuaNative.lua_tonumber(L, 3);
            var radius = LuaNative.lua_tonumber(L, 4);
            var filterType = LuaNative.lua_gettop(L) >= 5 ? LuaNative.lua_type(L, 5) : 0;
            var damageableOnly = filterType == 5
                || (filterType == 3 && Math.Round((double)LuaNative.lua_tonumber(L, 5)) != 0);

            var ids = bridge.QueryObjectsInRadius(x, y, z, radius, damageableOnly)
                .OrderBy(id => bridge.TryGetPosition(id, out var px, out var py, out var pz)
                    ? (px - x) * (px - x) + (py - y) * (py - y) + (pz - z) * (pz - z)
                    : float.MaxValue)
                .ToList();
            LuaNative.lua_createtable(L, ids.Count, 0);
            for (var i = 0; i < ids.Count; i++)
            {
                LuaNative.lua_pushnumber(L, ids[i]);
                LuaNative.lua_rawseti(L, -2, i + 1);
            }
            return 1;
        }
        catch
        {
            LuaNative.lua_createtable(L, 0, 0);
            return 1;
        }
    }

    // Retail @0x00a0aed0: args (x, y, z, radius, [filter: 0|nounType|whitelist table|objectId],
    // [objectType flags, default 1]) → 1 return: 1-based table of objectId numbers, cap 256,
    // no alive gate. Scripts pass nSporeLabs.damageableObjectTypes (a table) as the filter —
    // approximated here as damageable-only (MaxHealth > 0).
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetObjectsInRadius(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            if (bridge is null)
            {
                LuaNative.lua_createtable(L, 0, 0);
                return 1;
            }

            var x = LuaNative.lua_tonumber(L, 1);
            var y = LuaNative.lua_tonumber(L, 2);
            var z = LuaNative.lua_tonumber(L, 3);
            var radius = LuaNative.lua_tonumber(L, 4);
            var filterType = LuaNative.lua_gettop(L) >= 5 ? LuaNative.lua_type(L, 5) : 0;
            var damageableOnly = filterType == 5
                || (filterType == 3 && Math.Round((double)LuaNative.lua_tonumber(L, 5)) != 0);

            var ids = bridge.QueryObjectsInRadius(x, y, z, radius, damageableOnly);
            LuaNative.lua_createtable(L, ids.Count, 0);
            for (var i = 0; i < ids.Count; i++)
            {
                LuaNative.lua_pushnumber(L, ids[i]);
                LuaNative.lua_rawseti(L, -2, i + 1);
            }
            return 1;
        }
        catch
        {
            LuaNative.lua_createtable(L, 0, 0);
            return 1;
        }
    }

    // Retail @0x009fb160: 1 arg (object id), 1 boolean — pure existence check, no side effects.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int IsValidObject(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            var valid = LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER
                && bridge is not null
                && bridge.ObjectExists((uint)Math.Round((double)LuaNative.lua_tonumber(L, 1)));
            LuaNative.lua_pushboolean(L, valid ? 1 : 0);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushboolean(L, 0);
            return 1;
        }
    }
}
