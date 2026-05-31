# Objects & Objectives System (Dungeon)

End-to-end map of the in-game **object** and **objective** packet systems on the single-player Dungeon path: the C++ server source, the actual wire (working capture), and the client's consumption. Built 2026-05-31 because this area is the live crash lead (`D-003`/`D-009`) and was under-mapped.

> **CRITICAL — capture ≠ source.** The working C++ server is a **prebuilt binary** (`…/DarksporeBin/Server/`) that has **drifted** from the `ReCapCpp` source tree. For wire-format truth, trust the **capture** (`captures/cpp_loopback.pcapng`, game conv UDP 3659↔57210), not the source. See the objectives format below for proof. C# was ported from the *source*, so where the source drifted, C# is wrong.

Roots: C++ source `…/ReCapCpp/darkspore_server/source`; C# `ReCap.Server/`.

---

## 1. Object system

### 1.1 Packets (C++ senders)

| Packet | ID | C++ sender (file:line) | Wire layout |
|---|---|---|---|
| ObjectCreate | 0x8C | `Server::SendObjectCreate` (Server.cpp:1460) | `u8 id + u32 objId + cGameObjectCreateData::WriteReflection + Object::WriteReflection` |
| ObjectUpdate | 0x8D | `Server::SendObjectUpdate` (Server.cpp:1519) | `u8 id + u32 objId + Object::WriteReflection` (only dirty bits) |
| ObjectDelete | 0x8E | `Server::SendObjectDelete` (Server.cpp:1536) | `u8 id + u32 objId` (+ vectored multi-id overload) |
| ObjectJump | 0x8F | `Server::SendObjectJump` (Server.cpp:1684) | id + dest vec3 + dir vec3 + 4× f32 jump params |
| ObjectTeleport | 0x90 | `Server::SendObjectTeleport` (Server.cpp:1702) | id + position vec3 + orientation quat |
| ObjectPlayerMove | 0x91 | `Server::SendObjectPlayerMove` (Server.cpp:1716) | id + goalFlags + goalPos + facing + extLinVel + extForce + stopDists + targetPos + targetId |
| LocomotionDataUpdate | 0x94 | `Server::SendLocomotionDataUpdate` (Server.cpp:1767) | id + `LocomotionData::WriteTo` (0x48C binary blob) |
| AttributeDataUpdate | 0x96 | `Server::SendAttributeDataUpdate` (Server.cpp:1798) | id + `Attributes::WriteReflection` (>16 fields → field-byte + 0xFF) |
| CombatantDataUpdate | 0x97 | `Server::SendCombatantDataUpdate` (Server.cpp:1813) | id + `cCombatantData::WriteReflection` (2 fields: f32 hp, f32 mana) |
| InteractableDataUpdate | 0x98 | `Server::SendInteractableDataUpdate` (Server.cpp:1828) | id + `cInteractableData::WriteTo` (0x34 binary blob: @0x08 s32 timesUsed, s32 usesAllowed; @0x14 u32 ability) |
| AgentBlackboardUpdate | 0x99 | `Server::SendAgentBlackboardUpdate` (Server.cpp:1847) | id + `cAgentBlackboard::WriteReflection` (5 fields: u32 targetId, bool inCombat, u8 stealth, bool targetable, u32 numAttackers) |

### 1.2 `cGameObjectCreateData::WriteReflection` — `reflection_serializer<10>` (Types.cpp:415)
2-byte LE bitmap, then set fields: `0 noun(u32)`, `1 position(vec3)`, `2 rotXDeg(f32)`, `3 rotYDeg`, `4 rotZDeg`, `5 assetId(u64)`, `6 scale(f32)`, `7 team(u8)`, `8 hasCollision(bool)`, `9 playerControlled(bool)`. Heroes/enemies set all 10 (bitmap `0x03FF`).

