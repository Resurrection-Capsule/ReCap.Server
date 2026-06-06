using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

public static unsafe class NEventModule
{
    public static void Register(nint L)
    {
        LuaApiModule.RegisterNamespace(L, "nEvent",
            ("Notify", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&Notify));
    }

    // Retail contract (Ghidra @0x00a0c1d0): arg 1 = Lua table of named event fields, parsed via
    // AssetTypeRegistry descriptor (type hash 0x8619ff24) into a 0x98 event struct, dispatched to
    // the C++ event system + network serializer. 0 returns. Server dispatch (ServerEvent 0x9B
    // recipes) is Simulation-phase work — for now harvest the field names scripts actually send.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Notify(nint L)
    {
        try
        {
            if (LuaNative.lua_gettop(L) < 1 || LuaNative.lua_type(L, 1) != 5)
                return 0;

            var fields = new StringBuilder();
            LuaNative.lua_pushnil(L);
            while (LuaNative.lua_next(L, 1) != 0)
            {
                if (LuaNative.lua_type(L, -2) == 4)
                {
                    if (fields.Length > 0) fields.Append(", ");
                    fields.Append(LuaNative.ToManagedString(L, -2));
                    if (LuaNative.lua_type(L, -1) == 3)
                        fields.Append('=').Append(LuaNative.lua_tonumber(L, -1).ToString("G"));
                }
                LuaNative.lua_settop(L, -2);
            }
            Util.Logging.Log.Lua.Debug($"nEvent.Notify {{{fields}}}");
            return 0;
        }
        catch
        {
            return 0;
        }
    }
}

public static unsafe class NDebugModule
{
    public static void Register(nint L)
    {
        LuaApiModule.RegisterNamespace(L, "nDebug",
            ("IsAbilityDebugEnabled", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&IsAbilityDebugEnabled));
    }

    // Retail @0x009f97b0: hardcoded PushBoolean(false), no flag read.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int IsAbilityDebugEnabled(nint L)
    {
        LuaNative.lua_pushboolean(L, 0);
        return 1;
    }
}
