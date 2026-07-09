# AI Director — Faithful Vertical Slice — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make one pre-spawned Dungeon enemy aggro, pursue, and cast its ability at the hero — driven by its own `AIDefinition` gambit graph and the Ghidra-verified aggro/perception model — reusing the existing ability-cast, locomotion, and CombatEvent paths.

**Architecture:** Add a C# `AgentBlackboard` (aggro list + perception) to AI-agent `GameObject`s, mirroring `cAgentBlackboard`@obj+0x2b0. An `AggroSystem` ticked from `ObjectManager.Update` perceives hostiles and populates the aggro list; an `AIController` walks the enemy's `AIDefinition` node graph (condition→transition, gambit condition→ability) and acts through existing `InvokeAbility` (attack) + locomotion (move). The `nAgent`/`nBehaviorTree`/`nCondition` Lua namespaces (today empty stubs) bind to this data.

**Tech Stack:** C# / net10.0, xUnit, native Lua 5.1.4 (P/Invoke, `delegate* unmanaged[Cdecl]`), `AssetData.Parser.Core` (AIDefinition schema), existing `ScriptContext`/`IScriptGameBridge`.

## Global Constraints

- Wire is **little-endian** (`[[endianness-wrapper-is-LE]]`); reflection/packet code already handles this — this slice sends no new packets (reuses 0x9B/0xBA/0x95/0x8E).
- **No hardcoded reflection schemas** — any `AssetData::`/`AssetType::` layout comes from `AssetData.Parser` (`new AssetParser().Structs[name]`), never a hand-written field list.
- **No comments** except a short cite when a wire/offset layout is non-obvious and Ghidra-verified (`file:line`/address).
- Follow existing native-module pattern: `public static unsafe class NXModule { Register(nint L){ LuaApiModule.RegisterNamespace(L,"nX",(name,(nint)(delegate* unmanaged[Cdecl]<nint,int>)&Fn)…); } }`; every native is `[UnmanagedCallersOnly(CallConvs=[typeof(CallConvCdecl)])] private static int Fn(nint L)` and wraps its body in try/catch returning a safe default.
- **Model tiering:** mechanical lookups/tests can use Haiku; keep Ghidra/design reasoning on the main model.
- `ObjectManager._objects` is a `ConcurrentDictionary` (spawns arrive on the RakNet thread, AI ticks on the game-loop thread) — never enumerate-and-mutate it in one pass.

---

## File Structure

- Create `ReCap.Server/Domain/Gameplay/AI/AgentBlackboard.cs` — aggro list + perception state (pure domain data, no deps).
- Create `ReCap.Server/Domain/Gameplay/AI/AggroSystem.cs` — perception→aggro population + prune + best-target, over `ObjectManager`.
- Create `ReCap.Server/Domain/Gameplay/AI/AIController.cs` — per-agent `AIDefinition` graph walker.
- Modify `ReCap.Server/Domain/Gameplay/ObjectManager.cs` — add `AgentBlackboard? Agent` to `GameObject`; create it for AI agents in `Spawn`; tick `AggroSystem` in `Update`.
- Create `ReCap.Server/Adapters/Scripting/Api/NAgentModule.cs` — `nAgent` natives.
- Create `ReCap.Server/Adapters/Scripting/Api/NBehaviorTreeModule.cs` — `nBehaviorTree` natives.
- Modify `ReCap.Server/Adapters/Scripting/Api/StubNamespaces.cs` — replace the empty `nAgent`/`nBehaviorTree` registrations with the real modules.
- Modify `ReCap.Server/Adapters/Scripting/ScriptContextRegistry.cs` (interface) + `Services/Scripting/GameScriptContext.cs` — bridge methods the natives need.
- Create tests under `ReCap.Tests/Gameplay/AI/` and `ReCap.Tests/Scripting/`.

---

## Task 1: AgentBlackboard domain model

**Files:**
- Create: `ReCap.Server/Domain/Gameplay/AI/AgentBlackboard.cs`
- Test: `ReCap.Tests/Gameplay/AI/AgentBlackboardTests.cs`

**Interfaces:**
- Produces: `AgentBlackboard` with `float PerceptionRadius {get;set;}`, `bool Aggroed {get;set;}`, `IReadOnlyList<AggroEntry> AggroList {get;}`, `void AddAggro(uint objectId, float threat=0f)`, `void RemoveAggro(uint objectId)`, `bool HasTargets {get;}`, `uint GetBestTarget(Func<uint,bool> isValid)`; `readonly record struct AggroEntry(uint ObjectId, float Threat)`.

- [ ] **Step 1: Write the failing test**

```csharp
using System.Numerics;
using ReCap.Server.Domain.Gameplay.AI;

namespace ReCap.Tests.Gameplay.AI;

public class AgentBlackboardTests
{
    [Fact]
    public void AddAggro_IsIdempotentPerObject_AndAccumulatesThreat()
    {
        var bb = new AgentBlackboard();
        bb.AddAggro(10, 5f);
        bb.AddAggro(10, 3f);
        bb.AddAggro(20, 1f);
        Assert.Equal(2, bb.AggroList.Count);
        Assert.Equal(8f, bb.AggroList.Single(e => e.ObjectId == 10).Threat);
        Assert.True(bb.HasTargets);
    }

    [Fact]
    public void GetBestTarget_ReturnsFirstValidInListOrder()
    {
        var bb = new AgentBlackboard();
        bb.AddAggro(10);
        bb.AddAggro(20);
        // 10 is invalid (dead/gone) -> best target is the next valid, 20.
        Assert.Equal(20u, bb.GetBestTarget(id => id == 20));
        Assert.Equal(0u, bb.GetBestTarget(_ => false));
    }

    [Fact]
    public void RemoveAggro_DropsTarget()
    {
        var bb = new AgentBlackboard();
        bb.AddAggro(10);
        bb.RemoveAggro(10);
        Assert.False(bb.HasTargets);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~AgentBlackboardTests"`
