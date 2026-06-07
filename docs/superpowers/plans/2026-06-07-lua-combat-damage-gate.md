# Lua Combat Damage Gate Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make a player-cast basic melee ability (Pummel) deal real, rolled weapon damage to an enemy server-side, by reimplementing `nGameObject.TakeDamage` faithfully (the damage engine) and committing its prerequisite natives.

**Architecture:** The Lua chunk is the ability asset; the melee `tick` coroutine computes a `{min,max}` damage table and passes it to `nGameObject.TakeDamage` with 11 args. The current `TakeDamage` reads arg 3 as a number (it is a table) → 0 damage. We reimplement it per the verified engine contract (roll min..max, crit from attacker snapshot, apply HP via the bridge), keeping all gameplay access behind `IScriptGameBridge`. Prerequisite natives (`nPlayer.IsPlayerControlledObject`, `nGameObject.GetWeaponDamage`, hero weapon attributes) are already implemented and green — committed first as their own unit.

**Tech Stack:** .NET 10 (net10.0), xUnit, `recaplua51.dll` (float ABI), `LuaChunkDump` (disasm), real `ServerData.package` for the integration gate.

**Spec:** `docs/superpowers/specs/2026-06-07-lua-scripting-system-robust-design.md` (§5 combat data flow, §7 open divergences L-1..L-5).

**Hard rules for the executor:**
- NEVER add `Co-Authored-By` or "Generated with" to commits (standing user order).
- NEVER `git add -A`/`-a`; stage only the listed paths.
- No C# comments except verified-cite one-liners (CLAUDE.md).
- ABI rule (C7): `lua_error` may only be called from a point with **no enclosing managed `try`** (it longjmps). Every `[UnmanagedCallersOnly]` callback is otherwise exception-proof.
- Logging only via `ReCap.Server.Util.Logging.Log`.

---

## Verified contract (embed before coding)

`nGameObject.TakeDamage(snapshotHandle, targetId, damageTable, damageType, damageSource, damageCoefficient, descriptors, damageMultiplier, dirX, dirY, dirZ)` — 11 args, 3 returns. Source: client stub `0x00a06170` + C++ reference `Object.cpp:1377`, Ghidra 2026-06-07.

- `damageTable` = Lua **table `{[1]=min,[2]=max}`**. A non-table raises a Lua error (string `"Expected a table for damage range in TakeDamage!"`).
- `baseDamage = Random(min, max)` (uniform float).
- **Crit** (attacker snapshot): `AutoCrit`(attr 19) `> 0` → always crit; else `Random(0,1) < CriticalRating`(attr 10)`/100`; if crit `baseDamage *= CriticalDamageIncrease`(attr 22)`+ 1`. No snapshot → no crit, damage still applies.
- **HP:** subtract `baseDamage` from the target (clamped at 0 via existing `ApplyHeal`). Death-event packet flow is **out of scope** (spec L-3).
- **Returns:** `(hitSuccess=true, damageDealt, isCrit)`. Caller `TEST`s the first as a hit gate.
- **Target missing → 0 Lua return values** (nil gate).
- **Accepted-but-unused (spec divergence L-1/L-2):** `damageCoefficient`, `damageMultiplier`, `damageType`, `damageSource`, `descriptors`, direction, target defense. Do NOT invent scaling/mitigation; reference applies none. Document, leave for wire capture.

---

## File Map

| Path | Responsibility | Action |
|---|---|---|
| `ReCap.Server/Adapters/Scripting/Api/NGameObjectModule.cs` | `TakeDamage` native + `RollDamage`/`ApplyCrit` helpers | Modify |
| `ReCap.Tests/Scripting/GameBridgeTests.cs` | unit tests for `TakeDamage` (table roll, missing target, non-table error) | Modify |
| `ReCap.Tests/Scripting/PummelStallReproTests.cs` | integration gate: enemy loses HP after the cast | Modify |

