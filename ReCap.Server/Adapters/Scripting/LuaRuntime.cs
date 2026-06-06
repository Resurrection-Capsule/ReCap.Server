using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Api;
using ReCap.Server.Adapters.Scripting.Native;
using Serilog.Events;

namespace ReCap.Server.Adapters.Scripting;

public sealed class LuaScriptException(string message) : Exception(message);

public sealed class LuaRuntime : IDisposable
{
    private static readonly ConcurrentDictionary<nint, Func<string, byte[]?>> _resolvers = new();

    private readonly LuaStateHandle _handle;
    private int _tracebackRef;
    internal nint L { get; }

    private LuaRuntime(nint state, LuaStateHandle handle)
    {
        L = state;
        _handle = handle;
    }

    private const int WatchdogInstructionBudget = 50_000_000;

    internal static void InstallWatchdog(nint threadState)
    {
        unsafe
        {
            LuaNative.lua_sethook(threadState,
                (nint)(delegate* unmanaged[Cdecl]<nint, nint, void>)&LuaStubs.WatchdogHook,
                LuaNative.LUA_MASKCOUNT, WatchdogInstructionBudget);
        }
    }

    public static LuaRuntime CreateSandboxedState(Func<string, byte[]?>? chunkResolver = null, string contextTag = "boot")
    {
        var L = LuaNative.luaL_newstate();
        var handle = new LuaStateHandle();
        System.Runtime.InteropServices.Marshal.InitHandle(handle, L);
        var rt = new LuaRuntime(L, handle);
        rt.OpenSandboxedLibraries();
        if (chunkResolver is not null)
            _resolvers[L] = chunkResolver;
        unsafe
        {
            LuaNative.lua_pushcclosure(L, (nint)(delegate* unmanaged[Cdecl]<nint, int>)&LuaStubs.Require, 0);
        }
        LuaNative.lua_setfield(L, LuaNative.LUA_GLOBALSINDEX, "require");
        StubNamespaces.RegisterAll(L);
        NUtilModule.Register(L);
        NMathUtilModule.Register(L);
        ScriptContextRegistry.Register(L, new ScriptStateContext { Registry = new ScriptRegistry() });
        Api.RegistrarModule.Register(L);
        LuaNative.lua_getfield(L, LuaNative.LUA_GLOBALSINDEX, "math");
        LuaNative.lua_pushstring(L, "random");
        unsafe { LuaNative.lua_pushcclosure(L, (nint)(delegate* unmanaged[Cdecl]<nint, int>)&LuaStubs.MathRandom, 0); }
        LuaNative.lua_rawset(L, -3);
        LuaNative.lua_settop(L, -2);
        Api.StubTelemetry.TagState(L, contextTag);
        InstallWatchdog(L);
        return rt;
    }

    internal static bool TryResolveChunk(nint L, string name, out byte[]? chunk)
    {
        chunk = null;
        if (!_resolvers.TryGetValue(L, out var resolver))
            return false;
        try
        {
            chunk = resolver(name);
            return chunk is not null;
        }
        catch
        {
            return false;
        }
    }

    private void OpenSandboxedLibraries()
    {
        LuaNative.luaopen_base(L);
        LuaNative.lua_settop(L, 0);
        LuaNative.luaopen_table(L);
        LuaNative.lua_settop(L, 0);
        LuaNative.luaopen_string(L);
        LuaNative.lua_settop(L, 0);
        LuaNative.luaopen_math(L);
        LuaNative.lua_settop(L, 0);
        LuaNative.luaopen_debug(L);
        LuaNative.lua_settop(L, 0);
        LuaNative.lua_getfield(L, LuaNative.LUA_GLOBALSINDEX, "debug");
        LuaNative.lua_getfield(L, -1, "traceback");
        _tracebackRef = LuaNative.luaL_ref(L, LuaNative.LUA_REGISTRYINDEX);
        LuaNative.lua_settop(L, 0);

        foreach (var banned in new[] { "debug", "loadstring", "dofile", "loadfile", "loadlib", "package", "module" })
        {
            LuaNative.lua_pushnil(L);
            LuaNative.lua_setfield(L, LuaNative.LUA_GLOBALSINDEX, banned);
        }
        unsafe
        {
            LuaNative.lua_pushcclosure(L, (nint)(delegate* unmanaged[Cdecl]<nint, int>)&LuaStubs.Print, 0);
        }
        LuaNative.lua_setfield(L, LuaNative.LUA_GLOBALSINDEX, "print");
    }

    public void Execute(byte[] chunk, string chunkName)
    {
        LuaNative.lua_rawgeti(L, LuaNative.LUA_REGISTRYINDEX, _tracebackRef);
        var status = LuaNative.luaL_loadbuffer(L, chunk, (nuint)chunk.Length, chunkName);
        if (status != LuaNative.LUA_OK)
            ThrowTop(chunkName);
        status = LuaNative.lua_pcall(L, 0, 0, -2);
        if (status != LuaNative.LUA_OK)
            ThrowTop(chunkName);
        LuaNative.lua_settop(L, 0);
    }

    public bool EvalBool(byte[] chunk)
    {
        LuaNative.lua_rawgeti(L, LuaNative.LUA_REGISTRYINDEX, _tracebackRef);
        if (LuaNative.luaL_loadbuffer(L, chunk, (nuint)chunk.Length, "eval") != LuaNative.LUA_OK)
            ThrowTop("eval");
        if (LuaNative.lua_pcall(L, 0, 1, -2) != LuaNative.LUA_OK)
            ThrowTop("eval");
        var result = LuaNative.lua_toboolean(L, -1) != 0;
        LuaNative.lua_settop(L, 0);
        return result;
    }