Expected: FAIL — `AgentBlackboard` does not exist (compile error).

- [ ] **Step 3: Write minimal implementation**

```csharp
namespace ReCap.Server.Domain.Gameplay.AI;

// Mirrors the client cAgentBlackboard (GameObject+0x2b0, Ghidra 2026-07-09): the aggro list
// (+0x228/0x22c, pre-prioritized) + per-object perception radius. GetBestTarget returns the first
// still-valid entry in list order (client nAgent::SelectFirstValidAggroTarget @0x009e8dd0), NOT a
// nearest-scan. Present only on AI-agent objects; absence == the obj+0x2b0==0 "not an agent" gate.
public sealed class AgentBlackboard
{
    private readonly List<AggroEntry> _aggro = new();

    public float PerceptionRadius { get; set; }
    public bool Aggroed { get; set; }

    public IReadOnlyList<AggroEntry> AggroList => _aggro;
    public bool HasTargets => _aggro.Count > 0;

    public void AddAggro(uint objectId, float threat = 0f)
    {
        var i = _aggro.FindIndex(e => e.ObjectId == objectId);
        if (i >= 0) _aggro[i] = _aggro[i] with { Threat = _aggro[i].Threat + threat };
        else _aggro.Add(new AggroEntry(objectId, threat));
    }

    public void RemoveAggro(uint objectId) => _aggro.RemoveAll(e => e.ObjectId == objectId);

    public uint GetBestTarget(Func<uint, bool> isValid)
    {
        foreach (var entry in _aggro)
            if (isValid(entry.ObjectId)) return entry.ObjectId;
        return 0;
    }
}

public readonly record struct AggroEntry(uint ObjectId, float Threat);
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~AgentBlackboardTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add ReCap.Server/Domain/Gameplay/AI/AgentBlackboard.cs ReCap.Tests/Gameplay/AI/AgentBlackboardTests.cs
git commit -m "feat(ai): AgentBlackboard aggro-list model (Ghidra cAgentBlackboard contract)"
```

---

## Task 2: Attach AgentBlackboard to AI-agent GameObjects on spawn

**Files:**
- Modify: `ReCap.Server/Domain/Gameplay/ObjectManager.cs` (add `AgentBlackboard? Agent` to `GameObject`; create it in `Spawn` for non-player nouns whose `AIDefinition` resolved)
- Test: `ReCap.Tests/Gameplay/AI/AgentSpawnTests.cs`

**Interfaces:**
- Consumes: `AgentBlackboard` (Task 1); existing `ObjectManager.Spawn(uint,uint,Vector3,float,byte,bool)`, `GameObject.AIDefinition`.
- Produces: `GameObject.Agent` (`AgentBlackboard?`, non-null iff the object is an AI agent); `const float GameObject.DefaultPerceptionRadius = 15f`.

- [ ] **Step 1: Write the failing test**

```csharp
using System.Numerics;
using ReCap.Server.Domain.Gameplay;

namespace ReCap.Tests.Gameplay.AI;

public class AgentSpawnTests
{
    [Fact]
    public void PlayerControlled_GetsNoAgentBlackboard()
    {
        var om = new ObjectManager(null);
        var hero = om.Spawn(1, 100, Vector3.Zero, 1f, team: 1, playerControlled: true);
        Assert.Null(hero.Agent);
    }

    [Fact]
    public void NpcWithoutResolvedAiDefinition_GetsNoAgentBlackboard()
    {
        var om = new ObjectManager(null); // null db -> AIDefinition stays null
        var npc = om.Spawn(2, 200, Vector3.Zero, 1f, team: 2, playerControlled: false);
        Assert.Null(npc.Agent);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~AgentSpawnTests"`
Expected: FAIL — `GameObject.Agent` does not exist.

- [ ] **Step 3: Write minimal implementation**

In `ObjectManager.cs`, add to `GameObject` (near the locomotion fields):

```csharp
    public const float DefaultPerceptionRadius = 15f;
    // Non-null iff this object is an AI agent (mirrors cAgentBlackboard at client obj+0x2b0).
    public AI.AgentBlackboard? Agent { get; set; }
```

Add `using ReCap.Server.Domain.Gameplay.AI;` is unnecessary (same root namespace); reference as `AI.AgentBlackboard`. In `Spawn`, immediately before `_objects[objectId] = obj;`, insert:

```csharp
        // AI agent = non-player noun with a resolved AIDefinition (the client's obj+0x2b0 gate).
        if (!playerControlled && aiDef is not null)
            obj.Agent = new AI.AgentBlackboard { PerceptionRadius = GameObject.DefaultPerceptionRadius };
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~AgentSpawnTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add ReCap.Server/Domain/Gameplay/ObjectManager.cs ReCap.Tests/Gameplay/AI/AgentSpawnTests.cs
git commit -m "feat(ai): attach AgentBlackboard to AI-agent objects on spawn"
```

---

## Task 3: AggroSystem — perception, population, prune, best-target

**Files:**
- Create: `ReCap.Server/Domain/Gameplay/AI/AggroSystem.cs`
- Test: `ReCap.Tests/Gameplay/AI/AggroSystemTests.cs`

**Interfaces:**
- Consumes: `ObjectManager` (`Objects` dict, `GameObject.Agent/Position/Team/PlayerControlled/Dead`), `AgentBlackboard` (Task 1).
- Produces: `AggroSystem(ObjectManager om)` with `void Tick()`; static `bool InPerceptionCircle(Vector3 agentPos, Vector3 point, float radius, float offset = 0f)`; `uint BestTargetFor(GameObject agent)`.

- [ ] **Step 1: Write the failing test**