Prerequisite natives already implemented & green (committed in Task 1, no edits needed): `NPlayerModule.cs`, `NGameObjectModule.GetWeaponDamage`, `ObjectManager.Spawn` weapon attrs, `ScriptContextRegistry.IsPlayerControlled`, `GameScriptContext.IsPlayerControlled`, `LuaRuntime` wiring, plus existing tests in `GameBridgeTests`.

---

### Task 1: Commit the prerequisite natives (already implemented, green)

These were implemented and verified earlier this session (full suite 153/153). They are necessary independent of `TakeDamage` and are committed first as one logical unit.

**Files (already modified in the working tree):**
- `ReCap.Server/Adapters/Scripting/Api/NPlayerModule.cs` (new)
- `ReCap.Server/Adapters/Scripting/Api/NGameObjectModule.cs` (`GetWeaponDamage`)
- `ReCap.Server/Adapters/Scripting/ScriptContextRegistry.cs` (`IsPlayerControlled` on `IScriptGameBridge`)
- `ReCap.Server/Services/Scripting/GameScriptContext.cs` (`IsPlayerControlled` impl)
- `ReCap.Server/Domain/Gameplay/ObjectManager.cs` (player weapon attrs 101/102)
- `ReCap.Server/Adapters/Scripting/LuaRuntime.cs` (`NPlayerModule.Register` wiring)
- `ReCap.Tests/Scripting/GameBridgeTests.cs` (`IsPlayerControlledObjectFollowsBridge`, `GetWeaponDamageReturnsMinMaxTable`, FakeBridge additions)

- [ ] **Step 1: Verify the full suite is green**

Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj`
Expected: `Aprovado: 153, Falha: 0`.

- [ ] **Step 2: Commit (stage only these paths)**

```powershell
git add ReCap.Server/Adapters/Scripting/Api/NPlayerModule.cs `
  ReCap.Server/Adapters/Scripting/Api/NGameObjectModule.cs `
  ReCap.Server/Adapters/Scripting/ScriptContextRegistry.cs `
  ReCap.Server/Services/Scripting/GameScriptContext.cs `
  ReCap.Server/Domain/Gameplay/ObjectManager.cs `
  ReCap.Server/Adapters/Scripting/LuaRuntime.cs `
  ReCap.Tests/Scripting/GameBridgeTests.cs
git commit -m "feat(scripting): nPlayer.IsPlayerControlledObject + nGameObject.GetWeaponDamage + hero weapon attrs"
```

---

### Task 2: Reimplement `TakeDamage` as the damage engine (TDD)

**Files:**
- Modify: `ReCap.Server/Adapters/Scripting/Api/NGameObjectModule.cs`
- Test: `ReCap.Tests/Scripting/GameBridgeTests.cs`, `ReCap.Tests/Scripting/PummelStallReproTests.cs`

- [ ] **Step 1: Write the failing unit tests** in `GameBridgeTests.cs` — add immediately after `GetWeaponDamageReturnsMinMaxTable`:

```csharp
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
```

- [ ] **Step 2: Run the unit tests, verify they fail**

Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~GameBridgeTests"`
Expected: the 3 new tests FAIL (current `TakeDamage` reads arg3 as a number → `dealt` is 0 not 5; no error on number arg).

- [ ] **Step 3: Replace the `TakeDamage` method** in `NGameObjectModule.cs`. Find the existing method (it begins `private static int TakeDamage(nint L)` under the comment `// TakeDamage(snapshotHandle, targetId, damage, ...`) and replace the WHOLE method body with:

