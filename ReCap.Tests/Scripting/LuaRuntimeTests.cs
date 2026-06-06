using ReCap.Server.Adapters.Scripting;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

public class LuaRuntimeTests
{
    [Fact]
    public void NumbersAreFloat_NotDouble()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        Assert.True(rt.EvalBool(LuaFixtures.Compile("return (2^24 + 1) == 2^24")));
    }

    [Fact]
    public void ScriptErrorSurfacesAsExceptionWithTraceback()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        var ex = Assert.Throws<LuaScriptException>(
            () => rt.Execute(LuaFixtures.Compile("local f = function() error('boom') end f()", strip: false), "errchunk"));
        Assert.Contains("boom", ex.Message);
        Assert.Contains("errchunk", ex.Message);
    }
}
