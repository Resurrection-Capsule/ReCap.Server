using ReCap.Server.Adapters.Scripting;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

public class ScriptRegistryTests
{
    [Fact]
    public void RegisterAbilityStoresTableByFnvName()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        rt.Execute(LuaFixtures.Compile(
            "nAbility.RegisterAbility('TestStrike', { rank = 3, tick = function() end })"), "reg");
        var registry = ScriptContextRegistry.Get(rt.L)!.Registry;
        var entry = registry.Find(ScriptKind.Ability, ScriptVfs.Hash("TestStrike"));
        Assert.NotNull(entry);
        Assert.Equal("TestStrike", entry!.Name);
        Assert.True(entry.HasTick);
    }

    [Fact]
    public void DuplicateRegistrationKeepsFirst()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        rt.Execute(LuaFixtures.Compile(
            "nAffix.RegisterAffix('Dup', { a = 1 }) nAffix.RegisterAffix('Dup', { a = 2 })"), "dup");
        var registry = ScriptContextRegistry.Get(rt.L)!.Registry;
        Assert.Equal(1, registry.Count(ScriptKind.Affix));
    }

    [Fact]
    public void RegisterReturnsZeroValues()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        Assert.True(rt.EvalBool(LuaFixtures.Compile(
            "local r = nModifier.RegisterModifier('M1', {}) return r == nil")));
    }
}
