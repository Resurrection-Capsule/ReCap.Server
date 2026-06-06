using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

public static unsafe class NAttributeModule
{
    public static void Register(nint L)
    {
        LuaApiModule.RegisterNamespace(L, "nAttribute",
            ("GetAttributeValue", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetAttributeValue),
            ("GetAttributeValue_FromSnapshot", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetAttributeValueFromSnapshot));
    }

    // Retail contracts (Ghidra 2026-06-06):
    // GetAttributeValue @0x009feca0 — args (objectId, attributeId int), 1 float return; attribute
    //   array stride 4 at component+0x26c, id domain 0..0x73. CONFIRMED ids: 0=Strength
    //   1=Dexterity 2=Mind 4=MaxHealth; unknown ids return 0 and are debug-logged (harvest).
    // GetAttributeValue_FromSnapshot @0x009fede0 — args (snapshotHandle number, attributeId int),
    //   1 float return, raw frozen value (no modifier recompute), 0 when handle/id invalid.

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetAttributeValue(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            var objectId = (uint)Math.Round((double)LuaNative.lua_tonumber(L, 1));
            var attributeId = (int)Math.Round((double)LuaNative.lua_tonumber(L, 2));

            if (ctx?.GameBridge is { } bridge && bridge.TryGetAttributeValue(objectId, attributeId, out var value))
            {
                LuaNative.lua_pushnumber(L, value);
                return 1;
            }

            Util.Logging.Log.Lua.Debug($"nAttribute.GetAttributeValue unknown id={attributeId} obj={objectId}");
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetAttributeValueFromSnapshot(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            var handle = (uint)Math.Round((double)LuaNative.lua_tonumber(L, 1));
            var attributeId = (int)Math.Round((double)LuaNative.lua_tonumber(L, 2));

            if (ctx is not null
                && ctx.TryGetAttributeSnapshot(handle, out var attributes)
                && attributes.TryGetValue(attributeId, out var value))
            {
                LuaNative.lua_pushnumber(L, value);
                return 1;
            }

            Util.Logging.Log.Lua.Debug($"nAttribute.GetAttributeValue_FromSnapshot miss handle={handle} id={attributeId}");
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }
}
