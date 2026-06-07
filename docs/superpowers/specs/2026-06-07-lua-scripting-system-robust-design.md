# Lua Scripting System — Robust Reimplementation Design (all modules)

> Status: design. Supersedes the scope of the P0–P2 runtime spec (`2026-06-05-lua-scripting-system-design.md`) by adding the architecture-level model and the repeatable method for implementing **every** native/module, not just the runtime. Combat (`TakeDamage`) is the worked example.

## 1. Purpose & North Star

Run Darkspore's **original compiled Lua 5.1 chunks** server-side (ReCap), so abilities, modifiers, affixes, conditions and objectives behave as the original game did — **data-driven, zero hardcoded**. The data lives in the chunks; our job is to execute them faithfully and provide natives whose semantics match the engine. Where the C++ reference is an incomplete approximation, we match the **client's required contract** (verified by disasm / wire), never a guess.

This is the umbrella that makes the remaining ~370 unimplemented natives a **repeatable, consistent** exercise rather than 370 ad-hoc puzzles.

## 2. Verified architecture (the load-bearing facts)

| Fact | Evidence | Consequence for us |
|---|---|---|
| Lua = **5.1.4, `lua_Number=float`** | header `1B 4C 75 61 51 00 01 04 04 04 04 00`; client `0x00909cb0` | `recaplua51.dll` (vendored float build); all numbers float; 24-bit mantissa exact ≤16M |
| **The Lua chunk IS the asset** | Ghidra `RegisterAbility 0x00a43040`: reads Lua table → writes C++ struct **one-way**, never writes back; no `.ability` file lookup | **No asset-file→table property-merge needed.** Data is in the chunk. We just run chunks + read back the registered table for server-side (non-Lua) consumers |
| Register* = table→struct via **reflection** (type-hash dispatch) | `FUN_00a0a3c0` iterates table, `FUN_00a09f90` marshals by field type-hash | If we later need a C# ability-metadata struct, drive it generically from the table, same dispatch |
| Register* dedup = **skip-if-exists** (unless reload mode `L+0x4c>0`), returns **0** | `0x00a43040` | Already matched in `RegistrarModule` |
| Boot = 10 group hashes, ~1017 chunks; **GlobalDefinitions** defines `Class`, `nDescriptors`, enums | `LuaSystem::Initialize 0x00a0c810`; disasm GlobalDefinitions | Boot order must run Lua group (defs) before consumers; done |
| Objects are **uint32 IDs as float**; positions = **3 separate floats** | `GetObjectFromLuaArg 0x009f9740`, `GetPosition 0x009fb870` | `PushId=(float)id`, `ReadId=round`; no userdata |

### Runtime seams (already built, P0–P4)

```
ServerData.package ──(ScriptVfs: Group!Name.ext)──┐
                                                   ▼
LuaRuntime (float ABI, sandbox, require) ── ScriptEngine (boot 10 groups)
   │                                              │
   ├─ ScriptContextRegistry (L → ScriptStateContext)
   │      ├─ ScriptRegistry (Register* storage: kind→FNV→entry)
   │      ├─ LuaCoroutineScheduler (1 coroutine/object, Sleep/Wait/WakeUp)
   │      ├─ AbilityInvocation (per-thread cast context)
   │      └─ IScriptGameBridge ──► Game / ObjectManager (the Lua↔gameplay seam)
   └─ Api/ modules (the natives)
```

**Design rule:** gameplay is reached **only** through `IScriptGameBridge`. Natives never touch `Game` directly. This keeps natives unit-testable with a fake bridge (see `GameBridgeTests`).

## 3. Native taxonomy — the key to doing all modules consistently

The ~432 natives across ~20 namespaces fall into **7 implementation patterns**. Each pattern has one shape; once the shape is right, every native in it is mechanical.

| Class | Examples | Pattern | Test shape |
|---|---|---|---|
| **A — Pure/compute** | `nBit.*`, `nMathUtil.*`, `nUtil.SPID/ToGUID` | no state; read args → compute → push | golden vectors |
| **B — Context getters** | `nAbility.GetAgentID/GetTargetID/GetTargetPosition/GetRank/GetAnimationSequenceIndex` | read per-thread `AbilityInvocation` | set invocation → assert |
| **C — Object readers** | `nGameObject.GetPosition/GetHitPoints/GetTeam/GetWeaponDamage`, `nAttribute.GetAttributeValue[_FromSnapshot]` | read `GameObject` via bridge | fake bridge |
| **D — State mutators / engines** | `nGameObject.TakeDamage/HealDamage/SetAnimationState`, `nModifier.*` | read args → mutate via bridge → maybe broadcast packet → return per contract | fake bridge asserts mutation |
| **E — Scheduler/control** | `nThread.Sleep/WaitForXSeconds/WaitForever/WakeUp/CreateThreadForObject` | yield/condition on `LuaCoroutineScheduler` | scheduler tick tests |
| **F — Registrars/preload** | `RegisterAbility/Modifier/Affix/Condition/Objective`, `Preload*` | store/echo per C4/C3 | registry tests |
| **G — Design-phase subsystems** | `nObjectManager.CreateObject` (spawn), `nLocomotion.*` (movement), `AddEffect`/`AddAttributeModifier` (FX/modifier→ServerEvent 0x9B) | new gameplay capability behind a new bridge method | integration |

