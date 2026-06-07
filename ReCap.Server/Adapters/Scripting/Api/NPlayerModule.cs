using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

public static unsafe class NPlayerModule
{
    public static void Register(nint L)
    {
        LuaApiModule.RegisterNamespace(L, "nPlayer",
            ("IsPlayerControlledObject", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&IsPlayerControlledObject));
    }

    // Melee GetDamage routes basic abilities to weapon damage only for player-controlled agents
    // (disasm template_ability_melee GetDamage 2026-06-07); 1 arg (objectId), 1 bool return.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int IsPlayerControlledObject(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            var controlled = bridge is not null
                && LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER
                && bridge.IsPlayerControlled((uint)Math.Round((double)LuaNative.lua_tonumber(L, 1)));
            LuaNative.lua_pushboolean(L, controlled ? 1 : 0);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushboolean(L, 0);
            return 1;
        }
    }
}
