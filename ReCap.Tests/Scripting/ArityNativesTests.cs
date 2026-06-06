using ReCap.Server.Adapters.Scripting;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

public class ArityNativesTests
{
    [Fact]
    public void PreloadsReturnExactlyOneNumber()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        Assert.True(rt.EvalBool(LuaFixtures.Compile("""
            local a = nAbility.PreloadAsset('x.Noun', 'prop')
            local b = nAbility.PreloadAnimation('x.Noun', 'anim')
            local c = nAbility.PreloadModifier(7, 'mod')
            return type(a) == 'number' and type(b) == 'number' and c == 7
            """)));
    }

    [Fact]
    public void BitOrFoldsRoundedArgs()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        Assert.True(rt.EvalBool(LuaFixtures.Compile("return nBit.Or(1, 2, 4) == 7")));
    }

    [Fact]
    public void GetAssetReturnsStableNumber()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        Assert.True(rt.EvalBool(LuaFixtures.Compile(
            "return nUtil.GetAsset('Thing.Noun') == nUtil.GetAsset('Thing.Noun') and type(nUtil.GetAsset('Thing.Noun')) == 'number'")));
    }
}
