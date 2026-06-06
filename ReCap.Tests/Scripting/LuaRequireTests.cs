using ReCap.Server.Adapters.Scripting;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

public class LuaRequireTests
{
    [Fact]
    public void RequireLoadsThroughResolverOnce()
    {
        var loads = 0;
        var dep = LuaFixtures.Compile("DepLoaded = (DepLoaded or 0) + 1");
        using var rt = LuaRuntime.CreateSandboxedState(name =>
        {
            Assert.Equal("Lua!Dep.lua", name);
            loads++;
            return dep;
        });
        rt.Execute(LuaFixtures.Compile("require('Lua!Dep.lua') require('Lua!Dep.lua')"), "main");
        Assert.Equal(1, loads);
        Assert.True(rt.EvalBool(LuaFixtures.Compile("return DepLoaded == 1")));
    }

    [Fact]
    public void RequireUnknownChunkRaisesLuaError()
    {
        using var rt = LuaRuntime.CreateSandboxedState(_ => null);
        Assert.Throws<LuaScriptException>(
            () => rt.Execute(LuaFixtures.Compile("require('Lua!Missing.lua')"), "main"));
    }

    [Fact]
    public void RequireWithoutResolverRaisesLuaError()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        Assert.Throws<LuaScriptException>(
            () => rt.Execute(LuaFixtures.Compile("require('Lua!Anything.lua')"), "main"));
    }
}
