using System.Runtime.InteropServices;

namespace ReCap.Server.Adapters.Scripting.Native;

internal static partial class LuaNative
{
    private const string Dll = "recaplua51";

    public const int LUA_REGISTRYINDEX = -10000;
    public const int LUA_ENVIRONINDEX = -10001;
    public const int LUA_GLOBALSINDEX = -10002;
    public const int LUA_MULTRET = -1;

    public const int LUA_TNIL = 0;
    public const int LUA_TBOOLEAN = 1;
    public const int LUA_TLIGHTUSERDATA = 2;
    public const int LUA_TNUMBER = 3;
    public const int LUA_TSTRING = 4;
    public const int LUA_TTABLE = 5;
    public const int LUA_TFUNCTION = 6;

    public const int LUA_OK = 0;
    public const int LUA_YIELD = 1;
    public const int LUA_ERRRUN = 2;
    public const int LUA_ERRSYNTAX = 3;
    public const int LUA_ERRMEM = 4;
    public const int LUA_ERRERR = 5;

    public const int LUA_MASKCOUNT = 8;

    [LibraryImport(Dll)] internal static partial nint luaL_newstate();
    [LibraryImport(Dll)] internal static partial void lua_close(nint L);
    [LibraryImport(Dll)] internal static partial nint lua_newthread(nint L);
    [LibraryImport(Dll)] internal static partial nint lua_atpanic(nint L, nint panicf);

    [LibraryImport(Dll, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int luaL_loadbuffer(nint L, byte[] buff, nuint sz, string name);
    [LibraryImport(Dll)] internal static partial int lua_pcall(nint L, int nargs, int nresults, int errfunc);
    [LibraryImport(Dll)] internal static partial void lua_call(nint L, int nargs, int nresults);
    [LibraryImport(Dll)] internal static partial int lua_resume(nint L, int narg);
    [LibraryImport(Dll)] internal static partial int lua_status(nint L);

    [LibraryImport(Dll)] internal static partial int lua_gettop(nint L);
    [LibraryImport(Dll)] internal static partial void lua_settop(nint L, int idx);
    [LibraryImport(Dll)] internal static partial void lua_pushvalue(nint L, int idx);
    [LibraryImport(Dll)] internal static partial void lua_remove(nint L, int idx);
    [LibraryImport(Dll)] internal static partial void lua_insert(nint L, int idx);
    [LibraryImport(Dll)] internal static partial int lua_checkstack(nint L, int sz);
    [LibraryImport(Dll)] internal static partial void lua_xmove(nint from, nint to, int n);

    [LibraryImport(Dll)] internal static partial int lua_type(nint L, int idx);
    [LibraryImport(Dll)] internal static partial float lua_tonumber(nint L, int idx);
    [LibraryImport(Dll)] internal static partial int lua_toboolean(nint L, int idx);
    [LibraryImport(Dll)] internal static partial nint lua_tolstring(nint L, int idx, out nuint len);
    [LibraryImport(Dll)] internal static partial nuint lua_objlen(nint L, int idx);
    [LibraryImport(Dll)] internal static partial nint lua_touserdata(nint L, int idx);

    [LibraryImport(Dll)] internal static partial void lua_pushnil(nint L);
    [LibraryImport(Dll)] internal static partial void lua_pushnumber(nint L, float n);
    [LibraryImport(Dll)] internal static partial void lua_pushinteger(nint L, nint n);
    [LibraryImport(Dll, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void lua_pushstring(nint L, string s);
    [LibraryImport(Dll)] internal static partial void lua_pushlstring(nint L, byte[] s, nuint len);
    [LibraryImport(Dll)] internal static partial void lua_pushboolean(nint L, int b);
    [LibraryImport(Dll)] internal static partial void lua_pushcclosure(nint L, nint fn, int n);
    [LibraryImport(Dll)] internal static partial void lua_pushlightuserdata(nint L, nint p);

    [LibraryImport(Dll, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void lua_getfield(nint L, int idx, string k);
    [LibraryImport(Dll, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void lua_setfield(nint L, int idx, string k);
    [LibraryImport(Dll)] internal static partial void lua_gettable(nint L, int idx);
    [LibraryImport(Dll)] internal static partial void lua_settable(nint L, int idx);
    [LibraryImport(Dll)] internal static partial void lua_rawget(nint L, int idx);
    [LibraryImport(Dll)] internal static partial void lua_rawset(nint L, int idx);
    [LibraryImport(Dll)] internal static partial void lua_rawgeti(nint L, int idx, int n);
    [LibraryImport(Dll)] internal static partial void lua_rawseti(nint L, int idx, int n);
    [LibraryImport(Dll)] internal static partial void lua_createtable(nint L, int narr, int nrec);
    [LibraryImport(Dll)] internal static partial int lua_setmetatable(nint L, int idx);
    [LibraryImport(Dll)] internal static partial int lua_getmetatable(nint L, int idx);
    [LibraryImport(Dll)] internal static partial int lua_next(nint L, int idx);

    [LibraryImport(Dll)] internal static partial int lua_error(nint L);
    [LibraryImport(Dll)] internal static partial int lua_gc(nint L, int what, int data);
    [LibraryImport(Dll)] internal static partial int lua_sethook(nint L, nint func, int mask, int count);
    [LibraryImport(Dll)] internal static partial int luaL_ref(nint L, int t);
    [LibraryImport(Dll)] internal static partial void luaL_unref(nint L, int t, int @ref);

    [LibraryImport(Dll)] internal static partial int luaopen_base(nint L);
    [LibraryImport(Dll)] internal static partial int luaopen_table(nint L);
    [LibraryImport(Dll)] internal static partial int luaopen_string(nint L);
    [LibraryImport(Dll)] internal static partial int luaopen_math(nint L);
    [LibraryImport(Dll)] internal static partial int luaopen_debug(nint L);

    internal static string? ToManagedString(nint L, int idx)
    {
        var ptr = lua_tolstring(L, idx, out var len);
        return ptr == 0 ? null : Marshal.PtrToStringUTF8(ptr, (int)len);
    }
}