    private void ThrowTop(string chunkName)
    {
        var msg = LuaNative.ToManagedString(L, -1) ?? "unknown lua error";
        LuaNative.lua_settop(L, 0);
        throw new LuaScriptException($"[{chunkName}] {msg}");
    }

    public void Dispose()
    {
        _resolvers.TryRemove(L, out _);
        Api.StubTelemetry.UntagState(L);
        ScriptContextRegistry.Unregister(L);
        _handle.Dispose();
    }
}

internal static class LuaStubs
{
    [System.Runtime.InteropServices.UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static void WatchdogHook(nint L, nint ar)
    {
        LuaNative.lua_pushstring(L, "instruction budget exceeded");
        LuaNative.lua_error(L);
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static int Print(nint L)
    {
        if (!ReCap.Server.Util.Logging.Log.Lua.IsEnabled(LogEventLevel.Debug))
            return 0;
        try
        {
            var top = LuaNative.lua_gettop(L);
            var parts = new List<string>(top);
            for (var i = 1; i <= top; i++)
            {
                var t = LuaNative.lua_type(L, i);
                parts.Add(t == LuaNative.LUA_TSTRING
                    ? LuaNative.ToManagedString(L, i) ?? string.Empty
                    : t == LuaNative.LUA_TNUMBER
                        ? LuaNative.lua_tonumber(L, i).ToString(System.Globalization.CultureInfo.InvariantCulture)
                        : t.ToString());
            }
            ReCap.Server.Util.Logging.Log.Lua.Debug($"[print] {string.Join("\t", parts)}");
        }
        catch (Exception ex)
        {
            try { ReCap.Server.Util.Logging.Log.Lua.Error($"[stub] print failed: {ex.Message}"); } catch { }
        }
        return 0;
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static int MathRandom(nint L)
    {
        try
        {
            var argc = LuaNative.lua_gettop(L);
            if (argc == 0)
            {
                LuaNative.lua_pushnumber(L, (float)Random.Shared.NextDouble());
                return 1;
            }
            if (argc == 1)
            {
                var m = (int)System.Math.Round((double)LuaNative.lua_tonumber(L, 1));
                if (m < 1) m = 1;
                LuaNative.lua_pushnumber(L, (float)Random.Shared.Next(1, m + 1));
                return 1;
            }
            var lo = (int)System.Math.Round((double)LuaNative.lua_tonumber(L, 1));
            var hi = (int)System.Math.Round((double)LuaNative.lua_tonumber(L, 2));
            if (hi < lo) hi = lo;
            LuaNative.lua_pushnumber(L, (float)Random.Shared.Next(lo, hi + 1));
            return 1;
        }
        catch (Exception ex)
        {
            try { ReCap.Server.Util.Logging.Log.Lua.Error($"[stub] math.random failed: {ex.Message}"); } catch { }
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    internal static int Require(nint L)
    {
        string? error = null;
        string? name = null;
        byte[]? chunk = null;
        var alreadyLoaded = false;
        try
        {
            name = LuaNative.lua_type(L, 1) == LuaNative.LUA_TSTRING ? LuaNative.ToManagedString(L, 1) : null;
            if (name is null)
                error = "require: string expected";
            else
            {
                LuaNative.lua_getfield(L, LuaNative.LUA_REGISTRYINDEX, "recap.loaded");
                if (LuaNative.lua_type(L, -1) != LuaNative.LUA_TTABLE)
                {
                    LuaNative.lua_settop(L, 1);
                    LuaNative.lua_createtable(L, 0, 32);
                    LuaNative.lua_pushvalue(L, -1);
                    LuaNative.lua_setfield(L, LuaNative.LUA_REGISTRYINDEX, "recap.loaded");
                }
                LuaNative.lua_getfield(L, -1, name);
                alreadyLoaded = LuaNative.lua_toboolean(L, -1) != 0;
                LuaNative.lua_settop(L, 1);
                if (!alreadyLoaded && (!LuaRuntime.TryResolveChunk(L, name, out chunk) || chunk is null))
                    error = $"require: chunk not found: {name}";
            }
        }
        catch (Exception ex)
        {
            error = $"require: internal failure: {ex.Message}";
        }
        if (error is null && !alreadyLoaded)
        {
            var status = LuaNative.luaL_loadbuffer(L, chunk, (nuint)chunk!.Length, name!);
            if (status != LuaNative.LUA_OK)
                return LuaNative.lua_error(L);
            status = LuaNative.lua_pcall(L, 0, 0, 0);
            if (status != LuaNative.LUA_OK)
                return LuaNative.lua_error(L);
            try
            {
                LuaNative.lua_getfield(L, LuaNative.LUA_REGISTRYINDEX, "recap.loaded");
                LuaNative.lua_pushboolean(L, 1);
                LuaNative.lua_setfield(L, -2, name!);
                LuaNative.lua_settop(L, 1);
            }
            catch { }
        }
        if (error is not null)
        {
            LuaNative.lua_pushstring(L, error);
            return LuaNative.lua_error(L);
        }
        LuaNative.lua_pushboolean(L, 1);
        return 1;
    }
}
