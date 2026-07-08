# Lua demanded-natives catalog + port plan (2026-07-08)

## Goal

Close the **27 Lua natives** that retail tick-scripts actually call but ReCap does not
yet implement. Scope is *demand-driven*, not the blind 432-native surface: only what the
`NativeDemandHarvest` test proves is exercised by real ability/modifier tick coroutines.

This document is the **contract catalog + implementation order** produced by a research
fan-out (5 read-only Sonnet subagents, 2026-07-08) plus main-session Ghidra confirmation.
It contains **no production code**. Implementation is a later round, gated per-subsystem.

## Method (validated, and its one operational caveat)

Priority of sources, in order:

1. **Call-site disasm (`tools/scratch/LuaChunkDump`)** — the arbiter for arity, arg order,
   and return count. `--scan "<Name>"` finds caller chunks; disasm of the CALL site is
   ground truth. This beat Ghidra twice historically and did so again here (see `nBit.Mask`).
2. **Ghidra client (`Darkspore.exe`)** — the arbiter for *state semantics* + wire, because
   these natives live in the client, not the (divergent) C++ reference.
3. **C++ reference (`ReCapCpp`, dalkon)** — cross-check only. Heavily modified/divergent.
   Every claim from it is flagged `dalkon-approx`. Where it and Ghidra/disasm disagree,
   ReCapCpp is wrong. (Confirmed this round: dalkon's `GetPrivateTable`,
   `WaitForHitpointsAbove`, `SetAttributeSnapshot` are incomplete stubs — do not copy.)

**Operational caveat discovered this round:** subagents generally could **not** invoke the
Ghidra `decompile_function`/`search_functions` MCP tools (bridge reported them `callable`
but calls returned `No such tool available`). Only the main session reliably reaches them.
→ **Division of labour going forward: subagents do call-site disasm; the main session is
the Ghidra arbiter.** Every disasm-only claim is Ghidra-confirmed in the main session
before it drives an implementation decision.

## Scope: the 27 (from `NativeDemandHarvest`, 2026-07-08)

`Ability: started=283 completed=283 errored=181` / `Modifier: started=99 completed=66 errored=37`
→ demanded natives: **27** (down from 31; combat + death path closed the difference).

**Wave 1 landed 2026-07-08 (9 natives) → demanded 27 → 18.** Implemented: nBit.Mask,
nPlayer.GetPlayerIdForObject, nGameObject.ResetAnimationState, nGameObject.SetAttributeSnapshot,
nThreadData.GetPrivateTable, nThreadData.SetGUID, nAbility.ReleaseAgent, nThread.WaitForHitpointsAbove,
nThread.WaitForFadeOutInXSeconds — plus reusable scheduler predicate (`WakeWhen`). Plan:
`docs/superpowers/plans/2026-07-08-lua-natives-wave1-mechanical.md`. GetModifiedMoveSpeed /
WaitForJumpComplete / WaitForNearGoal were moved into Wave 2 (their predicates read locomotion state).
Remaining 18 = Wave 2 (locomotion) + Wave 3 (modifier/FX) + Wave 4 (spawn).

**Wave 2 landed 2026-07-08 (9 natives) → demanded 18 → 9.** Implemented: nLocomotion.{Stop,SlideToPoint,MoveToCircleEdge,TurnToFace}, nGameObject.{SetTargetPosition,SetNavCollision,GetModifiedMoveSpeed}, nThread.{WaitForNearGoal,WaitForJumpComplete} — plus the client-verified `0x95 LocomotionDataUnreliableUpdate` packet (objId+goalPos), `FlushObjectUpdates` goal→0x95 routing, GameObject locomotion fields, and `IScriptGameBridge` locomotion API. Plan: `docs/superpowers/plans/2026-07-08-lua-natives-wave2-locomotion.md`. Deferred (flagged): visible turn-in-place (TurnToFace server-side only), per-noun move speed / jump duration (named constants pending LocomotionTuning parse), real arrival detection (time-estimate, no server-side movement integration yet). Remaining 9 = Wave 3 (modifier/FX: nModifier×5 + nAttribute.AddAttributeModifier + nGameObject.AddEffect) + Wave 4 (spawn: nObjectManager×2).

