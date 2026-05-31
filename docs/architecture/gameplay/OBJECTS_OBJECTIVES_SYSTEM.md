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

## 3. Client consumption (Ghidra) — PENDING

Open questions for the client (`Darkspore.exe`, base 0x400000), under analysis:
- Crash `@0x00551f47` (`MOV ECX,[ESI+0x20]`, +0x20 = null GFx movie) reached from per-frame UI update `FUN_007ee9d0` — which HUD subview is it (deck `cPlayerDeck` @0x51b860, or objectives HUD)?
- What gates that subview's movie creation (+0x20) — a packet/event?
- The `OnGms` parse layout the client expects for `0x8C`/`0x8D`/`0xB7`/`0xB8` (resolves the 7- vs 56-byte ObjectivesInit question definitively).

_This section will be completed from the in-progress Ghidra analysis, then the confirmed fixes implemented (one commit each, golden-tested)._

---

## 4. C# divergence summary (→ DIVERGENCE_LEDGER D-009)
- ✅ Hero ObjectCreate/ObjectUpdate field sets aligned (`35a64f8`, golden-tested).
- 🔴 ObjectivesInit format: C# 56-byte/objective vs working-wire 7-byte/objective (pending client-parse confirm).
- 🔴 ObjectiveUpdated (0xB8): C# sends none; C++ sends ~1/tick.
- 🔴 Level objects: C# spawns 3 (heroes) vs ~278 (full level) — needs level/markerset loader.
- 🔴 Companions (0x98 InteractableData, 0x99 AgentBlackboard, 0x94 Locomotion, 0x90/0x91) not emitted by C#.
