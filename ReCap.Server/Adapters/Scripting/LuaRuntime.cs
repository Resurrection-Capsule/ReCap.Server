using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting;

public sealed class LuaScriptException(string message) : Exception(message);

public sealed class LuaRuntime : IDisposable
{
    private readonly LuaStateHandle _handle;
    private int _tracebackRef;
    internal nint L { get; }

    private LuaRuntime(nint state, LuaStateHandle handle)
    {
        L = state;
        _handle = handle;
    }

    public static LuaRuntime CreateSandboxedState()
    {
        var L = LuaNative.luaL_newstate();
        var handle = new LuaStateHandle();
        System.Runtime.InteropServices.Marshal.InitHandle(handle, L);
        var rt = new LuaRuntime(L, handle);
        rt.OpenSandboxedLibraries();
        return rt;
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
    }

    public void Execute(byte[] chunk, string chunkName)
    {
        LuaNative.lua_rawgeti(L, LuaNative.LUA_REGISTRYINDEX, _tracebackRef);
        var status = LuaNative.luaL_loadbuffer(L, chunk, (nuint)chunk.Length, chunkName);
        if (status != LuaNative.LUA_OK)
            ThrowTop(chunkName);
        status = LuaNative.lua_pcall(L, 0, 0, 1);
        if (status != LuaNative.LUA_OK)
            ThrowTop(chunkName);
        LuaNative.lua_settop(L, 0);
    }

    public bool EvalBool(byte[] chunk)
    {
        LuaNative.lua_rawgeti(L, LuaNative.LUA_REGISTRYINDEX, _tracebackRef);
        if (LuaNative.luaL_loadbuffer(L, chunk, (nuint)chunk.Length, "eval") != LuaNative.LUA_OK)
            ThrowTop("eval");
        if (LuaNative.lua_pcall(L, 0, 1, 1) != LuaNative.LUA_OK)
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

    public void Dispose() => _handle.Dispose();
}