### 1.3 `Object::WriteReflection` — `reflection_serializer<23>` (Object.cpp:1763), field-byte mode + `0xFF`
`0 Team(u8)`, `1 PlayerControlled(bool)`, `2 InputSyncStamp(u32)`, `3 PlayerIdx(u8)`, `4 LinearVelocity(vec3)`, `5 AngularVelocity(vec3)`, `6 Position(vec3)`, `7 Orientation(quat)`, `8 Scale(f32)`, `9 MarkerScale(f32)`, `10 LastAnimState(u32)`, `11 LastAnimPlayTime`, `12 OverrideMoveIdleAnim`, `13 GraphicsState`, `14 GfxStateStart`, `15 NewGfxStateStart`, `16 Visible(bool)`, `17 HasCollision(bool)`, `18 Owner(u32)`, `19 MovementType(u8)`, `20 DisableRepulsion(bool)`, `21 InteractableState(u32)`, `22 MarkerId(u32)`.

### 1.4 Per-type field sets (WIRE-VERIFIED, cpp_loopback)
| Object type | ObjectCreate wire size | object-reflection bits | Notes |
|---|---|---|---|
| **Hero / character** | **59B** | `{0 Team, 1 PlayerControlled, 3 PlayerIdx, 17 HasCollision}` | createData all-10 with position; **no** position(6) in reflection — C++ positions hero via ObjectUpdate. |
| **Enemy / agent** | **93B** | richer set incl. position(6) etc. | the bulk (×278). |
| (variant) | **81B** | — | a third spawn variant (×3). |
| **Hero ObjectUpdate** | **21B** | `{6 Position, 16 Visible}` | C++ `0x8D`; 19B variant = `{6}` only. |

C# fix `35a64f8` aligned the hero create/update to these (was a single bloated `{0,1,3,6,7,16,17}` objData → 91B create + 46B update). Golden-tested in `ReCap.Tests/Packets/ObjectCreateTests.cs`.

### 1.5 Level-spawn pipeline (C++)
`Instance::OnPlayerStart` (Instance.cpp:308) → `LoadLevel()` reads `data/serverdata/level/<level>.level.xml` (Level.cpp:354), whose `<markersets>` name markerset files `data/serverdata/markerset/<name>.xml` (pugixml; `Markerset::Load` Level.cpp:245). Named subsets by suffix: `_obelisk_1` (obelisks), `_design`/`_design_spawners` (teleporters/design), `_AI_Wander*` (enemy spawns). Each marker → `ObjectManager::Create(marker)` (sets position/orientation/scale/markerId, adds to `mActiveObjects`). Enemies use `ObjectManager::Create(mChainData.GetEnemyNoun(rand 0..5))` at director markers (Instance.cpp:355). Then `OnPlayerStart` loops `GetActiveObjects()` (Instance.cpp:404) → `SendObjectCreate` each (the **278** objects in the capture); the 3 heroes (pre-created by `Player::SetCharacter`) get `SendObjectUpdate`.

**C# gap (`D-009`):** C# spawns only the 3 heroes; the ~275 level objects + companions (`0x98`/`0x90`/`0x91`/`0x94`) are not emitted — needs a C# level/markerset loader. Status: open.

---

## 2. Objective system

### 2.1 Packets (C++ senders)
| Packet | ID | C++ sender (file:line) | Source-tree wire layout |
|---|---|---|---|
| ObjectivesInitForLevel | 0xB7 | `Server::SendObjectivesInitForLevel` (Server.cpp:2216) | `u8 id + u8 count + count×Objective::WriteTo` |
| ObjectiveUpdated | 0xB8 | `Server::SendObjectiveUpdate` (Server.cpp:2232) | `u8 id + u32 objId + u8 clientId + u8 medal + u32 voiceover + bool showNotif + u32 value + u32 unk(=2) + u32 unk(=3)` = **24B** |
| ObjectivesComplete | 0xB9 | `Server::SendObjectivesComplete` (Server.cpp:2251) | `u8 id + u8 count + count×Objective::WriteTo + u32 medals` |
| ObjectiveAdd | 0xCA | `Server::SendObjectiveAdd` (Server.cpp:2273) | `u8 id + Objective::WriteTo` |

