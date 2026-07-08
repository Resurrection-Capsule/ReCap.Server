# Lua Natives — Wave 3 (Modifier / FX) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the 7 modifier/FX demanded Lua natives — the nModifier per-thread context getters, nModifier.PreloadAsset, nAttribute.AddAttributeModifier, and nGameObject.AddEffect (which emits the client-verified 0x9B ServerEvent).

**Architecture:** The nModifier getters read the same per-thread invocation slot as the nAbility getters — retail confirms `ModifierInvocation == AbilityInvocation` (`GetMyStackCount` decompiles as `nAbility::GetMyStackCount@0x00a41600`; `RegisterModifier == RegisterAbility@0x00a43040`; modifiers dispatch through the same `nAbilityFns` table). We extend `AbilityInvocation` with `InitiatorId` + `StackCount` and add an `NModifierModule` that reads it, mirroring `NAbilityContextModule`. `nGameObject.AddEffect` broadcasts a `ServerEventPacket` (0x9B, already implemented, client-verified) via a new `Game.BroadcastServerEvent`. `nAttribute.AddAttributeModifier` applies an additive attribute delta on the target `GameObject` through the bridge and returns an opaque handle.

**Tech Stack:** C# net10.0, native lua 5.1.4 (float ABI) P/Invoke, xUnit.

## Global Constraints

- Target framework **net10.0**; native lua `LUA_NUMBER = float` — pushed numbers are `(float)`.
- Every native body is exception-proof: try/catch, push fallback / return N. Never throw across the Lua boundary.
- **0x9B ServerEvent is client-verified** (`ServerEventPacket`, handler `ClientNet::OnGmsServerEvent@0x0053ec80`, 26-field reflection). AddEffect uses the **attached FX recipe `{6 ServerEventDef, 7 ObjectId}`** (+ AttackerId field 9 when an initiator is given). Do not invent new fields; reuse `ServerEventPacket` as-is.
- Contracts: `RegisterModifier == RegisterAbility@0x00a43040`; nModifier getters read the per-thread invocation; `nModifier.PreloadAsset` == `nAbility.PreloadAsset` (asset name → `ScriptVfs.Hash` FNV handle, 1 number).
- No code comments except a short verified cite (Ghidra `addr` / catalog). No AI attribution.
- No hardcoded data-derived values without a `DEFERRED:` cite.
- Build/test on Windows; close the server before rebuilding. Tests: `dotnet test ReCap.Tests/ReCap.Tests.csproj`. Ratchet: `RECAP_HARVEST=1 dotnet test --filter NativeDemandHarvest` — `demanded` count may only decrease.

## Scope

**In (7 natives):** nModifier.GetMyAgentID, nModifier.GetMyInitiatorID, nModifier.GetMyStackCount, nModifier.GetInitiatorAttributeSnapshot, nModifier.PreloadAsset, nAttribute.AddAttributeModifier, nGameObject.AddEffect. Plus `AbilityInvocation` initiator/stack fields, a bridge attribute-modifier + effect-emit API, and `Game.BroadcastServerEvent`.

**Explicitly deferred (flagged, not silently dropped):**
- **Modifier application subsystem** (`nModifier.RequestModifier`, when/how modifiers get attached and invoked with a live initiator/stack context): NOT in scope. In production, modifiers are not yet invoked (only abilities via 0x9C→`InvokeAbility`), so the nModifier getters are correct but **dormant** until that subsystem lands. They read the invocation slot and return the extended fields (0 when unset). This is the natural boundary; the getters + their reads are what Wave 3 delivers.
- **RemoveAttributeModifier / RemoveEffect**: not demanded (only the Add side). AddAttributeModifier applies an additive delta directly to `GameObject.Attributes` (no separate modifier-layer recompute — a simplification; retail layers modifiers) and returns a handle; the reversal store is recorded for a future Remove but Remove itself is deferred.
- **AddAttributeModifier additive-vs-multiplicative**: unverified; additive chosen (most common), flagged.

## File Structure