```csharp
using System.Numerics;
using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Domain.Gameplay.AI;

namespace ReCap.Tests.Gameplay.AI;

public class AggroSystemTests
{
    [Fact]
    public void InPerceptionCircle_MatchesGhidraSemantics()
    {
        // point within radius+offset of agent -> true (client InPerceptionCircle @0x00a059a0).
        Assert.True(AggroSystem.InPerceptionCircle(Vector3.Zero, new Vector3(3, 0, 4), radius: 5f));
        Assert.False(AggroSystem.InPerceptionCircle(Vector3.Zero, new Vector3(3, 0, 4), radius: 4f));
        Assert.True(AggroSystem.InPerceptionCircle(Vector3.Zero, new Vector3(3, 0, 4), radius: 4f, offset: 1f));
    }

    [Fact]
    public void Tick_AggrosHostilePlayerInPerception_AndPrunesWhenGone()
    {
        var om = new ObjectManager(null);
        var enemy = om.Spawn(2, 200, Vector3.Zero, 1f, team: 2, playerControlled: false);
        enemy.Agent = new AgentBlackboard { PerceptionRadius = 15f };
        var hero = om.Spawn(1, 100, new Vector3(5, 0, 0), 1f, team: 1, playerControlled: true);
        hero.Health = hero.MaxHealth = 100f;

        var sys = new AggroSystem(om);
        sys.Tick();
        Assert.Equal(1u, enemy.Agent.GetBestTarget(id => om.Objects.ContainsKey(id)));

        hero.Position = new Vector3(100, 0, 0); // out of perception
        sys.Tick();
        Assert.False(enemy.Agent.HasTargets);
    }

    [Fact]
    public void Tick_IgnoresSameTeamAndNonPlayers()
    {
        var om = new ObjectManager(null);
        var enemy = om.Spawn(2, 200, Vector3.Zero, 1f, team: 2, playerControlled: false);
        enemy.Agent = new AgentBlackboard { PerceptionRadius = 15f };
        om.Spawn(3, 200, new Vector3(1, 0, 0), 1f, team: 2, playerControlled: false); // ally npc, close
        var sys = new AggroSystem(om);
        sys.Tick();
        Assert.False(enemy.Agent.HasTargets);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~AggroSystemTests"`
Expected: FAIL — `AggroSystem` does not exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
using System.Numerics;

namespace ReCap.Server.Domain.Gameplay.AI;

// Server-side aggro/perception, ticked once per game frame from ObjectManager.Update. Mirrors the
// client's per-agent perception + aggro-list maintenance (nAgent::InPerceptionCircle @0x00a059a0,
// AddAggroForObject @0x009fd690). Slice rule: a hostile player-controlled object inside an agent's
// perception circle is on its aggro list; targets that die or leave perception are pruned.
public sealed class AggroSystem
{
    private readonly ObjectManager _om;
    public AggroSystem(ObjectManager om) => _om = om;

    public static bool InPerceptionCircle(Vector3 agentPos, Vector3 point, float radius, float offset = 0f)
        => radius >= Vector3.Distance(agentPos, point) - offset;

    public bool IsValidTarget(uint id)
        => _om.Objects.TryGetValue(id, out var o) && !o.Dead && o.Health > 0f;

    public uint BestTargetFor(GameObject agent)
        => agent.Agent is null ? 0u : agent.Agent.GetBestTarget(IsValidTarget);

