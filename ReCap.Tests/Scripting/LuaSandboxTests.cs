using ReCap.Server.Adapters.Scripting;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

public class LuaSandboxTests
{
    private static bool GlobalIsNil(string name)
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        return rt.EvalBool(LuaFixtures.Compile($"return {name} == nil"));
    }

    [Theory]
    [InlineData("debug")]
    [InlineData("loadstring")]
    [InlineData("dofile")]
    [InlineData("loadfile")]
    [InlineData("loadlib")]
    [InlineData("package")]
    [InlineData("module")]
    public void BannedGlobalsAreNil(string name) => Assert.True(GlobalIsNil(name));

    [Theory]
    [InlineData("coroutine.resume")]
    [InlineData("string.format")]
    [InlineData("table.insert")]
    [InlineData("math.floor")]
    [InlineData("pcall")]
    [InlineData("pairs")]
    public void AllowedLibsArePresent(string expr)
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        Assert.True(rt.EvalBool(LuaFixtures.Compile($"return {expr} ~= nil")));
    }

    [Fact]
    public void PrintIsStubbedNotMissing()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        rt.Execute(LuaFixtures.Compile("print('hello from sandbox')"), "printtest");
    }
}