| Bucket | Count | Natives |
|---|---|---|
| Mechanical | 10 | nThreadData.GetPrivateTable/SetGUID, nAbility.ReleaseAgent, nGameObject.ResetAnimationState/GetModifiedMoveSpeed/SetAttributeSnapshot, nThread.WaitFor{HitpointsAbove,FadeOutInXSeconds,JumpComplete,NearGoal} |
| Modifier/FX | 7 | nModifier.{GetMyAgentID,GetMyInitiatorID,GetMyStackCount,GetInitiatorAttributeSnapshot,PreloadAsset}, nAttribute.AddAttributeModifier, nGameObject.AddEffect |
| Locomotion/nav | 6 | nLocomotion.{MoveToCircleEdge,SlideToPoint,Stop,TurnToFace}, nGameObject.SetTargetPosition/SetNavCollision |
| Spawn | 2 | nObjectManager.CreateObject/AttachTriggerVolume |
| Ambiguous (resolved) | 2 | nBit.Mask, nPlayer.GetPlayerIdForObject |

---

## Catalog

Notation: **A**rity → **R**eturns · **S**emantics · **N**etwork · **D**eps · confidence.
`obj = ObjectManager::GetObjectFromLuaArg(argN)` throughout. Locomotion component ptr is at
`obj+0x298`; its goalFlags word is at `comp+0x144`; flag `0x800` = face-during-move.

### Ambiguous (both RESOLVED, Ghidra-confirmed main session)

**nBit.Mask(value, ...flags)** — *variadic* · R:1 number ·
S: `value & ~f1 & ~f2 …` — **clears** the listed bits (AND-NOT). **NOT** equal to `nBit.And`.
Ghidra `nBit::Mask@0x009fa170` (rounds each arg, folds `uVar2 &= ~flag`).
Single call site `template_modifier_dot.lua` initially read as "isolate to flag" (AND) by the
disasm-only agent — Ghidra refuted it. HIGH.

**nPlayer.GetPlayerIdForObject(objId)** — A:1 → R:1 number ·
S: returns the byte at `obj+0x55` (small player-id space, adjacent to team `obj+0x54`) when
`obj` exists and has a combatant component (`obj+0x2CC != 0`); else pushes a fallback string.
Bridges object-id space → player-id space for `nPlayer.*` APIs (PickupCrystal, IsOverdriveActive).
Ghidra `nPlayer::GetPlayerIdForObject@0x009ff410`. 3 consistent call sites. HIGH.

### Mechanical

**nThreadData.GetPrivateTable(objId)** — A:1 → R:1 (table or **nil**) ·
S: looks up the object's private Lua table, does **not** create if absent (contrast
`CreatePrivateTable`). N: none. D: per-object private-table store (exists). HIGH.
Cite: `Behavior_Death.lua` (inst 0xD6945860) instr 4-6.

**nThreadData.SetGUID(slotIndex, guid)** — A:2 → R:0 ·
S: stores a GUID at a per-thread indexed slot (paired with `GetGUID(slot)`); used to remember
an effect GUID for cleanup in `deactivate`. N: none. D: per-thread GUID slot array. HIGH.
Cite: `template_ability_instantcast.lua` (inst 0xF76B6B7F) instr 14-18, 94-98.

**nAbility.ReleaseAgent()** — A:0 → R:0 ·
S: acts on the implicit current-ability context; releases/detaches the agent the ability
instance holds (e.g. a placed trap becomes a free world object). N: possibly an
ownership/visibility update — DEFERRED (needs `nAbility::ReleaseAgent` decompile). D: ability
context. MED (arity HIGH). Cite: `modifier_claymoretrap_passive.lua` (inst 0x6AA78CE6) instr 9-11.

**nGameObject.ResetAnimationState(objId)** — A:1 → R:0 ·
S: undo of `SetAnimationStateToDeath` — restores the normal anim-state machine on revive
(called after `IsMarkedForDelete` false, before RemoveEffect/collision restore). N: none. HIGH.
Cite: `Behavior_Death.lua` instr 20-23.

