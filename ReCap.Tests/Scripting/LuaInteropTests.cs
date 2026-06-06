using ReCap.Server.Adapters.Scripting.Native;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

public class LuaInteropTests
{
    [Fact]
    public void LoadsAndRunsCompiledChunk()
    {
        var chunk = LuaFixtures.Compile("return 21 * 2");
        var L = LuaNative.luaL_newstate();
        try
        {
            Assert.Equal(LuaNative.LUA_OK, LuaNative.luaL_loadbuffer(L, chunk, (nuint)chunk.Length, "fixture"));
            Assert.Equal(LuaNative.LUA_OK, LuaNative.lua_pcall(L, 0, 1, 0));
            Assert.Equal(42f, LuaNative.lua_tonumber(L, -1));
        }
        finally { LuaNative.lua_close(L); }
    }

    [Fact]
    public void ChunkHeaderMatchesDarksporeFormat()
    {
        var chunk = LuaFixtures.Compile("return 0");
        Assert.Equal(new byte[] { 0x1B, 0x4C, 0x75, 0x61, 0x51, 0x00, 0x01, 0x04, 0x04, 0x04, 0x04, 0x00 },
                     chunk.Take(12).ToArray());
    }
}