    public void Tick()
    {
        foreach (var agent in _om.Objects.Values)
        {
            var bb = agent.Agent;
            if (bb is null || agent.Dead) continue;

            foreach (var other in _om.Objects.Values)
            {
                if (!other.PlayerControlled || other.Dead || other.Team == agent.Team) continue;
                if (InPerceptionCircle(agent.Position, other.Position, bb.PerceptionRadius))
                    bb.AddAggro(other.ObjectId);
            }

            foreach (var entry in bb.AggroList.ToArray())
            {
                if (!_om.Objects.TryGetValue(entry.ObjectId, out var t) || t.Dead ||
                    !InPerceptionCircle(agent.Position, t.Position, bb.PerceptionRadius))
                    bb.RemoveAggro(entry.ObjectId);
            }
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~AggroSystemTests"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add ReCap.Server/Domain/Gameplay/AI/AggroSystem.cs ReCap.Tests/Gameplay/AI/AggroSystemTests.cs
git commit -m "feat(ai): AggroSystem perception + aggro population/prune"
```

---

## Task 4: Bridge aggro/perception methods + nAgent natives

**Files:**
- Modify: `ReCap.Server/Adapters/Scripting/ScriptContextRegistry.cs` (interface `IScriptGameBridge`)
- Modify: `ReCap.Server/Services/Scripting/GameScriptContext.cs` (implement)
- Create: `ReCap.Server/Adapters/Scripting/Api/NAgentModule.cs`
- Modify: `ReCap.Server/Adapters/Scripting/Api/StubNamespaces.cs` (register real nAgent)
- Modify test fakes: `ReCap.Tests/Scripting/GameBridgeTests.cs` (`FakeBridge`), `ReCap.Tests/Scripting/SchedulerPredicateTests.cs` (`MutableHpBridge`)
- Test: `ReCap.Tests/Scripting/NAgentModuleTests.cs`

**Interfaces:**
- Consumes: `AggroSystem` (Task 3), `IScriptGameBridge`, existing native-module pattern.
- Produces on `IScriptGameBridge`: `IReadOnlyList<uint> GetAggroTargets(uint agentId)`, `bool HasAggroTargets(uint agentId)`, `uint GetBestTarget(uint agentId)`, `bool InPerceptionCircle(uint agentId, float x, float y, float z, float offset)`.

- [ ] **Step 1: Write the failing test**

```csharp
using ReCap.Server.Adapters.Scripting.Native;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

public class NAgentModuleTests
{
    [Fact]
    public void GetBestTarget_ReturnsBridgeValue()
    {
        using var lua = LuaTestHost.Create(new FakeBridge()); // agentId 10 -> best target 55 (fake)
        Assert.True(lua.EvalBool("return nAgent.GetBestTarget(10) == 55"));
    }

    [Fact]
    public void HasTargetsOnAggroList_ReflectsBridge()
    {
        using var lua = LuaTestHost.Create(new FakeBridge());
        Assert.True(lua.EvalBool("return nAgent.HasTargetsOnAggroList(10)"));
        Assert.True(lua.EvalBool("return nAgent.HasTargetsOnAggroList(999) == false"));
    }
}
```

> **Note on `LuaTestHost`:** reuse the existing Lua test harness used by `GameBridgeTests` (it constructs a `ScriptContext` bound to a bridge and runs a Lua string). If the existing harness has a different entry point, match it — read `ReCap.Tests/Scripting/GameBridgeTests.cs` top for the exact helper and mirror it here. Do not invent a new harness.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~NAgentModuleTests"`
Expected: FAIL — `nAgent.GetBestTarget` is nil (namespace empty) / bridge methods missing (compile error in fakes).

- [ ] **Step 3: Write minimal implementation**

Add to `IScriptGameBridge` (after `BroadcastCombatEvent`):

```csharp
    IReadOnlyList<uint> GetAggroTargets(uint agentId);
    bool HasAggroTargets(uint agentId);
    uint GetBestTarget(uint agentId);
    bool InPerceptionCircle(uint agentId, float x, float y, float z, float offset);
```

Implement in `GameScriptContext` (needs an `AggroSystem` — construct one over `_game.Objects`; add a field `private readonly AI.AggroSystem _aggro;` initialized in the ctor as `_aggro = new AI.AggroSystem(_game.Objects);` — adjust to the ctor's actual `_game` field):

```csharp
    public IReadOnlyList<uint> GetAggroTargets(uint agentId)
        => _game.Objects.Objects.TryGetValue(agentId, out var o) && o.Agent is { } bb
            ? bb.AggroList.Select(e => e.ObjectId).ToList() : [];

    public bool HasAggroTargets(uint agentId)
        => _game.Objects.Objects.TryGetValue(agentId, out var o) && o.Agent is { HasTargets: true };

    public uint GetBestTarget(uint agentId)
        => _game.Objects.Objects.TryGetValue(agentId, out var o) ? _aggro.BestTargetFor(o) : 0u;

    public bool InPerceptionCircle(uint agentId, float x, float y, float z, float offset)
        => _game.Objects.Objects.TryGetValue(agentId, out var o) && o.Agent is { } bb
           && AI.AggroSystem.InPerceptionCircle(o.Position, new System.Numerics.Vector3(x, y, z), bb.PerceptionRadius, offset);
```

Create `NAgentModule.cs` (mirror `NGameObjectModule` structure; client contract: `nAgent` @0x00a05a90 — GetTargetsOnAggroList/HasTargetsOnAggroList/GetBestTarget/InPerceptionCircle):

```csharp
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

public static unsafe class NAgentModule
{
    public static void Register(nint L)
    {
        LuaApiModule.RegisterNamespace(L, "nAgent",
            ("GetTargetsOnAggroList", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetTargetsOnAggroList),
            ("HasTargetsOnAggroList", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&HasTargetsOnAggroList),
            ("GetBestTarget", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetBestTarget),
            ("InPerceptionCircle", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&InPerceptionCircle));
    }

    private static uint ReadId(nint L, int idx) => (uint)Math.Round((double)LuaNative.lua_tonumber(L, idx));

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetTargetsOnAggroList(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            var ids = bridge is not null && LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER
                ? bridge.GetAggroTargets(ReadId(L, 1)) : [];
            LuaNative.lua_createtable(L, ids.Count, 0);
            var table = LuaNative.lua_gettop(L);
            for (int i = 0; i < ids.Count; i++)
            {
                LuaNative.lua_pushnumber(L, i + 1);
                LuaNative.lua_pushnumber(L, ids[i]);
                LuaNative.lua_rawset(L, table);
            }
            return 1;
        }
        catch { LuaNative.lua_createtable(L, 0, 0); return 1; }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int HasTargetsOnAggroList(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            bool has = bridge is not null && LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER && bridge.HasAggroTargets(ReadId(L, 1));
            LuaNative.lua_pushboolean(L, has ? 1 : 0);
            return 1;
        }
        catch { LuaNative.lua_pushboolean(L, 0); return 1; }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetBestTarget(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            uint best = bridge is not null && LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER ? bridge.GetBestTarget(ReadId(L, 1)) : 0u;
            LuaNative.lua_pushnumber(L, best);
            return 1;
        }
        catch { LuaNative.lua_pushnumber(L, 0); return 1; }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int InPerceptionCircle(nint L)
    {
        try
        {
            var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
            bool inside = false;
            if (bridge is not null && LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER)
            {
                float x = (float)LuaNative.lua_tonumber(L, 2), y = (float)LuaNative.lua_tonumber(L, 3), z = (float)LuaNative.lua_tonumber(L, 4);
                float offset = LuaNative.lua_gettop(L) >= 5 ? (float)LuaNative.lua_tonumber(L, 5) : 0f;
                inside = bridge.InPerceptionCircle(ReadId(L, 1), x, y, z, offset);
            }
            LuaNative.lua_pushboolean(L, inside ? 1 : 0);
            return 1;
        }
        catch { LuaNative.lua_pushboolean(L, 0); return 1; }
    }
}
```

In `StubNamespaces.cs`, replace `LuaApiModule.RegisterNamespace(L, "nAgent");` with `NAgentModule.Register(L);`.

Add to both test fakes (`FakeBridge`, `MutableHpBridge`) — for `FakeBridge` make agent 10 have targets and best 55:

```csharp
    // FakeBridge:
    public IReadOnlyList<uint> GetAggroTargets(uint agentId) => agentId == 10 ? new uint[] { 55 } : System.Array.Empty<uint>();
    public bool HasAggroTargets(uint agentId) => agentId == 10;
    public uint GetBestTarget(uint agentId) => agentId == 10 ? 55u : 0u;
    public bool InPerceptionCircle(uint agentId, float x, float y, float z, float offset) => agentId == 10;
```

```csharp
    // MutableHpBridge (SchedulerPredicateTests):
    public IReadOnlyList<uint> GetAggroTargets(uint a) => System.Array.Empty<uint>();
    public bool HasAggroTargets(uint a) => false;
    public uint GetBestTarget(uint a) => 0;
    public bool InPerceptionCircle(uint a, float x, float y, float z, float o) => false;
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~NAgentModuleTests"`
Expected: PASS. Then run the whole suite to confirm the fakes compile: `dotnet test ReCap.Tests/ReCap.Tests.csproj` — expect the same 1 pre-existing RED (`EnemyNounCombatDataTests`) and everything else green.

- [ ] **Step 5: Commit**

```bash
git add ReCap.Server/Adapters/Scripting/Api/NAgentModule.cs ReCap.Server/Adapters/Scripting/Api/StubNamespaces.cs ReCap.Server/Adapters/Scripting/ScriptContextRegistry.cs ReCap.Server/Services/Scripting/GameScriptContext.cs ReCap.Tests/Scripting/NAgentModuleTests.cs ReCap.Tests/Scripting/GameBridgeTests.cs ReCap.Tests/Scripting/SchedulerPredicateTests.cs
git commit -m "feat(ai): nAgent natives (aggro list + perception) over AggroSystem"
```

---

## Task 5: nBehaviorTree natives (self/target context)

**Files:**
- Create: `ReCap.Server/Adapters/Scripting/Api/NBehaviorTreeModule.cs`
- Modify: `ReCap.Server/Adapters/Scripting/Api/StubNamespaces.cs` (register real nBehaviorTree)
- Test: `ReCap.Tests/Scripting/NBehaviorTreeModuleTests.cs`

**Interfaces:**
- Consumes: the ability/behavior invocation context (`ScriptStateContext` / `AbilityInvocation` — the current `AgentId`/`TargetId` the tick is running for). Client contract: `GetMyObjectID` @0x009fad20, `GetTargetObjectID` @0x009fad60 read `Simulation::GetThreadContext` self/target.
- Produces: `nBehaviorTree.GetMyObjectID()` → current self object id; `nBehaviorTree.GetTargetObjectID()` → current target id.

- [ ] **Step 1: Write the failing test**

```csharp
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

public class NBehaviorTreeModuleTests
{
    [Fact]
    public void GetMyObjectID_ReturnsCurrentBehaviorSelf()
    {
        using var lua = LuaTestHost.Create(new FakeBridge());
        lua.SetBehaviorContext(self: 42, target: 7); // mirror how ability invocation sets AgentId/TargetId
        Assert.True(lua.EvalBool("return nBehaviorTree.GetMyObjectID() == 42"));
        Assert.True(lua.EvalBool("return nBehaviorTree.GetTargetObjectID() == 7"));
    }
}
```

> **Note:** `SetBehaviorContext` mirrors the existing mechanism that sets the per-invocation `AgentId`/`TargetId` (see `ScriptStateContext`/`AbilityInvocation` in `ScriptContextRegistry.cs` and how `InvokeAbility` pushes an invocation). Read that code and use the SAME storage the natives read — do not add a parallel context store. If the harness can't set it directly, drive it by invoking a trivial ability that the natives observe.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~NBehaviorTreeModuleTests"`
Expected: FAIL — `nBehaviorTree.GetMyObjectID` is nil.

- [ ] **Step 3: Write minimal implementation**

Create `NBehaviorTreeModule.cs` reading the current invocation self/target from the same `ScriptContext` store `InvokeAbility` uses (adapt the accessor name to the real one found in Step 1's read):

```csharp
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

public static unsafe class NBehaviorTreeModule
{
    public static void Register(nint L)
    {
        LuaApiModule.RegisterNamespace(L, "nBehaviorTree",
            ("GetMyObjectID", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetMyObjectID),
            ("GetTargetObjectID", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetTargetObjectID));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetMyObjectID(nint L)
    {
        try { LuaNative.lua_pushnumber(L, ScriptContextRegistry.Get(L)?.CurrentSelfId ?? 0u); return 1; }
        catch { LuaNative.lua_pushnumber(L, 0); return 1; }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetTargetObjectID(nint L)
    {
        try { LuaNative.lua_pushnumber(L, ScriptContextRegistry.Get(L)?.CurrentTargetId ?? 0u); return 1; }
        catch { LuaNative.lua_pushnumber(L, 0); return 1; }
    }
}
```

Expose `CurrentSelfId`/`CurrentTargetId` on the context from the active `AbilityInvocation` (add thin getters that return `AgentId`/`TargetId` of the current invocation — reuse the existing per-L invocation storage; do NOT add a second store). Register in `StubNamespaces.cs`: replace `RegisterNamespace(L, "nBehaviorTree");` with `NBehaviorTreeModule.Register(L);`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~NBehaviorTreeModuleTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add ReCap.Server/Adapters/Scripting/Api/NBehaviorTreeModule.cs ReCap.Server/Adapters/Scripting/Api/StubNamespaces.cs ReCap.Server/Adapters/Scripting/ScriptContextRegistry.cs ReCap.Tests/Scripting/NBehaviorTreeModuleTests.cs
git commit -m "feat(ai): nBehaviorTree self/target context natives"
```

---

## Task 6: AIController — AIDefinition graph walk (synthetic, harvest-independent)

**Files:**
- Create: `ReCap.Server/Domain/Gameplay/AI/AIController.cs`
- Test: `ReCap.Tests/Gameplay/AI/AIControllerTests.cs`

**Interfaces:**
- Consumes: `AssetValue` navigation (`FindByName`, `AsX`, array iteration — `Services/Assets/AssetValueExtensions.cs`), `AgentBlackboard`/`AggroSystem` best-target, and an injected action sink (so this task needs no game data and no Lua).
- Produces: `AIController` with ctor `(AssetValue aiDefinition, IAiActions actions)`, `void Tick(uint selfId, uint targetId)`, `int CurrentNode {get;}`; interface `IAiActions { bool ConditionMet(string ns, string name, uint self, uint target); void CastAbility(uint gambitAbility, uint self, uint target); void MoveToward(uint self, uint target); }`.

**Design note:** the graph walk operates on the generic `AssetValue` shape of `AIDefinition` (`ainode[]` with `mpConditionData`/`mpPhaseData`/`output[]`). This task builds and tests the WALK (node transitions + gambit condition→ability dispatch) against a **synthetic `AssetValue`** so it needs no game data; the exact condition `namespace::name`s and phase encodings a real enemy uses are wired in Task 8 after the harvest. Keep the node/gambit reader tolerant: unknown shapes → no-op + return, never throw.

- [ ] **Step 1: Write the failing test** (build a synthetic 2-node AIDefinition via the AssetData.Parser model types; assert transition + cast)

```csharp
using AssetData.Parser.Model;
using ReCap.Server.Domain.Gameplay.AI;

namespace ReCap.Tests.Gameplay.AI;

public class AIControllerTests
{
    private sealed class RecordingActions : IAiActions
    {
        public HashSet<(string, string)> TrueConditions { get; } = new();
        public List<uint> Casts { get; } = new();
        public List<(uint self, uint target)> Moves { get; } = new();
        public bool ConditionMet(string ns, string name, uint self, uint target) => TrueConditions.Contains((ns, name));
        public void CastAbility(uint ability, uint self, uint target) => Casts.Add(ability);
        public void MoveToward(uint self, uint target) => Moves.Add((self, target));
    }

    // Helper builds an AIDefinition AssetValue with 2 nodes: node0 transitions to node1 when
    // condition ("ai","InRange") is true; node1's gambit casts ability 999 when ("ai","HasTarget").
    // (Uses the AssetData.Parser Model builders; see AIController for the exact field names it reads.)
    [Fact]
    public void Tick_TransitionsOnCondition_ThenCastsGambitAbility()
    {
        var actions = new RecordingActions();
        var ai = SyntheticAi.TwoNodeGambit();       // test helper (below the test class)
        var ctrl = new AIController(ai, actions);

        // Node 0: no condition true yet -> stays, no cast.
        ctrl.Tick(self: 2, target: 1);
        Assert.Empty(actions.Casts);

        // Enable the transition condition -> moves to node 1.
        actions.TrueConditions.Add(("ai", "InRange"));
        ctrl.Tick(2, 1);
        Assert.Equal(1, ctrl.CurrentNode);

        // Node 1 gambit fires when its condition is true.
        actions.TrueConditions.Add(("ai", "HasTarget"));
        ctrl.Tick(2, 1);
        Assert.Contains(999u, actions.Casts);
    }
}
```

> **`SyntheticAi.TwoNodeGambit()`** constructs the `AssetValue` using the same `AssetData.Parser.Model` node types the real parser emits (`StructValue`/`ArrayValue`/`NumberValue`/`StringValue`). Write it in the test file to exactly match the field names `AIController` reads (`ainode`, `mpConditionData`, `mpPhaseData`, `output`, and within phase data the gambit's `condition`/`ability` + `conditionProps`/`abilityProps` carrying the `namespace`/`name`). Keep it minimal — two nodes, one edge, one gambit.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~AIControllerTests"`
Expected: FAIL — `AIController`/`IAiActions` do not exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
using AssetData.Parser.Model;
using ReCap.Server.Services.Assets;

namespace ReCap.Server.Domain.Gameplay.AI;

public interface IAiActions
{
    bool ConditionMet(string ns, string name, uint self, uint target);
    void CastAbility(uint gambitAbility, uint self, uint target);
    void MoveToward(uint self, uint target);
}

// Walks an AIDefinition node graph (cAINode[]: mpConditionData decides transitions via output edges;
// mpPhaseData carries gambits = condition->ability). Operates on the generic AssetValue shape so it
// needs no game data to unit-test; real condition namespace::names + phase encodings are exercised
// in-game after the Task 7 harvest. Tolerant: unknown/absent shapes -> no-op, never throws.
public sealed class AIController
{
    private readonly AssetValue _def;
    private readonly IAiActions _actions;
    public int CurrentNode { get; private set; }

    public AIController(AssetValue aiDefinition, IAiActions actions)
    {
        _def = aiDefinition;
        _actions = actions;
    }

    public void Tick(uint selfId, uint targetId)
    {
        var nodes = _def.FindByName("ainode");
        var node = NodeAt(nodes, CurrentNode);
        if (node is null) return;

        // Transition: first output edge whose condition data is met.
        if (Condition(node.FindByName("mpConditionData"), selfId, targetId))
        {
            var outputs = node.FindByName("output");
            var next = FirstInt(outputs);
            if (next is int n && NodeAt(nodes, n) is not null) { CurrentNode = n; return; }
        }

        // Phase: run gambits (condition -> ability) at the current node.
        var phase = node.FindByName("mpPhaseData");
        foreach (var gambit in Gambits(phase))
        {
            if (!Condition(gambit.FindByName("condition"), selfId, targetId)) continue;
            var ability = gambit.FindByName("ability").AsUInt32();
            if (ability != 0) { _actions.CastAbility(ability, selfId, targetId); return; }
        }

        if (targetId != 0) _actions.MoveToward(selfId, targetId);
    }

    private bool Condition(AssetValue? conditionData, uint self, uint target)
    {
        if (conditionData is null) return false;
        var ns = (conditionData.FindByName("namespace") as StringValue)?.Value;
        var name = (conditionData.FindByName("name") as StringValue)?.Value;
        return !string.IsNullOrEmpty(ns) && !string.IsNullOrEmpty(name) && _actions.ConditionMet(ns, name, self, target);
    }

    private static AssetValue? NodeAt(AssetValue? nodes, int index)
        => nodes is ArrayValue a && index >= 0 && index < a.Items.Count ? a.Items[index] : null;

    private static IEnumerable<AssetValue> Gambits(AssetValue? phase)
        => phase is ArrayValue a ? a.Items : phase is not null ? new[] { phase } : System.Array.Empty<AssetValue>();

    private static int? FirstInt(AssetValue? arr)
        => arr is ArrayValue a && a.Items.Count > 0 ? (int)a.Items[0].AsUInt32() : null;
}
```

> Match `AssetValue`/`ArrayValue`/`StringValue`/`.Items`/`.AsUInt32()`/`.FindByName()` to the real API in `Services/Assets/AssetValueExtensions.cs` and `AssetData.Parser.Model` — adjust member names if they differ (e.g. `Items` vs `Values`). The test's `SyntheticAi` must build nodes with the same names.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~AIControllerTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add ReCap.Server/Domain/Gameplay/AI/AIController.cs ReCap.Tests/Gameplay/AI/AIControllerTests.cs
git commit -m "feat(ai): AIController AIDefinition graph walker (synthetic-tested)"
```

---

## Task 7: HARVEST (runs on the user's machine with game data) — pin the slice enemy's AI contract

**Files:**
- Create: `docs/architecture/research/AI_SLICE_HARVEST.md` (the harvested facts)

This task produces DATA, not code — it requires the Darkspore game data (this sandbox has no `--game-path`, so **the user runs it**). It gates Task 8.

- [ ] **Step 1: Pick a basic Dungeon minion noun** that spawns on the current single-player path (cross-ref the level's markerset spawn list; a common melee minion).

- [ ] **Step 2: Dump its AIDefinition graph.** Run the server with game data and add a one-off debug dump (temporary), or extend the existing asset probe, to print for the chosen noun: `AIDefinition.deathAbility`, and per `ainode`: index, `output` edges, and `mpConditionData`/`mpPhaseData` resolved contents (the gambits: `condition`+`ability` keys, and each `cAICondition`'s `namespace`/`name`/`properties`). Record verbatim in `AI_SLICE_HARVEST.md`.

- [ ] **Step 3: List the condition `namespace::name`s** the enemy references (e.g. `nCondition::IsInAbilityRange`, `nCondition::HasAggroTarget`) and the ability keys its gambits cast. These are the exact set Task 8 implements.

- [ ] **Step 4: Confirm the aggro-population trigger.** In Ghidra, check `AddAggroForObject` @0x009fd690 call sites (perceive-driven vs on-damage) to confirm Task 3's perceive-on-tick rule matches, or note the delta. Record.

- [ ] **Step 5: Commit the harvest doc.**

```bash
git add docs/architecture/research/AI_SLICE_HARVEST.md
git commit -m "docs(ai): harvest slice enemy AIDefinition + conditions + aggro trigger"
```

---

## Task 8: nCondition natives + wire AIController to real conditions (harvest-driven)

**Files:**
- Create: `ReCap.Server/Adapters/Scripting/Api/NConditionModule.cs`
- Modify: `ReCap.Server/Adapters/Scripting/Api/StubNamespaces.cs`
- Create: `ReCap.Server/Domain/Gameplay/AI/BridgeAiActions.cs` (real `IAiActions` over the bridge)
- Test: `ReCap.Tests/Scripting/NConditionModuleTests.cs`, `ReCap.Tests/Gameplay/AI/BridgeAiActionsTests.cs`

**Interfaces:**
- Consumes: harvest output (Task 7 — the exact conditions), `IAiActions` (Task 6), `IScriptGameBridge` (aggro/perception/position), existing `InvokeAbility`/locomotion.
- Produces: `nCondition.<Name>` natives (one per harvested condition), and `BridgeAiActions : IAiActions` translating `ConditionMet`/`CastAbility`/`MoveToward` into bridge calls.

- [ ] **Step 1: Write the failing test** — for EACH harvested condition, a test asserting its truth table. Example (replace with the real harvested conditions):

```csharp
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

public class NConditionModuleTests
{
    [Fact]
    public void HasAggroTarget_TrueWhenBridgeHasTargets()
    {
        using var lua = LuaTestHost.Create(new FakeBridge());
        lua.SetBehaviorContext(self: 10, target: 55);
        // exact native name comes from the harvest; this is the shape:
        Assert.True(lua.EvalBool("return nCondition.HasAggroTarget()"));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~NConditionModuleTests"`
Expected: FAIL — `nCondition.<Name>` nil.

- [ ] **Step 3: Implement `NConditionModule`** with exactly the harvested conditions (each a native reading self/target from context + querying the bridge), following the `NAgentModule` pattern. Implement `BridgeAiActions`:

```csharp
namespace ReCap.Server.Domain.Gameplay.AI;

// Translates AIController graph actions into real engine effects: conditions -> nCondition/bridge
// predicates, ability -> the existing InvokeAbility cast path (FX + damage + CombatEvent), movement ->
// locomotion goal toward target. One instance per agent per tick.
public sealed class BridgeAiActions(ReCap.Server.Adapters.Scripting.IScriptGameBridge bridge) : IAiActions
{
    public bool ConditionMet(string ns, string name, uint self, uint target)
        => bridge.EvaluateAiCondition(ns, name, self, target);
    public void CastAbility(uint ability, uint self, uint target)
        => bridge.CastAiAbility(ability, self, target);
    public void MoveToward(uint self, uint target)
        => bridge.MoveAgentToward(self, target);
}
```

Add `EvaluateAiCondition`/`CastAiAbility`/`MoveAgentToward` to `IScriptGameBridge` + `GameScriptContext`: `CastAiAbility` calls the existing `InvokeAbility(abilityHash, self, target, cursorX,Y,Z, rank)` with the target's position as cursor; `MoveAgentToward` sets the locomotion goal toward the target (reuse `SetLocomotionGoal`); `EvaluateAiCondition` dispatches the harvested conditions in C# (same logic the nCondition natives expose to Lua). Register `NConditionModule.Register(L)` in `StubNamespaces.cs`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~NConditionModuleTests|FullyQualifiedName~BridgeAiActionsTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add ReCap.Server/Adapters/Scripting/Api/NConditionModule.cs ReCap.Server/Adapters/Scripting/Api/StubNamespaces.cs ReCap.Server/Domain/Gameplay/AI/BridgeAiActions.cs ReCap.Server/Adapters/Scripting/ScriptContextRegistry.cs ReCap.Server/Services/Scripting/GameScriptContext.cs ReCap.Tests/Scripting/NConditionModuleTests.cs ReCap.Tests/Gameplay/AI/BridgeAiActionsTests.cs
git commit -m "feat(ai): nCondition natives + BridgeAiActions (harvested slice enemy)"
```

---

## Task 9: Integration — tick AggroSystem + AIController from the game loop, in-game gate

**Files:**
- Modify: `ReCap.Server/Domain/Gameplay/ObjectManager.cs` (`Update` ticks `AggroSystem` + each agent's `AIController`)
- Modify: `ReCap.Server/Domain/Gameplay/Game.cs` (own the `AggroSystem`/per-agent `AIController` map; pass the bridge for `BridgeAiActions`)
- Test: `ReCap.Tests/Gameplay/AI/AiTickIntegrationTests.cs`

**Interfaces:**
- Consumes: everything above. `ObjectManager.Update(delta)` already runs each frame (Game.cs:63).

- [ ] **Step 1: Write the failing test** — an integration test with a real `ObjectManager` + a spawned agent (synthetic AIDefinition) + a hero in perception, asserting one `Tick` produces a MoveToward (via a recording `IAiActions`) and, when in range, a cast.

```csharp
using System.Numerics;
using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Domain.Gameplay.AI;

namespace ReCap.Tests.Gameplay.AI;

public class AiTickIntegrationTests
{
    [Fact]
    public void Update_AggrosAndDrivesController_ForAgentWithHeroInPerception()
    {
        var om = new ObjectManager(null);
        var enemy = om.Spawn(2, 200, Vector3.Zero, 1f, team: 2, playerControlled: false);
        enemy.Agent = new AgentBlackboard { PerceptionRadius = 15f };
        var hero = om.Spawn(1, 100, new Vector3(5, 0, 0), 1f, team: 1, playerControlled: true);
        hero.Health = hero.MaxHealth = 100f;

        var actions = new RecordingActions(); // same recorder as Task 6
        om.AttachController(enemy.ObjectId, new AIController(SyntheticAi.PursueThenCast(), actions));

        om.Update(0.05);

        Assert.Equal(1u, enemy.Agent.GetBestTarget(id => om.Objects.ContainsKey(id)));
        Assert.NotEmpty(actions.Moves); // pursued the hero
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~AiTickIntegrationTests"`
Expected: FAIL — `ObjectManager.AttachController` / AI tick not wired.

- [ ] **Step 3: Wire the tick.** In `ObjectManager`: add `private readonly AI.AggroSystem _aggro; private readonly Dictionary<uint, AI.AIController> _controllers = new();` (init `_aggro` in ctor); `public void AttachController(uint id, AI.AIController c) => _controllers[id] = c;`. In `Update`, replace the no-op body with:

```csharp
        _aggro.Tick();
        foreach (var (id, controller) in _controllers)
        {
            if (!_objects.TryGetValue(id, out var agent) || agent.Dead || agent.Agent is null) continue;
            controller.Tick(id, _aggro.BestTargetFor(agent));
        }
```

In `Game`/spawn wiring, when an AI-agent object with a resolved `AIDefinition` spawns, build `new AIController(agent.AIDefinition!, new BridgeAiActions(ScriptContext!))` and `Objects.AttachController(id, …)` (guard on `ScriptContext` present). Remove controllers on death/remove (`ObjectManager.Remove` also drops `_controllers[id]`).

- [ ] **Step 4: Run tests to verify they pass + full suite**

Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj`
Expected: PASS except the 1 pre-existing RED (`EnemyNounCombatDataTests`).

- [ ] **Step 5: In-game gate (user machine).** Boot the server + client, enter a Dungeon with the harvested enemy. Verify: enemy stays idle until the hero enters perception; then it turns aggro (anim), pursues (0x95 smooth-move), casts its ability (FX 0x9B + damage numbers 0xBA), and dies to its deathAbility. Record the result in `AI_SLICE_HARVEST.md`.

- [ ] **Step 6: Commit**

```bash
git add ReCap.Server/Domain/Gameplay/ObjectManager.cs ReCap.Server/Domain/Gameplay/Game.cs ReCap.Tests/Gameplay/AI/AiTickIntegrationTests.cs
git commit -m "feat(ai): tick AggroSystem + AIController from the game loop (vertical slice live)"
```

---

## Self-review notes

- **Spec coverage:** AgentBlackboard (T1/T2), AggroSystem/perception (T3), nAgent (T4), nBehaviorTree (T5), AIController graph walk (T6), harvest (T7), nCondition + real actions (T8), integration + in-game gate (T9). deathAbility/death reuse D-025 (T9 gate). nGameDirector explicitly deferred (spec non-goal).
- **Harvest gating:** T1–T6 are fully buildable/testable in-sandbox (no game data); T7 is the game-data discovery the user runs; T8–T9 consume it. This matches the spec's discovery dependency.
- **Reuse:** attack=InvokeAbility, move=locomotion 0x95, damage feedback=CombatEvent 0xBA, death=D-025 — no new wire formats.
- **API-name caution:** Tasks 4/5/6 call out that `AssetValue`/`.Items`/`.AsUInt32`/the Lua test harness/the invocation-context accessor must be matched to the real code found by reading the cited files before implementing — the reviewer verifies against those, not against guessed names.