**nGameObject.GetModifiedMoveSpeed(objId)** — A:1 → R:1 float (units/sec) ·
S: read-only post-modifier move speed (base × CombatSpeed × active buffs). N: none.
D: attribute/locomotion-speed system. HIGH (arity); MED (exact stack folded in).
Cite: `template_ability_charge.lua` (inst 0x56F1B93F) instr 134-137.

**nGameObject.SetAttributeSnapshot(objId, snapshotHandle)** — A:2 → R:0 ·
S: applied to a freshly-spawned projectile right after `SetTeam`; forwards the caster's
attribute snapshot so the projectile's later damage uses cast-time stats. N: none.
D: attribute-snapshot handle (paired with GetAgentAttributeSnapshot). HIGH (arity); MED (storage).
dalkon `LuaFunctions.cpp:779` = no-op stub (arity match only). Cite: `template_ability_projectile.lua`
(inst 0xCE0FC9AA) instr 381-385.

**nThread.WaitForHitpointsAbove(objId, hpThreshold, timeoutSeconds)** — A:3 → R:0 (yield) ·
S: yields until `Health(objId) > hpThreshold` OR `timeoutSeconds` elapse. `0` timeout ⇒
strongly-inferred "infinite/disabled". N: none. D: scheduler predicate + per-tick HP check.
HIGH (arity, HP half); MED (timeout=0 meaning). dalkon `LuaFunctions.cpp:1666` implements the
HP predicate but the timer is an admitted stub. Cite: `Behavior_Death.lua` instr 57-62, 64-70.

**nThread.WaitForFadeOutInXSeconds(objId, fadeSeconds)** — A:2 → R:0 (yield) ·
S: timed wait tied to corpse fade (called after `SetCorpseFadingAway` + fade `AddEffect`,
before `MarkForDelete`). Likely a plain wake-at-time, possibly also gated on still-valid-object
— DEFERRED. N: none observed. D: scheduler wake-at-time. MED. Cite: `Behavior_Death.lua` instr 90-95.

**nThread.WaitForJumpComplete(objId)** — A:1 → R:0 (yield) ·
S: yields until the object's jump/slide locomotion finishes (used after `SlideToPoint`; also in
`Behavior_Jump*`). No timeout (unbounded). N: none. D: scheduler predicate on locomotion state.
HIGH (arity); MED (exact predicate field). Cite: `template_ability_charge.lua` instr 106-109.

**nThread.WaitForNearGoal(objId, distThreshold, arg3=-1, arg4=10, arg5=true)** — A:5 → R:0 (yield) ·
S: yields until distance-to-goal ≤ `distThreshold` (2nd arg is a real varying computed value)
OR bounded by arg4 (likely 10s timeout). args 3-5 are constant literals across every observed
site (`-1, 10, true`) → fixed defaults. N: none. D: scheduler predicate + goal-distance query.
MED — args 3-5 roles DEFERRED (no site varies them; needs `nThread::WaitForNearGoal` decompile).
Cite: `template_ability_charge.lua` 4 sites, e.g. instr 142-149.

### Modifier/FX

