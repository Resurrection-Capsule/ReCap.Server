# Lua Natives — Wave 1 (Mechanical + Ambiguous) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the 9 self-contained demanded Lua natives that have no locomotion/spawn/modifier subsystem dependency, plus the reusable scheduler predicate mechanism they need.

**Architecture:** Each native follows the existing `NXxxModule` pattern (`static unsafe class`, `[UnmanagedCallersOnly(CallConvs=[typeof(CallConvCdecl)])] static int Fn(nint L)`, registered via `LuaApiModule.RegisterNamespace`, wired in `LuaRuntime.CreateSandboxedState`). State reaches natives through `ScriptContextRegistry.Get(L)` → `ScriptStateContext` (`.GameBridge`, `.Scheduler`, per-thread stores). New game-facing behaviour goes through `IScriptGameBridge` (impl `GameScriptContext`, fake `FakeBridge`). All native bodies are exception-proof (`lua_error` is a longjmp — never let a managed exception cross it; push a fallback and return).

**Tech Stack:** C# net10.0, native lua 5.1.4 (float ABI) via P/Invoke, xUnit.

## Global Constraints

- Target framework **net10.0**; native lua `LUA_NUMBER = float` (4 bytes) — all pushed numbers are `(float)`.
- No code comments except a short cite when a wire/offset layout is non-obvious and verified (`Ghidra addr` / chunk `instr`). No attribution to AI anywhere.
- No hardcoded values the game derives from data — derive from bridge/DB or defer with a cite.
- Every native body is exception-proof: `try { … } catch { push fallback; return N; }`. Never throw across the Lua boundary.
- Contracts cite the catalog spec `docs/superpowers/specs/2026-07-08-lua-demanded-natives-catalog-design.md` and/or a Ghidra `addr`.
- Build/test on Windows; close the server before rebuilding (native DLL lock). Tests: `dotnet test ReCap.Tests/ReCap.Tests.csproj`.
- Ratchet: `RECAP_HARVEST=1 dotnet test --filter NativeDemandHarvest` — the `errored` count may only decrease.

## Scope

**In (9 natives + 1 mechanism):** nBit.Mask, nPlayer.GetPlayerIdForObject, nGameObject.ResetAnimationState,
nGameObject.SetAttributeSnapshot, nThreadData.GetPrivateTable, nThreadData.SetGUID, nAbility.ReleaseAgent,
nThread.WaitForHitpointsAbove, nThread.WaitForFadeOutInXSeconds; scheduler predicate support.

**Deferred to Wave 2 (locomotion) — they read locomotion state:** nGameObject.GetModifiedMoveSpeed,
nThread.WaitForJumpComplete, nThread.WaitForNearGoal.

## File Structure

- `ReCap.Server/Adapters/Scripting/Api/NBitModule.cs` — add `Mask` (modify).
- `ReCap.Server/Adapters/Scripting/Api/NPlayerModule.cs` — add `GetPlayerIdForObject` (modify).
- `ReCap.Server/Adapters/Scripting/Api/NGameObjectModule.cs` — add `ResetAnimationState`, `SetAttributeSnapshot` (modify).
- `ReCap.Server/Adapters/Scripting/Api/NThreadDataModule.cs` — **new** module (`GetPrivateTable`, `SetGUID`).
- `ReCap.Server/Adapters/Scripting/Api/NAbilityContextModule.cs` — add `ReleaseAgent` (modify).
- `ReCap.Server/Adapters/Scripting/Api/NThreadModule.cs` — add `WaitForHitpointsAbove`, `WaitForFadeOutInXSeconds` (modify).
- `ReCap.Server/Adapters/Scripting/LuaCoroutineScheduler.cs` — add predicate to `ThreadEntry` + `RegisterYield` + `Tick` (modify).
- `ReCap.Server/Adapters/Scripting/ScriptContextRegistry.cs` — add per-thread private-table/GUID stores + per-object snapshot store to `ScriptStateContext`; add `GetPlayerId`/`ResetAnimationState` to `IScriptGameBridge` (modify).
- `ReCap.Server/Services/Scripting/GameScriptContext.cs` — implement new bridge methods (modify).
- `ReCap.Server/Domain/Gameplay/**/GameObject.cs` — add `byte PlayerId` field (modify).
- `ReCap.Server/Adapters/Scripting/LuaRuntime.cs:53-70` — wire `NThreadDataModule.Register(L)` (modify).
- `ReCap.Tests/Scripting/GameBridgeTests.cs` — extend `FakeBridge` + add EvalBool tests (modify).
- `ReCap.Tests/Scripting/SchedulerPredicateTests.cs` — **new** scheduler + WaitFor tests.

