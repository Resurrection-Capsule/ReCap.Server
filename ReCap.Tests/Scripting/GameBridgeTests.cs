using ReCap.Server.Adapters.Scripting;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

internal sealed class FakeBridge : IScriptGameBridge
{
    // kAttribute ids: 0=Strength 1=Dexterity 2=Mind 4=MaxHealth (VERIFIED_FACTS C3 addendum).
    public Dictionary<int, float> Attributes { get; } = new() { [0] = 12f, [1] = 8f, [2] = 5f, [4] = 100f };

    public bool TryGetPosition(uint id, out float x, out float y, out float z)
    { x = 1.5f; y = 2.5f; z = 3.5f; return id == 10; }
    public float GetHitPoints(uint id) => id == 10 ? 80f : 0f;
    public float GetMaxHitPoints(uint id) => id == 10 ? 100f : 0f;
    public bool ObjectExists(uint id) => id == 10;
    public byte GetTeam(uint id) => 2;
    public uint GetTargetId(uint id) => 77;
    public bool TryGetAttributeValue(uint id, int attributeId, out float value)
    {
        value = 0f;
        return id == 10 && Attributes.TryGetValue(attributeId, out value);
    }
    public IReadOnlyDictionary<int, float>? GetAttributeTable(uint id) => id == 10 ? Attributes : null;
    public bool TryGetOrientation(uint id, out float x, out float y, out float z, out float w)
    { x = 0f; y = 0f; z = 0f; w = 1f; return id == 10; }
    public List<(uint ObjectId, uint State)> AnimationBroadcasts { get; } = [];
    public void BroadcastAnimationState(uint objectId, uint stateHash) => AnimationBroadcasts.Add((objectId, stateHash));
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
        ctx.SetInvocation(rt.L, new AbilityInvocation(AgentId: 10, TargetId: 77, CursorX: 4f, CursorY: 5f, CursorZ: 6f, Rank: 2));
        Assert.True(rt.EvalBool(LuaFixtures.Compile("""
            local x, y, z = nAbility.GetTargetPosition()
            return nAbility.GetAgentID() == 10 and nAbility.GetTargetID() == 77 and x == 4 and z == 6
            """)));
    }

    [Fact]
    public void AttributeValueReadsConfirmedIdsAndZeroForUnknown()
    {
        using var rt = Make();
        Assert.True(rt.EvalBool(LuaFixtures.Compile("""
            return nAttribute.GetAttributeValue(10, 0) == 12
               and nAttribute.GetAttributeValue(10, 2) == 5
               and nAttribute.GetAttributeValue(10, 99) == 0
               and nAttribute.GetAttributeValue(55, 0) == 0
            """)));
    }

    [Fact]
    public void AttributeSnapshotRoundTripsFrozenValues()
    {
        using var rt = Make();
        Assert.True(rt.EvalBool(LuaFixtures.Compile("""
            local snap = nAbility.GetAgentAttributeSnapshot(10)
            return snap > 0
               and nAttribute.GetAttributeValue_FromSnapshot(snap, 1) == 8
               and nAttribute.GetAttributeValue_FromSnapshot(snap, 4) == 100
               and nAttribute.GetAttributeValue_FromSnapshot(snap, 99) == 0
               and nAttribute.GetAttributeValue_FromSnapshot(12345, 0) == 0
            """)));
    }

    [Fact]
    public void SnapshotFallsBackToInvocationAgent()
    {
        using var rt = Make();
        var ctx = ScriptContextRegistry.Get(rt.L)!;
        ctx.SetInvocation(rt.L, new AbilityInvocation(AgentId: 10, TargetId: 0, CursorX: 0f, CursorY: 0f, CursorZ: 0f, Rank: 1));
        Assert.True(rt.EvalBool(LuaFixtures.Compile("""
            local snap = nAbility.GetAgentAttributeSnapshot()
            return nAttribute.GetAttributeValue_FromSnapshot(snap, 0) == 12
            """)));
    }

    [Fact]
    public void AbilityInstanceAndRangeFlagFollowInvocation()
    {
        using var rt = Make();
        var ctx = ScriptContextRegistry.Get(rt.L)!;
        ctx.SetInvocation(rt.L, new AbilityInvocation(AgentId: 10, TargetId: 77, CursorX: 0f, CursorY: 0f, CursorZ: 0f, Rank: 1,
            AbilityHash: 0xBEEF, InstanceId: 42, TargetInRangeAtStart: true));
        Assert.True(rt.EvalBool(LuaFixtures.Compile("""
            return nAbility.GetAbilityInstanceID() == 42
               and nAbility.TargetInRangeAtStart() == true
            """)));
    }

    [Fact]
    public void VoidNativesReturnZeroValuesAndDebugFlagIsFalse()
    {
        using var rt = Make();
        // Retail arity contract: PayCooldownAndMana / PlayAnimationSequence / nEvent.Notify push
        // NOTHING; IsAbilityDebugEnabled is hardcoded false (@0x009f97b0).
        Assert.True(rt.EvalBool(LuaFixtures.Compile("""
            return select('#', nAbility.PayCooldownAndMana()) == 0
               and select('#', nAbility.PlayAnimationSequence()) == 0
               and select('#', nEvent.Notify({ objectId = 7, eventType = 3 })) == 0
               and nDebug.IsAbilityDebugEnabled() == false
               and nAbility.GetAnimationSequenceIndex() == 0
            """)));
    }
}
