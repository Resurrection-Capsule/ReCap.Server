using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

public static unsafe class LuaApiModule
{
    // Push a 32-bit asset hash as the Lua number scripts pass around, registering the lossy
    // float32→exact-hash map on the context so downstream consumers (nEvent.Notify → ServerEventDef)
    // recover the exact value. EVERY string→hash boxing site (GetAsset, SPID, PreloadAsset/Animation)
    // must route through here — an unregistered box (e.g. an effect preloaded once, fired later)
    // reaches the wire as a rounded hash that never resolves an asset. See ScriptStateContext.
    public static void PushHash(nint L, uint hash)
        => LuaNative.lua_pushnumber(L, ScriptContextRegistry.Get(L)?.RegisterAssetHash(hash) ?? (float)hash);

    public static void RegisterNamespace(nint L, string name,
        params (string Name, nint Fn)[] entries)
    {
        LuaNative.lua_getfield(L, LuaNative.LUA_GLOBALSINDEX, name);
        if (LuaNative.lua_type(L, -1) != LuaNative.LUA_TTABLE)
        {
            LuaNative.lua_settop(L, -2);
            LuaNative.lua_createtable(L, 0, entries.Length);
            LuaNative.lua_pushvalue(L, -1);
            LuaNative.lua_setfield(L, LuaNative.LUA_GLOBALSINDEX, name);
        }
        foreach (var (fnName, fn) in entries)
        {
            LuaNative.lua_pushstring(L, fnName);
            LuaNative.lua_pushcclosure(L, fn, 0);
            LuaNative.lua_rawset(L, -3);
        }
        AttachStubFallback(L, name);
        LuaNative.lua_settop(L, -2);
    }

    private static void AttachStubFallback(nint L, string nsName)
    {
        LuaNative.lua_createtable(L, 0, 1);
        LuaNative.lua_pushstring(L, "__index");
        LuaNative.lua_pushstring(L, nsName);
        LuaNative.lua_pushcclosure(L, (nint)(delegate* unmanaged[Cdecl]<nint, int>)&StubIndex, 1);
        LuaNative.lua_rawset(L, -3);
        LuaNative.lua_setmetatable(L, -2);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int StubIndex(nint L)
    {
        try
        {
            var ns = LuaNative.ToManagedString(L, LuaUpvalueIndex(1)) ?? "?";
            var key = LuaNative.lua_type(L, 2) == LuaNative.LUA_TSTRING
                ? (LuaNative.ToManagedString(L, 2) ?? "?")
                : "?";
            StubTelemetry.RecordLookup(L, ns, key);
            LuaNative.lua_pushstring(L, ns);
            LuaNative.lua_pushstring(L, key);
            LuaNative.lua_pushcclosure(L, (nint)(delegate* unmanaged[Cdecl]<nint, int>)&StubCall, 2);
            return 1;
        }
        catch (Exception ex)
        {
            try { Util.Logging.Log.Lua.Error($"[stub] __index failed: {ex.Message}"); } catch { }
            LuaNative.lua_pushnil(L);
            return 1;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int StubCall(nint L)
    {
        try
        {
            var ns = LuaNative.ToManagedString(L, LuaUpvalueIndex(1)) ?? "?";
            var key = LuaNative.ToManagedString(L, LuaUpvalueIndex(2)) ?? "?";
            StubTelemetry.RecordCall(L, ns, key);
        }
        catch (Exception ex)
        {
            try { Util.Logging.Log.Lua.Error($"[stub] call failed: {ex.Message}"); } catch { }
        }
        return 0;
    }

    internal static int LuaUpvalueIndex(int i) => LuaNative.LUA_GLOBALSINDEX - i;
}

internal static class StubTelemetry
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _seen = [];
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<nint, string> _tags = new();

    public static void TagState(nint L, string tag) => _tags[L] = tag;
    public static void UntagState(nint L) => _tags.TryRemove(L, out _);
    private static string Tag(nint L) => _tags.TryGetValue(L, out var t) ? t : "untagged";

    public static void RecordLookup(nint L, string ns, string key)
    {
        if (_seen.TryAdd($"{Tag(L)}|{ns}.{key}", 0))
            Util.Logging.Log.Lua.Warn($"unimplemented {ns}.{key} (first lookup)");
    }

    public static void RecordCall(nint L, string ns, string key)
    {
        if (_seen.TryAdd($"{Tag(L)}|{ns}.{key}:called", 0))
            Util.Logging.Log.Lua.Warn($"unimplemented {ns}.{key} CALLED - returns nothing");
    }

    public static IReadOnlyCollection<string> Snapshot() => _seen.Keys.OrderBy(k => k).ToList();

    public static void Clear() => _seen.Clear();

    internal static string GetTag(nint L) => Tag(L);
}