- `ReCap.Server/Adapters/Scripting/ScriptContextRegistry.cs` — add `InitiatorId`/`StackCount` to `AbilityInvocation`; add bridge methods `AddAttributeModifier`, `EmitEffect`.
- `ReCap.Server/Adapters/Scripting/Api/NModifierModule.cs` — **new** (5 natives).
- `ReCap.Server/Adapters/Scripting/Api/NAttributeModule.cs` — add `AddAttributeModifier`.
- `ReCap.Server/Adapters/Scripting/Api/NGameObjectModule.cs` — add `AddEffect`.
- `ReCap.Server/Services/Scripting/GameScriptContext.cs` — implement the two bridge methods.
- `ReCap.Server/Domain/Gameplay/Game.cs` — add `BroadcastServerEvent`.
- `ReCap.Server/Adapters/Scripting/LuaRuntime.cs:53-70` — wire `NModifierModule.Register(L)`.
- `ReCap.Tests/Scripting/GameBridgeTests.cs` — extend `FakeBridge` + tests.
- `ReCap.Tests/Scripting/SchedulerPredicateTests.cs` — extend `MutableHpBridge` for the 2 new interface members.

---

### Task 1: AbilityInvocation initiator/stack + NModifierModule

**Files:** Modify `ScriptContextRegistry.cs` (record fields); Create `NModifierModule.cs`; Modify `LuaRuntime.cs`; Test `GameBridgeTests.cs`.

**Interfaces:** `AbilityInvocation` gains `uint InitiatorId = 0, int StackCount = 0` (positional record params with defaults — existing constructions unaffected). Produces Lua `nModifier.GetMyAgentID()`→1 number (AgentId), `GetMyInitiatorID()`→1 (InitiatorId), `GetMyStackCount()`→1 (StackCount), `GetInitiatorAttributeSnapshot()`→1 (snapshot handle of InitiatorId), `PreloadAsset(name,[tag])`→1 (FNV hash).

- [ ] **Step 1: Write the failing test** — in `GameBridgeTests.cs`:

```csharp
[Fact]
public void ModifierContextGettersReadInvocation()
{
    using var rt = Make();
    var ctx = ScriptContextRegistry.Get(rt.L)!;
    ctx.SetInvocation(rt.L, new AbilityInvocation(AgentId: 10, TargetId: 77, CursorX: 0f, CursorY: 0f, CursorZ: 0f, Rank: 1,
        InitiatorId: 20, StackCount: 3));
    Assert.True(rt.EvalBool(LuaFixtures.Compile("""
        return nModifier.GetMyAgentID() == 10
           and nModifier.GetMyInitiatorID() == 20
           and nModifier.GetMyStackCount() == 3
        """)));
}

[Fact]
public void ModifierInitiatorSnapshotAndPreloadReturnHandles()
{
    using var rt = Make();
    var ctx = ScriptContextRegistry.Get(rt.L)!;
    // Initiator 10 has attributes in FakeBridge; snapshot handle must round-trip via _FromSnapshot.
    ctx.SetInvocation(rt.L, new AbilityInvocation(AgentId: 99, TargetId: 0, CursorX: 0f, CursorY: 0f, CursorZ: 0f, Rank: 1,
        InitiatorId: 10, StackCount: 1));
    Assert.True(rt.EvalBool(LuaFixtures.Compile("""
        local snap = nModifier.GetInitiatorAttributeSnapshot()
        local h = nModifier.PreloadAsset("some_effect.ServerEventDef", "Owner")
        return snap > 0 and nAttribute.GetAttributeValue_FromSnapshot(snap, 0) == 12 and h > 0
        """)));
}
```

- [ ] **Step 2: Run tests, verify they fail** — Run: `dotnet test … --filter "ModifierContextGettersReadInvocation|ModifierInitiatorSnapshotAndPreloadReturnHandles"`. Expected: FAIL (`AbilityInvocation` has no `InitiatorId`/`StackCount` → compile error; `nModifier` getters are stubs).

- [ ] **Step 3: Extend AbilityInvocation** — in `ScriptContextRegistry.cs`, change the record to append the two fields:

```csharp
public readonly record struct AbilityInvocation(
    uint AgentId, uint TargetId, float CursorX, float CursorY, float CursorZ, int Rank,
    uint AbilityHash = 0, uint InstanceId = 0, bool TargetInRangeAtStart = false,
    uint InitiatorId = 0, int StackCount = 0);
```

- [ ] **Step 4: Create NModifierModule** — `NModifierModule.cs`:

```csharp
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

// nModifier context getters read the same per-thread invocation slot as nAbility — retail
// ModifierInvocation == AbilityInvocation (GetMyStackCount decompiles as nAbility::GetMyStackCount
// @0x00a41600; RegisterModifier == RegisterAbility @0x00a43040). PreloadAsset == nAbility.PreloadAsset.
public static unsafe class NModifierModule
{
    public static void Register(nint L) =>
        LuaApiModule.RegisterNamespace(L, "nModifier",
            ("GetMyAgentID", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetMyAgentID),
            ("GetMyInitiatorID", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetMyInitiatorID),
            ("GetMyStackCount", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetMyStackCount),
            ("GetInitiatorAttributeSnapshot", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetInitiatorAttributeSnapshot),
            ("PreloadAsset", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&PreloadAsset));

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetMyAgentID(nint L)
    {
        try { var inv = ScriptContextRegistry.Get(L)?.GetInvocation(L); LuaNative.lua_pushnumber(L, inv.HasValue ? (float)inv.Value.AgentId : 0f); return 1; }
        catch { LuaNative.lua_pushnumber(L, 0f); return 1; }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetMyInitiatorID(nint L)
    {
        try { var inv = ScriptContextRegistry.Get(L)?.GetInvocation(L); LuaNative.lua_pushnumber(L, inv.HasValue ? (float)inv.Value.InitiatorId : 0f); return 1; }
        catch { LuaNative.lua_pushnumber(L, 0f); return 1; }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetMyStackCount(nint L)
    {
        try { var inv = ScriptContextRegistry.Get(L)?.GetInvocation(L); LuaNative.lua_pushnumber(L, inv.HasValue ? (float)inv.Value.StackCount : 0f); return 1; }
        catch { LuaNative.lua_pushnumber(L, 0f); return 1; }
    }

    // Mirrors nAbility.GetAgentAttributeSnapshot but freezes the INITIATOR's attributes.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetInitiatorAttributeSnapshot(nint L)
    {
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            var initiatorId = ctx?.GetInvocation(L)?.InitiatorId ?? 0;
            var attributes = ctx?.GameBridge is { } b && initiatorId != 0 ? b.GetAttributeTable(initiatorId) : null;
            if (ctx is null || attributes is null) { LuaNative.lua_pushnumber(L, 0f); return 1; }
            LuaNative.lua_pushnumber(L, ctx.StoreAttributeSnapshot(new Dictionary<int, float>(attributes)));
            return 1;
        }
        catch { LuaNative.lua_pushnumber(L, 0f); return 1; }
    }

    // nModifier.PreloadAsset(name, [ownerTag]) -> FNV hash handle (== nAbility.PreloadAsset, C3).
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int PreloadAsset(nint L)
    {
        try
        {
            var name = LuaNative.lua_type(L, 1) == LuaNative.LUA_TSTRING ? LuaNative.ToManagedString(L, 1) : null;
            var hash = string.IsNullOrEmpty(name) ? 0u : ScriptVfs.Hash(name);
            LuaNative.lua_pushnumber(L, hash);
            return 1;
        }
        catch { LuaNative.lua_pushnumber(L, 0f); return 1; }
    }
}
```

Note: verify `ScriptVfs.Hash` and `LuaNative.ToManagedString` exist with these signatures (both are used elsewhere — `PreloadModule` uses `ScriptVfs.Hash`, `LuaApiModule` uses `ToManagedString`). If `PreloadModule` computes the hash differently, match its exact form.

- [ ] **Step 5: Wire registration** — in `LuaRuntime.cs`, after `Api.NLocomotionModule.Register(L);` add:

```csharp
        Api.NModifierModule.Register(L);
```

- [ ] **Step 6: Run tests, verify pass** — Run: `dotnet test … --filter "ModifierContextGettersReadInvocation|ModifierInitiatorSnapshotAndPreloadReturnHandles|Scripting"` (ignore only the known pre-existing `EnemyNounCombatDataTests` failure). Expected: green.

- [ ] **Step 7: Commit**

```bash
git add ReCap.Server/Adapters/Scripting/ScriptContextRegistry.cs ReCap.Server/Adapters/Scripting/Api/NModifierModule.cs ReCap.Server/Adapters/Scripting/LuaRuntime.cs ReCap.Tests/Scripting/GameBridgeTests.cs
git commit -m "feat(lua): nModifier context getters + PreloadAsset (invocation initiator/stack)"
```

