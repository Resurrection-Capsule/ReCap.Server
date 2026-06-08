# Lua Scripting P3 Implementation Plan — Scheduler, Registrars, Ability Tick Gate

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A real retail ability chunk runs its `tick` coroutine to completion inside a test Game — registrars store script tables, nThread yields/resumes across Game.Update ticks, and the object bridge serves retail-contract reads.

**Architecture:** Boot-fidelity fixes make the 16 boot failures collapse to 13 tagged retail-missing warnings. Registrar natives store Lua table refs in per-state C# registries keyed by FNV(name). A per-context coroutine scheduler replicates the client's cLuaThread model (one thread per object, resume-condition polling) ticked from `Game.Update`. `GameScriptContext` binds a booted runtime + scheduler + registries to a `Game`; nGameObject/nAbility natives marshal per the **verified retail contract: objects are numeric IDs (float), positions are 3 separate floats**.

**Tech Stack:** .NET 9 (net10.0 TFM), custom recaplua51.dll (float ABI), xUnit, real ServerData.package for integration gates.

**Prereqs done (P0–P2):** LuaNative/LuaRuntime/sandbox/require, ScriptVfs, 28 stub namespaces + StubTelemetry, ScriptEngine boot (1,017 chunks), GameInstallLocator.

---

## VERIFIED CONTRACTS (embed-in-head before any task — all Ghidra/client-decompile or package-probe evidence, 2026-06-05)

**C1 — Object identity:** scripts hold objects as **uint32 IDs pushed as `lua_Number` (float)**. No userdata/tables. Read pattern (client `ObjectManager::GetObjectFromLuaArg` @0x009f9740): if `lua_type==LUA_TNUMBER` → `(uint)Math.Round((double)lua_tonumber(...))`. Replicate exactly (PushId = `lua_pushnumber((float)id)`).