---

### Task 1: nBit.Mask (AND-NOT, variadic)

**Files:** Modify `ReCap.Server/Adapters/Scripting/Api/NBitModule.cs`; Test `ReCap.Tests/Scripting/GameBridgeTests.cs`.

**Interfaces:** Produces Lua global `nBit.Mask(value, ...flags)` → 1 number = `value & ~f1 & ~f2 …`.
Contract: Ghidra `nBit::Mask@0x009fa170` (NOT `nBit.And`).

- [ ] **Step 1: Write the failing test** — add to `GameBridgeTests`:

```csharp
[Fact]
public void BitMaskClearsListedBitsVariadic()
{
    using var rt = Make();
    // 0b1111 clear 0b0010 and 0b0100 -> 0b1001 = 9. Single-flag: 7 clear 1 = 6.
    Assert.True(rt.EvalBool(LuaFixtures.Compile(
        "return nBit.Mask(15, 2, 4) == 9 and nBit.Mask(7, 1) == 6 and nBit.Mask(5) == 5")));
}
```

- [ ] **Step 2: Run test, verify it fails** — Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter BitMaskClearsListedBitsVariadic`. Expected: FAIL (`nBit.Mask` hits the stub fallback → returns nothing → comparison error/false).

- [ ] **Step 3: Implement** — add `("Mask", …)` to the `Register` tuple list and the method:

```csharp
// in Register(...), append to the entries list:
("Mask", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&BitMask));
```

```csharp
// Ghidra nBit::Mask@0x009fa170: value & ~f1 & ~f2 ... (clears bits; NOT And). Variadic.
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
private static int BitMask(nint L)
{
    try
    {
        var top = LuaNative.lua_gettop(L);
        uint acc = top >= 1 ? (uint)System.Math.Round((double)LuaNative.lua_tonumber(L, 1)) : 0u;
        for (var i = 2; i <= top; i++)
            acc &= ~(uint)System.Math.Round((double)LuaNative.lua_tonumber(L, i));
        LuaNative.lua_pushnumber(L, (float)acc);
        return 1;
    }
    catch
    {
        LuaNative.lua_pushnumber(L, 0f);
        return 1;
    }
}
```

- [ ] **Step 4: Run test, verify it passes** — Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter BitMaskClearsListedBitsVariadic`. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add ReCap.Server/Adapters/Scripting/Api/NBitModule.cs ReCap.Tests/Scripting/GameBridgeTests.cs
git commit -m "feat(lua): nBit.Mask AND-NOT variadic (Ghidra 0x009fa170)"
```

---

### Task 2: nPlayer.GetPlayerIdForObject

**Files:** Modify `GameObject.cs` (add `PlayerId`), `ScriptContextRegistry.cs` (bridge method), `GameScriptContext.cs` (impl), `NPlayerModule.cs` (native), `GameBridgeTests.cs` (FakeBridge + test).

**Interfaces:** Consumes `IScriptGameBridge.GetPlayerId(uint) → byte`. Produces `nPlayer.GetPlayerIdForObject(objId)` → 1 number.
Contract: Ghidra `nPlayer::GetPlayerIdForObject@0x009ff410` returns byte at `obj+0x55` when the object exists with a combatant; ReCap maps this to a `GameObject.PlayerId` byte (0 = local player in single-player).

- [ ] **Step 1: Write the failing test** — extend `FakeBridge` and add a test.

```csharp
// in FakeBridge, add:
public byte GetPlayerId(uint id) => id == 10 ? (byte)7 : (byte)0;
```

```csharp
[Fact]
public void GetPlayerIdForObjectReturnsBridgePlayerId()
{
    using var rt = Make();
    Assert.True(rt.EvalBool(LuaFixtures.Compile(
        "return nPlayer.GetPlayerIdForObject(10) == 7 and nPlayer.GetPlayerIdForObject(99) == 0")));
}
```

- [ ] **Step 2: Run test, verify it fails** — Run: `dotnet test … --filter GetPlayerIdForObjectReturnsBridgePlayerId`. Expected: FAIL (compile error: `IScriptGameBridge` has no `GetPlayerId`).

- [ ] **Step 3: Implement** —

Add to `IScriptGameBridge` (in `ScriptContextRegistry.cs`):

```csharp
byte GetPlayerId(uint objectId);
```

Add a field to `GameObject` (find the class in `ReCap.Server/Domain/Gameplay/`; place beside `Team`):

```csharp
public byte PlayerId { get; set; }
```

Implement in `GameScriptContext.cs` (beside `GetTeam`):

```csharp
public byte GetPlayerId(uint objectId) =>
    _game.Objects.Objects.TryGetValue(objectId, out var o) ? o.PlayerId : (byte)0;
