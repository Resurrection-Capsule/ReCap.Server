using ReCap.Server.Adapters.Scripting;
using ReCap.Server.Adapters.Scripting.Native;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

// nAbility cooldown-cluster natives (Ghidra 2026-07-13): Reset/Remove/Scale/AddCooldownTime.
// Retail keys per (ability, initiator, rank) on the object's cooldown component; we model per
// (agent, abilityHash) on the context deadline map and rebroadcast 0xC1 on every mutation.
public class CooldownNativesTests
{
    // --- context deadline-map API (pure) ---

    [Fact]
    public void ClearCooldown_RemovesOnlyThatAbility()
    {
        var ctx = new ScriptStateContext { Registry = new ScriptRegistry() };
        ctx.StampCooldown(2, 0x1234, 5d);
        ctx.StampCooldown(2, 0x5678, 5d);

        ctx.ClearCooldown(2, 0x1234);

        Assert.False(ctx.IsOnCooldown(2, 0x1234, 3d)); // cleared -> ready
        Assert.True(ctx.IsOnCooldown(2, 0x5678, 3d));  // sibling untouched
    }

    [Fact]
    public void CooldownAbilities_ListsEveryAbilityOnObject()
    {
        var ctx = new ScriptStateContext { Registry = new ScriptRegistry() };
        ctx.StampCooldown(2, 0x1111, 5d);
        ctx.StampCooldown(2, 0x2222, 5d);
        ctx.StampCooldown(9, 0x3333, 5d); // different object

        var abilities = ctx.CooldownAbilities(2);

        Assert.Equal(2, abilities.Count);
        Assert.Contains(0x1111u, abilities);
        Assert.Contains(0x2222u, abilities);
        Assert.DoesNotContain(0x3333u, abilities);
    }

    [Fact]
    public void CooldownRemaining_IsDeadlineMinusNowFlooredAtZero()
    {
        var ctx = new ScriptStateContext { Registry = new ScriptRegistry() };
        ctx.StampCooldown(2, 0x1234, readyAtSeconds: 5d);

        Assert.Equal(2d, ctx.CooldownRemaining(2, 0x1234, nowSeconds: 3d));
        Assert.Equal(0d, ctx.CooldownRemaining(2, 0x1234, nowSeconds: 5d)); // already ready
        Assert.Equal(0d, ctx.CooldownRemaining(2, 0x9999, nowSeconds: 0d)); // never stamped
    }

    // --- Lua-driven natives + 0xC1 broadcast ---

    [Fact]
    public void ResetAbilityCooldown_ClearsAndBroadcastsReady()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        var ctx = ScriptContextRegistry.Get(rt.L)!;
        var bridge = new RecordingBridge();
        ctx.GameBridge = bridge;
        ctx.StampCooldown(10, 0x1234, 5d);

        // (agent, abilityId, initiatorId, rank) — retail arity; initiator/rank ignored server-side.
        rt.Execute(LuaFixtures.Compile("nAbility.ResetAbilityCooldown(10, 0x1234, 0, 0)"), "reset");

