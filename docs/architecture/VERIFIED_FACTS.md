# VERIFIED FACTS — the only trusted protocol truths

Every entry here is backed by **evidence**: a C++ `file:line` cite (tree root `C:\CodingProjects\Personal\ReCapCpp\darkspore_server\source`) and/or a wire capture frame. Nothing enters this file on the word of an old doc or memory. If a claim isn't here with a cite, treat it as **unverified** and confirm before relying on it.

Created 2026-05-31 as part of the Phase-0 hard reset (see `docs/superpowers/specs/2026-05-31-port-fidelity-plan-design.md`). Replaces the deleted dogma docs (`PARITY.md`, `ENDIANNESS.md`) and the CLAUDE.md FROZEN section.

---

## Wire encoding

- **`Write<T>` wrapper → little-endian on the wire.** The C++ `Write<T>(BitStream&, T)` wrapper (`RakNet/Types.h:218-226`) bswaps once, then `BitStream::Write<T>` byte-reverses again on an LE host — two swaps cancel, wire is **LE**. Verified 2026-05-28 by raw capture vs the working C++ server, re-confirmed 2026-05-31 (LPU/GameState/deploy all LE on the wire). Darkspore game packets are LE.
  - Corollary: the old "Write<T> = WriteBE" rule is **wrong** (see superseded list).
  - **Why the second swap actually happens (settles a recurring re-litigation):** `Server.h:20` *intends* `#define __BITSTREAM_NATIVE_END` (which would disable RakNet's swap and yield BE). But it is a **no-op due to header ordering**: `Server.cpp:3` includes `Server.h` first; `Server.h:9` includes `Types.h`; `Types.h:11` includes `<BitStream.h>` **before** the `#define` at `Server.h:20`. The include guard compiles BitStream.h's body once — at that first include the macro is undefined — so `DoEndianSwap()` is compiled to **TRUE** (RakNet swaps) and the later `#define` never takes effect. Net: the legacy double-swap path is what's compiled → **wire = LE**. A source-only read of `Server.h:20` looks like BE; the include order defeats it. Verified by reading `Server.cpp:3`, `Server.h:9/20`, `Types.h:11` directly (2026-05-31).
- **ReflectionSerializer bitmap sizes** (C# `Adapters/RakNet/ReflectionSerializer.cs`, matches C++): ≤8 fields → 1-byte bitmap; 9–16 → 2-byte bitmap; >16 → field-ID byte per field + `0xFF` terminator. Confirmed on wire: ObjectCreate createData (10 fields → 2-byte bitmap), Object/Character reflections (>16 → field-ID/0xFF). **CLIENT-CONFIRMED 2026-06-04 (the arbiter):** `ClientNet::ParseReflectionMessage` @0x00a22e80 (Darkspore.exe, found via "Tom Bui: malformed reflection message" assert @0x0102fdb8) — comparisons are `count < 9` → 1B bitmap, `count < 0x11` → 2B bitmap, else field-ID sequence terminated by sentinel `0xFF`; missing sentinel → assert + `return 0` (parse rejected, no hard crash in the parser itself). Handlers: `ClientNet::OnGmsObjectCreate` @0x0053f550, `ClientNet::OnGmsObjectUpdate` @0x0053dd00.
- **WriteTo vs WriteReflection:** nested class arrays inside a reflection block use raw fixed-size `WriteTo()`; top-level standalone blocks use `WriteReflection()`. Confirmed: Player reflection field 3 = 3× `Character::WriteTo` (raw 0x620).

## RakNet transport (RakNexus)

- **RakNexus delivers correctly.** Wire capture 2026-05-31: sequential datagram numbers, client ACKs every datagram, split-packet LPU reassembled, client acts (state transitions, deploys). The transport is NOT a source of gameplay bugs.
- Only framing divergence vs real RakNet: datagram flag byte `0x80` (C#) vs `0x84` (C++) — the `0x04` bit is RakNet's optional B&AS congestion-control values, which RakNexus omits. Harmless; valid datagram.

## Character block — `Character::WriteTo` (Game/Character.cpp:99)

Fixed **0x620** (1568) byte block, all fields via `Write<T>` → LE. Verified offsets (C# `LabsCharacterData.WriteTo` matches):
- `0x000–0x007`: zero (not written)
- `0x008`: u64 mAssetId · `0x010`: i32 mVersion
- `0x0B4`: u32 mNounId
- `0x0B8`: f32[] mPartAttributes, **base 0x0B8, stride 4** per AttributeType index (e.g. MaxHealth idx4 @0x0C8, AttackSpeedScale idx0x17 @0x114, MinWeaponDamage idx0x65 @0x258)
- `0x3B8`: u32 mCreatureType
- `0x3C0`: u64 mDeployCooldown · `0x3C8`: u32 mAbilityPoints · `0x3CC`: u32[9] ability ranks
- `0x3F0`: f32 Health, MaxHealth, Mana, MaxMana, GearScore, GearScoreFlattened
- Gaps contain whatever (C++ leaves uninitialized heap there — pointer garbage on the wire); the client only reads the fields it cares about, so gap content is irrelevant.

## LabsPlayerUpdate / dataBits

- **C++ ships 3 (default) Character blocks in the HelloPlayer LPU** via player dataBit 3 (set in `Player` ctor, `Player.cpp:64`); `Player::WriteReflection` field 3 emits 3× `Character::WriteTo`. The 3 `Character` objects exist from `std::array<Character,3>` construction (default/zero values; no squad yet at hello).
- **Adding dataBit 3 does NOT break chain vote.** Tested 2026-05-31: with bit 3 in `SetInitialDataBits`, hello LPU = 4818B with chars, and the client still sent `ChainPlayerMsgs(byteCount=6)` (the vote). Directly disproves the old FROZEN claim. (Current C# `SetInitialDataBits` = `{0,3,4,5,6,7,8,12,15,16,18,21,22}`.)

## Client crash @0x00551f47 — ✅ FIXED 2026-06-04 (ledger D-013, client-confirmed)

**Root cause: the RakNet `QuickGameMsgs` (0xC4) `reset` byte.** C# `QuickGameMsgsPacket` sent `(byte)0`; C++ `Server::SendQuickGame` (Server.cpp:2346) sends `Write<bool>(reset=true)` = `0x01` on the Dungeon path (Server.cpp:1199, after DirectorState, before OnPlayerStart). `reset` governs stay-in-dungeon (0x01) vs fall-back-to-ship/map-room state (0x00). C# `0x00` drove the retail client into the ship state, which activated `cMapRoomUI` (the ship/map-room UI) inside the dungeon; that UI's Scaleform movie (FE_MM2b) was never bound for the in-dungeon context → null `Movie_InvokeMethod` → ACCESS_VIOLATION @0x00551f47. Fix: send `(byte)1`. Open since 2026-05-30; dead now. The C++ source comment "if true: set state Spaceship" is the author's admitted guess and is misleading — empirically 0x01 keeps the client in the dungeon (no crash). Found by C++↔C# setup-phase source-diff after establishing the crash was insensitive to gameplay-packet content (survived the D-011 world cleanup and the D-012 game-type fix). The cMapRoomUI null-movie mechanism below was the correct symptom diagnosis.

### (historical) symptom mechanism — ROOT-CAUSED 2026-06-04 (live VEH debug + minidump)

The crash function is **`cMapRoomUI::Update` @0x0051b860**, NOT `cPlayerDeck::UpdateHud` (that was a 2026-05-31 mis-mapping; corrected in Ghidra). Evidence: `cMapRoomUI::Create` @0x0051cde0 allocs with tag **`SP_UI/cMapRoomUI`**; its Ctor @0x0051a970 writes vtable `PTR_FUN_00fe6120`; the crashing object (minidump esi=0x301cd0c8) carries exactly that vtable. The function still hosts the deck/squad **gearScore** HUD push loop, so the "deck HUD" role was right — the class name and offset were wrong.

- **Mechanism (definitive):** `cMapRoomUI::Update` runs every frame via the HUD dispatcher (FUN_00457700, sub-widgets at +0x1c ability bar / +0x20 cMapRoomUI / +0x24 cSpaceshipNavigation). When the **UI-mode** field `gameState(DAT_0143ffd8)+0x3244 == 3`, the activation gate `FUN_004e87b0` returns true → sets `cMapRoomUI+0x40 != -1` (active). Once active, the cooldown sub-path (ret 0x0051b9db) calls `FUN_004015b0(this+0x20)` → `Scaleform::Movie_InvokeMethod(*(this+0x28))`. The **Scaleform movie handle at `cMapRoomUI+0x28` is NULL** (Ctor leaves the +0x20..+0x28 GFx sub-struct zeroed; the SWF was never bound) → `mov ecx,[edi]` with edi=0 → ACCESS_VIOLATION read 0x0 @0x00551f47.
- **Runtime proof:** minidump 06-04 15.05.59 — eip=0x551f47, edi=0, esi=0x301cd0c8 (vtable 0x00fe6120), stack has 0x004015e1 (in FUN_004015b0) then 0x0051b9db (in cMapRoomUI::Update). Live VEH BP at 0x551f47 logged 405 healthy invokes (movie=0x4F7F6140) + 1 null = the fault.
- **NOT the deck character data / LPU / ObjectCreate** — those were exonerated separately; the crash is a client UI-movie lifecycle bug (UI activates before/without its movie SWF binding).
- **UI-mode + movie-bind trace (2026-06-04):** setter `FUN_004e91e0(mode)` writes gameState+0x3244 — callers set mode 4 (FUN_00457a10, from Blaze NotifyGameSetup handlers), 0 (logout), 1/2 (cMapRoomUI__HandleEvent arena/MM paths). Mode **3 = Arena/matchmaking-lobby only** (`ClientUI__IsInMatchmakingLobby` 0x004e87b0); no SP-path caller passes 3. Movie load: `cMapRoomUI::Create` (0x0051cde0) → Ctor (0x0051a970) → `cMapRoomUI__Init` (0x0051cc50) async-loads GFx **FE_MM2b**, callback `cMapRoomUI__OnMovieLoaded` (0x0051c360) binds +0x28.
- **⛔ REFUTED hypothesis (do not pursue):** "ReCap must serve FE_MM2b.swf." VERIFIED 2026-06-04: `Darkspore/Data/FlashUI/animations~/FE_MM2b.gfx` exists locally (104KB); the install has **0 .swf** (Scaleform UI ships as `.gfx`, loaded locally); the client never HTTP-fetches it (REST log). The null movie is NOT a missing/unserved asset.
- **✅ ROOT CAUSE FOUND 2026-06-04 (ledger D-012) — the RakNet GameState game-mode field.** C++ `Server::SendGameState` writes `Blaze::GameType::Chain` (=2) into the per-tick GameState packet (Server.cpp:1369 → client `simulator+0x3B420`, default 0xFFFFFFFF). C# hardcoded **`GameType = 0`** (Game.cs:66) — and **0 is not a valid `Blaze::GameType`** (Tutorial=1…DirectEntry=7, Types.h:158-166). The client dispatches its UI state machine off this game-mode; the invalid 0 sends it down the matchmaking/map-room path → cMapRoomUI activates (UI-mode 3) without its movie bound → the null-movie crash above. The same retail client doesn't crash under C++ purely because C++ sends Chain(2). FIX: `LabsGameType` enum + `Game.Update` sends `LabsGameType.Chain`. This is the **server behavioral diff** (client identical both sides); the empty-world/cubes (D-011) and the "serve FE_MM2b.swf" idea were both red herrings. Client-verify gate pending.

## SporelabsObject wire field table (client registrar, 2026-06-04)

Recovered from Darkspore.exe `AssetData::RegisterSporelabsObjectFields` @0x00f8ded0 (cross-checked vs C++ `Object::WriteReflection` Object.cpp:1763 / `Object::WriteTo` Object.cpp:1701, struct size 0x308). Full id→name (types/offsets in `memory/object-wire-field-table.md` + Ghidra plate comment):
`0 mTeam u8 · 1 mbPlayerControlled bool · 2 mInputSyncStamp u32 · 3 mPlayerIndex u8 · 4 mLinearVelocity vec3 · 5 mAngularVelocity vec3 · 6 mPosition vec3 · 7 mOrientation quat(16B) · 8 mScale f32 · 9 mMarkerScale f32 · 10 mLastAnimationState u32 · 11 mLastAnimationPlayTimeMs u64 · 12 mOverrideMoveIdleAnimationState u32 · 13 mGraphicsState u32 · 14 mGraphicsStateStartTimeMs u64 · 15 mNewGraphicsStateStartTimeMs u64 · 16 mVisible bool · 17 mbHasCollision bool · 18 mOwnerID u32 · 19 mMovementType u8 · 20 mDisableRepulsion bool · 21 mInteractableState u32 · 22 sourceMarkerKey.markerId u32`
- Wire-used field sets re-validated against this table: hero ObjectCreate {0,1,3,17}, marker/world object {6,7,8,17,22}, hero ObjectUpdate {6,16} — names/ids exact.
- **⚠ OPEN CONTRADICTION (verify before any sender uses fields 11/14/15):** client table + C++ Object.cpp say fields 11/14/15 are **u64 (8B)**; C# `SporelabsObject` declares them `uint` (4B) — commit 35a64f8 changed them ulong→uint citing "C++ u32", the opposite. No current fieldset sends 11/14/15, so it's latent, but any future sender must settle this first (capture or client parse single-step). A 4B write where the client reads 8B desyncs every later field in the block.
- Teleporter component (`AssetData::RegisterTeleporterDefFields` @0x00f97bc0): `0 destinationMarkerId u32 · 1 triggerVolume (nullable TriggerVolumeDef) · 2 deferTriggerCreation bool`. `destinationMarkerId` links to `cLabsMarker.markerId` (registrar @0x00f8b3c0, 38 fields).

## ActionCommand contract (client ground-truth, Ghidra 2026-06-05)

Client sender: `ClientNet::SendActionCommandMsgs` @0x0053be60 (single builder, validates type<14, dispatches to wire + local `GameSimulator::OnActionCommandMsgs` @0x009c5b40). Payload sizes from `ActionCommand::GetPayloadSize` @0x00a1ca80.

- **Common header 40B** (FillCommandHeader @0x004e2150 + FUN_004e2970): `u8 type · u8 commandStamp · u16 pad · u32 locomotionFlags (hero+0x58; C++ Types.h:831 misnames it inputSyncStamp) · u32 objectId (SendActionCommandMsgs overwrites from locomotion+0x2a8+8; observed = hero object id on wire) · vec3 pos · quat orient`.
- **commandStamp (wire +0x01)** = rolling u8 counter `DAT_011205e0`, written for enqueue-path commands (5/6/9/11/12/13 via `EnqueueActionCommand` @0x004e2f40 → block `DAT_0143fde0+2` → wire byte [1] in FUN_004e2970). Movement (3/4) carries NO stamp (EnqueueMoveCommand @0x004e2e40 leaves it 0).
- **Type table (client emits):** 3 Movement(24B) · 4 StopMovement(24B) · 5 SwitchCharacter(4B) · **6 Overdrive(1B u8 heroSlot, emit @0x004ebb50 — absent from the C++ ref)** · 7/8 Use{Character,Squad}Ability(44B, C++ Types.h:855: u32 targetId, vec3 cursorPos, vec3 targetPos, u32 index, i32 rank, u32 unk, u32 userData; emit @0x004d8a80, type = 8-(slot<5)) · 9 CatalystPickup(20B, C++ Types.h:847: u32 objectId, vec3 pos, u32 rank — client writes 0xFF low byte; emit @0x0044ece0) · 10 Cancel(24B movement struct, internal sim path FUN_009c2260) · 11 UseInteractableObject(4B u32 objectId) · 12 Dance / 13 Taunt (0B payload; `/dance` `/taunt` chat, emits @0x0040cdd5/0x0040d0ed). **Types 1/2 exist in GetPayloadSize but have NO emit path — inert, do not implement.**
- **Pending-command lock (the dance-freeze mechanism, found 2026-06-05):** enqueue-path commands route through `FUN_004d9150` (route=1, server-deferred) → lock block `DAT_0143ffd8+0x50`: `+4`=pending ptr, `+8`=stamp, `+0x10`=deadline now+3000ms. While locked, `PlayerCtrl::ValidatePendingCommand` @0x004e25b0 (via FUN_004e1cd0/FUN_004d8910) **hard-rejects movement clicks (0xffffd8f2) — the client sends NOTHING** until the deadline auto-expires (clear + anim reset @0x004d8932) or the server clears it via ActionCommandResponse.
- **ActionCommandResponse 0xA8 (56B body, exact):** handler `ClientNet::OnGmsActionCommandResponse` @0x0053cb10 reads exactly 0x38B → `FUN_004d9ba0` switches on byte[1]: 1=ability ack, **2=complete (clears lock when byte[0] echoes the commandStamp: `*param_1 == block[8]`; also resets hero anim via FUN_004f7980(…,0,…) and clears the pending flag via FUN_004e2030)**, 4=stop ack (secondary block +0x18 only), 8=movement GO/promote, 0x10=interactable clear. Layout: `[0] u8 stampEcho · [1] u8 responseType · [2-3] pad · [4] u32 objectId · [8] u32 · [0xC] u32 pad · [0x10/0x18/0x20/0x28] u64×4 times · [0x30] u32 pad · [0x34] u32 userData (≥0 writes client DAT_0143fe3c; 0xFFFFFFFF skips)`. C++ writer shape agrees (Server.cpp:1659-1682). **Server contract for emotes: send response type=2 (stamp echo) FIRST, then SetAnimationState — case 2's anim reset would otherwise kill the emote anim.**
- **Emote anims loop forever:** `emote_dance_all`/`emote_taunt_all` have anim non-loop flag +0x70 = 0 (`FUN_004ddde0`) — they never self-terminate. Retail-like cancel = server sends SetAnimationState(state=0) when the player issues the next move/stop/switch/cancel command.
- **SetAnimationState 0xA5 (25B)** handler @0x0053efe0: the repeated trailing u32 state is a **network-player-ID guard** (compared vs playerCtrlBlock+0x1c), not client data; non-overlay writes obj fields 0xac/0xb0 (state) + 0xb8 (converted timestamp); overlay=1 uses a separate overlay anim slot (FUN_004f79f0), skipping the object field writes.

## ServerEvent 0x9B contract (client ground-truth, Ghidra 2026-06-05)

Handler `ClientNet::OnGmsServerEvent` @0x0053ec80; registrar `AssetData::RegisterServerEventFields` @0x00f60ea0 (struct 0x98). 26-field reflection → sequence mode (field-ID + payload, 0xFF sentinel). The C++ ref sender (Server.cpp:1899) is debug-disabled and was never validated — these facts come from the client parse.

- **Field table:** `0 simpleSwarmEffectID u32 · 1 objectFxIndex u8 · 2 bRemove · 3 bHardStop · 4 bForceAttach · 5 bCritical · 6 asset u32 (FNV of "name.ServerEventDef") · 7 objectId u32 · 8 secondaryObjectId u32 · 9 attackerId u32 · 10 position vec3 · 11 facing vec3 · 12 orientation quat · 13 targetPoint vec3 · 14 textValue i32 · 15 clientEventID u32 · 16 clientIgnoreFlags u8 · 17-25 loot descriptor (lootReferenceId u64, lootInstanceId u64, rigblock/suffix/prefix1/prefix2 u32, itemLevel i32, rarity i32, creationTime u64)`. C++'s "14-15/17-25 unused" comment is WRONG.
- **Dispatch is mutually exclusive on field 15:** clientEventID==0 → FX path (`ClientNet::PlayServerEventEffect` @0x0050a970); !=0 → UI dispatcher (`ClientUI::DispatchClientEvent` @0x004e4c90). FX + UI together = two packets.
- **FX recipes:** at-position = {6, 10}; attached = {6, 7} (+{1 slot 1-16, 4 forceAttach} for tracked creature slots); stop = {7, 1, 2} (+3 hardStop; asset not required). Unresolved asset hash = silent skip (no crash). Visibility filter `GetAsset::ServerEventDef_` @0x004e33b0 (team vs ShowPickups* cvars).
- **clientEventID values** (UI events, dispatcher @0x004e4c90): PlayerEnteredTunnel 0x8D5AB239, TeleportersDeactivated 0x57CCFCE5, hero-enters-portal 0x6F8812D2, hero-exits-portal 0x414B80B8, etc. (full enum in C++ ServerEvent.h:22-66, hashes confirmed in the dispatcher switch).
- **Data-driven teleport FX:** marker `componentData.teleporter.triggerVolume.events.onEnterEvent/onExitEvent` are .ServerEventDef keys — the server plays them via 0x9B (ReCap: Game.RegisterTeleporterTrigger/CheckTeleporterTriggers).

## Client object visual pipeline + render gates (Ghidra 2026-06-05, D-024 open)

`ObjectManager::UpdateObjectGraphics` @0x009ec530 picks the visual path: **creature** (obj+0x298 locomotion != 0 && obj+0x61 == 0 → graphics component obj+0x2C0 via `Render::CreateCreatureGraphicsComponent` @0x00a19200) vs **interactable/prop** (→ obstacle+render handle obj+0x2BC via `Render::SubmitNavObstacle` @0x00a11760 → `Render::SubmitAndAssign` @0x00a261f0).
- **Interactable render gates** (@0x009ec597-af, all required, silent skip otherwise): `noun+5 isFixed == true` · `noun+0xB8 physicsType != 0` · `obj+0x60 hasCollision == true`.
- Obelisk nouns (probe vs AssetData_Binary, tools/scratch/NounProbe): `isFixed=True`, `physicsType` enum (value not extracted), `modelKey = "prefab_*_obelisk_gfx.Markerset"` — the obelisk visual is a **gfx markerset composition**, a third pipeline distinct from creature (npcClassData) and bmdl prop. SecurityTeleporter `modelKey = "Shared!teleporter_level.bmdl"` — the visible in-game portal IS the spawned trigger object rendering its own model (client-confirmed: portal visible, scale 0 notwithstanding).
- Obelisk invisibility (D-024) remains UNRESOLVED: both wire shapes (bare {6,7} and marker-bound {6,7,8,17,22}) verified non-rendering at point-blank; gates above are the candidates (physicsType value / hasCollision routing / gfx-markerset modelKey resolution). Decisive next = runtime BP at 0x009ec530 with a live obelisk create.

## Client markerset architecture (2026-06-04)

- **Client loads level markersets locally** from its own asset data (`ClientLevel::BuildMarkersetHashInfo` @0x004ed250 reads the in-memory markerset vector, publishes `LABS_LEVEL_MARKERSET_INFO`/`LABS_local_MARKERSET_HASH` as script vars). No RakNet packet carries markerset data.
- **Teleporter destinations resolve client-side**: Lua API `nGameObject::GetTeleporterDestination` + `GetMarkerID` (registrar @0x00a08bc0) — the client looks up `destinationMarkerId` in its OWN loaded markerset. The server only needs the trigger object (SecurityTeleporter.noun, scale 0); no destination data on the wire.
- `LABS_LEVEL_MARKERSET_INFO` is also accepted via the HTTP startup-options pipe (`ClientStartup::HandleStartupMessage` @0x00c2d5b0) — startup-time init channel, NOT a RakNet gameplay packet.
- Domain split confirmed client-side: asset schema refs use bare names (no extension), wire/script payloads use names WITH extension — matches the server-side rule (DBPF `InstanceId = FNV(bare)`, wire id = FNV(name+ext), one FNV-1 multiply-then-xor lowercase fn everywhere).

## Capture build ≠ source tree (CRITICAL methodology note, 2026-05-31)

The working C++ server is a **prebuilt binary** (`…/Darkspore/DarksporeBin/Server/`) that does NOT necessarily match the `ReCapCpp` **source tree** — they have drifted. Proven by the objectives packet: the `cpp_loopback` capture's `ObjectivesInitForLevel` (0xB7) is **37 bytes** = `u8 count(5) + 5×(u32 id + u24 value)` (**7 bytes/objective**), whereas `ReCapCpp` source `Objective::WriteTo` (Types.cpp:1335) writes **56 bytes/objective** (id + u32 value + 0x30 description). The 5 capture ids match the FNV-1 hashes of the 5 hardcoded objective names exactly (FinishLevelQuickly=0xFF9733EE, DoDamageOften=0xAC4273F3, TouchAllObelisks=0x61C07561, DefeatAllMonsters=0xA28485CC, HugeDamage=0x0478FACB), so it IS the objectives packet — just a different wire format.
- **Consequence:** for wire-format fidelity, the **capture is the ground truth**, not the source tree (the binary that actually drives the client is what matters). The source is a guide that may have regressed. Formats that did NOT drift (Character/LPU block, hero ObjectCreate {0,1,3,17}) matched both; objectives drifted.
- **C# bug (high crash suspicion):** C# `ObjectiveData.WriteTo` emits 56-byte entries (id + u32 value + 48-byte description) → 282B ObjectivesInit, but the client (per the working capture) expects **7-byte entries (u32 id + u24 value)** → 37B. The 282B desyncs the client's per-objective read. Pending exact client-parse confirmation (Ghidra OnGms 0xB7 handler).

## Lua registrar boot group order (Ghidra 2026-06-05)

`LuaSystem::Initialize` @0x00a0c810 iterates a 10-element `uint local_34[10]` stack array
(indices 0→9) and issues one `AssetCatalog::GetInstance` vtable query per group to collect
all Lua scripts for that group before executing them.

Exact iteration order (array index → hash):
`[0] 0x3681d755=lua · [1] 0xda09176b=UNRESOLVED · [2] 0xfc0ff8f5=modifiers ·
[3] 0xd2fcb262=UNRESOLVED · [4] 0x7153bbb1=abilities · [5] 0xd79fa88c=UNRESOLVED ·
[6] 0xc130a42a=behaviors · [7] 0xb2a79c5c=UNRESOLVED · [8] 0xee84d09a=UNRESOLVED ·
[9] 0x24f78aa1=UNRESOLVED`

Hashes 0/2/4/6 resolved via FNV (multiply-then-xor, same fn as `WireHash.cs::Fnv1a`).
Hashes 1/3/5/7/8/9 remain unresolved — not found in any binary string or candidate list;
require the original Darkspore Lua asset package filenames to crack.

Full per-namespace function tables in `docs/architecture/research/LUA_REGISTRAR_TABLES.md`.

## Lua ID representation: lua_pushnumber (float32), round-tripped (Ghidra 2026-06-05)

`nUtil::SPID` @0x009f9e00 and `nUtil::ToGUID` @0x009f9e40 both call **`lua_pushnumber`**.
In this client `lua_Number = float` (32-bit IEEE 754, 24-bit mantissa) — verified via
bytecode-header check @0x00909cb0 (`sizeof(lua_Number) == 4`) and all 1,042
ServerData.package chunks carrying chunk header byte 10 = `0x04`.
Consumer `ObjectManager::GetObjectFromLuaArg` @0x009f9740 reads back with `lua_type==3`
(LUA_TNUMBER) and `ROUND(float10)` to recover the integer.

- **uint32 values > 2^24 DO truncate** in the low bits when passing through a Lua number.
  Runtime object IDs (small sequential ints) are exact in float; full 32-bit hashes truncate
  identically on client and server — retail scripts were written against this behavior.
- **64-bit GUIDs**: `ToGUID` parses a hex STRING input → the GUID flow avoids the float
  problem entirely; do not push GUIDs as lua_Number.

C# binding contract (bug-compatible — replicate exactly, do NOT fix the truncation):
`PushId(uint id)` → `lua_pushnumber(L, (float)id)`.
`ReadId()` → `(uint)Math.Round((double)lua_tonumber(L, n))`. For GUID (uint64): pass as
hex string, not as a number.

## Lua require: hex-prefix group form (2026-06-05, P0 gate)

Retail ServerData.package Lua chunks use **hex uint group prefixes** in `require` strings — e.g.
`require("0x3681d755!GlobalDefinitions.lua")` — where the group slot is the raw FNV-1 hash as a
`0x`-prefixed hex literal, not a human-readable group name.

- **Root cause discovery:** executing 1,017 real ServerData chunks at the P0 gate produced 615
  cascade failures; all traced to `require` strings with hex prefixes that ScriptVfs was not
  parsing. Fixed in `ReCap.Server/Adapters/Scripting/ScriptVfs.cs` (`ParseReference`).
- **Unit test:** `ParsesHexPrefixedGroupForm` (vector test in `ReCap.Tests`).
- **Cross-reference:** boot group hash `0x3681d755` = FNV-1("lua") — same hash used as the boot
  group key in `LuaSystem::Initialize` @0x00a0c810 (see "Lua registrar boot group order" entry above).
- **Contract:** `ScriptVfs.ParseReference(str)` must handle both `"GroupName!File.lua"` (named) and
  `"0xHEXUINT!File.lua"` (hex-prefix) as equivalent references to the same group slot.

## Lua scripting — marshalling contracts (Ghidra 2026-06-05/06, P3)

### C1 — Object identity: lua_Number float IDs, round-trip via ROUND()

Scripts hold game objects as **uint32 IDs pushed as `lua_Number` (float)**. No userdata or tables.
Read pattern from `ObjectManager::GetObjectFromLuaArg` @0x009f9740: if `lua_type == LUA_TNUMBER (3)` →
`(uint)Math.Round((double)lua_tonumber(...))`. C# binding: `PushId(uint id)` → `lua_pushnumber(L, (float)id)`;
`ReadId()` → `(uint)Math.Round((double)lua_tonumber(L, n))`. Bug-compatible — do NOT fix float truncation
for values > 2^24; retail scripts were written against it (see "Lua ID representation" entry above).

### C2 — Position returns: 3 separate floats, error on missing object

`nGameObject.GetPosition(id)` returns **3 separate floats x, y, z** on the Lua stack (3 return values).
Client implementation @0x009fb870 pushes 3 numbers via `lua_pushnumber`; on missing object it raises a
Lua error `"Could not find object!"` (NOT a 0,0,0 fallback). Do NOT return a vec3 table or userdata —
dalkon's vec3 usertype was his approximation, not the retail contract.

### C3 — Return arities (client-decompiled; MUST match — scripts consume return counts)

Arities verified from the per-native client decompiles at the registrar-table addresses in
`docs/architecture/research/LUA_REGISTRAR_TABLES.md` (nAbility @0x00a435c0, nModifier second block,
nGameObject @0x00a08bc0, nBit @0x009fa3b0, nUtil @0x00a02220).

| Native | Returns |
|---|---|
| `n*.Register{Ability,Modifier,Affix,Condition,Objective}` | **0** |
| `nAbility.PreloadAsset` | **1** number (opaque handle) |
| `nAbility.PreloadAnimation` | **1** number (resolved hash/id) |
| `nAbility.PreloadModifier` | **1** number (echoes objectId arg) |
| `nBit.Or / And / Xor / Not / LShift / RShift` | **1** number (fold op on rounded-uint args) |
| `nUtil.GetAsset` | **1** number (opaque handle; C# uses FNV(name) as stable equivalent) |
| `nThread.Sleep / WaitForever` | yields **0** values |
| `nThread.WaitForXSeconds` | yields **0** values |
| `nThread.WakeUp` | **0** |
| `nThread.CreateThreadForObject` | **0** |
| `nGameObject.GetHitPoints / GetMaxHitPoints` | **1** float (0.0 if object missing) |
| `nGameObject.IsAlive` | **1** bool (hp > 0; false if missing) |
| `nGameObject.GetTeam` | **1** number on hit; **0 values** if object missing |
| `nGameObject.GetTargetID` | **1** number (0 if missing) |
| `nAbility.GetAgentID / GetTargetID` | **1** number from running invocation context |

### C4 — Register* semantics: FNV key, skip-if-exists, 0 returns

Client `RegisterAbility` @0x00a43040; same template: `RegisterCondition` @0x00a430e0,
`RegisterAffix` @0x00a0c570, `RegisterObjective` @0x00a0c340. **`nModifier.RegisterModifier` is the
same native as `RegisterAbility`** — same function pointer, same code path. Semantics:
FNV(name) → per-kind registry; if key exists → skip silently; else store; return 0 values.
Server implementation: keep a `luaL_ref` to the props table + name + hash.

### C5 — CORRECTION: SimulatorControl @0x008f6e40 = lua_gc controller, NOT coroutine scheduler

`FUN_008f6e40` (previously labeled "SimulatorControl" during the 2026-06-05 audit; renamed
`LuaManager::ControlGc` in Ghidra 2026-06-06) is a **`lua_gc` controller** — it maps integer inputs
to `LUA_GCSTOP`, `LUA_GCRESTART`, `LUA_GCCOLLECT`, `LUA_GCCOUNT`, `LUA_GCCOUNTB`, `LUA_GCSTEP`,
`LUA_GCSETPAUSE`, `LUA_GCSETSTEPMUL` and calls `lua_gc(L, cmd, data)`. It is NOT the coroutine
scheduler. Supersedes the "SimulatorControl = scheduler" audit label from 2026-06-05.

**SECOND CORRECTION (2026-06-06, decompile + caller-chain verified):** the earlier claim here that
"the real coroutine stepper is at `FUN_00902700` / `FUN_00902160`" was ALSO wrong — those are
**lua 5.1.4 lgc.c internals**, not a scheduler: `0x00902700` = `atomic()` (gray/grayagain/weak
lists at g+0x24/+0x28/+0x2C, white-bit flip `^3`), `0x00902160` = `propagatemark()` (switch on GC
tag 5/6/8/9, returns traversed size). Full chain renamed in Ghidra: `Lua51::GcStep` @0x00902980
(`luaC_step`, called from luaC_checkGC sites incl. the VM), `Lua51::GcSingleStep` @0x00902880,
`Lua51::GcFullCollect` @0x00902a00 (sole caller = `LuaManager::ControlGc`), `Lua51::GcAtomic`,
`Lua51::GcPropagateMark`. **The retail cLuaThread resume loop (real scheduler) remains UNLOCATED**
in the client binary. ReCap's `LuaCoroutineScheduler` was built from the nThread yield/wake
semantics (verified per-native) and passed the 25-ability gate, so no C# behavior depends on the
mislocated address — but any future claim about "the client scheduler" must locate it first.

### C6 — Tick-call contract: always 7 args (self, agentId, targetId, cx, cy, cz, rank)

Evidence in `docs/architecture/research/LUA_ABILITY_TICK_CONTRACT.md` (investigated 2026-06-06).
Gate results: 25 retail abilities invoked → 25 completed (9 clean / 16 errored on still-stubbed natives).
Boot registry at gate: Ability=477, Modifier=501, Affix=11, Condition=53, Objective=13 (total 1,055);
tick-capable: 283 abilities + 99 modifiers.

Always call `tick(self, agentId, targetId, cursorX, cursorY, cursorZ, rank)` — 7 total args, no
`numparams` branching. 1-param self-only ticks (e.g. `template_ability_firstaggro`) discard extras;
getter-style scripts read from the per-thread invocation slot. Single call form satisfies all
families.

**CORRECTION (2026-06-06, luac disasm of template_ability_projectile):** the earlier "9-param
positional projectile tick" claim was a misread — the 9-param function (`<?:179,325>`, layout
`(agentId, targetId, ?, SELF, dirX, dirY, dirZ, rank, ?)` with self at POSITION 4) is an internal
launch helper, NOT the registered tick. The actual `tick` (CLOSURE 16, `<?:403,558>`, 2 params) is
fully getter-style: `nAbility.GetAgentID()/GetAgentAttributeSnapshot()/GetTargetID()/
GetTargetPosition()`. The 7-arg dispatch is correct for every family observed. The historical 16
gate errors were missing-native errors (stub 0-return arity), not arg-layout errors.

Stub-errored natives (P4 backlog): `PayCooldownAndMana`, `GetAgentAttributeSnapshot`,
`PlayAnimationSequence`, `GetAbilityInstanceID`, `TargetInRangeAtStart`, `GetAnimationSequenceIndex`,
`nAttribute.GetAttributeValue`, `nEvent.Notify`, `nDebug.IsAbilityDebugEnabled`.

### C3b — Combat-native contracts (Ghidra-decompiled 2026-06-06; arity = hard contract)

| Native | Addr | Args | Returns | Semantics |
|---|---|---|---|---|
| `nAbility.GetAbilityInstanceID` | 0x00a403e0 | 0 | 1 num | per-cast instance id (`*ctx[0]`), 0 ok |
| `nAbility.GetAgentAttributeSnapshot` | 0x00a417a0 | 0-1 (opt agent id) | 1 num | OPAQUE snapshot handle (`**agent[0x38]`), consumed by `_FromSnapshot` |
| `nAbility.PayCooldownAndMana` | 0x00a43470 | 0 | **0** | no success bool; mana clamps to 0; stamps cooldown + net event |
| `nAbility.TargetInRangeAtStart` | 0x00a410a0 | 0-1 | 1 bool | cached at-cast flag (`ctx[0x28]`), NOT live range test |
| `nAbility.GetAnimationSequenceIndex` | 0x00a41ce0 | 0 | 1 num | reads `ctx[0x164]` (written by PlayAnimationSequence) |
| `nAbility.PlayAnimationSequence` | 0x00a41d20 | 0 | **0** | network anim broadcast (GetWarmupData_New + event queue) |
| `nAttribute.GetAttributeValue` | 0x009feca0 | 2 (objId, attrId int) | 1 float | attr array `comp+0x26c+id*4`, modifier chain + tuning caps; ids 0=Strength 1=Dexterity 2=Mind 4=MaxHealth CONFIRMED (domain 0..0x73, rest unmapped) |
| `nAttribute.GetAttributeValue_FromSnapshot` | 0x009fede0 | 2 (handle, attrId) | 1 float | raw frozen read `snap+0x1c+id*4`, 0 on invalid |
| `nEvent.Notify` | 0x00a0c1d0 | 1 table | **0** | named fields → FNV → event struct (type hash 0x8619ff24) → C++ event system + net serializer |
| `nDebug.IsAbilityDebugEnabled` | 0x009f97b0 | 0 | 1 bool | hardcoded false |
| `nGameObject.ValidateHostileTarget` | 0x009fd3c0 | 2-3 (src, target, [allowDead]) | 1 bool | hostile validity |
| `nGameObject.ValidateFriendlyTarget` | 0x009fd460 | 2-3 | 1 bool | friendly variant |
| `nGameObject.GetCenterPoint` | 0x009fb7f0 | 1 | 3 floats | collision-volume center; lua error on missing |
| `nGameObject.GetOrientation` | 0x00a02bb0 | 1 | 4 floats | quaternion XYZW from obj+0x24..0x30 |
| `nGameObject.GetFacing` | 0x009fb9a0 | 1 | 3 floats | forward vector derived from orientation |
| `nGameObject.GetFootprintRadius` | 0x009fbb20 | 1 | 1 float | noun physics footprint; 0 on null |
| `nGameObject.SetAnimationState` | 0x009fc000 | 2 (id, state str→FNV \| num) | **0** | sets obj+0xAC + timestamp, NETWORK broadcast (SporeNet msg) |
| `nObjectManager.IsValidObject` | 0x009fb160 | 1 | 1 bool | pure existence check |
| `nMathUtil.TransformVector` | 0x00a06bb0 | 7 (v3 + quat xyzw) | 3 floats | rotate vector by quaternion |
| `nThread.WaitUntilTime` | 0x00a02280 | 1-3 (simTime, [obj], [bool]) | yield | waits until absolute SIM seconds (cast-speed/overdrive scaling via opt args) |

`nAbility.GetRankedValue` is NOT a client native — `global.lua` (pre-boot chunk 0x57572DAC)
defines it in Lua (alongside GetRankedValueWithRank/GetRankedValueNPC/GetManaCost/GetDuration/
GetRankedValueHelper), so it exists after boot with zero server work.

Gate progression with these implemented server-side (arity-exact): 25 retail abilities →
**17 clean / 8 errored** (was 9/16). `nTimeManager.GetSimTimeDt` (0.05 fixed) was the single
biggest unblocker. Remaining tier-3 frontier (next phase): `nObjectManager.GetObjectsInRadius` /
`CreateObject` (server-side projectile spawn!), `nGameObject.SetIsVisible/SetStealthType/
SetAttributeSnapshot/HealDamage/MarkForDelete`, `nThreadData.GetPrivateTable/SetGUID`,
`nThread.WaitForHitpointsAbove/WaitForFadeOutInXSeconds`.

### C7 — ActionCommand ability `index` = PlayerClass slot; type byte = 8 - (slot < 5)

Client `BuildAbilityCommandStruct` @0x004d8a80 writes `type = 8 - (slot < 5)` — slots 0-4 emit
type 7 (UseCharacterAbility), slots ≥5 emit type 8 (UseSquadAbility). Slot resolver
@0x009c7180 maps the index to the CURRENT hero's PlayerClass asset fields:
`[0]=basicAbility [1]=specialAbility1 [2]=specialAbility2 [3]=specialAbility3 [4]=passiveAbility`
(AssetData::PlayerClass registrar field order). Squad slots 6/7/8 recurse into the squad
character (client charIdx 4) sub-slots 0/1/2. `rank` = per-character per-slot rank array at
PlayerCtrl+0x3e4 (via @0x009c7530).

The PlayerClass field value is the **bare ability name** (e.g. `"LightningRogueBasic"`); its FNV
hash is the RegisterAbility registry key — same lookup the client does via the ability registry
getter @0x009dfb40. Package-verified server-side: 118 hero nouns resolve 5 slots; 114/118
basicAbility hashes land in the boot-populated registry (the 4 misses are retail-missing chunks);
100 are tick-capable (`AbilitySlotResolutionTests`).

dalkon's C++ slot switch (Player.cpp:104: 0→0, 2→1, 3→3) does NOT match the client mapping —
his JSON-db ability ids are a parallel domain; ReCap resolves from the PlayerClass asset instead
(`AssetDatabase.ResolveAbilitySlots`).

---

## To re-verify before trusting (carried over, NOT yet confirmed this cycle)

These were asserted by old docs; keep until verified, then move up with a cite or kill:
- `ChainVoteMsgs` 0x151 buffer = LE (consistent with the Write<T>=LE fact, but not independently re-captured this cycle).
- `HelloPlayer` (0x80) body = 8 bytes `u8 type, u8 gameplayIndex, u32 IPv4, u16 Port` (claimed via Ghidra `FUN_00a93d50`).
- `GameType = 0` in the update loop; `LabsPlayerData.DataSetup = false`.
- Wire state codes: Spaceship 0x02, PreDungeon 0x05, Dungeon 0x06, ChainVoting 0x0B, ChainCashOut 0x0C.

---

## Superseded "laws" changelog

Claims removed in the Phase-0 reset, with the evidence that killed or demoted them:

| Old "law" | Source | Verdict | Evidence |
|---|---|---|---|
| `Write<T>` wrapper = **Big-Endian** ("most game data BE") | CLAUDE.md, PORTING_PLAN #5, ENDIANNESS.md (pre-correction) | **WRONG → LE** | Raw capture vs working C++ server (2026-05-28); LPU/GameState/deploy all LE on wire (2026-05-31). |
| `SetInitialDataBits` MUST be 12 bits; adding `{3,13,14,17}` breaks chain vote — "FROZEN, never change" | CLAUDE.md FROZEN, PARITY.md | **DISPROVEN for bit 3** | Added bit 3 → vote still fired (`ChainPlayerMsgs` byteCount=6), hello LPU 4818B (2026-05-31). Was likely a malformed-Character-block artifact, since fixed. (13/14/17 not yet tested.) |
| "Follow C++ reference religiously" / "Frozen rules inviolable" | CLAUDE.md, PORTING_PLAN #3 | **Demoted** | Replaced by evidence-over-dogma + "match the client's required contract, not C++ byte-garbage". |

When a future finding kills another old claim, add a row here.