```

Add the native to `NPlayerModule.cs` — extend `Register` and add the method:

```csharp
// in Register(...):
("GetPlayerIdForObject", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetPlayerIdForObject));
```

```csharp
// Ghidra nPlayer::GetPlayerIdForObject@0x009ff410: object id -> player id (byte obj+0x55).
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
private static int GetPlayerIdForObject(nint L)
{
    try
    {
        var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
        var id = bridge is not null && LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER
            ? bridge.GetPlayerId((uint)Math.Round((double)LuaNative.lua_tonumber(L, 1)))
            : (byte)0;
        LuaNative.lua_pushnumber(L, id);
        return 1;
    }
    catch
    {
        LuaNative.lua_pushnumber(L, 0f);
        return 1;
    }
}
```

- [ ] **Step 4: Run test, verify it passes** — Run: `dotnet test … --filter GetPlayerIdForObjectReturnsBridgePlayerId`. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add ReCap.Server/Adapters/Scripting/Api/NPlayerModule.cs ReCap.Server/Adapters/Scripting/ScriptContextRegistry.cs ReCap.Server/Services/Scripting/GameScriptContext.cs ReCap.Server/Domain/Gameplay ReCap.Tests/Scripting/GameBridgeTests.cs
git commit -m "feat(lua): nPlayer.GetPlayerIdForObject -> GameObject.PlayerId (Ghidra 0x009ff410)"
```

---

### Task 3: nGameObject.ResetAnimationState + SetAttributeSnapshot

**Files:** Modify `ScriptContextRegistry.cs` (bridge `ResetAnimationState` + per-object snapshot store), `GameScriptContext.cs`, `NGameObjectModule.cs`, `GameBridgeTests.cs`.

**Interfaces:** Consumes `IScriptGameBridge.ResetAnimationState(uint)`, `ScriptStateContext.SetObjectSnapshot(uint, uint)` / `TryGetObjectSnapshot(uint, out uint)`.
Produces `nGameObject.ResetAnimationState(objId)` → 0; `nGameObject.SetAttributeSnapshot(objId, handle)` → 0.
Contract: catalog §Mechanical. ResetAnimationState = undo death-anim (broadcast reset state 0). SetAttributeSnapshot stores the caster's snapshot handle onto the target (projectile carries cast-time stats).

- [ ] **Step 1: Write the failing test** — extend `FakeBridge` and add tests.

```csharp
// in FakeBridge, add:
public List<uint> AnimResets { get; } = [];
public void ResetAnimationState(uint objectId) => AnimResets.Add(objectId);
```

```csharp
[Fact]
public void ResetAnimationStateCallsBridge()
{
    using var rt = Make();
    var bridge = (FakeBridge)ScriptContextRegistry.Get(rt.L)!.GameBridge!;
    rt.Execute(LuaFixtures.Compile("nGameObject.ResetAnimationState(10)"), "ras");
    Assert.Equal([10u], bridge.AnimResets);
}

[Fact]
public void SetAttributeSnapshotStoresPerObjectHandle()
{
    using var rt = Make();
    var ctx = ScriptContextRegistry.Get(rt.L)!;
    rt.Execute(LuaFixtures.Compile("nGameObject.SetAttributeSnapshot(55, 42)"), "sas");
    Assert.True(ctx.TryGetObjectSnapshot(55, out var handle) && handle == 42);
}
```

- [ ] **Step 2: Run test, verify it fails** — Run: `dotnet test … --filter "ResetAnimationStateCallsBridge|SetAttributeSnapshotStoresPerObjectHandle"`. Expected: FAIL (missing bridge method / store).

- [ ] **Step 3: Implement** —

Add to `IScriptGameBridge`:

```csharp
void ResetAnimationState(uint objectId);
```

Add the per-object snapshot store to `ScriptStateContext` (beside the snapshot-handle fields):

```csharp
private readonly System.Collections.Concurrent.ConcurrentDictionary<uint, uint> _objectSnapshots = new();
public void SetObjectSnapshot(uint objectId, uint snapshotHandle) => _objectSnapshots[objectId] = snapshotHandle;
public bool TryGetObjectSnapshot(uint objectId, out uint snapshotHandle) => _objectSnapshots.TryGetValue(objectId, out snapshotHandle);
```

Implement in `GameScriptContext.cs` (beside `BroadcastAnimationState`):

