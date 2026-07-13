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
    // the C++ event system + network serializer. 0 returns. Ability hit/impact scripts fire these to
    // drive the client FX — we map the table to a ServerEvent (0x9B) "attached" recipe
    // ({6 ServerEventDef, 7 ObjectId}, +Critical): `asset` is the float-boxed effect hash resolved
    // back to its exact value (LUA_NUMBER float precision, see ScriptStateContext.ResolveAssetHash).
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Notify(nint L)
    {
        try
        {
            if (LuaNative.lua_gettop(L) < 1 || LuaNative.lua_type(L, 1) != 5)
                return 0;

            var ctx = ScriptContextRegistry.Get(L);

            var assetBoxed = ReadNumberField(L, "asset");
            var objectId = (uint)Math.Round(ReadNumberField(L, "objectId"));
            var attackerId = (uint)Math.Round(ReadNumberField(L, "attackerId"));
            var critical = ReadBoolField(L, "bCritical");
            var position = ReadVec3Field(L, "position");
            var facing = ReadVec3Field(L, "facing");

            var serverEventDef = ctx is not null ? ctx.ResolveAssetHash(assetBoxed) : (uint)assetBoxed;

            Util.Logging.Log.Lua.Debug(
                $"nEvent.Notify asset=0x{serverEventDef:X8} obj={objectId} attacker={attackerId} crit={critical} " +
                $"pos={(position is { } p ? $"({p.X:F0},{p.Y:F0},{p.Z:F0})" : "-")}");

            ctx?.GameBridge?.EmitServerEvent(serverEventDef, objectId, attackerId, critical, position, facing);
            return 0;
        }
        catch
        {
            return 0;
        }
    }

    private static float ReadNumberField(nint L, string field)
    {
        LuaNative.lua_getfield(L, 1, field);
        var value = LuaNative.lua_type(L, -1) == 3 ? LuaNative.lua_tonumber(L, -1) : 0f;
        LuaNative.lua_settop(L, -2);
        return value;
    }

    private static bool ReadBoolField(nint L, string field)
    {
        LuaNative.lua_getfield(L, 1, field);
        var value = LuaNative.lua_toboolean(L, -1) != 0;
        LuaNative.lua_settop(L, -2);
        return value;
    }

    // Vectors are array-indexed Lua tables ({[1]=x,[2]=y,[3]=z}), same shape the damage-range table
    // uses. Returns null when the field is absent/not a table so the caller can pick the FX recipe.
    private static System.Numerics.Vector3? ReadVec3Field(nint L, string field)
    {
        LuaNative.lua_getfield(L, 1, field);
        if (LuaNative.lua_type(L, -1) != 5) { LuaNative.lua_settop(L, -2); return null; }
        var v = new System.Numerics.Vector3(RawNumber(L, 1), RawNumber(L, 2), RawNumber(L, 3));
        LuaNative.lua_settop(L, -2);
        return v;
    }

    private static float RawNumber(nint L, int index)
    {
        LuaNative.lua_rawgeti(L, -1, index);
        var value = LuaNative.lua_type(L, -1) == 3 ? LuaNative.lua_tonumber(L, -1) : 0f;
        LuaNative.lua_settop(L, -2);
        return value;
    }
}

// nDebug — script debug helpers. LogToConsole/Assert surface the game scripts' OWN diagnostics into
// our Lua log (valuable when driving the client: the scripts narrate what they do). Draw calls are
// server-side no-ops.
public static unsafe class NDebugModule
{
    public static void Register(nint L)
    {
        LuaApiModule.RegisterNamespace(L, "nDebug",
            ("IsAbilityDebugEnabled", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&IsAbilityDebugEnabled),
            ("LogToConsole", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&LogToConsole),
            ("Assert", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&Assert),
            ("DrawCircle", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&DrawCircle));
    }

    // Retail @0x009f97b0: hardcoded PushBoolean(false), no flag read.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int IsAbilityDebugEnabled(nint L)
    {
        LuaNative.lua_pushboolean(L, 0);
        return 1;
    }

    // LogToConsole(message) — print a script diagnostic (routed to the Lua log category).
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int LogToConsole(nint L)
    {
        try
        {
            var msg = LuaNative.lua_type(L, 1) == LuaNative.LUA_TSTRING ? LuaNative.ToManagedString(L, 1)
                : LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER ? LuaNative.lua_tonumber(L, 1).ToString()
                : null;
            if (msg is not null) ReCap.Server.Util.Logging.Log.Lua.Debug($"[nDebug] {msg}");
        }
        catch { }
        return 0;
    }

    // Assert(condition, [message]) — warn when the condition is falsy (nil/false/0). Never throws.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Assert(nint L)
    {
        try
        {
            var truthy = LuaNative.lua_type(L, 1) switch
            {
                LuaNative.LUA_TNIL => false,
                LuaNative.LUA_TBOOLEAN => LuaNative.lua_toboolean(L, 1) != 0,
                LuaNative.LUA_TNUMBER => LuaNative.lua_tonumber(L, 1) != 0f,
                _ => true,
            };
            if (!truthy)
            {
                var msg = LuaNative.lua_type(L, 2) == LuaNative.LUA_TSTRING ? LuaNative.ToManagedString(L, 2) : "assertion failed";
                ReCap.Server.Util.Logging.Log.Lua.Warn($"[nDebug] assert: {msg}");
            }
        }
        catch { }
        return 0;
    }

    // DrawCircle(x,y,z, r,g,b, radius) — debug overlay; no server-side effect.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int DrawCircle(nint L) => 0;
}
