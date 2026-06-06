using ReCap.Server.Adapters.Scripting;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

internal sealed class FakeBridge : IScriptGameBridge
{
    public bool TryGetPosition(uint id, out float x, out float y, out float z)
    { x = 1.5f; y = 2.5f; z = 3.5f; return id == 10; }
    public float GetHitPoints(uint id) => id == 10 ? 80f : 0f;
    public float GetMaxHitPoints(uint id) => id == 10 ? 100f : 0f;
    public bool ObjectExists(uint id) => id == 10;
    public byte GetTeam(uint id) => 2;
    public uint GetTargetId(uint id) => 77;
}

public class GameBridgeTests
{
    private static LuaRuntime Make()
    {
        var rt = LuaRuntime.CreateSandboxedState();
        ScriptContextRegistry.Get(rt.L)!.GameBridge = new FakeBridge();
        return rt;
    }

    [Fact]
    public void GetPositionReturnsThreeFloats()
    {
        using var rt = Make();
        Assert.True(rt.EvalBool(LuaFixtures.Compile(
            "local x, y, z = nGameObject.GetPosition(10) return x == 1.5 and y == 2.5 and z == 3.5")));
    }

    [Fact]
    public void HitPointsAndAliveFollowContract()
    {
        using var rt = Make();
        Assert.True(rt.EvalBool(LuaFixtures.Compile("""
            return nGameObject.GetHitPoints(10) == 80
               and nGameObject.GetMaxHitPoints(10) == 100
               and nGameObject.IsAlive(10) == true
               and nGameObject.IsAlive(99) == false
               and nGameObject.GetHitPoints(99) == 0
            """)));
    }

    [Fact]
    public void AbilityContextGettersReadInvocation()
    {
        using var rt = Make();
        var ctx = ScriptContextRegistry.Get(rt.L)!;
        ctx.CurrentInvocation = new AbilityInvocation(AgentId: 10, TargetId: 77, CursorX: 4f, CursorY: 5f, CursorZ: 6f, Rank: 2);
        Assert.True(rt.EvalBool(LuaFixtures.Compile("""
            local x, y, z = nAbility.GetTargetPosition()
            return nAbility.GetAgentID() == 10 and nAbility.GetTargetID() == 77 and x == 4 and z == 6
            """)));
    }
}