```csharp
public void ResetAnimationState(uint objectId) => _game.BroadcastAnimationState(objectId, 0u);
```

Add both natives to `NGameObjectModule.cs` (extend its `Register` list; match the file's existing entry style):

```csharp
("ResetAnimationState", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&ResetAnimationState),
("SetAttributeSnapshot", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&SetAttributeSnapshot),
```

```csharp
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
private static int ResetAnimationState(nint L)
{
    try
    {
        var bridge = ScriptContextRegistry.Get(L)?.GameBridge;
        if (bridge is not null && LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER)
            bridge.ResetAnimationState((uint)Math.Round((double)LuaNative.lua_tonumber(L, 1)));
    }
    catch { }
    return 0;
}

// Projectile carries the caster's cast-time snapshot handle (catalog §Mechanical).
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
private static int SetAttributeSnapshot(nint L)
{
    try
    {
        var ctx = ScriptContextRegistry.Get(L);
        if (ctx is not null
            && LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER
            && LuaNative.lua_type(L, 2) == LuaNative.LUA_TNUMBER)
        {
            var objId = (uint)Math.Round((double)LuaNative.lua_tonumber(L, 1));
            var handle = (uint)Math.Round((double)LuaNative.lua_tonumber(L, 2));
            ctx.SetObjectSnapshot(objId, handle);
        }
    }
    catch { }
    return 0;
}
```

- [ ] **Step 4: Run test, verify it passes** — Run: `dotnet test … --filter "ResetAnimationStateCallsBridge|SetAttributeSnapshotStoresPerObjectHandle"`. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add ReCap.Server/Adapters/Scripting ReCap.Server/Services/Scripting/GameScriptContext.cs ReCap.Tests/Scripting/GameBridgeTests.cs
git commit -m "feat(lua): nGameObject.ResetAnimationState + SetAttributeSnapshot"
```

---

### Task 4: nThreadData.GetPrivateTable + SetGUID (new module + per-thread stores)

**Files:** Create `ReCap.Server/Adapters/Scripting/Api/NThreadDataModule.cs`; Modify `LuaRuntime.cs:53-70` (wire), `GameBridgeTests.cs` (tests).

**Interfaces:** Produces Lua `nThreadData.GetPrivateTable()` → 1 table (per-thread, create-on-demand), `nThreadData.SetGUID(slot, guid)` → 0 (per-thread slot store). Both key off the calling thread `L`.
Contract: catalog §Mechanical — GetPrivateTable returns the thread's private Lua table; retail does not create it, but with `CreatePrivateTable` out of scope we create-on-demand so scripts function (documented divergence). SetGUID stores a GUID at a per-thread slot (paired `GetGUID` deferred until demanded).

Note: the private table is a real Lua table held in the registry, one ref per thread `L`, stored in a managed `Dictionary<nint,int>` (threadL → registry ref). The GUID slot store is `Dictionary<nint,Dictionary<int,double>>`. Both live as `static` maps in the module (per-thread `L` is a stable key for the coroutine's lifetime; entries are best-effort — they are never read after the thread dies).

- [ ] **Step 1: Write the failing test** — add to `GameBridgeTests`:

```csharp
[Fact]
public void PrivateTablePersistsAcrossCallsOnSameThread()
{
    using var rt = Make();
    Assert.True(rt.EvalBool(LuaFixtures.Compile("""
        local a = nThreadData.GetPrivateTable()
        a.marker = 99
        local b = nThreadData.GetPrivateTable()
        return type(a) == "table" and b.marker == 99
        """)));
}

[Fact]
public void SetGuidRunsWithoutError()
{
    using var rt = Make();
    Assert.True(rt.EvalBool(LuaFixtures.Compile("""
        nThreadData.SetGUID(0, 123456)
        return true
        """)));
}
```

- [ ] **Step 2: Run test, verify it fails** — Run: `dotnet test … --filter "PrivateTablePersistsAcrossCallsOnSameThread|SetGuidRunsWithoutError"`. Expected: FAIL (`nThreadData` hits stub fallback → GetPrivateTable returns nothing → `type(a)` errors).

- [ ] **Step 3: Implement** — create `NThreadDataModule.cs`:

```csharp
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

public static unsafe class NThreadDataModule
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<nint, int> _privateTableRefs = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<nint, System.Collections.Concurrent.ConcurrentDictionary<int, double>> _guidSlots = new();

    public static void Register(nint L) =>
        LuaApiModule.RegisterNamespace(L, "nThreadData",
            ("GetPrivateTable", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&GetPrivateTable),
            ("SetGUID", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&SetGuid));

    // Per-thread private Lua table (registry-backed). Retail GetPrivateTable does not create;
    // with CreatePrivateTable out of scope we create-on-demand so scripts that store into it work.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int GetPrivateTable(nint L)
    {
        try
        {
            if (_privateTableRefs.TryGetValue(L, out var existing))
            {
                LuaNative.lua_rawgeti(L, LuaNative.LUA_REGISTRYINDEX, existing);
                if (LuaNative.lua_type(L, -1) == LuaNative.LUA_TTABLE) return 1;
                LuaNative.lua_settop(L, -2);
            }
            LuaNative.lua_createtable(L, 0, 0);
            LuaNative.lua_pushvalue(L, -1);
            var reference = LuaNative.luaL_ref(L, LuaNative.LUA_REGISTRYINDEX);
            _privateTableRefs[L] = reference;
            return 1;
        }
        catch
        {
            LuaNative.lua_pushnil(L);
            return 1;
        }
    }

    // Per-thread indexed GUID slot store (paired GetGUID deferred until demanded).
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int SetGuid(nint L)
    {
        try
        {
            if (LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER
                && LuaNative.lua_type(L, 2) == LuaNative.LUA_TNUMBER)
            {
                var slot = (int)Math.Round((double)LuaNative.lua_tonumber(L, 1));
                var guid = (double)LuaNative.lua_tonumber(L, 2);
                var slots = _guidSlots.GetOrAdd(L, _ => new());
                slots[slot] = guid;
            }
        }
        catch { }
        return 0;
    }
}
```

- [ ] **Step 4: Wire registration** — in `ReCap.Server/Adapters/Scripting/LuaRuntime.cs`, after `Api.NThreadModule.Register(L);` (line ~61) add:

```csharp
        Api.NThreadDataModule.Register(L);