**Subsystem — ModifierInvocation IS AbilityInvocation.** `GetMyStackCount` decompiles as
`nAbility::GetMyStackCount@0x00a41600`; `RegisterModifier == RegisterAbility@0x00a43040`;
modifier scripts dispatch handlers through the same `nAbilityFns` slot-key table as abilities.
The per-thread invocation is the top of an invocation stack (`FUN_008f3d00` = stack-top peek).
→ ReCap already has `AbilityInvocation`; the nModifier getters read the **same** per-thread
context. Only genuinely new field: **stackCount at `ctx+0x150`** (abilities don't use it).
Exact byte offsets for agent/initiator/snapshot within the ctx are an **impl-time Ghidra
verify** (getters adjacent to `0x00a41600`; NAbilityContextModule already models agent/target/snapshot).

**Subsystem — 0x9B ServerEvent.** `nGameObject.AddEffect` **emits 0x9B** (FX "attached" recipe
`{6 asset, 7 objectId}`); its asset arg is always `nAbility.PreloadAsset("<name>.ServerEventDef", tag)`,
matching VERIFIED_FACTS field-6 exactly; `RemoveEffect` emits the stop recipe `{7,1,2}`.
`ServerEventPacket` already exists. `nAttribute.AddAttributeModifier` does **NOT** emit 0x9B —
it mutates the attribute block on the normal update channel.

**nModifier.GetMyAgentID()** — A:0 → R:1 · object the modifier runs on (self). ctx read. HIGH.
**nModifier.GetMyInitiatorID()** — A:0 → R:1 · object that applied the modifier (caster/source). HIGH.
**nModifier.GetMyStackCount()** — A:0 → R:1 · `*(ctx+0x150)`. Ghidra-confirmed. HIGH.
**nModifier.GetInitiatorAttributeSnapshot()** — A:0 → R:1 (opaque handle) · frozen snapshot of
the initiator, same pattern as `nAbility.GetAgentAttributeSnapshot@0x00a417a0`; consumed by
`GetAttributeValue_FromSnapshot`. HIGH (arity); MED (fresh-vs-cached — impl-time Ghidra).
**nModifier.PreloadAsset(assetNameWithExt, ownerTag)** — A:2 → R:1 handle · no direct call site;
by RegisterModifier==RegisterAbility precedent, almost certainly the same native as
`nAbility.PreloadAsset`. MED — confirm pointer-equality at impl time.

**nAttribute.AddAttributeModifier(objId, attributeTypeEnum, value)** — A:3 → R:1 (opaque handle,
optional to capture) · applies an additive (add-vs-mult unverified) modifier; handle later fed to
`RemoveAttributeModifier(handle)`. N: none (no 0x9B). D: `nAttributeType` enum (exists), per-modifier
handle store. HIGH. Cite: `modifier_scope_debuff.lua` instr 30-37 (return captured + removed).

**nGameObject.AddEffect(objId, serverEventDefHandle, [initiatorId])** — A:2-3 → R:1 (effect-instance
handle, optional) · attaches a `.ServerEventDef` FX to objId; handle later fed to `RemoveEffect`.
N: **emits 0x9B** attached recipe. D: `nAbility.PreloadAsset` (exists), `ServerEventPacket` (exists).
HIGH (arity); MED-HIGH (0x9B linkage — impl-time confirm the native's SendServerEvent). Cite:
`modifier_pushpullboss.lua` Tick instr 36-43 (3-arg + captured), `modifier_reflective_npcaffix.lua` (2-arg).

### Locomotion/nav

**Subsystem.** None of the six send a packet directly. They mutate `LocomotionData`
(goal/facing/target/nav flags) and mark the object dirty; the existing per-tick
`Game.FlushObjectUpdates` (`Game.cs:247-266`) converts that into **0x90 ObjectTeleport** (if
GoalFlags `& 0x020` stop/teleport bit) else **0x94 LocomotionDataUpdate**. Much C# plumbing
already exists (`LocomotionData.cs`).

**nLocomotion.Stop(objId)** — A:1 → R:0 · matches `LocomotionData.Stop()` 1:1 (clears
target/velocity/facing, GoalFlags=`0x020`) → routes to 0x90. **Already implemented — no gap.**
Ghidra `Locomotion::Stop@0x00a1a150`. HIGH.

**nGameObject.SetTargetPosition(objId, x, y, z)** — A:4 → R:0 · sets the object's homing/tracking
target (`LocomotionData.TargetPosition`, wire offset `0x1AC`, already in `WriteTo`), distinct from
GoalPosition. Called every tick in homing-projectile loops to re-aim. N: via 0x94 once dirty.
D: field exists; native must set `ObjectDirtyFlags.Locomotion`. HIGH. Cite: `template_ability_projectile.lua`
instr 75-80, 85-91.

**nLocomotion.SlideToPoint(objId, x, y, z, speed)** — A:5 → R:0 · positional goal with an explicit
speed override via setter `FUN_00a1af30`, then marks dirty (`FUN_00a25cd0`). Needs a per-goal speed
field (not currently in `LocomotionData`). N: via 0x94. Ghidra `nLocomotion::SlideToPoint@0x00a046b0`.
HIGH (arity); exact goalFlags bit is inside `FUN_00a1af30` — impl-time read. Cite: `template_ability_charge.lua` instr 98-105.

**nLocomotion.MoveToCircleEdge(objId, x, y, z, radius, [faceGoal:bool])** — A:5 req +1 opt →
**R:1 bool** (had-locomotion; discardable) · approach-to-range via
`Locomotion::SetGoalPositionWithDistance` (stop-distance = radius); optional bool sets flag `0x800`
(face-on-arrival), then marks dirty. N: via 0x94. Ghidra `nLocomotion::MoveToCircleEdge@0x00a049c0`.
HIGH. Cite: `template_ability_charge.lua` instr 170-178. (Corrects the disasm-only "returns 0".)

**nLocomotion.TurnToFace(objId, x, y, z, [immediate:bool])** — A:4 req +1 opt → R:0 · faces a
**point** via `Locomotion::SetFacing` (NOT TurnToFaceTargetObject); optional bool sets flag `0x800`.
N: via 0x94 (facing bit). Ghidra `nLocomotion::TurnToFace@0x00a05300`. HIGH. Cite: `template_ability_charge.lua` instr 28-35.

**nGameObject.SetNavCollision(objId, collidable:bool)** — A:2 → R:0 · **server-side only, no wire.**
Writes inverted bool at `obj+0x284` (`arg2==0`), gated on component `obj+0x298`, then local recompute
(`FUN_009eb950`). Lets a charging creature pass through collidables. Refutes the S-agent's 0x93
PhysicsChanged hypothesis. N: **none**. D: new nav-collision flag on the object. Ghidra
`nGameObject::SetNavCollision@0x009fe7c0`. HIGH. Cite: `template_ability_charge.lua` instr 59-63.

### Spawn

**Subsystem.** `nObjectManager::CreateObject@0x00a07f40` → `ObjectManager::CreateCreature@0x009db960`
→ `CreateObjectInternal@0x009d6de0`. **Exhaustive callee walk: the spawn native sends NO wire
packet.** Client visibility comes from the **existing 0x8C ObjectCreate** packet
(`ClientNet::OnGmsObjectCreate@0x0053f550`) which ReCap already ships (`ObjectCreatePacket` +
`GameObjectCreateData` + `SporelabsObject`, used today for markers/heroes at `Game.cs:563,636`).
→ **No new wire format.** A server-side `CreateObject` must spawn, then explicitly unicast the
existing `ObjectCreatePacket` per client, mirroring the hero/marker path.

**nObjectManager.CreateObject(assetHandle, x, y, z, …)** — variadic, `lua_gettop`-gated →
R:1 number (new object id) · arg1 is a numeric **asset handle** (`nUtil.GetAsset("Foo.Noun")`,
read via `GetObjectIdArg`, not a string). Shapes: 4-arg minimal `(handle,x,y,z)`; 6-9-arg
`(…, yaw°, scale, [owner])` (orientation derived from sin/cos of yaw); 10-11-arg
`(…, qx,qy,qz,qw, scale, owner, [guidString])`. Callers chain `SetTeam`/`SetAttributeSnapshot`/
`SetOwnerID` after — CreateObject sets none of these. N: none in native; announce via existing 0x8C.
D: AssetDatabase (resolve `nUtil.GetAsset` handle), `ObjectManager.Spawn` (exists), existing packet,
**a new Lua-context spawn+announce bridge** (Game.cs sends are hardcoded for markers/heroes).
HIGH (4-arg); MED-HIGH (extended forms; owner-arg forwarding shows a decompiler dead-store —
impl-time re-check). Cite: `template_ability_projectile.lua` instr 366-371; `ability_plasmawall.lua` ~496-497.

**nObjectManager.AttachTriggerVolume(objectRef, radius, [cb1], [cb2], [cb3])** — A:2 req +3 opt →
R:1 number (trigger handle) · **sphere-only** (`CreateTriggerVolumeSphere@0x00a1c190`); args 3-5 are
optional **Lua callback closures** (onEnter/onExit/onStay-style, captured via `luaL_ref`-equivalent),
**not** position offsets (refutes the TeleporterDef-offset hypothesis). After creation, links the
handle onto the object (`obj+0x40..0x4c`). `deferTriggerCreation` is caller-side timing, not a native
arg. N: no broadcast in native — trigger volumes appear to be a server-local physics construct;
**DEFERRED-pending-capture** for any wire claim (genuinely unknown, not confirmed-absent).
D: a C# sphere-trigger-volume + Lua callback-ref mechanism (neither exists yet). HIGH (arity/sphere);
LOW (wire). Ghidra `nObjectManager::AttachTriggerVolume@0x00a07e00`.

---

## Implementation order (dependency-sorted)

Implementation is a **future round**, per-subsystem, TDD, with the harvest ratchet + an in-game
gate per subsystem. Order chosen so prerequisites land before their consumers:

1. **Mechanical batch** (10). No cross-deps beyond existing scheduler/stores. Fast win; validates
   the ratchet early. Sub-order: nThreadData ×2 → ResetAnimationState → GetModifiedMoveSpeed →
   SetAttributeSnapshot → ReleaseAgent → the 4 `nThread.WaitFor*` (each a scheduler predicate).
   Also fold in the 2 resolved ambiguous (`nBit.Mask` as AND-NOT variadic; `GetPlayerIdForObject`
   reading `obj+0x55`) — trivial, no subsystem.
2. **Locomotion/nav** (6). Most plumbing exists. Order: `Stop` (already done — verify) →
   `SetTargetPosition` (field exists) → `SlideToPoint` (adds goal-speed field) →
   `MoveToCircleEdge` (SetGoalPositionWithDistance + flag 0x800) → `TurnToFace` (SetFacing + 0x800) →
   `SetNavCollision` (new server-side flag). Gate: NPC charge/homing-projectile moves smoothly in-game.
3. **Modifier/FX** (7). Prereq: extend `AbilityInvocation` with stackCount (+0x150) and expose it as
   the modifier context (same struct). Order: nModifier getters ×4 → PreloadAsset (confirm ptr==nAbility)
   → AddAttributeModifier (+ RemoveAttributeModifier handle store) → AddEffect (wire to existing
   ServerEventPacket 0x9B). Gate: Pummel's Silence debuff + a `.ServerEventDef` FX show in-game.
4. **Spawn** (2). Prereq: a Lua-context spawn+announce bridge (spawn via ObjectManager, then unicast
   existing `ObjectCreatePacket`). Order: `CreateObject` (4-arg first, then extended) → `AttachTriggerVolume`
   (needs new sphere-volume + Lua callback-ref). Gate: a homing projectile / summoned pet appears and
   acts in-game. Highest risk — do last, capture-verify the 0x8C payload before shipping.

## Residual Ghidra verifications (impl-time, main session only — exact targets)

- ModifierInvocation agent/initiator/snapshot offsets: getters adjacent to `0x00a41600`.
- `nModifier.PreloadAsset` vs `nAbility.PreloadAsset` pointer-equality: registrar block `0x00a435c0`.
- `nGameObject.AddEffect` SendServerEvent call site (confirm 0x9B emit + exact fields).
- `nThread.WaitForNearGoal` args 3-5 roles + `WaitForHitpointsAbove` timeout=0 semantics.
- `nAbility.ReleaseAgent` state mutation (does it unlink the owning creature?).
- `nLocomotion.SlideToPoint` goalFlags bit inside `FUN_00a1af30`.
- `CreateObject` owner-arg forwarding (decompiler dead-store `uVar8=0`) + `ParseReflectionMessage`
  full-23-field vs subset for the 0x8C payload.

## Gate / ratchet

- Re-run `RECAP_HARVEST=1 dotnet test --filter NativeDemandHarvest` after each subsystem; the
  `errored` count is a monotonic ratchet — it may only decrease.
- Per-subsystem in-game gate (listed above). Close the server before rebuilding (native DLL lock).
- Keep the golden test suite green; new tests cite this catalog + Ghidra `addr` / chunk `instr`,
  never restated dogma.