**Implication:** classes A/B/C/E/F are bounded and batchable. Class D each needs an **engine contract** (disasm + Ghidra/C++). Class G are genuine features (own mini-designs). Roadmap (§6) is ordered by this.

## 4. The data-consumption principle (project rule, made concrete)

> Every native consumes **all** of its arguments / the chunk's data faithfully. No hardcoded value that the chunk or asset derives from data.

The cautionary worked example is `TakeDamage`: the melee tick calls it with **11 arguments**, but the prior stub read arg 3 as a number and ignored the other 9 → **0 damage**. A robust native is defined by **two contracts**, both evidence-backed, never guessed:

1. **Call-site contract** — arity + the meaning of each arg, from the **Lua caller disasm** (`LuaChunkDump`). This is authoritative for *how the native is invoked* (it corrected Ghidra-agent guesses before).
2. **Engine contract** — what the native *does* with those args (roll, scale, mutate, branch), from **Ghidra** (client) and/or the **C++ reference**, reconciled against wire capture when they disagree.

A native is "done" only when both are satisfied and a test pins them.

## 5. Worked example — the combat/damage data flow (fully grounded)

End-to-end path the retail client drives, and where each datum is consumed:

```
0x9C ActionCommand (slot)
  └► AssetDatabase.ResolveAbilitySlots(heroNoun) → ability name → FNV → registry
     └► InvokeAbility(hash, agent, target, cursor, rank) → tick coroutine (self = registered table)
        └► melee tick (template_ability_melee, 58-slot fn):
           snapshot = nAbility.GetAgentAttributeSnapshot(agent)        -- frozen attacker attrs
           dmgTable = self:GetDamage()                                 -- ↓ class-C/D
             ├ if nBit.And(self.descriptors, nDescriptors.IsBasic)≠0   -- descriptors from chunk (GlobalDefinitions enum)
             │   and nPlayer.IsPlayerControlledObject(agent):
             │     return nGameObject.GetWeaponDamage(agent)           -- {min,max} from attrs 101/102
             └ else: return deepcopy(nAbility.GetRankedValue(self.damage))   -- ranked table by rank
           target = nTargetUtils.FindBestTargetInArc(arc, agent)
           nGameObject.TakeDamage(snapshot, target, dmgTable,
              damageType, damageSource, GetDamageCoefficent(), descriptors,
              damageMultiplier, dirX, dirY, dirZ)                      -- 11 args, the engine
```

### `TakeDamage` engine contract (Ghidra + C++ reference `Object.cpp:1377`, 2026-06-07)

- **arg3 must be a table `{[1]=min,[2]=max}`** (size 2) — a number raises `lua_error`. (Client `0x00a06170` is a stub returning `{true,max,false}`; the real roll is server-side.)
- `baseDamage = Random(min, max)` (uniform float).
- **Crit:** `AutoCrit`(attr 19) `>0` → always crit; else `Random(0,1) < CriticalRating`(attr 10)`/100`; if crit `baseDamage *= CriticalDamageIncrease`(attr 22)`+1`. Crit reads the **attacker snapshot**; invalid snapshot → no crit, damage still applies.
- **HP:** `SetHealth(hp - baseDamage)`; if `≤0` → death event. Returns **`{hitGate(truthy), damageDealt, isCrit}`**; caller `TEST`s the first as hit-success.
- **Target missing → 0 Lua return values** (nil gate).
- **Accepted-but-unused in the reference:** `damageCoefficient`, `damageMultiplier`, `damageType`, `damageSource`, `descriptors`, direction, **and any target defense/mitigation**. → **Open divergence (§7).**

### Bridge additions for the example
`IScriptGameBridge`: keep `ApplyHeal(target, +/-amount)` (damage = negative; already mutates HP server-side and clamps). Death/`OnObjectDeath` packet flow is a follow-up (currently `MarkForDelete`).

