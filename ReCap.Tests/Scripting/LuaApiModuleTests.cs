using ReCap.Server.Adapters.Scripting;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

public class LuaApiModuleTests
{
    [Fact]
    public void UnknownNamespaceFunctionIsCallableStub()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        rt.Execute(LuaFixtures.Compile("nGameObject.TotallyUnknownFn(1, 2, 3)"), "stubtest");
    }

    [Fact]
    public void NUtilSpidMatchesFnvVector()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        Assert.True(rt.EvalBool(LuaFixtures.Compile(
            "return nUtil.SPID('Affix_EnemyHealthRegen') == nUtil.SPID('affix_enemyhealthregen')")));
    }

    [Fact]
    public void NamespacesExistAfterRegistration()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        Assert.True(rt.EvalBool(LuaFixtures.Compile(
            "return nAbility ~= nil and nGameObject ~= nil and nThread ~= nil and nModifier ~= nil")));
    }

    [Fact]
    public void MathRandomIsNativeOverride()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        Assert.True(rt.EvalBool(LuaFixtures.Compile(
            "local a = math.random() local b = math.random(10) return a >= 0 and a < 1 and b >= 1 and b <= 10")));
    }
}