### 2.2 Objective source (NOT level/Lua)
Objectives are **hardcoded** in `Instance::Instance()` (Instance.cpp:62-76): 5 objectives, ids = FNV-1 of the names, all `value=1`, `medal=Gold`, `description="Do some stuff bruh"`:

| # | Name | FNV-1 id |
|---|---|---|
| 0 | FinishLevelQuickly | `0xFF9733EE` |
| 1 | DoDamageOften | `0xAC4273F3` |
| 2 | TouchAllObelisks | `0x61C07561` |
| 3 | DefeatAllMonsters | `0xA28485CC` |
| 4 | HugeDamage | `0x0478FACB` |

**Cadence:** `Instance::Update` (50ms tick, Instance.cpp:468) sends `SendObjectiveUpdate(client, 0, hash("vo_ship_obelisk_accessed"))` every tick with objective-0 value = `elapsed/1000` → 0xB8 fires ~20 Hz. The other 4 are sent once at init (Instance.cpp:399) with voiceover=0.

### 2.3 ⚠️ ObjectivesInit format — source/wire DIVERGENCE (the build-drift proof)

| | wire format (per objective) | total (5 objectives) |
|---|---|---|
| **Working capture (binary)** | `u32 id + u24 value` = **7 bytes** | **37B** (`b7 05` + 5×7) |
| **ReCapCpp source `Objective::WriteTo`** | `u32 id + u32 value + 0x30 description` = **56 bytes** | **282B** |
| **C# `ObjectiveData.WriteTo`** | `u32 id + u32 value + 48B desc` = **56 bytes** | **282B** (matches source, NOT the working wire) |

Capture `0xB7` bytes: `b705 ee3397ff 090000 f37342ac 010000 6175c061 010000 cc8584a2 010000 cbfa7804 010000` — count=5; obj0 `id=0xFF9733EE value=9`, obj1-4 `value=1`. The 5 ids match the FNV table above exactly ⇒ same objectives, **7-byte** wire.

**Hypothesis (high crash suspicion):** the client expects 7-byte entries; C# sends 56-byte entries → per-objective read desyncs → objectives HUD fails. **PENDING:** confirm the client's exact 0xB7 parse struct in Ghidra (OnGms 0xB7) before changing C#. `0xB8 ObjectiveUpdated` (24B) decoded clean from the capture and matches the source layout; **C# currently sends zero 0xB8** (no per-tick objective update at all).

---

## 3. Client consumption (Ghidra) — PARTIAL (analysis stalled)