---

### Task 2: nAttribute.AddAttributeModifier

**Files:** Modify `ScriptContextRegistry.cs` (bridge method), `GameScriptContext.cs`, `NAttributeModule.cs`, `GameBridgeTests.cs` (FakeBridge), `SchedulerPredicateTests.cs` (MutableHpBridge).

**Interfaces:** Bridge `uint AddAttributeModifier(uint objectId, int attributeId, float value)` — applies an additive delta to the object's attribute and returns an opaque handle (0 if object missing). Produces Lua `nAttribute.AddAttributeModifier(objId, attributeId, value)` → 1 number (handle).

- [ ] **Step 1: Write failing test** — in `GameBridgeTests.cs`, extend `FakeBridge`:

```csharp
public uint AddAttributeModifier(uint objectId, int attributeId, float value)
{
    if (objectId != 10) return 0;
    Attributes[attributeId] = (Attributes.TryGetValue(attributeId, out var cur) ? cur : 0f) + value;
    return ++_nextAttrModHandle;
}
private uint _nextAttrModHandle;
```

Add the test:

```csharp
[Fact]
public void AddAttributeModifierAppliesDeltaAndReturnsHandle()
{
    using var rt = Make();
    Assert.True(rt.EvalBool(LuaFixtures.Compile("""
        local before = nAttribute.GetAttributeValue(10, 0)   -- Strength 12
        local h = nAttribute.AddAttributeModifier(10, 0, 5)
        local after = nAttribute.GetAttributeValue(10, 0)
        local miss = nAttribute.AddAttributeModifier(99, 0, 5)
        return before == 12 and after == 17 and h > 0 and miss == 0
        """)));
}
```

- [ ] **Step 2: Run test, verify it fails** — Run: `dotnet test … --filter AddAttributeModifierAppliesDeltaAndReturnsHandle`. Expected: FAIL (interface/native missing).

- [ ] **Step 3: Implement** —

Add to `IScriptGameBridge`: `uint AddAttributeModifier(uint objectId, int attributeId, float value);`

Implement in `GameScriptContext.cs`:

```csharp
private uint _nextAttrModHandle;
// DEFERRED: retail layers attribute modifiers (recomputed by GetAttributeValue); ReCap applies an
// additive delta directly to GameObject.Attributes (add-vs-mult unverified) and records the reversal
// for a future RemoveAttributeModifier (not yet demanded). Handle 0 = object missing.
public uint AddAttributeModifier(uint objectId, int attributeId, float value)
{
    if (!_game.Objects.Objects.TryGetValue(objectId, out var o)) return 0;
    o.Attributes[attributeId] = (o.Attributes.TryGetValue(attributeId, out var cur) ? cur : 0f) + value;
    return ++_nextAttrModHandle;
}
```

Add to `NAttributeModule.cs` Register + method:

```csharp
("AddAttributeModifier", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&AddAttributeModifier));
```

```csharp
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
private static int AddAttributeModifier(nint L)
{
    try
    {
        var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
        var handle = bridge is not null
            && LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER
            && LuaNative.lua_type(L, 2) == LuaNative.LUA_TNUMBER
            && LuaNative.lua_type(L, 3) == LuaNative.LUA_TNUMBER
            ? bridge.AddAttributeModifier(
                (uint)Math.Round((double)LuaNative.lua_tonumber(L, 1)),
                (int)Math.Round((double)LuaNative.lua_tonumber(L, 2)),
                (float)LuaNative.lua_tonumber(L, 3))
            : 0u;
        LuaNative.lua_pushnumber(L, handle);
        return 1;
    }
    catch
    {
        LuaNative.lua_pushnumber(L, 0f);
        return 1;
    }
}
```

Add `AddAttributeModifier` to `MutableHpBridge` in `SchedulerPredicateTests.cs`: `public uint AddAttributeModifier(uint id, int a, float v) => 0;`

- [ ] **Step 4: Run test, verify pass** — Run: `dotnet test … --filter "AddAttributeModifierAppliesDeltaAndReturnsHandle|Scripting"` (ignore the known pre-existing failure). Expected: green.

- [ ] **Step 5: Commit**