        Assert.False(ctx.IsOnCooldown(10, 0x1234, 0d));
        Assert.Contains((10u, 0x1234u, 0f), bridge.Cooldowns);
    }

    [Fact]
    public void RemoveCooldownTime_OneArgClearsAll_TwoArgsClearsOne()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        var ctx = ScriptContextRegistry.Get(rt.L)!;
        ctx.GameBridge = new RecordingBridge();

        ctx.StampCooldown(10, 0x1111, 5d);
        ctx.StampCooldown(10, 0x2222, 5d);
        rt.Execute(LuaFixtures.Compile("nAbility.RemoveCooldownTime(10, 0x1111)"), "rm1");
        Assert.False(ctx.IsOnCooldown(10, 0x1111, 0d));
        Assert.True(ctx.IsOnCooldown(10, 0x2222, 0d)); // only the named ability cleared

        rt.Execute(LuaFixtures.Compile("nAbility.RemoveCooldownTime(10)"), "rmall");
        Assert.Empty(ctx.CooldownAbilities(10)); // every cooldown cleared
    }

    [Fact]
    public void ScaleCooldownTime_ScalesRemainingAndRebroadcasts()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        var ctx = ScriptContextRegistry.Get(rt.L)!;
        var bridge = new RecordingBridge();
        ctx.GameBridge = bridge;
        ctx.StampCooldown(10, 0x1234, readyAtSeconds: 4d); // Now is 0 -> 4s remaining

        rt.Execute(LuaFixtures.Compile("nAbility.ScaleCooldownTime(10, 0.5)"), "scale");

        Assert.Equal(2d, ctx.CooldownRemaining(10, 0x1234, 0d));
        Assert.Contains((10u, 0x1234u, 2f), bridge.Cooldowns);
    }

    [Fact]
    public void AddCooldownTime_ExtendsRemaining()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        var ctx = ScriptContextRegistry.Get(rt.L)!;
        var bridge = new RecordingBridge();
        ctx.GameBridge = bridge;
        ctx.StampCooldown(10, 0x1234, readyAtSeconds: 3d); // 3s remaining at Now 0

        rt.Execute(LuaFixtures.Compile("nAbility.AddCooldownTime(10, 0x1234, 2)"), "add");

        Assert.Equal(5d, ctx.CooldownRemaining(10, 0x1234, 0d));
        Assert.Contains((10u, 0x1234u, 5f), bridge.Cooldowns);
    }

    private sealed class RecordingBridge : IScriptGameBridge
    {
        public readonly List<(uint Object, uint Ability, float Seconds)> Cooldowns = [];
        public void SendCooldownUpdate(uint objectId, uint abilityId, float cooldownSeconds)
            => Cooldowns.Add((objectId, abilityId, cooldownSeconds));

        public bool TryGetPosition(uint id, out float x, out float y, out float z) { x = y = z = 0f; return true; }
        public float GetHitPoints(uint id) => 0f;
        public float GetMaxHitPoints(uint id) => 0f;
        public bool ObjectExists(uint id) => true;
        public byte GetTeam(uint id) => 0;
        public void SetTeam(uint id, byte t) { }
        public byte GetPlayerId(uint id) => 0;
        public uint GetTargetId(uint id) => 0;
        public bool IsPlayerControlled(uint id) => false;
        public bool TryGetAttributeValue(uint id, int a, out float v) { v = 0f; return false; }
        public IReadOnlyDictionary<int, float>? GetAttributeTable(uint id) => null;
        public bool TryGetOrientation(uint id, out float x, out float y, out float z, out float w) { x = y = z = 0f; w = 1f; return true; }
        public void BroadcastAnimationState(uint id, uint s) { }
        public void ResetAnimationState(uint id) { }
        public IReadOnlyList<uint> QueryObjectsInRadius(float x, float y, float z, float r, bool d) => [];
        public float ApplyHeal(uint id, float a) => 0f;
        public void MarkForDelete(uint id) { }
        public void SetVisible(uint id, bool v) { }
        public void SetLocomotionGoal(uint id, float x, float y, float z, float stop) { }
        public void SetLocomotionTarget(uint id, float x, float y, float z) { }
        public void SetFacing(uint id, float x, float y, float z) { }
        public void StopLocomotion(uint id) { }
        public void SetNavCollision(uint id, bool collidable) { }
        public float GetModifiedMoveSpeed(uint id) => 0f;
        public bool TryGetGoalDistance(uint id, out float d) { d = 0f; return false; }
        public uint AddAttributeModifier(uint id, int a, float v) => 0;
        public uint EmitEffect(uint id, uint fx, uint init) => 0;
        public void EmitServerEvent(uint fx, uint id, uint attacker, bool critical, System.Numerics.Vector3? position, System.Numerics.Vector3? facing) { }
        public uint CreateObject(uint n, float x, float y, float z) => 0;
        public void BroadcastCombatEvent(uint target, uint source, float delta, int hp, ushort flags) { }
        public uint CreateModifier(uint targetId, uint casterId, uint modifierGuid, int rank) => 0;
        public bool RemoveModifier(uint instanceId) => false;
        public uint FindModifierByGuid(uint targetId, uint modifierGuid) => 0;
        public int GetModifierStackCount(uint instanceId) => 0;
        public int IncrementModifierStack(uint instanceId) => 0;
        public void ResetModifierDuration(uint instanceId) { }
        public void DispatchTookDamage(uint targetId, uint attackerId, float amount, int descriptors) { }
        public void DispatchDealtDamage(uint attackerId, uint targetId) { }
        public IReadOnlyList<uint> GetAggroTargets(uint a) => [];
        public bool HasAggroTargets(uint a) => false;
        public uint GetBestTarget(uint a) => 0;
        public bool InPerceptionCircle(uint a, float x, float y, float z, float o) => false;
        public void CastAiAbility(string abilityName, uint self, uint target) { }
        public bool EvaluateAiCondition(string conditionName, IReadOnlyList<(string Name, string Value)> props, uint self, uint target) => false;
    }
}
