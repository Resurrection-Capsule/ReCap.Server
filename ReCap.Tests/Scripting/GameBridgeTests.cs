using ReCap.Server.Adapters.Scripting;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

internal sealed class FakeBridge : IScriptGameBridge
{
    // kAttribute ids: 0=Strength 1=Dexterity 2=Mind 4=MaxHealth, 101=MinWeaponDamage 102=MaxWeaponDamage.
    public Dictionary<int, float> Attributes { get; } = new()
        { [0] = 12f, [1] = 8f, [2] = 5f, [4] = 100f, [101] = 1f, [102] = 5f };

    public bool TryGetPosition(uint id, out float x, out float y, out float z)
    { x = 1.5f; y = 2.5f; z = 3.5f; return id == 10; }
    public float GetHitPoints(uint id) => id == 10 ? 80f : 0f;
    public float GetMaxHitPoints(uint id) => id == 10 ? 100f : 0f;
    public bool ObjectExists(uint id) => id == 10;
    public byte GetTeam(uint id) => 2;
    public uint GetTargetId(uint id) => 77;
    public bool IsPlayerControlled(uint id) => id == 10;
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

    public float Health = 80f;
    public IReadOnlyList<uint> QueryObjectsInRadius(float x, float y, float z, float radius, bool damageableOnly)
        => radius >= 5f ? [10u, 77u] : [];
    public float ApplyHeal(uint targetId, float amount)
    {
        if (targetId != 10) return 0f;
        var before = Health;
        Health = Math.Clamp(Health + amount, 0f, 100f);
        return Health - before;
    }
    public List<uint> Deleted { get; } = [];
    public void MarkForDelete(uint objectId) => Deleted.Add(objectId);
    public void SetVisible(uint objectId, bool visible) { }
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
    public void ObjectsInRadiusReturnsIpairsReadyTable()
    {
        using var rt = Make();
        Assert.True(rt.EvalBool(LuaFixtures.Compile("""
            local hits = nObjectManager.GetObjectsInRadius(0, 0, 0, 6, { 1, 2 })
            local count, last = 0, 0
            for i, id in ipairs(hits) do count = count + 1 last = id end
            local empty = nObjectManager.GetObjectsInRadius(0, 0, 0, 1)
            return count == 2 and last == 77 and #empty == 0
            """)));
    }

    [Fact]
    public void IsPlayerControlledObjectFollowsBridge()
    {
        using var rt = Make();
        Assert.True(rt.EvalBool(LuaFixtures.Compile(
            "return nPlayer.IsPlayerControlledObject(10) == true and nPlayer.IsPlayerControlledObject(99) == false")));
    }

    [Fact]
    public void GetWeaponDamageReturnsMinMaxTable()
    {
        using var rt = Make();
        // Melee GetDamage path: basic + player-controlled → nGameObject.GetWeaponDamage(agent)
        // returns a {[1]=min,[2]=max} table (disasm template_ability_melee 2026-06-07).
        Assert.True(rt.EvalBool(LuaFixtures.Compile("""
            local w = nGameObject.GetWeaponDamage(10)
            local miss = nGameObject.GetWeaponDamage(99)
            return w[1] == 1 and w[2] == 5 and miss[1] == 0 and miss[2] == 0
            """)));
    }

    [Fact]
    public void TakeDamageRollsTableAndAppliesToTarget()
    {
        using var rt = Make();
        // {5,5} → deterministic roll 5; snapshot handle 0 → no crit. FakeBridge target 10 hp 80→75.
        Assert.True(rt.EvalBool(LuaFixtures.Compile("""
            local hit, dealt, crit = nGameObject.TakeDamage(0, 10, {5, 5}, 0, 0, 0, 0, 1)
            return hit == true and dealt == 5 and crit == false
            """)));
    }

    [Fact]
    public void TakeDamageMissingTargetReturnsNoValues()
    {
        using var rt = Make();
        Assert.True(rt.EvalBool(LuaFixtures.Compile(
            "return select('#', nGameObject.TakeDamage(0, 99, {5, 5})) == 0")));
    }

    [Fact]
    public void TakeDamageNonTableDamageRaisesError()
    {
        using var rt = Make();
        Assert.Throws<LuaScriptException>(() =>
            rt.Execute(LuaFixtures.Compile("nGameObject.TakeDamage(0, 10, 5)"), "td"));
    }

    [Fact]
    public void HealDamageAppliesClampedAndReturnsTwoValues()
    {
        using var rt = Make();
        Assert.True(rt.EvalBool(LuaFixtures.Compile("""
            local healed, crit = nGameObject.HealDamage(0, 10, 50)
            local over = nGameObject.HealDamage(0, 10, 50)
            local damaged = nGameObject.HealDamage(0, 10, -30)
            return healed == 20 and crit == false and over == 0 and damaged == -30
            """)));
    }

    [Fact]
    public void PlayAnimationSequenceRotatesAndBroadcasts()
    {
        using var rt = Make();
        var ctx = ScriptContextRegistry.Get(rt.L)!;
        var bridge = (FakeBridge)ctx.GameBridge!;
        Assert.True(rt.EvalBool(LuaFixtures.Compile("""
            nAbility.RegisterAbility('SeqTest', {
                tick = function() end,
                animationSequence = {
                    { hit = 0.25, animationstate = 111 },
                    { hit = 0.40, animationstate = 222 },
                },
            })
            return true
            """)));
        ctx.SetInvocation(rt.L, new AbilityInvocation(AgentId: 10, TargetId: 0, CursorX: 0f, CursorY: 0f, CursorZ: 0f, Rank: 1,
            AbilityHash: ReCap.Server.Adapters.Scripting.ScriptVfs.Hash("SeqTest"), InstanceId: 1));

        Assert.True(rt.EvalBool(LuaFixtures.Compile("""
            nAbility.PlayAnimationSequence()
            local first = nAbility.GetAnimationSequenceIndex()
            nAbility.PlayAnimationSequence()
            local second = nAbility.GetAnimationSequenceIndex()
            nAbility.PlayAnimationSequence()
            local third = nAbility.GetAnimationSequenceIndex()
            return first == 0 and second == 1 and third == 0
            """)));
        Assert.Equal([(10u, 111u), (10u, 222u), (10u, 111u)], bridge.AnimationBroadcasts);
    }

    [Fact]
    public void CircleIntersectsArcCoversConeHitCases()
    {
        using var rt = Make();
        Assert.True(rt.EvalBool(LuaFixtures.Compile("""
            local inFront  = nMathUtil.CircleIntersectsArc(3, 0, 0.5, 0, 0, 1, 0, 5, math.pi / 2)
            local behind   = nMathUtil.CircleIntersectsArc(-3, 0, 0.5, 0, 0, 1, 0, 5, math.pi / 2)
            local tooFar   = nMathUtil.CircleIntersectsArc(10, 0, 0.5, 0, 0, 1, 0, 5, math.pi / 2)
            local overlap  = nMathUtil.CircleIntersectsArc(0.2, 0, 0.5, 0, 0, -1, 0, 5, math.pi / 2)
            local edgeGraze= nMathUtil.CircleIntersectsArc(0, 3, 2.5, 0, 0, 1, 0, 5, math.pi / 2)
            return inFront == true and behind == false and tooFar == false
               and overlap == true and edgeGraze == true
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