```csharp
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int TakeDamage(nint L)
    {
        bool argError = false;
        uint targetId = 0;
        float min = 0f, max = 0f;
        IReadOnlyDictionary<int, float>? snapshot = null;
        IScriptGameBridge? bridge = null;
        try
        {
            var ctx = ScriptContextRegistry.Get(L);
            bridge = ctx?.GameBridge;
            if (bridge is null || LuaNative.lua_type(L, 2) != LuaNative.LUA_TNUMBER)
                return 0;
            targetId = (uint)System.Math.Round((double)LuaNative.lua_tonumber(L, 2));
            if (!bridge.ObjectExists(targetId))
                return 0;
            if (LuaNative.lua_type(L, 3) != LuaNative.LUA_TTABLE)
            {
                argError = true;
            }
            else
            {
                LuaNative.lua_rawgeti(L, 3, 1);
                min = (float)LuaNative.lua_tonumber(L, -1);
                LuaNative.lua_settop(L, -2);
                LuaNative.lua_rawgeti(L, 3, 2);
                max = (float)LuaNative.lua_tonumber(L, -1);
                LuaNative.lua_settop(L, -2);
                if (LuaNative.lua_type(L, 1) == LuaNative.LUA_TNUMBER)
                {
                    var handle = (uint)System.Math.Round((double)LuaNative.lua_tonumber(L, 1));
                    if (handle != 0) ctx!.TryGetAttributeSnapshot(handle, out snapshot);
                }
            }
        }
        catch
        {
            return 0;
        }

        if (argError)
        {
            LuaNative.lua_pushstring(L, "Expected a table for damage range in TakeDamage!");
            return LuaNative.lua_error(L);
        }

        var damage = RollDamage(min, max);
        var isCrit = ApplyCrit(snapshot, ref damage);
        float dealt = 0f;
        try { dealt = damage > 0f ? -bridge!.ApplyHeal(targetId, -damage) : 0f; }
        catch { dealt = 0f; }

        LuaNative.lua_pushboolean(L, 1);
        LuaNative.lua_pushnumber(L, dealt);
        LuaNative.lua_pushboolean(L, isCrit ? 1 : 0);
        return 3;
    }

    // Crit attrs (C++ Object::CheckCritical, Attributes.h): AutoCrit=19, CriticalRating=10,
    // CriticalDamageIncrease=22. Reference TakeDamage applies NO coefficient/multiplier/defense
    // scaling (spec divergence L-1/L-2) — damage is the rolled range modified only by crit.
    private static float RollDamage(float min, float max)
    {
        if (max <= min) return min;
        return min + System.Random.Shared.NextSingle() * (max - min);
    }

    private static bool ApplyCrit(IReadOnlyDictionary<int, float>? snapshot, ref float damage)
    {
        if (snapshot is null) return false;
        bool crit;
        if (snapshot.TryGetValue(19, out var autoCrit) && autoCrit > 0f)
        {
            crit = true;
        }
        else
        {
            snapshot.TryGetValue(10, out var criticalRating);
            crit = System.Random.Shared.NextSingle() < criticalRating / 100f;
        }
        if (crit)
        {
            snapshot.TryGetValue(22, out var critIncrease);
            damage *= critIncrease + 1f;
        }
        return crit;
    }
```

Also update the contract comment above the method (the block starting `// Caller contract (melee tick disasm`) to read:

```csharp
    // Caller contract (melee tick disasm, CALL 36 12 4 = 11 args / 3 returns):
    // TakeDamage(snapshotHandle, targetId, damageTable{min,max}, damageType, damageSource,
    // coefficient, descriptors, damageMultiplier, dirX, dirY, dirZ) → (true, damageDealt, isCrit).
    // Engine (C++ Object.cpp:1377): baseDamage = Random(min,max); crit from snapshot; subtract HP.
    // arg3 MUST be a table — a number raises a Lua error. Coefficient/multiplier/damageType/
    // damageSource/descriptors/defense are accepted but unscaled in the reference (spec L-1/L-2).
    // Missing target → 0 Lua return values (nil gate).
```

- [ ] **Step 4: Run the unit tests, verify they pass**

Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj --filter "FullyQualifiedName~GameBridgeTests"`
Expected: all PASS (including the 3 new tests).

- [ ] **Step 5: Add the integration assertion** to `PummelStallReproTests.cs`. Replace the line:

```csharp
        Assert.Equal(0, ctx.Scheduler.ErrorCount);
```
with:
```csharp
        Assert.Equal(0, ctx.Scheduler.ErrorCount);
        Assert.True(enemy.Health < 375f,
            $"Pummel dealt no damage — enemy still {enemy.Health}hp (weapon-damage → TakeDamage path)");