### Gate
- **Offline (RED→GREEN):** `PummelStallReproTests` asserts `enemy.Health < start` after the cast (hero weapon 1–5 → always >0).
- **In-game:** Pummel from melee range → `HealDamage hp 18→<18` decreasing → enemy reaches 0 → observe death path.

## 6. Implementation roadmap (priority by demand, then by class)

Harvest (`NativeDemandHarvestTests`, `RECAP_HARVEST=1`): **382 tick-scripts demand only 31 natives.** Implemented today ≈63/432. Order:

1. **Combat engine fix (this spec's gate):** `TakeDamage` faithful (class D). Unblocks all melee/ranged basics.
2. **Batch the ~31 mechanical demands** (classes A/B/C/E) — parallelizable; one module-PR each, ratcheted by the harvest re-run:
   - context: `nModifier` per-thread ×4 + `PreloadAsset`; `nThreadData.GetPrivateTable/SetGUID`
   - compute/read: `nBit.Mask`, `nGameSimulator.GetGameTime`, `nPlayer.GetPlayerIdForObject`, `nGameObject.GetModifiedMoveSpeed/IsModifierActive/SetTeam/ResetAnimationState/SetAttributeSnapshot`, `nAttribute` gaps
   - scheduler waits: `nThread.WaitForHitpointsAbove/NearGoal/JumpComplete/FadeOut`
   - `nAbility.ReleaseAgent`
3. **Design-phase subsystems (class G), each its own mini-design:**
   - `nObjectManager.CreateObject` + `AttachTriggerVolume` (server-side projectile/pet spawn + wire)
   - `nGameObject.SetTargetPosition` + `nLocomotion.*` (homing/movement)
   - `AddEffect`/`AddAttributeModifier` → modifier system + `ServerEvent 0x9B` (FX)
   - channeled/vtable dispatch (NecroRandom, Ghostform) — non-tick families
4. **Combatant/HP reflection to client:** `CombatantDataUpdate 0x97` so HP changes show; response `0xA8 type=1` (cooldown UI).

## 7. Open divergences (must resolve by evidence, not guess)

| # | Item | State | Resolution |
|---|---|---|---|
| L-1 | Damage scaling (coefficient / primary-stat / multiplier) | C++ reference accepts but **does not apply** | Wire-capture a working-binary hit; if damage scales, add formula; else keep reference behavior |
| L-2 | Target defense/mitigation | not in reference `TakeDamage` | Same — capture before adding; may live in a separate Simulation step |
| L-3 | Death path (`OnObjectDeath` packets, vaporize bool) | not modeled (only `MarkForDelete`) | Trace on first enemy reaching 0 hp |
| L-4 | `WaitUntilTime` timing (cast-relative clock vs tick-granular) | `max(Now,x)` approximation | Settle in Simulation phase (hit must match anim) |
| L-5 | Hit-gate return type (number vs bool) of `TakeDamage[1]` | truthy either way today | Confirm if any script does arithmetic on it |

## 8. Port method (so every module is done the same way)

```
harvest (what's demanded) → call-site disasm (arity+arg meaning, LuaChunkDump --scan/-l)
  → Ghidra/C++ (engine semantics) → reconcile vs wire when they disagree
  → implement in the class pattern (Api/ module) → offline repro test (RED→GREEN)
  → in-game gate → harvest re-run ratchet (errored count only goes down)
```

- **Model tiering:** Haiku = mechanical lookups; Sonnet = disasm sweeps, Ghidra semantics, multi-file batches, agent fan-out; Opus = hard reasoning only.
- **Parallelism:** independent natives' contract-extraction (disasm/Ghidra) fans out to concurrent agents; implementation is serialized per module to keep the tree clean.
- **Tooling:** `tools/scratch/LuaChunkDump` (`--scan <str>`, `<name|0xINST>` disasm with float luac); `NativeDemandHarvestTests`; per-ability `GateErrorProbeTests`.

## 9. Verification strategy

- **Boot fidelity:** 1017 chunks / 0 failures / ≤13 retail-missing (ratchet test).
- **Native correctness:** per-module unit tests with `FakeBridge` (classes A–F).
- **Combat gate:** offline repro (damage applied) + in-game (enemy dies).
- **Demand ratchet:** harvest re-run; `errored` natives strictly decrease.
- **No silent caps:** any bounded coverage is `log()`-ed.

## 10. Non-goals (this spec)

- Asset-file→Lua property-merge (disproven — chunk is the asset).
- Pure-C# Lua port ("ReCap.Luptos") — deferred; native is the differential oracle.
- Full Simulation-phase combat math (scaling/defense) until L-1/L-2 captured.
- Multiplayer / non-Dungeon paths.