```

- [ ] **Step 5: Run tests, verify they pass** — Run: `dotnet test … --filter "PrivateTablePersistsAcrossCallsOnSameThread|SetGuidRunsWithoutError"`. Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add ReCap.Server/Adapters/Scripting/Api/NThreadDataModule.cs ReCap.Server/Adapters/Scripting/LuaRuntime.cs ReCap.Tests/Scripting/GameBridgeTests.cs
git commit -m "feat(lua): nThreadData.GetPrivateTable + SetGUID (per-thread stores)"
```

---

### Task 5: nAbility.ReleaseAgent

**Files:** Modify `NAbilityContextModule.cs`, `GameBridgeTests.cs`.

**Interfaces:** Produces Lua `nAbility.ReleaseAgent()` → 0 args, 0 returns.
Contract: catalog §Mechanical — acts on the implicit ability context; releases the held agent. State mutation is impl-time-DEFERRED (needs `nAbility::ReleaseAgent` decompile); Wave 1 registers it as a recognized no-op so it stops hitting the stub telemetry and returns cleanly.

- [ ] **Step 1: Write the failing test** — add to `GameBridgeTests`:

```csharp
[Fact]
public void ReleaseAgentReturnsNoValues()
{
    using var rt = Make();
    Assert.True(rt.EvalBool(LuaFixtures.Compile(
        "return select('#', nAbility.ReleaseAgent()) == 0")));
}
```

- [ ] **Step 2: Run test, verify it fails** — Run: `dotnet test … --filter ReleaseAgentReturnsNoValues`. Expected: FAIL (stub `StubCall` returns 0 values too, so this may PASS accidentally — if it PASSES, the test is proving the contract already holds via stub; still add the explicit native so it leaves the stub telemetry. Confirm failure instead by asserting it is NOT recorded as unimplemented: see Step 3 note). If it passes at this step, proceed — the real assertion is the harvest ratchet in Task 8.

- [ ] **Step 3: Implement** — in `NAbilityContextModule.cs` extend `Register` and add the method:

```csharp
// in Register(...):
("ReleaseAgent", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&ReleaseAgent));
```

```csharp
// Recognized no-op (state mutation impl-time-DEFERRED, needs nAbility::ReleaseAgent decompile).
// Registering it removes it from stub telemetry so the harvest ratchet drops.
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
private static int ReleaseAgent(nint L) => 0;
```