```bash
git add ReCap.Server/Adapters/Scripting/ScriptContextRegistry.cs ReCap.Server/Services/Scripting/GameScriptContext.cs ReCap.Server/Adapters/Scripting/Api/NAttributeModule.cs ReCap.Tests/Scripting/GameBridgeTests.cs ReCap.Tests/Scripting/SchedulerPredicateTests.cs
git commit -m "feat(lua): nAttribute.AddAttributeModifier (additive delta + handle)"
```

---

### Task 3: nGameObject.AddEffect (0x9B ServerEvent)

**Files:** Modify `ScriptContextRegistry.cs` (bridge method), `Game.cs` (BroadcastServerEvent), `GameScriptContext.cs`, `NGameObjectModule.cs`, `GameBridgeTests.cs`, `SchedulerPredicateTests.cs`.

**Interfaces:** Bridge `uint EmitEffect(uint objectId, uint serverEventDef, uint initiatorId)` — broadcasts a 0x9B `ServerEventPacket` (attached recipe `{6 ServerEventDef, 7 ObjectId}`, + AttackerId when initiator>0) and returns an effect-instance handle (0 if serverEventDef is 0). Produces Lua `nGameObject.AddEffect(objId, serverEventDefHandle, [initiatorId])` → 1 number (handle).

- [ ] **Step 1: Write failing test** — in `GameBridgeTests.cs`, extend `FakeBridge`:

```csharp
public List<(uint ObjectId, uint Effect, uint Initiator)> Effects { get; } = [];
public uint EmitEffect(uint objectId, uint serverEventDef, uint initiatorId)
{
    if (serverEventDef == 0) return 0;
    Effects.Add((objectId, serverEventDef, initiatorId));
    return (uint)Effects.Count;
}
```

Add the test:

```csharp
[Fact]
public void AddEffectEmitsAndReturnsHandle()
{
    using var rt = Make();
    var b = (FakeBridge)ScriptContextRegistry.Get(rt.L)!.GameBridge!;
    Assert.True(rt.EvalBool(LuaFixtures.Compile("""
        local fx = nAbility.PreloadAsset("boom.ServerEventDef", "Owner")
        local h = nGameObject.AddEffect(10, fx, 20)
        return h > 0
        """)));
    Assert.Single(b.Effects);
    Assert.Equal(10u, b.Effects[0].ObjectId);
    Assert.Equal(20u, b.Effects[0].Initiator);
    Assert.True(b.Effects[0].Effect != 0);
}
```

- [ ] **Step 2: Run test, verify it fails** — Run: `dotnet test … --filter AddEffectEmitsAndReturnsHandle`. Expected: FAIL (interface/native missing).

- [ ] **Step 3: Implement** —

Add to `IScriptGameBridge`: `uint EmitEffect(uint objectId, uint serverEventDef, uint initiatorId);`

Add to `Game.cs` (near `BroadcastAnimationState`):

```csharp
// nGameObject.AddEffect → 0x9B ServerEvent (client OnGmsServerEvent @0x0053ec80). Attached FX
// recipe {6 ServerEventDef, 7 ObjectId} (+ AttackerId when an initiator is given).
public void BroadcastServerEvent(ServerEventPacket packet) => BroadcastToAllPlayers(packet);
```

(Ensure `ReCap.Server.Adapters.RakNet.Packets` is usable in `Game.cs` — it already constructs packets like `ObjectDeletePacket` there, so the namespace is in scope.)

Implement in `GameScriptContext.cs`:

```csharp
private uint _nextEffectHandle;
public uint EmitEffect(uint objectId, uint serverEventDef, uint initiatorId)
{
    if (serverEventDef == 0) return 0;
    _game.BroadcastServerEvent(new ReCap.Server.Adapters.RakNet.Packets.ServerEventPacket
    {
        ServerEventDef = serverEventDef,
        ObjectId = objectId,
        AttackerId = initiatorId,
    });
    return ++_nextEffectHandle;
}
```

Add to `NGameObjectModule.cs` Register + method:

```csharp
("AddEffect", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&AddEffect));
```

