using ReCap.Server.Adapters.Scripting;
using ReCap.Server.Domain.Gameplay.AI;

namespace ReCap.Tests.Gameplay.AI;

public class BridgeAiActionsTests
{
    private sealed class RecordingBridge : IScriptGameBridge
    {
        public List<(string Name, uint Self, uint Target)> AiCasts { get; } = [];
        public void CastAiAbility(string abilityName, uint self, uint target) => AiCasts.Add((abilityName, self, target));
        public bool EvaluateAiCondition(string conditionName, IReadOnlyList<(string Name, string Value)> props, uint self, uint target) => false;

        public bool TryGetPosition(uint id, out float x, out float y, out float z) { x = y = z = 0f; return false; }
        public float GetHitPoints(uint id) => 0f;
        public float GetMaxHitPoints(uint id) => 0f;
        public bool ObjectExists(uint id) => false;
        public byte GetTeam(uint id) => 0;
        public void SetTeam(uint id, byte team) { }
        public byte GetPlayerId(uint id) => 0;
        public uint GetTargetId(uint id) => 0;
        public bool IsPlayerControlled(uint id) => false;
        public bool TryGetAttributeValue(uint id, int attributeId, out float value) { value = 0f; return false; }
        public IReadOnlyDictionary<int, float>? GetAttributeTable(uint id) => null;
        public bool TryGetOrientation(uint id, out float x, out float y, out float z, out float w) { x = y = z = 0f; w = 1f; return false; }
        public void BroadcastAnimationState(uint id, uint stateHash) { }
        public void ResetAnimationState(uint id) { }
        public IReadOnlyList<uint> QueryObjectsInRadius(float x, float y, float z, float radius, bool damageableOnly) => [];
        public float ApplyHeal(uint id, float amount) => 0f;
        public void MarkForDelete(uint id) { }
        public void SetVisible(uint id, bool visible) { }
        public void SetLocomotionGoal(uint id, float x, float y, float z, float stopDistance) { }
        public void SetLocomotionTarget(uint id, float x, float y, float z) { }
        public void SetFacing(uint id, float x, float y, float z) { }
        public void StopLocomotion(uint id) { }
        public void SetNavCollision(uint id, bool collidable) { }
        public float GetModifiedMoveSpeed(uint id) => 0f;
        public bool TryGetGoalDistance(uint id, out float distance) { distance = 0f; return false; }
        public uint AddAttributeModifier(uint id, int attributeId, float value) => 0;
        public uint EmitEffect(uint id, uint serverEventDef, uint initiatorId) => 0;
        public uint CreateObject(uint nounId, float x, float y, float z) => 0;
        public void BroadcastCombatEvent(uint targetId, uint sourceId, float deltaHealth, int integerHpChange, ushort flags) { }
        public IReadOnlyList<uint> GetAggroTargets(uint agentId) => [];
        public bool HasAggroTargets(uint agentId) => false;
        public uint GetBestTarget(uint agentId) => 0;
        public bool InPerceptionCircle(uint agentId, float x, float y, float z, float offset) => false;
    }

    [Fact]
    public void CastAbility_ForwardsNameSelfTarget_ToBridge()
    {
        var bridge = new RecordingBridge();
        var actions = new BridgeAiActions(bridge);

        actions.CastAbility("X", 2, 1);

        Assert.Single(bridge.AiCasts);
        Assert.Equal(("X", 2u, 1u), bridge.AiCasts[0]);
    }

    [Fact]
    public void ConditionPasses_DelegatesToBridge_ReturnsFalseForSlice()
    {
        var bridge = new RecordingBridge();
        var actions = new BridgeAiActions(bridge);

        var result = actions.ConditionPasses("Distance", [], 2, 1);

        Assert.False(result);
    }
}