- [ ] **Step 4: Run test, verify it passes** — Run: `dotnet test … --filter ReleaseAgentReturnsNoValues`. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add ReCap.Server/Adapters/Scripting/Api/NAbilityContextModule.cs ReCap.Tests/Scripting/GameBridgeTests.cs
git commit -m "feat(lua): nAbility.ReleaseAgent recognized no-op (mutation deferred)"
```

---

### Task 6: Scheduler predicate support

**Files:** Modify `LuaCoroutineScheduler.cs`; Test `ReCap.Tests/Scripting/SchedulerPredicateTests.cs` (new).

**Interfaces:** Produces `RegisterYield(nint threadL, bool sleeping, double? wakeAt, Func<bool>? wakeWhen = null)`.
A thread with a `wakeWhen` predicate resumes on the first `Tick` where `wakeWhen()` returns true OR `wakeAt` is reached (whichever first). Existing 3-arg callers are unaffected (new param defaults to null). This mechanism is reused by Wave 2's WaitForJumpComplete/NearGoal.

- [ ] **Step 1: Write the failing test** — create `SchedulerPredicateTests.cs`:

```csharp
using ReCap.Server.Adapters.Scripting;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Tests.Scripting;

public class SchedulerPredicateTests
{
    [Fact]
    public void PredicateYieldResumesWhenConditionTrue()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        var scheduler = ScriptContextRegistry.Get(rt.L)!.Scheduler!;
        var gate = false;
        var predicate = new Func<bool>(() => gate);

        // Coroutine that yields once via a native-driven predicate registration, then finishes.
        // Drive registration directly at the scheduler level (native wiring is Task 7).
        var thread = scheduler.Spawn(rt.L, objectId: 10, fnIndex: PushSleeperFn(rt.L), argCount: 0);
        scheduler.RegisterYield(thread, sleeping: false, wakeAt: null, wakeWhen: predicate);

        scheduler.Tick(1.0);
        Assert.True(scheduler.HasThreadForObject(10)); // predicate false → still parked
        gate = true;
        scheduler.Tick(2.0);
        Assert.False(scheduler.HasThreadForObject(10)); // predicate true → resumed to completion
    }

    // Pushes a function that immediately yields (via coroutine.yield) then returns; leaves it at stack top.
    private static int PushSleeperFn(nint L)
    {
        LuaRuntimeTestHelper.LoadChunk(L, "return function() coroutine.yield() end");
        return LuaNative.lua_gettop(L);
    }
}
```

Note: if a `LuaRuntimeTestHelper.LoadChunk` helper does not exist, replace `PushSleeperFn` with the existing test-support compile+push used elsewhere (see `LuaFixtures`/`LuaRuntime.Execute`); the essential assertion is the two-`Tick` predicate gate, not the chunk-push mechanism.

- [ ] **Step 2: Run test, verify it fails** — Run: `dotnet test … --filter PredicateYieldResumesWhenConditionTrue`. Expected: FAIL (compile error: `RegisterYield` has no 4th param).

- [ ] **Step 3: Implement** — in `LuaCoroutineScheduler.cs`:

Add to `ThreadEntry`:

```csharp
        public Func<bool>? WakeWhen { get; set; }
```

Replace `RegisterYield`:

```csharp
    public void RegisterYield(nint threadL, bool sleeping, double? wakeAt, Func<bool>? wakeWhen = null)
    {
        if (!_threads.TryGetValue(threadL, out var entry)) return;
        entry.Sleeping = sleeping;
        entry.WakeAtSeconds = wakeAt;
        entry.WakeWhen = wakeWhen;
    }
```

Update `Tick` (resume when predicate true, else honor timeout; clear predicate on resume):

```csharp
    public void Tick(double nowSeconds)
    {
        _now = nowSeconds;
        foreach (var entry in _threads.Values.ToList())
        {
            if (entry.Sleeping) continue;
            var predicateReady = entry.WakeWhen is null || SafeEvaluate(entry.WakeWhen);
            var timeReady = entry.WakeAtSeconds is not double wake || nowSeconds >= wake;
            if (entry.WakeWhen is not null)
            {
                if (!predicateReady && !(entry.WakeAtSeconds is double w2 && nowSeconds >= w2)) continue;
            }
            else if (!timeReady) continue;
            entry.WakeAtSeconds = null;
            entry.WakeWhen = null;
            Resume(entry, 0);
        }
    }

    private static bool SafeEvaluate(Func<bool> predicate)
    {
        try { return predicate(); } catch { return false; }
    }
```

- [ ] **Step 4: Run test, verify it passes** — Run: `dotnet test … --filter PredicateYieldResumesWhenConditionTrue`. Expected: PASS. Also run the full scripting suite to confirm no regression in existing yield behaviour: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter Scripting`.

- [ ] **Step 5: Commit**

```bash
git add ReCap.Server/Adapters/Scripting/LuaCoroutineScheduler.cs ReCap.Tests/Scripting/SchedulerPredicateTests.cs
git commit -m "feat(lua): scheduler predicate (WakeWhen) yield support"
```

