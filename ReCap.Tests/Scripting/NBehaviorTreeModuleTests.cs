using ReCap.Server.Adapters.Scripting;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

public class NBehaviorTreeModuleTests
{
    private static LuaRuntime Make()
    {
        var rt = LuaRuntime.CreateSandboxedState();
        ScriptContextRegistry.Get(rt.L)!.GameBridge = new FakeBridge();
        return rt;
    }

    [Fact]
    public void GetMyObjectID_ReturnsCurrentInvocationSelfAndTarget()
    {
        using var rt = Make();
        var ctx = ScriptContextRegistry.Get(rt.L)!;
        ctx.SetInvocation(rt.L, new AbilityInvocation(AgentId: 42, TargetId: 7, CursorX: 0f, CursorY: 0f, CursorZ: 0f, Rank: 1));
        Assert.True(rt.EvalBool(LuaFixtures.Compile("""
            return nBehaviorTree.GetMyObjectID() == 42 and nBehaviorTree.GetTargetObjectID() == 7
            """)));
    }
}
