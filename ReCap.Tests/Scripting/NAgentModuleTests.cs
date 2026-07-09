using ReCap.Server.Adapters.Scripting;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

public class NAgentModuleTests
{
    private static LuaRuntime Make()
    {
        var rt = LuaRuntime.CreateSandboxedState();
        ScriptContextRegistry.Get(rt.L)!.GameBridge = new FakeBridge();
        return rt;
    }

    [Fact]
    public void GetBestTarget_ReturnsBridgeValue()
    {
        using var rt = Make();
        Assert.True(rt.EvalBool(LuaFixtures.Compile("return nAgent.GetBestTarget(10) == 55")));
    }

    [Fact]
    public void HasTargetsOnAggroList_ReflectsBridge()
    {
        using var rt = Make();
        Assert.True(rt.EvalBool(LuaFixtures.Compile("""
            return nAgent.HasTargetsOnAggroList(10) == true
               and nAgent.HasTargetsOnAggroList(999) == false
            """)));
    }

    [Fact]
    public void GetTargetsOnAggroList_ReturnsIpairsReadyTable()
    {
        using var rt = Make();
        Assert.True(rt.EvalBool(LuaFixtures.Compile("""
            local hits = nAgent.GetTargetsOnAggroList(10)
            local count, last = 0, 0
            for i, id in ipairs(hits) do count = count + 1 last = id end
            local empty = nAgent.GetTargetsOnAggroList(999)
            return count == 1 and last == 55 and #empty == 0
            """)));
    }

    [Fact]
    public void InPerceptionCircle_FollowsBridge()
    {
        using var rt = Make();
        Assert.True(rt.EvalBool(LuaFixtures.Compile("""
            return nAgent.InPerceptionCircle(10, 0, 0, 0, 0) == true
               and nAgent.InPerceptionCircle(999, 0, 0, 0, 0) == false
            """)));
    }
}