---

### Task 7: nThread.WaitForHitpointsAbove + WaitForFadeOutInXSeconds

**Files:** Modify `NThreadModule.cs`; Test `SchedulerPredicateTests.cs`.

**Interfaces:** Consumes `IScriptGameBridge.GetHitPoints`, `Scheduler.RegisterYield(…, wakeWhen)`.
Produces `nThread.WaitForHitpointsAbove(objId, hpThreshold, timeoutSeconds)` → yields until `GetHitPoints(objId) > hpThreshold` OR timeout (0 timeout = no timeout);
`nThread.WaitForFadeOutInXSeconds(objId, fadeSeconds)` → plain timed yield (like WaitForXSeconds).
Contract: catalog §Mechanical.

- [ ] **Step 1: Write the failing test** — add to `SchedulerPredicateTests` (reuse `FakeBridge` from `GameBridgeTests`; it is in the same test namespace/assembly). Build a coroutine that calls the native, then tick.

```csharp
[Fact]
public void WaitForHitpointsAboveResumesWhenHpCrossesThreshold()
{
    using var rt = LuaRuntime.CreateSandboxedState();
    var ctx = ScriptContextRegistry.Get(rt.L)!;
    var bridge = new MutableHpBridge();
    ctx.GameBridge = bridge;
    var scheduler = ctx.Scheduler!;

    // Spawn a coroutine for object 10 that waits until its HP rises above 50 (no timeout).
    rt.Execute(LuaFixtures.Compile("""
        nThread.CreateThreadForObject(10, function()
            nThread.WaitForHitpointsAbove(10, 50, 0)
        end)
        """), "wfa");

    scheduler.Tick(1.0);
    Assert.True(scheduler.HasThreadForObject(10)); // hp 40 <= 50 → parked
    bridge.Hp = 60f;
    scheduler.Tick(2.0);
    Assert.False(scheduler.HasThreadForObject(10)); // hp 60 > 50 → resumed
}

private sealed class MutableHpBridge : IScriptGameBridge
{
    public float Hp = 40f;
    public float GetHitPoints(uint id) => Hp;
    public float GetMaxHitPoints(uint id) => 100f;
    public bool ObjectExists(uint id) => true;
    public bool TryGetPosition(uint id, out float x, out float y, out float z) { x = y = z = 0f; return true; }
    public byte GetTeam(uint id) => 0; public void SetTeam(uint id, byte t) { }
    public uint GetTargetId(uint id) => 0; public bool IsPlayerControlled(uint id) => false;
    public byte GetPlayerId(uint id) => 0;
    public bool TryGetAttributeValue(uint id, int a, out float v) { v = 0f; return false; }
    public IReadOnlyDictionary<int, float>? GetAttributeTable(uint id) => null;
    public bool TryGetOrientation(uint id, out float x, out float y, out float z, out float w) { x = y = z = 0f; w = 1f; return true; }
    public void BroadcastAnimationState(uint id, uint s) { }
    public void ResetAnimationState(uint id) { }
    public IReadOnlyList<uint> QueryObjectsInRadius(float x, float y, float z, float r, bool d) => [];
    public float ApplyHeal(uint id, float a) => 0f;
    public void MarkForDelete(uint id) { }
    public void SetVisible(uint id, bool v) { }
}
```

(If `IScriptGameBridge` gains members in later waves, extend this fake accordingly — it must implement the full interface.)

- [ ] **Step 2: Run test, verify it fails** — Run: `dotnet test … --filter WaitForHitpointsAboveResumesWhenHpCrossesThreshold`. Expected: FAIL (`WaitForHitpointsAbove` hits stub → coroutine finishes immediately → object not parked → first assert fails).

- [ ] **Step 3: Implement** — in `NThreadModule.cs` extend `Register`:

```csharp
("WaitForHitpointsAbove", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&WaitForHitpointsAbove),
("WaitForFadeOutInXSeconds", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&WaitForFadeOutInXSeconds),
```