**C2 — Positions:** `nGameObject.GetPosition(id)` returns **3 separate floats x, y, z** (client @0x009fb870 pushes 3 numbers; on missing object it raises a lua error "Could not find object!"). NOT a vec3 table/userdata (dalkon's vec3 usertype was his approximation).

**C3 — Return arities (client-decompiled, MUST match — scripts consume returns):**
| Native | Args | Returns |
|---|---|---|
| n*.Register{Ability,Modifier,Affix,Condition,Objective} | (string name, table props) | **0** |
| nAbility.PreloadAsset | (string assetName, string propName) | **1** number (handle) |
| nAbility.PreloadAnimation | (assetNameOrId, string animName) | **1** number (resolved hash/id) |
| nAbility.PreloadModifier | (objectIdOrNumber, string modName) | **1** number (echoes objectId) |
| nBit.Or (and And/Xor/…) | variadic numbers | **1** number (round each → uint, fold op) |
| nUtil.GetAsset | (string name) | **1** number (opaque handle) |
| nThread.Sleep / WaitForever | () | yields **0** values |
| nThread.WaitForXSeconds | (float seconds [, objectId [, bool]]) | yields **0** values |
| nThread.WakeUp | (objectId) | **0** |
| nThread.CreateThreadForObject | (objectId, function, ...args) | **0** |
| nGameObject.GetHitPoints / GetMaxHitPoints | (objectId) | **1** float (0.0 if object missing) |
| nGameObject.IsAlive | (objectId) | **1** bool (hp>0; false if missing) |
| nGameObject.GetTeam | (objectId) | **1** number, or **0 values** if object missing |
| nGameObject.GetTargetID | (objectId) | **1** number (0 if missing) |
| nAbility.GetAgentID / GetTargetID | () — context-based, no args | **1** number from the running ability's invocation context |

**C4 — Register semantics (client @0x00a43040 RegisterAbility; same template for Condition @0x00a430e0, Affix @0x00a0c570, Objective @0x00a0c340; nModifier.RegisterModifier IS the same native as RegisterAbility):** FNV(name) → per-kind registry; **if key exists → skip silently**; else store; return 0 values. Server-side equivalent: keep a `luaL_ref` to the props table + name + hash.

**C5 — Thread model (client):** one coroutine per object (guard: object's thread slot empty), created+resumed immediately; `Sleep` sets a sleeping flag cleared only by `WakeUp(objectId)`; `WaitForever` never auto-resumes; `WaitForXSeconds` stores a target duration, scheduler polls a resume condition each tick; completed/errored threads are released. Yield always carries 0 values; resume passes 0 results.

**C6 — Boot fixes (package-probe evidence):**
- `Class` OOP helper is defined by **global.lua = (group 0x3681D755, instance 0x57572DAC)**, which sorts to position ~36; a position-13 chunk requires `Abilities!ability_spawn.lua` which uses `Class` at module level → must pre-execute global.lua before the group loop.
- `Objectives!objective_LootCrystals.lua` exists at **(0x3681D755, 0x52528866)** — require uses group "Objectives" (0xE3B800E9) which is NOT where it lives → GetChunk needs instance-only fallback on exact-key miss.
- `Modifiers!modifier_shadowravager_support_fear.lua.lua` (double ext in retail source) — target exists as instance 0x0BB982E2 = FNV("modifier_shadowravager_support_fear") → ParseReference must strip repeated trailing ".lua".
- Remaining **13 failures are retail-missing content** (chunks genuinely absent from the package, incl. the "Modifers" typo family) — they must become tagged warnings, not errors.

**C7 — ABI rules (established P0–P2, non-negotiable):** every `[UnmanagedCallersOnly]` callback is exception-proof (try/catch, safe-value fallback); `lua_error` only from a point with NO enclosing managed try; non-coercing reads (`lua_type` check before `ToManagedString`); all numbers are `float`.

**Hard rules:** NEVER `git add -A`/`-a`; stage only listed paths. NEVER Co-Authored-By/Generated-with. No code comments (exception: verified-cite one-liners). File-scoped namespaces. Working tree has unrelated user WIP (GameStorageAdapter.cs, LocaleStore.cs untracked + Api.cs modified — never stage).

---

## File Map

| Path | Responsibility |
|---|---|
| `ReCap.Server/Adapters/Scripting/ScriptVfs.cs` (mod) | GetChunk instance-fallback; ParseReference repeated-ext strip |
| `ReCap.Server/Services/Scripting/ScriptEngine.cs` (mod) | global.lua pre-exec; BootReport {Failures, MissingRequires} split |
| `ReCap.Server/Adapters/Persistence/PackageMounts.cs` (mod) | explicit `Initialize(dataDir)` + config fallback |
| `ReCap.Server/Adapters/Scripting/LuaRuntime.cs` (mod) | watchdog hook install; thread-state helpers |
| `ReCap.Server/Adapters/Scripting/ScriptContextRegistry.cs` (new) | static L→context map (main + thread states) |
| `ReCap.Server/Adapters/Scripting/ScriptRegistry.cs` (new) | per-context Register* storage (kind → hash → entry) |
| `ReCap.Server/Adapters/Scripting/Api/RegistrarModule.cs` (new) | Register*/Preload*/nBit/GetAsset natives |
| `ReCap.Server/Adapters/Scripting/LuaCoroutineScheduler.cs` (new) | thread pool, resume conditions, Tick |
| `ReCap.Server/Adapters/Scripting/Api/NThreadModule.cs` (new) | Sleep/WaitForever/WaitForXSeconds/WakeUp/CreateThreadForObject |
| `ReCap.Server/Adapters/Scripting/Api/NGameObjectModule.cs` (new) | read-subset bridge (C1/C2/C3) |
| `ReCap.Server/Adapters/Scripting/Api/NAbilityContextModule.cs` (new) | GetAgentID/GetTargetID/GetTargetPosition from invocation ctx |
| `ReCap.Server/Services/Scripting/GameScriptContext.cs` (new) | per-Game: runtime+scheduler+registries+InvokeAbility |
| `ReCap.Server/Domain/Gameplay/Game.cs` (mod) | ScriptContext property; Update hook; position accessor |
| `ReCap.Server/Domain/Gameplay/ObjectManager.cs` (mod) | `GameObject.TargetId uint` field |
| `ReCap.Tests/Scripting/{BootFidelityTests,ScriptRegistryTests,SchedulerTests,GameBridgeTests,AbilityTickGateTests}.cs` (new) | per-task tests |
| `docs/architecture/research/LUA_REGISTRAR_TABLES.md` + `VERIFIED_FACTS.md` (mod) | contract addenda (T7 findings, marshalling) |

Integration tests use the self-skip pattern from `ScriptEngineBootTests.FindDataDir()` (env `RECAP_GAME_DATA` → known `C:\CodingProjects\Personal\Darkspore\Data` → skip).

---

### Task 1: Boot fidelity — pre-exec global.lua, require fallbacks, retail-missing classification

**Files:** Modify `ReCap.Server/Adapters/Scripting/ScriptVfs.cs`, `ReCap.Server/Services/Scripting/ScriptEngine.cs`; Test `ReCap.Tests/Scripting/BootFidelityTests.cs` (+ edit `ScriptEngineBootTests.cs` ratchet).

- [ ] **Step 1: Failing unit tests** (`BootFidelityTests.cs`):

```csharp
using ReCap.Server.Adapters.Scripting;

namespace ReCap.Tests.Scripting;

public class BootFidelityTests
{
    [Fact]
    public void ParseReferenceStripsRepeatedLuaExtensions()
    {
        var key = ScriptVfs.ParseReference("Modifiers!modifier_shadowravager_support_fear.lua.lua");
        Assert.Equal(ScriptVfs.Hash("modifier_shadowravager_support_fear"), key.InstanceId);
        Assert.Equal(ScriptVfs.Hash("Modifiers"), key.GroupId);
    }

    [Fact]
    public void ParseReferenceSingleExtensionUnchanged()
    {
        var key = ScriptVfs.ParseReference("Lua!GlobalDefinitions.lua");
        Assert.Equal(ScriptVfs.Hash("GlobalDefinitions"), key.InstanceId);
    }
}
```

- [ ] **Step 2:** Run `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~BootFidelityTests"` → FAIL (first test: name keeps ".lua").

- [ ] **Step 3: ScriptVfs changes.** In `ParseReference`, after computing `name`/`ext`, loop-strip:

```csharp
        while (name.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
            name = name[..^4];
```

(Place after the existing dot-split; `ext` stays "lua".) In `GetChunk`, add instance-only fallback when the exact key misses (C6 cross-group case):

```csharp
        if (key.GroupId != 0)
        {
            if (chunks.TryGetValue((key.GroupId, key.InstanceId), out var exact))
                return exact;
            foreach (var ((_, i), bytes) in chunks)
                if (i == key.InstanceId) return bytes;
            return null;
        }
```

- [ ] **Step 4: ScriptEngine changes.** Add pre-exec of global.lua before the group loop in `ExecuteBootScripts` (cite-comment allowed: verified C6):

```csharp
    private static readonly (uint Group, uint Instance)[] PreBootChunks =
    [
        (0x3681D755, 0x57572DAC),
    ];
```

In `ExecuteBootScripts`, before the `BootGroupOrder` loop: for each PreBootChunks entry, `vfs.GetChunk(new ScriptKey(g, i, 0))` → if non-null, `report.Total++` + execute with chunk name `"0x{g:X8}!0x{i:X8}"` in the same try/catch. Track executed instances in a `HashSet<(uint,uint)>` and SKIP them when the group loop reaches them (no double-exec).

Split the failure classes in `BootReport`:

```csharp
public sealed class BootReport
{
    public int Total { get; set; }
    public List<string> Failures { get; } = [];
    public List<string> MissingRequires { get; } = [];
}
```

In the catch: if `ex.Message.Contains("require: chunk not found")` → `report.MissingRequires.Add(ex.Message)` + `Log.Lua.Warn($"[retail-missing] {ex.Message}")`; else `report.Failures.Add` + `Log.Lua.Error`. Update the `--lua-smoke` block in `Program.cs` only if it reads `Failures` for its count line — report both counts (`failures=N retail-missing=M`).

- [ ] **Step 5: Tighten the integration ratchet** in `ScriptEngineBootTests.BootsAllServerDataScripts`:

```csharp
        Assert.True(report.Total >= 1000, $"expected ~1018 chunks, executed {report.Total}");
        Assert.Empty(report.Failures);
        Assert.True(report.MissingRequires.Count <= 13,
            $"retail-missing grew: {report.MissingRequires.Count}\n{string.Join("\n", report.MissingRequires)}");
```

- [ ] **Step 6:** Run full suite + integration: `dotnet test ReCap.Tests/ReCap.Tests.csproj` → all green; the boot test now asserts ZERO hard failures. Report actual counts.

- [ ] **Step 7: Commit**

```powershell
git add ReCap.Server/Adapters/Scripting/ScriptVfs.cs ReCap.Server/Services/Scripting/ScriptEngine.cs ReCap.Server/Program.cs ReCap.Tests/Scripting/BootFidelityTests.cs ReCap.Tests/Scripting/ScriptEngineBootTests.cs
git commit -m "fix(scripting): boot fidelity - global.lua pre-exec, require fallbacks, retail-missing classification"
```

---

### Task 2: Infra handoff — PackageMounts.Initialize, watchdog hook, telemetry context tags

**Files:** Modify `ReCap.Server/Adapters/Persistence/PackageMounts.cs`, `ReCap.Server/Adapters/Scripting/LuaRuntime.cs`, `ReCap.Server/Adapters/Scripting/Api/LuaApiModule.cs` (StubTelemetry), `ReCap.Server/Program.cs`; Test additions in `ReCap.Tests/Scripting/LuaRuntimeTests.cs`.

- [ ] **Step 1: PackageMounts explicit init.** Replace the `Lazy<PackageMounts?>` with:

```csharp
    private static PackageMounts? _initialized;
    private static readonly Lazy<PackageMounts?> _fromConfig = new(BuildDefault);
    public static PackageMounts? Default => _initialized ?? _fromConfig.Value;
    public static void Initialize(string dataDir) => _initialized = new PackageMounts(dataDir);
```

(`BuildDefault` unchanged.) In `Program.cs`, immediately after the locator success branch sets `serverOpts.DataDir`: call `PackageMounts.Initialize(install.DataDir);` (place AFTER `ServerConfig` receives the options so both paths agree — read the surrounding code and keep ordering coherent).

- [ ] **Step 2: Watchdog.** In `LuaNative` the binding exists (`lua_sethook`). Add to `LuaRuntime`:

```csharp
    private const int WatchdogInstructionBudget = 50_000_000;

    internal static void InstallWatchdog(nint threadState)
    {
        unsafe
        {
            LuaNative.lua_sethook(threadState,
                (nint)(delegate* unmanaged[Cdecl]<nint, nint, void>)&LuaStubs.WatchdogHook,
                LuaNative.LUA_MASKCOUNT, WatchdogInstructionBudget);
        }
    }
```

`LuaStubs.WatchdogHook` (hook signature `void (lua_State*, lua_Debug*)`):

```csharp
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    internal static void WatchdogHook(nint L, nint ar)
    {
        LuaNative.lua_pushstring(L, "instruction budget exceeded");
        LuaNative.lua_error(L);
    }
```

(`lua_error` here is bare — no managed try in this method; the raise lands in the enclosing pcall/resume. This is the ONE place a raise happens inside the VM run; keep the method exactly this small.) Call `InstallWatchdog(L)` at the end of `CreateSandboxedState` (covers the main state; Task 5 installs it on each spawned thread).

- [ ] **Step 3: Telemetry context tags.** In `StubTelemetry` add:

```csharp
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<nint, string> _tags = new();
    public static void TagState(nint L, string tag) => _tags[L] = tag;
    public static void UntagState(nint L) => _tags.TryRemove(L, out _);
    private static string Tag(nint L) => _tags.TryGetValue(L, out var t) ? t : "untagged";
```

Change `RecordLookup`/`RecordCall` to accept `nint L` (callers in `LuaApiModule.StubIndex/StubCall` pass their `L`) and key entries as `$"{Tag(L)}|{ns}.{key}"`; `Snapshot()` unchanged shape. `LuaRuntime.CreateSandboxedState` gains optional `string contextTag = "boot"` → `StubTelemetry.TagState(L, contextTag)`; `Dispose` untags.

- [ ] **Step 4: Test additions** (`LuaRuntimeTests.cs`):

```csharp
    [Fact]
    public void WatchdogAbortsRunawayLoop()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        Assert.Throws<LuaScriptException>(
            () => rt.Execute(LuaFixtures.Compile("while true do end"), "runaway"));
    }
```

- [ ] **Step 5:** Full suite green (`dotnet test ReCap.Tests/ReCap.Tests.csproj`). The runaway test must complete in seconds (budget 50M ≈ <1s native).

- [ ] **Step 6: Commit**

```powershell
git add ReCap.Server/Adapters/Persistence/PackageMounts.cs ReCap.Server/Adapters/Scripting/LuaRuntime.cs ReCap.Server/Adapters/Scripting/Api/LuaApiModule.cs ReCap.Server/Program.cs ReCap.Tests/Scripting/LuaRuntimeTests.cs
git commit -m "fix(scripting): explicit mount init, instruction watchdog, telemetry context tags"
```

---

### Task 3: ScriptContextRegistry + ScriptRegistry + Register* natives

**Files:** Create `ReCap.Server/Adapters/Scripting/ScriptContextRegistry.cs`, `ReCap.Server/Adapters/Scripting/ScriptRegistry.cs`, `ReCap.Server/Adapters/Scripting/Api/RegistrarModule.cs`; Modify `LuaRuntime.cs` (wire registration); Test `ReCap.Tests/Scripting/ScriptRegistryTests.cs`.

- [ ] **Step 1: Failing tests:**

```csharp
using ReCap.Server.Adapters.Scripting;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

public class ScriptRegistryTests
{
    [Fact]
    public void RegisterAbilityStoresTableByFnvName()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        rt.Execute(LuaFixtures.Compile(
            "nAbility.RegisterAbility('TestStrike', { rank = 3, tick = function() end })"), "reg");
        var registry = ScriptContextRegistry.Get(rt.L)!.Registry;
        var entry = registry.Find(ScriptKind.Ability, ScriptVfs.Hash("TestStrike"));
        Assert.NotNull(entry);
        Assert.Equal("TestStrike", entry!.Name);
        Assert.True(entry.HasTick);
    }

    [Fact]
    public void DuplicateRegistrationKeepsFirst()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        rt.Execute(LuaFixtures.Compile(
            "nAffix.RegisterAffix('Dup', { a = 1 }) nAffix.RegisterAffix('Dup', { a = 2 })"), "dup");
        var registry = ScriptContextRegistry.Get(rt.L)!.Registry;
        Assert.Equal(1, registry.Count(ScriptKind.Affix));
    }

    [Fact]
    public void RegisterReturnsZeroValues()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        Assert.True(rt.EvalBool(LuaFixtures.Compile(
            "local r = nModifier.RegisterModifier('M1', {}) return r == nil")));
    }
}
```

- [ ] **Step 2:** red run. **Step 3: `ScriptContextRegistry.cs`** — the L→context seam every native uses:

```csharp
namespace ReCap.Server.Adapters.Scripting;

public sealed class ScriptStateContext
{
    public required ScriptRegistry Registry { get; init; }
    public LuaCoroutineScheduler? Scheduler { get; set; }
    public object? GameBridge { get; set; }
}

public static class ScriptContextRegistry
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<nint, ScriptStateContext> _byState = new();

    public static void Register(nint L, ScriptStateContext context) => _byState[L] = context;
    public static void Unregister(nint L) => _byState.TryRemove(L, out _);
    public static ScriptStateContext? Get(nint L) => _byState.TryGetValue(L, out var c) ? c : null;
}
```

(`Scheduler`/`GameBridge` filled by Tasks 5/6 — declared now so signatures are stable. Thread states get registered with the SAME context instance as their main state.)

- [ ] **Step 4: `ScriptRegistry.cs`:**

```csharp
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting;

public enum ScriptKind { Ability, Modifier, Affix, Condition, Objective }

public sealed record ScriptEntry(string Name, uint Hash, int TableRef, bool HasTick, bool HasActivate, bool HasDeactivate);

public sealed class ScriptRegistry
{
    private readonly Dictionary<(ScriptKind, uint), ScriptEntry> _entries = [];

    public bool TryAdd(ScriptKind kind, ScriptEntry entry)
    {
        if (_entries.ContainsKey((kind, entry.Hash))) return false;
        _entries[(kind, entry.Hash)] = entry;
        return true;
    }

    public ScriptEntry? Find(ScriptKind kind, uint hash) =>
        _entries.TryGetValue((kind, hash), out var e) ? e : null;

    public int Count(ScriptKind kind) => _entries.Keys.Count(k => k.Item1 == kind);
}
```

- [ ] **Step 5: `RegistrarModule.cs`** — Register* natives (C4: skip-if-exists, 0 returns; C7 discipline). Core shape:

```csharp
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting.Api;

public static unsafe class RegistrarModule
{
    public static void Register(nint L)
    {
        LuaApiModule.RegisterNamespace(L, "nAbility",
            ("RegisterAbility", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&RegisterAbility));
        LuaApiModule.RegisterNamespace(L, "nModifier",
            ("RegisterModifier", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&RegisterModifier));
        LuaApiModule.RegisterNamespace(L, "nAffix",
            ("RegisterAffix", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&RegisterAffix));
        LuaApiModule.RegisterNamespace(L, "nCondition",
            ("RegisterCondition", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&RegisterCondition));
        LuaApiModule.RegisterNamespace(L, "nObjective",
            ("RegisterObjective", (nint)(delegate* unmanaged[Cdecl]<nint, int>)&RegisterObjective));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int RegisterAbility(nint L) => RegisterEntry(L, ScriptKind.Ability);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int RegisterModifier(nint L) => RegisterEntry(L, ScriptKind.Modifier);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int RegisterAffix(nint L) => RegisterEntry(L, ScriptKind.Affix);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int RegisterCondition(nint L) => RegisterEntry(L, ScriptKind.Condition);
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int RegisterObjective(nint L) => RegisterEntry(L, ScriptKind.Objective);

    private static int RegisterEntry(nint L, ScriptKind kind)
    {
        try
        {
            if (LuaNative.lua_type(L, 1) != LuaNative.LUA_TSTRING || LuaNative.lua_type(L, 2) != LuaNative.LUA_TTABLE)
                return 0;
            var name = LuaNative.ToManagedString(L, 1);
            var context = ScriptContextRegistry.Get(L);
            if (name is null || context is null) return 0;
            var hash = ScriptVfs.Hash(name);
            if (context.Registry.Find(kind, hash) is not null) return 0;
            var hasTick = TableHasFunction(L, 2, "tick");
            var hasActivate = TableHasFunction(L, 2, "activate");
            var hasDeactivate = TableHasFunction(L, 2, "deactivate");
            LuaNative.lua_pushvalue(L, 2);
            var tableRef = LuaNative.luaL_ref(L, LuaNative.LUA_REGISTRYINDEX);
            context.Registry.TryAdd(kind, new ScriptEntry(name, hash, tableRef, hasTick, hasActivate, hasDeactivate));
            return 0;
        }
        catch (Exception ex)
        {
            try { Util.Logging.Log.Lua.Error($"[registrar] {kind} failed: {ex.Message}"); } catch { }
            return 0;
        }
    }

    private static bool TableHasFunction(nint L, int tableIndex, string field)
    {
        LuaNative.lua_getfield(L, tableIndex, field);
        var isFn = LuaNative.lua_type(L, -1) == LuaNative.LUA_TFUNCTION;
        LuaNative.lua_settop(L, -2);
        return isFn;
    }
}
```

- [ ] **Step 6: wire.** In `LuaRuntime.CreateSandboxedState`, after `StubNamespaces.RegisterAll`/`NUtilModule.Register`: create the context + register the registrars:

```csharp
        ScriptContextRegistry.Register(L, new ScriptStateContext { Registry = new ScriptRegistry() });
        Api.RegistrarModule.Register(L);
```

`Dispose`: `ScriptContextRegistry.Unregister(L);` (before handle dispose, next to `_resolvers` removal). Expose `internal nint L` already exists — tests use `rt.L`; make it `public` if the test assembly path requires (InternalsVisibleTo exists → keep internal).

- [ ] **Step 7:** green run (filter, then full suite). Re-run the boot integration test — registrar telemetry entries for Register* should now DISAPPEAR from stub snapshot (they're real). Report new telemetry set.

- [ ] **Step 8: Commit**

```powershell
git add ReCap.Server/Adapters/Scripting/ScriptContextRegistry.cs ReCap.Server/Adapters/Scripting/ScriptRegistry.cs ReCap.Server/Adapters/Scripting/Api/RegistrarModule.cs ReCap.Server/Adapters/Scripting/LuaRuntime.cs ReCap.Tests/Scripting/ScriptRegistryTests.cs
git commit -m "feat(scripting): registrar natives store script tables in per-state registry (C4)"
```

---

### Task 4: Arity-correct Preload*, nBit family, nUtil.GetAsset

**Files:** Create `ReCap.Server/Adapters/Scripting/Api/PreloadModule.cs`, `ReCap.Server/Adapters/Scripting/Api/NBitModule.cs`; Modify `Api/NUtilModule.cs`, `LuaRuntime.cs` (wire); Test `ReCap.Tests/Scripting/ArityNativesTests.cs`.

- [ ] **Step 1: Failing tests:**

```csharp
using ReCap.Server.Adapters.Scripting;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

public class ArityNativesTests
{
    [Fact]
    public void PreloadsReturnExactlyOneNumber()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        Assert.True(rt.EvalBool(LuaFixtures.Compile("""
            local a = nAbility.PreloadAsset('x.Noun', 'prop')
            local b = nAbility.PreloadAnimation('x.Noun', 'anim')
            local c = nAbility.PreloadModifier(7, 'mod')
            return type(a) == 'number' and type(b) == 'number' and c == 7
            """)));
    }

    [Fact]
    public void BitOrFoldsRoundedArgs()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        Assert.True(rt.EvalBool(LuaFixtures.Compile("return nBit.Or(1, 2, 4) == 7")));
    }

    [Fact]
    public void GetAssetReturnsStableNumber()
    {
        using var rt = LuaRuntime.CreateSandboxedState();
        Assert.True(rt.EvalBool(LuaFixtures.Compile(
            "return nUtil.GetAsset('Thing.Noun') == nUtil.GetAsset('Thing.Noun') and type(nUtil.GetAsset('Thing.Noun')) == 'number'")));
    }
}
```

- [ ] **Step 2:** red. **Step 3: implement.**
- `PreloadModule.Register(L)` adds to `nAbility`: `PreloadAsset(name, prop)` → push `(float)ScriptVfs.Hash(name-arg-if-string else echo-rounded-number)`, return 1; `PreloadAnimation` same shape; `PreloadModifier(idOrNum, name)` → echo arg1 rounded back as number, return 1 (C3). All exception-proof, push `0f` fallback.
- `NBitModule.Register(L)` adds to `nBit`: `Or, And, Xor, Not, LShift, RShift` — each variadic per C3: read `lua_gettop` args, each `(uint)Math.Round((double)lua_tonumber(L,i))`, fold (Not = unary `~` of arg1; shifts: arg1 op arg2), push `(float)result`, return 1.
- `NUtilModule`: replace the GetAsset stub-gap — add real `GetAsset(name)` → push `(float)ScriptVfs.Hash(name)`, return 1. (Documented divergence: client returns a live AssetObject pointer-as-float — opaque to scripts either way; FNV hash is our stable equivalent. Cite VERIFIED_FACTS in T8.)
- Wire `PreloadModule.Register(L); NBitModule.Register(L);` after `RegistrarModule.Register(L)` in `CreateSandboxedState`.

- [ ] **Step 4:** green (filter + full suite). Boot integration: telemetry entries for Preload*/nBit.Or/GetAsset disappear. Report remaining telemetry.

- [ ] **Step 5: Commit**

```powershell
git add ReCap.Server/Adapters/Scripting/Api/PreloadModule.cs ReCap.Server/Adapters/Scripting/Api/NBitModule.cs ReCap.Server/Adapters/Scripting/Api/NUtilModule.cs ReCap.Server/Adapters/Scripting/LuaRuntime.cs ReCap.Tests/Scripting/ArityNativesTests.cs
git commit -m "feat(scripting): arity-correct Preload*/nBit/GetAsset natives (C3)"
```

---

### Task 5: LuaCoroutineScheduler + nThread core

**Files:** Create `ReCap.Server/Adapters/Scripting/LuaCoroutineScheduler.cs`, `ReCap.Server/Adapters/Scripting/Api/NThreadModule.cs`; Modify `LuaNative.cs` (if any missing binding), `LuaRuntime.cs` (wire); Test `ReCap.Tests/Scripting/SchedulerTests.cs`.

- [ ] **Step 1: Failing tests:**

```csharp
using ReCap.Server.Adapters.Scripting;
using ReCap.Tests.TestSupport;

namespace ReCap.Tests.Scripting;

public class SchedulerTests
{
    private static (LuaRuntime rt, LuaCoroutineScheduler sched) Make()
    {
        var rt = LuaRuntime.CreateSandboxedState();
        var sched = ScriptContextRegistry.Get(rt.L)!.Scheduler!;
        return (rt, sched);
    }

    [Fact]
    public void WaitForXSecondsResumesAfterDeadline()
    {
        var (rt, sched) = Make();
        using var _ = rt;
        rt.Execute(LuaFixtures.Compile(
            "nThread.CreateThreadForObject(1, function() nThread.WaitForXSeconds(1.0) Done = true end)"), "t");
        Assert.False(rt.EvalBool(LuaFixtures.Compile("return Done == true")));
        sched.Tick(0.5);
        Assert.False(rt.EvalBool(LuaFixtures.Compile("return Done == true")));
        sched.Tick(1.1);
        Assert.True(rt.EvalBool(LuaFixtures.Compile("return Done == true")));
    }

    [Fact]
    public void SleepOnlyWakesOnWakeUp()
    {
        var (rt, sched) = Make();
        using var _ = rt;
        rt.Execute(LuaFixtures.Compile(
            "nThread.CreateThreadForObject(2, function() nThread.Sleep() Woke = true end)"), "t");
        sched.Tick(100.0);
        Assert.False(rt.EvalBool(LuaFixtures.Compile("return Woke == true")));
        rt.Execute(LuaFixtures.Compile("nThread.WakeUp(2)"), "wake");
        sched.Tick(100.1);
        Assert.True(rt.EvalBool(LuaFixtures.Compile("return Woke == true")));
    }

    [Fact]
    public void OneThreadPerObjectGuard()
    {
        var (rt, sched) = Make();
        using var _ = rt;
        rt.Execute(LuaFixtures.Compile("""
            Count = 0
            nThread.CreateThreadForObject(3, function() Count = Count + 1 nThread.WaitForever() end)
            nThread.CreateThreadForObject(3, function() Count = Count + 100 end)
            """), "t");
        Assert.True(rt.EvalBool(LuaFixtures.Compile("return Count == 1")));
    }

    [Fact]
    public void ErroredCoroutineIsReleasedAndLogged()
    {
        var (rt, sched) = Make();
        using var _ = rt;
        rt.Execute(LuaFixtures.Compile(
            "nThread.CreateThreadForObject(4, function() nThread.WaitForXSeconds(0.1) error('boom') end)"), "t");
        sched.Tick(1.0);
        rt.Execute(LuaFixtures.Compile(
            "nThread.CreateThreadForObject(4, function() Recovered = true end)"), "t2");
        Assert.True(rt.EvalBool(LuaFixtures.Compile("return Recovered == true")));
    }
}
```

- [ ] **Step 2:** red. **Step 3: `LuaCoroutineScheduler.cs`:**

```csharp
using ReCap.Server.Adapters.Scripting.Native;

namespace ReCap.Server.Adapters.Scripting;

public sealed class LuaCoroutineScheduler(nint mainState)
{
    private sealed class ThreadEntry
    {
        public required nint ThreadL { get; init; }
        public required int ThreadRef { get; init; }
        public uint ObjectId { get; set; }
        public bool Sleeping { get; set; }
        public double? WakeAtSeconds { get; set; }
    }

    private readonly Dictionary<nint, ThreadEntry> _threads = [];
    private readonly Dictionary<uint, nint> _byObject = [];
    private double _now;

    public double Now => _now;
    public int ActiveCount => _threads.Count;

    public bool HasThreadForObject(uint objectId) => _byObject.ContainsKey(objectId);

    public nint Spawn(uint objectId, int functionStackIndex, int argCount)
    {
        var threadL = LuaNative.lua_newthread(mainState);
        var threadRef = LuaNative.luaL_ref(mainState, LuaNative.LUA_REGISTRYINDEX);
        LuaNative.lua_pushvalue(mainState, functionStackIndex);
        LuaNative.lua_xmove(mainState, threadL, 1);
        for (var i = 0; i < argCount; i++)
        {
            LuaNative.lua_pushvalue(mainState, functionStackIndex + 1 + i);
            LuaNative.lua_xmove(mainState, threadL, 1);
        }
        var entry = new ThreadEntry { ThreadL = threadL, ThreadRef = threadRef, ObjectId = objectId };
        _threads[threadL] = entry;
        if (objectId != 0) _byObject[objectId] = threadL;
        var context = ScriptContextRegistry.Get(mainState);
        if (context is not null) ScriptContextRegistry.Register(threadL, context);
        LuaRuntime.InstallWatchdog(threadL);
        Resume(entry, argCount);
        return threadL;
    }

    public void RegisterYield(nint threadL, bool sleeping, double? wakeAt)
    {
        if (!_threads.TryGetValue(threadL, out var entry)) return;
        entry.Sleeping = sleeping;
        entry.WakeAtSeconds = wakeAt;
    }

    public void WakeObject(uint objectId)
    {
        if (_byObject.TryGetValue(objectId, out var threadL) && _threads.TryGetValue(threadL, out var entry))
            entry.Sleeping = false;
    }

    public void Tick(double nowSeconds)
    {
        _now = nowSeconds;
        foreach (var entry in _threads.Values.ToList())
        {
            if (entry.Sleeping) continue;
            if (entry.WakeAtSeconds is double wake && nowSeconds < wake) continue;
            entry.WakeAtSeconds = null;
            Resume(entry, 0);
        }
    }

    private void Resume(ThreadEntry entry, int argCount)
    {
        var status = LuaNative.lua_resume(entry.ThreadL, argCount);
        if (status == LuaNative.LUA_YIELD) return;
        if (status != LuaNative.LUA_OK)
        {
            var message = LuaNative.ToManagedString(entry.ThreadL, -1) ?? "unknown";
            Util.Logging.Log.Lua.Error($"[coroutine] object {entry.ObjectId}: {message}");
        }
        Release(entry);
    }

    private void Release(ThreadEntry entry)
    {
        _threads.Remove(entry.ThreadL);
        if (entry.ObjectId != 0 && _byObject.TryGetValue(entry.ObjectId, out var l) && l == entry.ThreadL)
            _byObject.Remove(entry.ObjectId);
        ScriptContextRegistry.Unregister(entry.ThreadL);
        LuaNative.luaL_unref(mainState, LuaNative.LUA_REGISTRYINDEX, entry.ThreadRef);
    }
}
```

(Yield semantics per C5: 0 values yielded, 0 results on resume. The watchdog hook fires inside `lua_resume` → status error path handles it. Note `lua_resume(L, narg)` is the 5.1 signature already bound.)

- [ ] **Step 4: `NThreadModule.cs`** — natives over the scheduler (each via `ScriptContextRegistry.Get(L)!.Scheduler`):
- `CreateThreadForObject(objectIdNumber, function, ...)`: read id (C1 round), check `lua_type(L,2)==LUA_TFUNCTION`, guard `HasThreadForObject` → return 0 silently (C5); else `scheduler.Spawn(id, 2, lua_gettop(L)-2)`; return 0. NOTE: Spawn reads the function/args from the CALLING state's stack — when called from a coroutine, `mainState != L`; copy via `lua_xmove` from `L` instead: implement Spawn to take the caller `L` as source (adjust signature: `Spawn(nint callerL, uint objectId, int fnIndex, int argCount)` and xmove from `callerL`). Keep the thread itself created on `mainState` (shared globals).
- `Sleep()`: `RegisterYield(L, sleeping: true, wakeAt: null)` then `return LuaNative.lua_yield(L, 0);` — ADD the binding if missing: `[LibraryImport(Dll)] internal static partial int lua_yield(nint L, int nresults);`. `lua_yield` must be the RETURN value of the native (tail position, no managed try active — same ABI rule as lua_error: structure the method so the yield call is last, outside try).
- `WaitForever()`: same as Sleep (sleeping=true semantics: only WakeUp clears; identical handling).
- `WaitForXSeconds(seconds, ...)`: read float arg1 (clamp >= 0); extra args accepted and ignored (log once via StubTelemetry-style one-shot if present); `RegisterYield(L, false, scheduler.Now + seconds)`; `return lua_yield(L, 0);`.
- `WakeUp(objectIdNumber)`: `scheduler.WakeObject(id)`; return 0.
- `GetValue(key)`/`SetValue(key, v)` if telemetry showed them: SKIP unless boot telemetry lists them (YAGNI — check the T4 telemetry report; if present implement as per-thread Dictionary<string, stored luaL_ref>).
- Register in `CreateSandboxedState` after the other modules; also CREATE the scheduler there: `ScriptContextRegistry.Get(L)!.Scheduler = new LuaCoroutineScheduler(L);` (set on the context object created in T3).

- [ ] **Step 5:** green (filter + full). **Step 6: Commit**

```powershell
git add ReCap.Server/Adapters/Scripting/LuaCoroutineScheduler.cs ReCap.Server/Adapters/Scripting/Api/NThreadModule.cs ReCap.Server/Adapters/Scripting/Native/LuaNative.cs ReCap.Server/Adapters/Scripting/LuaRuntime.cs ReCap.Tests/Scripting/SchedulerTests.cs
git commit -m "feat(scripting): coroutine scheduler + nThread core (C5)"
```

---### Task 6: GameScriptContext + Game bridge + nGameObject/nAbility context natives

**Files:** Create `ReCap.Server/Services/Scripting/GameScriptContext.cs`, `ReCap.Server/Adapters/Scripting/Api/NGameObjectModule.cs`, `ReCap.Server/Adapters/Scripting/Api/NAbilityContextModule.cs`; Modify `ReCap.Server/Domain/Gameplay/Game.cs`, `ReCap.Server/Domain/Gameplay/ObjectManager.cs`; Test `ReCap.Tests/Scripting/GameBridgeTests.cs`.

- [ ] **Step 1: bridge interface.** The natives reach gameplay through an interface stored on `ScriptStateContext.GameBridge` (typed `object?` in T3 — now formalize). Add to `ScriptContextRegistry.cs`:

```csharp
public interface IScriptGameBridge
{
    bool TryGetPosition(uint objectId, out float x, out float y, out float z);
    float GetHitPoints(uint objectId);
    float GetMaxHitPoints(uint objectId);
    bool ObjectExists(uint objectId);
    byte GetTeam(uint objectId);
    uint GetTargetId(uint objectId);
}
```

Change `ScriptStateContext.GameBridge` to `IScriptGameBridge?`.

- [ ] **Step 2: failing tests** (`GameBridgeTests.cs`) — uses a fake bridge, no Game needed:

```csharp
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
```

Add to `ScriptStateContext`: `public AbilityInvocation? CurrentInvocation { get; set; }` and the record `public readonly record struct AbilityInvocation(uint AgentId, uint TargetId, float CursorX, float CursorY, float CursorZ, int Rank);` (in `ScriptContextRegistry.cs`).

- [ ] **Step 3:** red. Implement `NGameObjectModule.Register(L)` adding REAL entries over the stub table: `GetPosition` (C2: bridge hit → push 3 floats, return 3; miss → `lua_pushstring("Could not find object!")` then bare `return lua_error(L)` — replicating client), `GetHitPoints`/`GetMaxHitPoints` (1 float, 0f on miss), `IsAlive` (bool hp>0, false on miss, always 1 return), `GetTeam` (1 number on hit, **0 returns** on miss — C3), `GetTargetID` (1 number, 0 on miss). All read ids per C1.

`NAbilityContextModule.Register(L)` adds to `nAbility`: `GetAgentID()`, `GetTargetID()` (1 number each from `CurrentInvocation`, 0 when null), `GetTargetPosition()` (3 floats from cursor), `GetRank()` (1 number). Wire both modules in `CreateSandboxedState` after NThread.

- [ ] **Step 4: Game side.** `ObjectManager.cs`: add `public uint TargetId { get; set; }` to `GameObject`. `Game.cs`:
- `public Services.Scripting.GameScriptContext? ScriptContext { get; set; }`
- in `Update()` after `Objects.Update(delta)`: `ScriptContext?.Tick();`
- add accessor used by the bridge: `internal bool TryGetObjectPosition(uint objectId, out System.Numerics.Vector3 pos)` — player-controlled ids (in `_playerCharacterObjectIds.Values`): return `_objectLocomotion[id].PartialGoalPosition` when present else the GameObject.Position; others: GameObject.Position; false when object unknown.

- [ ] **Step 5: `GameScriptContext.cs`:**

```csharp
using ReCap.Server.Adapters.Scripting;
using ReCap.Server.Domain.Gameplay;

namespace ReCap.Server.Services.Scripting;

public sealed class GameScriptContext : IScriptGameBridge, IDisposable
{
    private readonly Game _game;
    private readonly LuaRuntime _runtime;
    private readonly LuaCoroutineScheduler _scheduler;
    private readonly ScriptRegistry _registry;
    private double _clockSeconds;

    public GameScriptContext(Game game, ScriptEngine engine)
    {
        _game = game;
        _runtime = engine.CreateBootedRuntime();
        var state = ScriptContextRegistry.Get(_runtime.L)!;
        state.GameBridge = this;
        _scheduler = state.Scheduler!;
        _registry = state.Registry;
    }

    public ScriptRegistry Registry => _registry;
    internal LuaRuntime Runtime => _runtime;
    internal LuaCoroutineScheduler Scheduler => _scheduler;

    public void Tick()
    {
        _clockSeconds += 0.05;
        _scheduler.Tick(_clockSeconds);
    }

    public void Dispose() => _runtime.Dispose();

    public bool TryGetPosition(uint objectId, out float x, out float y, out float z)
    {
        if (_game.TryGetObjectPosition(objectId, out var p)) { x = p.X; y = p.Y; z = p.Z; return true; }
        x = y = z = 0f;
        return false;
    }

    public float GetHitPoints(uint objectId) =>
        _game.Objects.Objects.TryGetValue(objectId, out var o) ? o.Health : 0f;

    public float GetMaxHitPoints(uint objectId) =>
        _game.Objects.Objects.TryGetValue(objectId, out var o) ? o.MaxHealth : 0f;

    public bool ObjectExists(uint objectId) => _game.Objects.Objects.ContainsKey(objectId);

    public byte GetTeam(uint objectId) =>
        _game.Objects.Objects.TryGetValue(objectId, out var o) ? o.Team : (byte)0;

    public uint GetTargetId(uint objectId) =>
        _game.Objects.Objects.TryGetValue(objectId, out var o) ? o.TargetId : 0u;
}
```

(Adjust member names to the REAL ObjectManager API — read it first: the dictionary accessor may be named differently; `Game.Objects` per the repo map. Tick uses fixed 0.05 = the 50ms loop; tests drive `Scheduler.Tick` directly. `CreateBootedRuntime` exists since P1; pass `contextTag: $"game-{game.Id}"` if the runtime factory supports it after T2 — extend `CreateBootedRuntime(string tag)` overload accordingly in this task.)

- [ ] **Step 6:** green: bridge tests (fake), full suite, and boot integration still green. **Step 7: Commit**

```powershell
git add ReCap.Server/Adapters/Scripting/ScriptContextRegistry.cs ReCap.Server/Adapters/Scripting/Api/NGameObjectModule.cs ReCap.Server/Adapters/Scripting/Api/NAbilityContextModule.cs ReCap.Server/Services/Scripting/GameScriptContext.cs ReCap.Server/Domain/Gameplay/Game.cs ReCap.Server/Domain/Gameplay/ObjectManager.cs ReCap.Server/Adapters/Scripting/LuaRuntime.cs ReCap.Server/Services/Scripting/ScriptEngine.cs ReCap.Tests/Scripting/GameBridgeTests.cs
git commit -m "feat(scripting): GameScriptContext bridge + nGameObject/nAbility retail-contract natives (C1/C2)"
```

---

### Task 7: Tick-signature investigation + InvokeAbility + THE GATE

**Files:** Create `ReCap.Tests/Scripting/AbilityTickGateTests.cs`, `docs/architecture/research/LUA_ABILITY_TICK_CONTRACT.md`; Modify `ReCap.Server/Services/Scripting/GameScriptContext.cs`.

- [ ] **Step 1: INVESTIGATION (evidence before code).** Determine the retail tick-call contract: how many params do registered `tick` functions declare, and do they use args or context getters? Method:

```powershell
# extract 3-5 ability chunks to temp and disassemble with our luac
# (a tiny C# snippet via existing tests is fine too; simplest: add a temporary
#  [Fact] that dumps chunks using ScriptVfs, run it, then DELETE it)
& native\lua51\out\luac.exe -l -p $env:TEMP\chunk.luac
```

Steps: pick `ability_spawn.lua` (0x7153BBB1!0x4C96D649) + 2 abilities that registered cleanly in the boot run; dump each chunk's function list (`luac -l` works on precompiled chunks); find the function assigned to the table's `tick` field (cross-reference SETTABLE/“tick” constant); record its `numparams` + whether its body calls `nAbility.GetAgentID`/`GetTargetID` (constants present). Write findings to `docs/architecture/research/LUA_ABILITY_TICK_CONTRACT.md` (numparams per sample, constants observed, conclusion). DECISION RULE: if numparams ≥ 4 → call `tick(selfTable, agentId, targetId, x, y, z, rank)` trimmed to numparams; if numparams ≤ 1 → call `tick(selfTable)` and rely on context getters. Implement what the evidence says; document it.

- [ ] **Step 2: `InvokeAbility` on GameScriptContext:**

```csharp
    public bool InvokeAbility(uint abilityHash, uint agentId, uint targetId, float cursorX, float cursorY, float cursorZ, int rank)
    {
        var entry = _registry.Find(ScriptKind.Ability, abilityHash);
        if (entry is null || !entry.HasTick) return false;
        if (_scheduler.HasThreadForObject(agentId)) return false;
        var state = ScriptContextRegistry.Get(_runtime.L)!;
        state.CurrentInvocation = new AbilityInvocation(agentId, targetId, cursorX, cursorY, cursorZ, rank);
        var L = _runtime.L;
        LuaNative.lua_rawgeti(L, LuaNative.LUA_REGISTRYINDEX, entry.TableRef);
        LuaNative.lua_getfield(L, -1, "tick");
        LuaNative.lua_insert(L, -2);
        var argCount = PushTickArgs(L, entry, agentId, targetId, cursorX, cursorY, cursorZ, rank);
        _scheduler.SpawnFromStack(L, agentId, argCount + 1);
        LuaNative.lua_settop(L, 0);
        return true;
    }
```

(`PushTickArgs` implements the Step-1 decision: at minimum pushes the self table (already on stack below the fn after `lua_insert` — count it) + any evidence-mandated args. `SpawnFromStack(callerL, objectId, valuesOnTop)` = a scheduler method variant that xmoves the top `fn+args` block to a new thread and resumes — refactor `Spawn` from T5 so both entry points share it. `CurrentInvocation` stays set for the life of the coroutine: store it on the ThreadEntry instead of the shared context if two abilities can overlap — implement per-thread: move `CurrentInvocation` into a `Dictionary<nint, AbilityInvocation>` on `ScriptStateContext` keyed by thread L, and make `NAbilityContextModule` read by caller L with fallback to main state. Adjust T6 test accordingly — set via the same API the runtime uses: `ctx.SetInvocation(rt.L, …)`.)

- [ ] **Step 3: THE GATE — integration test** (`AbilityTickGateTests.cs`):

```csharp
using ReCap.Server.Adapters.Persistence;
using ReCap.Server.Adapters.Scripting;
using ReCap.Server.Domain.Gameplay;
using ReCap.Server.Services.Scripting;

namespace ReCap.Tests.Scripting;

public class AbilityTickGateTests
{
    [Fact]
    public void RealRetailAbilityTickRunsToCompletion()
    {
        var dataDir = ScriptEngineBootTests.FindDataDirShared();
        if (dataDir is null) return;
        var vfs = new ScriptVfs(new PackageMounts(dataDir));
        var engine = new ScriptEngine(vfs);
        var game = new Game(1, GameType.Matched, null);
        using var ctx = new GameScriptContext(game, engine);
        game.ScriptContext = ctx;

        game.Objects.Spawn(10, 0, new System.Numerics.Vector3(0, 0, 0), 1f, 1, true);
        game.Objects.Spawn(20, 0, new System.Numerics.Vector3(5, 0, 0), 1f, 2, false);

        var candidates = ctx.Registry.AllWithTick(ScriptKind.Ability).Take(25).ToList();
        Assert.NotEmpty(candidates);

        var completed = 0;
        foreach (var entry in candidates)
        {
            var started = ctx.InvokeAbility(entry.Hash, 10, 20, 5f, 0f, 0f, 1);
            if (!started) continue;
            for (var tick = 0; tick < 600 && ctx.Scheduler.HasThreadForObject(10); tick++)
                ctx.Tick();
            if (!ctx.Scheduler.HasThreadForObject(10)) completed++;
        }
        Assert.True(completed >= 5,
            $"expected >=5 retail ability ticks to complete; got {completed}/{candidates.Count}");
    }
}
```

Supporting changes: `ScriptRegistry.AllWithTick(kind)` returning `IEnumerable<ScriptEntry>`; make `FindDataDir` reusable (`internal static string? FindDataDirShared()` on the boot tests class or move to TestSupport). Adjust `Spawn` signature to ObjectManager's REAL one. Errored coroutines also clear `HasThreadForObject` → count separately if useful for the report (log scan). The 600-tick cap = 30 simulated seconds. THRESHOLD HONESTY: if fewer than 5 complete, investigate top error patterns (telemetry + coroutine error log), apply ONLY contract-level fixes (arity, marshalling) — gameplay-behavior natives (TakeDamage etc.) stay stubs; then set the assert to the honest achieved number (≥N) and report it as the P4 baseline.

- [ ] **Step 4:** run gate + full suite. Capture for the report: completed count, top coroutine errors, telemetry top-20 (this defines P4 scope: likely nGameObject mutators + nModifier exec + projectile waits).

- [ ] **Step 5: Commit**

```powershell
git add ReCap.Server/Services/Scripting/GameScriptContext.cs ReCap.Server/Adapters/Scripting/ScriptRegistry.cs ReCap.Server/Adapters/Scripting/LuaCoroutineScheduler.cs ReCap.Server/Adapters/Scripting/ScriptContextRegistry.cs ReCap.Server/Adapters/Scripting/Api/NAbilityContextModule.cs ReCap.Tests/Scripting/AbilityTickGateTests.cs ReCap.Tests/Scripting/ScriptEngineBootTests.cs docs/architecture/research/LUA_ABILITY_TICK_CONTRACT.md
git commit -m "feat(scripting): InvokeAbility + retail ability tick gate (P3)"
```

---

### Task 8: Docs + memory closeout

**Files:** Modify `docs/architecture/VERIFIED_FACTS.md`, `docs/architecture/research/LUA_REGISTRAR_TABLES.md`, `docs/architecture/planning/PORTING_MATRIX.md`.

- [ ] **Step 1:** VERIFIED_FACTS append (match existing format): (a) object/position marshalling contract C1/C2 with client addresses (0x009f9740, 0x009fb870); (b) return-arity table C3 (cite the per-native addresses from LUA_REGISTRAR_TABLES.md); (c) Register* semantics C4 (0x00a43040 family; nModifier.RegisterModifier == RegisterAbility native); (d) correction: SimulatorControl @0x008f6e40 = lua_gc controller, NOT the coroutine scheduler (supersede the earlier audit label); (e) boot-order evidence: global.lua defines Class, pre-exec required (C6). 
- [ ] **Step 2:** LUA_REGISTRAR_TABLES.md: addendum section linking LUA_ABILITY_TICK_CONTRACT.md + the arity table. PORTING_MATRIX: Lua row → P3 done (scheduler+registrars+bridge+gate result numbers), Ability row → ⚠ tick-capable (P4 = cast wiring + mutators).
- [ ] **Step 3:** Run nothing; verify cited numbers against the T7 report. Commit:

```powershell
git add docs/architecture/VERIFIED_FACTS.md docs/architecture/research/LUA_REGISTRAR_TABLES.md docs/architecture/planning/PORTING_MATRIX.md
git commit -m "docs: P3 closeout - marshalling contracts, arities, scheduler facts"
```

---

## Self-Review (planning time)

- **Coverage vs P3 scope:** boot fixes (T1=C6), handoff (T2), registrars (T3=C4), arity natives (T4=C3), scheduler+nThread (T5=C5), bridge+context (T6=C1/C2), gate (T7), docs (T8). P4 explicitly out: cast wiring (0x9C slot→ability resolution), mutators (TakeDamage/AddEffect→0x9B), per-game packet flow.
- **Placeholders:** none; the one open contract (tick signature) is an explicit evidence-gathering step with a decision rule, not a TBD.
- **Type consistency:** `ScriptStateContext {Registry, Scheduler, GameBridge, CurrentInvocation/SetInvocation}` introduced T3, consumed T5/T6/T7 (T7 refines invocation storage per-thread — instruction included). `Spawn`/`SpawnFromStack` refactor noted in both T5/T7. `BootReport.MissingRequires` introduced T1, asserted in T1's ratchet edit.