```csharp
// nGameObject.AddEffect(objId, serverEventDefHandle, [initiatorId]) -> effect-instance handle;
// emits 0x9B attached FX (catalog §Modifier/FX). Effect handle is for a future RemoveEffect (deferred).
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
private static int AddEffect(nint L)
{
    try
    {
        var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
        if (bridge is null || LuaNative.lua_type(L, 1) != LuaNative.LUA_TNUMBER || LuaNative.lua_type(L, 2) != LuaNative.LUA_TNUMBER)
        {
            LuaNative.lua_pushnumber(L, 0f);
            return 1;
        }
        var objId = (uint)Math.Round((double)LuaNative.lua_tonumber(L, 1));
        var effect = (uint)Math.Round((double)LuaNative.lua_tonumber(L, 2));
        var initiator = LuaNative.lua_type(L, 3) == LuaNative.LUA_TNUMBER ? (uint)Math.Round((double)LuaNative.lua_tonumber(L, 3)) : 0u;
        LuaNative.lua_pushnumber(L, bridge.EmitEffect(objId, effect, initiator));
        return 1;
    }
    catch
    {
        LuaNative.lua_pushnumber(L, 0f);
        return 1;
    }
}
```

Add `EmitEffect` to `MutableHpBridge` in `SchedulerPredicateTests.cs`: `public uint EmitEffect(uint id, uint fx, uint init) => 0;`

- [ ] **Step 4: Run test, verify pass** — Run: `dotnet test … --filter "AddEffectEmitsAndReturnsHandle|Scripting"` (ignore the known pre-existing failure). Expected: green.

- [ ] **Step 5: Commit**

```bash
git add ReCap.Server/Adapters/Scripting/ScriptContextRegistry.cs ReCap.Server/Domain/Gameplay/Game.cs ReCap.Server/Services/Scripting/GameScriptContext.cs ReCap.Server/Adapters/Scripting/Api/NGameObjectModule.cs ReCap.Tests/Scripting/GameBridgeTests.cs ReCap.Tests/Scripting/SchedulerPredicateTests.cs
git commit -m "feat(lua): nGameObject.AddEffect emits 0x9B ServerEvent + returns handle"
```

---

### Task 4: Ratchet + full suite + gate note

**Files:** none (verification); update spec/memory.

- [ ] **Step 1: Full suite** — Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj`. Expected: all green except the known pre-existing `EnemyNounCombatDataTests` failure.
- [ ] **Step 2: Harvest ratchet** — Run: `RECAP_HARVEST=1 dotnet test ReCap.Tests/ReCap.Tests.csproj --filter NativeDemandHarvest --logger "trx;LogFileName=h.trx"`; extract demanded list. Expected: demanded dropped by the 7 Wave-3 natives (9 → 2). Remaining should be only Wave 4 (nObjectManager.CreateObject + AttachTriggerVolume).
- [ ] **Step 3: Record** — update the catalog spec's harvest number and note Wave 3 complete + deferrals (modifier application subsystem, Remove side, additive attr modifier) in `memory/lua-system-contract.md`. Commit:

```bash
git add docs/superpowers/specs/2026-07-08-lua-demanded-natives-catalog-design.md
git commit -m "docs: Wave 3 modifier/FX natives landed; update demanded count"
```

**In-game gate (manual, after merge):** an ability whose tick calls `nGameObject.AddEffect` with a `.ServerEventDef` should show the FX at the target in a dungeon (0x9B). AddAttributeModifier's effect is observable via a stat-scaled downstream calc.

## Self-Review

- **Spec coverage:** 7 natives → Tasks 1-3; ratchet gate → Task 4. The modifier-application subsystem, Remove side, and additive-vs-mult choice are stated in Scope and cited in code. ✓
- **Placeholder scan:** every code step has complete code; the deferred behaviors carry `DEFERRED:` cites; the two "verify the symbol exists" notes (`ScriptVfs.Hash`, `ToManagedString`) have explicit fallbacks. ✓
- **Type consistency:** `AbilityInvocation`'s new `InitiatorId`/`StackCount` (Task 1) are read by the nModifier getters (Task 1) and unaffected by existing constructions (positional defaults); bridge `AddAttributeModifier` (Task 2) and `EmitEffect` (Task 3) signatures match across interface, `GameScriptContext`, `FakeBridge`, `MutableHpBridge`; `Game.BroadcastServerEvent` (Task 3) consumes the existing `ServerEventPacket`. ✓

**Impl-time note:** `IScriptGameBridge` gains 2 members across Tasks 2-3 — after each, build all three implementers (`GameScriptContext`, `FakeBridge`, `MutableHpBridge`) so the fakes stay in sync, and re-run the full suite before the ratchet.