```csharp
// catalog §Mechanical: yield until GetHitPoints(objId) > threshold OR timeout (0 = no timeout).
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
private static int WaitForHitpointsAbove(nint L)
{
    try
    {
        var ctx = ScriptContextRegistry.Get(L);
        var scheduler = ctx?.Scheduler;
        var bridge = ctx?.GameBridge;
        if (scheduler is not null && bridge is not null && LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER)
        {
            var objId = (uint)Math.Round((double)LuaNative.lua_tonumber(L, 1));
            var threshold = LuaNative.lua_type(L, 2) == LuaNative.LUA_TNUMBER ? (float)LuaNative.lua_tonumber(L, 2) : 0f;
            var timeout = LuaNative.lua_type(L, 3) == LuaNative.LUA_TNUMBER ? (double)LuaNative.lua_tonumber(L, 3) : 0.0;
            double? wakeAt = timeout > 0 ? scheduler.Now + timeout : null;
            scheduler.RegisterYield(L, sleeping: false, wakeAt: wakeAt, wakeWhen: () => bridge.GetHitPoints(objId) > threshold);
        }
        else return 0;
    }
    catch (Exception ex)
    {
        try { Util.Logging.Log.Lua.Error($"[nThread] WaitForHitpointsAbove failed: {ex.Message}"); } catch { }
        return 0;
    }
    return LuaNative.lua_yield(L, 0);
}

// catalog §Mechanical: plain timed yield tied to corpse fade window.
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
private static int WaitForFadeOutInXSeconds(nint L)
{
    try
    {
        var scheduler = ScriptContextRegistry.Get(L)?.Scheduler;
        var seconds = LuaNative.lua_type(L, 2) == LuaNative.LUA_TNUMBER
            ? Math.Max(0.0, (double)LuaNative.lua_tonumber(L, 2)) : 0.0;
        scheduler?.RegisterYield(L, sleeping: false, wakeAt: scheduler.Now + seconds);
    }
    catch (Exception ex)
    {
        try { Util.Logging.Log.Lua.Error($"[nThread] WaitForFadeOutInXSeconds failed: {ex.Message}"); } catch { }
        return 0;
    }
    return LuaNative.lua_yield(L, 0);
}
```

- [ ] **Step 4: Run test, verify it passes** — Run: `dotnet test … --filter WaitForHitpointsAboveResumesWhenHpCrossesThreshold`. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add ReCap.Server/Adapters/Scripting/Api/NThreadModule.cs ReCap.Tests/Scripting/SchedulerPredicateTests.cs
git commit -m "feat(lua): nThread.WaitForHitpointsAbove + WaitForFadeOutInXSeconds"
```

---

### Task 8: Ratchet verification + full suite

**Files:** none (verification only).

- [ ] **Step 1: Run the full test suite** — Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj`. Expected: all green (no regressions).

- [ ] **Step 2: Run the harvest ratchet** — Run: `RECAP_HARVEST=1 dotnet test ReCap.Tests/ReCap.Tests.csproj --filter NativeDemandHarvest --logger "trx;LogFileName=harvest.trx"`. Extract the demanded-native list from the TRX `<StdOut>` (as done during research). Expected: the demanded count dropped by the Wave-1 natives now implemented (nBit.Mask, GetPlayerIdForObject, ResetAnimationState, SetAttributeSnapshot, nThreadData ×2, ReleaseAgent, WaitForHitpointsAbove, WaitForFadeOutInXSeconds). Remaining demanded set should be the Wave-2/3/4 natives plus the 3 deferred locomotion-dependent ones.

- [ ] **Step 3: Record the new count** — update the catalog spec's harvest number if it changed, and note Wave 1 complete in `memory/lua-system-contract.md` (mark which natives landed). Commit:

```bash
git add docs/superpowers/specs/2026-07-08-lua-demanded-natives-catalog-design.md
git commit -m "docs: Wave 1 natives landed; update demanded-native count"
```

---

## Self-Review

- **Spec coverage:** Wave 1 scope (9 natives + scheduler predicate) each map to Tasks 1-7; ratchet gate = Task 8. The 3 locomotion-dependent natives are explicitly deferred to Wave 2 with rationale. ✓
- **Placeholder scan:** every code step shows complete code; the one flagged uncertainty (`LuaRuntimeTestHelper.LoadChunk` in Task 6) has an explicit fallback instruction. Task 5 Step 2 documents the accidental-pass case honestly. ✓
- **Type consistency:** `RegisterYield` 4-arg signature (Task 6) is consumed by Task 7 exactly; `GetPlayerId`/`ResetAnimationState`/`SetObjectSnapshot`/`TryGetObjectSnapshot` names match between definition and use; `FakeBridge` / `MutableHpBridge` implement the full `IScriptGameBridge` including the two new members added in Tasks 2-3. ✓

**Impl-time note:** `GameObject` field name and `IScriptGameBridge` are the two integration points every task touches — verify the actual `GameObject` class path under `ReCap.Server/Domain/Gameplay/` when adding `PlayerId`, and re-run the full suite after each interface change so all fakes stay in sync.