A Ghidra agent stalled on the 49k-function binary before completing, but its partial findings point strongly at the **objectives HUD**:
- The crashing subview (reached from per-frame UI update `FUN_007ee9d0` via the subview manager at `[mgr+0xb8]`, `vtable[0x2c]→[0x30]`) is most likely **`cObjectivePopup`**. Its `+0x20` field (the `GFxMovieView*` movie handle) is **NOT initialized** in the common subview ctor `FUN_00423f60` (which zeroes +0x08/+0x0c/+0x18/+0x1c/+0x28/+0x2c but skips +0x20). cObjectivePopup also sets a secondary vtable `PTR_LAB_00fd4afc` at +0x30. So if the movie-load path for the objective popup never runs, `+0x20` stays uninitialized → the per-frame `MOV ECX,[ESI+0x20]; CALL 0x551f10` (GFx invoke) derefs garbage/null → crash `@0x551f47`.
- This corroborates the wire finding: the objectives feed was malformed/absent (C# sent 56-byte ObjectivesInit + zero ObjectiveUpdated), so the objective-popup movie was never properly driven.

Still unconfirmed (Ghidra route too slow): the exact `OnGms` parse for 0xB7/0xB8 and precisely what gates the cObjectivePopup movie-load. We chose to act on the wire evidence (match the working binary) rather than fight the slow Ghidra sweep — the client run is the decisive arbiter.

### 3.1 Crash mechanism CONFIRMED (targeted Ghidra, 2026-05-31)
`FUN_007ee9d0` (per-frame UI update; ends `param_1[0x70]++`) tail:
```
sv = (*(*param_1[0x2e] + 0x2c))();      // [0x2e] = subview manager (+0xb8); vtable[0x2c] -> the CURRENT subview
if (sv) (*(*sv + 0x30))(local_2c, fVar2); // per-frame update of the current subview
```
The crashing `MOV ECX,[ESI+0x20]; CALL 0x551f10` is inside that `sv->vtable[0x30]` chain: `ESI = sv` (the current subview), `[sv+0x20] = GFxMovieView*` used as `this` for the GFx invoke (`FUN_00551f10`). So **the subview that is "current" at crash time has a NULL movie (+0x20)**. `FUN_00423f60` (common subview ctor) zeroes +0x08/0c/18/1c/28/2c but NOT +0x20 — the movie is bound by a separate LoadMovie path that, in the C# session, never runs for the current subview.
- **Interpretation:** the client switches to / keeps "current" a HUD subview whose movie was never loaded, and the per-frame tick derefs it. This is consistent with the fall-back (the client leaving the live-game view for a menu/room view whose movie isn't ready, because the C# dungeon is an empty static world). We do NOT need to reverse the exact movie-load gate — the **working binary satisfies it by sending the full living world**, and we have the wire formats decoded. The capture is the confirmation oracle; replicate it.

---

## 5. Open questions — confirm BEFORE reimplementing (client is strict)

We can reimplement the living world cleanly (possibly better than C++'s reverse-eng approximation), but each of these must be **confirmed** (capture and/or Ghidra), not guessed — a misread null-derefs the client.

**Crucial (blocks the crash fix):**
1. **What is the client's "level-ready / stay-in-dungeon" gate?** The C# client re-issues `joinRoom`/`getPartList` ~1s after deploy (the C++ client never does) and then crashes — it is *falling back to a menu/room state*. What condition makes the client bail? A count of objects? A specific object type (obelisk/spawn)? A "level loaded" packet/event? A timeout waiting for movement? → **Ghidra: the per-frame `FUN_007ee9d0` subview update + what gates the cObjectivePopup/HUD movie (`+0x20`) load.** Without this we might spawn 278 objects and still bail.

**Important (correctness of the reimplementation):**
2. **Object ordering vs deploy.** C++ sends ALL ObjectCreate (heroes via `Player::SetCharacter` early, the rest in the `GetActiveObjects` batch) BEFORE `PlayerCharacterDeploy`; heroes go out as ObjectUpdate (already `Created`). Does the client require the full world present before deploy?
3. **Team per object type.** Heroes=1. Enemy/marker team is unconfirmed in C++ `Create(marker)` (left default). The client may target/color by team.
4. **Which markers spawn, and the enemy rule.** C++ loads `_obelisk_1`/`_design`/`_design_spawners`/`_AI_Wander*`, picks enemy nouns randomly from chain (`GetEnemyNoun(rand 0..5)`) at director markers, and has a `break` capping enemies to 1/set (looks like a C++ shortcut, not the original). The faithful/robust choice is open — confirm what the client expects vs what reads as a C++ hack.
5. **Marker `componentData` → InteractableData/Teleporter.** Obelisks need `InteractableDataUpdate` (0x98) content (timesUsed/usesAllowed/ability) and teleporters need the teleporter component. Not yet decoded.
6. **The 81B ObjectCreate variant** (`{6 Position,7 Orientation}`) — which object type (teleporter? plain marker?).
7. **assetId in createData** — 0 for heroes AND enemies in the capture; confirm 0 is acceptable for all (the client may resolve assets from the noun).

## 4. C# divergence summary (→ DIVERGENCE_LEDGER D-009)
- ✅ Hero ObjectCreate/ObjectUpdate field sets aligned (`35a64f8`, golden-tested).
- ✅ ObjectivesInit → 7-byte/objective + per-tick ObjectiveUpdated (`dd7bb02`, golden-tested). Awaiting client-verify gate.
- 🔴 Level objects: C# spawns 3 (heroes) vs ~278 (full level) — needs level/markerset loader.
- 🔴 Companions (0x98 InteractableData, 0x99 AgentBlackboard, 0x94 Locomotion, 0x90/0x91) not emitted by C#.