```

- [ ] **Step 6: Run the repro + full suite**

Run: `dotnet test ReCap.Tests/ReCap.Tests.csproj`
Expected: `Falha: 0`. If `PummelStallReproTests` still shows `enemy.Health == 375`, the cause is NOT `TakeDamage` (the unit tests prove it) — it is the arc/target-find step (`FindBestTargetInArc`/`ValidateHostileTarget`) not selecting the enemy in this offline geometry; investigate that separately before continuing, do not weaken the assertion.

- [ ] **Step 7: Commit**

```powershell
git add ReCap.Server/Adapters/Scripting/Api/NGameObjectModule.cs `
  ReCap.Tests/Scripting/GameBridgeTests.cs `
  ReCap.Tests/Scripting/PummelStallReproTests.cs
git commit -m "feat(scripting): TakeDamage rolls {min,max} table + crit, applies HP (combat damage engine)"
```

---

### Task 3: In-game gate (manual verification)

This requires the retail client; the executor cannot run it. Hand off to the user.

- [ ] **Step 1: Build the server**

Run: `dotnet build ReCap.Server/ReCap.Server.csproj`
Expected: `0 Erro(s)`. (Ensure no server instance is running — it locks the exe.)

- [ ] **Step 2: Ask the user to run the gate**

Provide these instructions verbatim:
> 1. Run the server elevated: `dotnet run --project ReCap.Server -- --log-level=Lua:debug`
> 2. In the client: enter the Dungeon, approach an enemy (18/22 hp), cast Pummel (slot 0) several times.
> 3. Paste the log lines around the casts.

- [ ] **Step 3: Confirm the gate from the log**

PASS criteria:
- `[lua] HealDamage target=<enemy> ... hp 18,0→<lower>` — HP **decreasing** (1–5 per hit).
- Repeated casts drive the enemy toward 0 hp.
- No coroutine errors, no thread-busy stall.

When the enemy reaches 0 hp, record what happens (the death/`OnObjectDeath` path is unmodeled — spec L-3, next plan).

- [ ] **Step 4: Update memory** (`memory/lua-system-contract.md`): mark the combat damage gate CLOSED with the in-game result; set the next target to the death path (L-3) or the 31-native batch (spec §6.2).

---

## Follow-up plans (out of scope here — separate plans)

- **31-native demand batch** (spec §6.2, classes A/B/C/E) — parallel contract-extraction, one module-PR each, ratcheted by `NativeDemandHarvestTests`.
- **Death path** (spec L-3) — `OnObjectDeath` packet flow when HP ≤ 0.
- **Design-phase subsystems** (spec §6.3, class G) — `CreateObject` spawn, `nLocomotion`, modifier/FX → `ServerEvent 0x9B`.
- **Damage scaling capture** (spec L-1/L-2) — wire-capture a working-binary hit to decide coefficient/stat/defense.

---

## Self-Review

- **Spec coverage:** §5 combat data flow → Task 2 (TakeDamage engine) + Task 3 (in-game gate). §7 L-1/L-2 → documented as accepted-but-unused (not invented). §6.1 (combat engine fix as first roadmap item) → this whole plan. Prerequisite natives (§3 classes B/C) → Task 1. Remaining §6.2/§6.3 → explicitly deferred to follow-up plans.
- **Placeholders:** none — every code step shows full code; the one conditional (Step 6 failure branch) gives an explicit diagnostic, not a TBD.
- **Type consistency:** `RollDamage(float,float)`, `ApplyCrit(IReadOnlyDictionary<int,float>?, ref float)`, `IScriptGameBridge.ApplyHeal`/`ObjectExists`, `ScriptStateContext.TryGetAttributeSnapshot(uint, out IReadOnlyDictionary<int,float>)` all match existing signatures (`ScriptContextRegistry.cs`, `GameScriptContext.cs`). Crit attr ids (19/10/22) used only inside `ApplyCrit`. Returns `(bool, number, bool)` match the caller's `local hit, dealt, crit = ...`.
- **ABI safety:** `lua_error` (Step 3) is called outside any `try` (after the `argError` branch), per C7.
```
