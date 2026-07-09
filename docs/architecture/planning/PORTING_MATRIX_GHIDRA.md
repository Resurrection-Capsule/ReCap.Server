# Porting Matrix — Ghidra floor (client contract as baseline)

Companion to [`PORTING_MATRIX.md`](./PORTING_MATRIX.md). Same goal (what is ported / partial /
missing) but **re-anchored on the retail client**: every row's *floor* is the client's own code
in Ghidra (`Darkspore.exe`, image base `0x00400000`), cited by address — **not** dalkon's C++,
which is being abandoned (errors + hardcoded fixtures). See CLAUDE.md and
[`../http/CLIENT_REST_FLOW.md`](../http/CLIENT_REST_FLOW.md).

> **Compiled 2026-07-08 by live Ghidra pull** (44 `OnGms*` handlers + 31 REST builders/parsers +
> struct registrars confirmed against the loaded program) plus a 3-agent harvest of the existing
> Ghidra-sourced docs. C# status columns are from `PORTING_MATRIX.md` (2026-07-08 refresh) +
> `COMMAND_MATRIX.md` + `VERIFIED_FACTS.md`.

## Why this doc

`PORTING_MATRIX.md` is organized **by C++ class** (`C++ handler / file:line`) and measures C# against
C++. That inverts the source of truth: C++ is a reverse-engineered approximation. This matrix flips
it — the **client's receive/send surface + REST + Blaze messages + the exact wire structs it parses**
are the floor; server-internal modules (AI, OctTree, Collision) are *means to fill that surface*, not
floor rows. Where the client floor is not yet mapped in Ghidra, we **cannot** measure C# — that is a
**sweep target**, never a licence to "reach C++ parity".

## Floor grades

| Grade | Meaning |
|---|---|
| 🟢 **Ghidra-floored** | Client fn/registrar **address confirmed in the loaded program**; C# is measurable against it |
| 🟡 **Partial floor** | Client dispatch/emit fn known, but the field-map is only C++-source — needs a field sweep |
| 🔴 **No client floor** | Only C++-source / wire-log inference — a Ghidra sweep must precede any parity claim |

## C# status legend

✅ ported · ⚠️ partial/diverges · ❌ missing · — not wired/unsent

---

## §1 — RakNet receive surface (S→C) 🟢

What the client can render = every message with an `OnGms*` handler. **Floor = the handler address**
(the client's own parse). Wire opcode ↔ name from the position-verified kGms table (`0x0118b488`,
pairs `{char* name @0x01036410+, u32 id}`); handler addresses **live-confirmed** (44 named
`ClientNet::OnGms*`). C# = does the server send it, correctly.

**In-game dispatcher** (`cSporeOnline_Common` functor @`0x0053fbb0`, switch on id−6, 40 cases):

| Wire | Message | Client handler (floor) | C# send | Note |
|---|---|---|---|---|
| 0x85 | PartyMergeComplete | `0x004e8790` | ✅ | triggers client DebugPing |
| 0x87 | VoteKickStarted | `0x0053cd30` | — | |
| 0x8A | GameState | `0x0053ca60` | ✅ | per-tick broadcast |
| 0x8B | DirectorState | `0x0053dcd0` | ✅ | reflection_serializer<7> — **schema-driven from AssetData.Parser cAIDirector** (was wrong raw-blob); golden-tested |
| 0x8C | ObjectCreate | `0x0053f550` | ✅ | markers+hero subset only |
| 0x8D | ObjectUpdate | `0x0053dd00` | ✅ | |
| 0x8E | ObjectDelete | `0x0053ddc0` | ✅ | D-025; flat u32[] verified |
| 0x8F | ObjectJump | `0x0053dee0` | — | |
| 0x90 | ObjectTeleport | `0x0053e0e0` | ✅ | Phase 2b; UNGATED |
| 0x91 | ObjectPlayerMove | `0x0053e1c0` | ✅ | local-hero gate (movement reply) |
| 0x92 | ForcePhysicsUpdate | `0x0053e380` | — | 40B layout known |
| 0x93 | PhysicsChanged | `0x0053e490` | — | |
| 0x94 | LocomotionDataUpdate | `0x0053e6f0` | ✅ | reflection-encoded |
| 0x95 | LocomotionDataUnreliableUpdate | `0x0053e600` | ✅ | UNGATED smooth-move channel |
| 0x96 | AttributeDataUpdate | `0x0053e7c0` | ✅ | |
| 0x97 | CombatantDataUpdate | `0x0053e890` | ✅ | HP/MP reflection |
| 0x98 | InteractableDataUpdate | `0x0053e960` | ⚠️ | dispatched, DBG-gated |
| 0x99 | AgentBlackboardUpdate | `0x0053ea30` | — | AI feedback |
| 0x9A | LootDataUpdate | `0x0053eb00` | — | |
| 0x9B | ServerEvent | `0x0053ec80` | ✅ | 26-field client-verified |
| 0xA1 | LabsPlayerUpdate | `0x0053ebf0` | ✅ | player+chars+catalysts |
| 0xA2 | ModifierCreated | `0x0053edd0` | — | **FIXED 37B struct** (u32 objId + 33B), not reflection; needs buff system |
| 0xA3 | ModifierUpdated | `0x0053ee80` | — | **FIXED 21B struct** (u32 objId + 17B) |
| 0xA4 | ModifierDeleted | `0x0053ef30` | — | **FIXED 8B** (u32 objId + u32 modId) |
| 0xA5 | SetAnimationState | `0x0053efe0` | ✅ | 25B |
| 0xA6 | SetObjectGfxState | `0x0053f170` | — | |
| 0xA7 | PlayerCharacterDeploy | `0x0053fa90` | ✅ | |
| 0xA8 | ActionCommandResponse | `0x0053cb10` → `FUN_004d9ba0` | ✅ | 56B; command-lock ack |
| 0xB0 | GamePrepareForStart | `0x0053cb80` | ✅ | |
| 0xB1 | GameStart | `0x0053cc40` | ✅ | |
| 0xB7 | ObjectivesInitForLevel | `0x0053c820` | ✅ | 56B/obj parser-confirmed; short-read tolerant → **not a crash vector** (§4) |
| 0xB8 | ObjectiveUpdated | `0x0053bb10` | ✅ | |
| 0xB9 | ObjectivesComplete | `0x0053bc50` | ⚠️ | **mis-encoded (wrong CashOut blob)** (§4) |
| 0xBA | CombatEvent | `0x0053ed50` | ✅ | reflection<8> **schema-driven** (AssetData.Parser CombatEvent); wired to TakeDamage → floating damage numbers; golden-tested |
| 0xBF | ReloadLevel | `0x0053cce0` | — | |
| 0xC0 | GravityForceUpdate | `0x0053f240` | — | |
| 0xC1 | CooldownUpdate | `0x0053f310` | — | **FIXED 36B** (u32 objId + 8×u32); needs cooldown system |
| 0xC3 | CrystalMessage | `0x0053f700` | ✅ | 29B reply to CrystalDrag |
| 0xCA | ObjectiveAdd | `0x0053c970` | — | |
| 0xCC | DebugPing | `0x0053d840` | ✅ | state-machine driver |

**Connection/lobby phase** (transport layer `nSporeNet::cClientSession`, `0x00a9xxxx` — separate from
the in-game dispatcher):

| Wire | Message | Client handler (floor) | C# send |
|---|---|---|---|
| 0x80 | HelloPlayer | `0x00a93d50` | ✅ |
| 0x82 | Connected | `0x00a93b50` | ✅ |
| 0x84 | PlayerJoined | `0x00a93f50` | ✅ |
| 0x86 | PlayerDeparted | `0x00a94040` | — |

**Not in either dispatcher = Chain*/Arena/mode families** (0xA9–0xAE, 0xB3–0xB6, 0xBB–0xBE, 0xC4–0xC9):
consumed by **per-game-state listeners registered on state entry** — a **second dispatcher that is
NOT yet swept** (heap-registered). 🔴 No client floor for any of these opcodes. → §6.

---

## §2 — RakNet send surface (C→S) 🟢

What the client emits = the server must handle. **Floor = the client's own embedded server
handlers** — the retail client ships `GameSimulator` (`FUN_009c6150` @`0x009c6150`,
"SimulatorDebugPing" registry), an in-process copy of the **original server's** command handling.
This is ground truth *richer than the C++ reference* for what the real server did.

| Wire | Message | Client embedded handler (floor) | C# handle |
|---|---|---|---|
| 0x7F | HelloPlayerRequest | transport `ConnectAndRegisterMessages` @`0x00a93353` | ✅ |
| 0x88 | PlayerStatusUpdate | `0x009c3020` | ✅ |
| 0x9C | ActionCommandMsgs | **`0x009c5b40`** | ✅ all client types (D-015..D-020) |
| 0xAC | ChainPlayerMsgs | (per-state listener) | ✅ |
| 0xC2 | CrystalDragMessage | `0x009c2f60` | ✅ reject-only until loot |
| 0xCB | LootDropMessage | `0x009c…` (stub) | ⚠️ raw-capture; **layout unmapped** → §6 |
| 0xCC | DebugPing | `0x009c5340` | ✅ |

`0x009c5b40` (ActionCommand) is the authoritative decode for the 0x9C sub-command table — deeper than
the C++ reference; deep-dive pending. **Inbound layer is functionally 100%** (every C→S the client
emits is parsed + answered); remaining depth is ability/loot *semantics*, not wire.

---

## §3 — REST / SporeNet 🟢

Class `SP_App::ApiRequest` — one builder per endpoint (vtable `PTR_FUN_00fdXXXX`), response handler
at **vtable slot +0x30**. All 31 builder/parser symbols **live-confirmed** in the program (namespace
`ClientRest`). **Floor = the builder (required request params) + the +0x30 parser (fields the client
reads back).** Full field maps in [`../http/CLIENT_REST_FLOW.md`](../http/CLIENT_REST_FLOW.md).

| Endpoint | Builder (floor) | Response parser (floor) | C# |
|---|---|---|---|
| account.auth | `BuildAuthRequest` `0x00464670` | `OnAccountResponse` `0x00469190` | ✅ (settings+server_tuning now emitted) |
| account.getAccount | `BuildGetAccountRequest` `0x00464c50` | → OnAccountResponse | ✅ |
| account.logout | `BuildLogoutRequest` `0x0045fd90` | no-op `0x0076f560` | ⚠️ notif only |
| account.setSettings | `BuildSetSettingsRequest` `0x00465e70` | no-op | ✅ persists |
| account.setNewPlayerStats | `BuildSetNewPlayerStatsRequest` `0x00465cc0` | no-op | ✅ |
| account.unlock | `BuildUnlockRequest` `0x00467710` | `ParseUnlockResponse` `0x00462fb0` | ✅ (account block) |
| creature.updateCreature | `BuildUpdateCreatureRequest` `0x00466140` | `ParseUpdateCreatureResponse` `0x00460c70` | ✅ |
| creature.unlockCreature | `BuildUnlockCreatureRequest` `0x00465fc0` | `ParseUnlockCreatureResponse` `0x00460b80` | ✅ |
| creature.resetCreature | `BuildResetCreatureRequest` `0x00464f80` | `ParseResetCreatureResponse` `0x00460020` | ✅ |
| creature.getTemplate | *(builder in doc)* | — | ✅ (new mapper) |
| deck.updateDecks | `BuildUpdateDecksRequest` `0x00466f90` | `OnUpdateDecksResponse` `0x0045a870` | ✅ |
| game.getGame / getRandomGame | `BuildGetGameRequest` `0x00465110` | `ParseGameResponse` `0x0046d880` | ⚠️ needs game-history store |
| game.getReplay | `BuildGetReplayRequest` `0x00465590` | `ParseReplayResponse` `0x004605f0` | ❌ |
| game.exitGame | `BuildExitGameRequest` `0x0045fcd0` | no-op | ✅ ack |
| inventory.getPartList | `BuildGetPartListRequest` `0x00465310` | `ParsePartListResponse` `0x004601a0` | ✅ |
| inventory.getPartOfferList | `BuildGetPartOfferListRequest` `0x004602c0` | `ParsePartOfferListResponse` `0x00460380` | ✅ **(2026-07-08: full pool + expires)** |
| inventory.updatePartStatus | `BuildUpdatePartStatusRequest` `0x004673c0` | no-op | ✅ |
| inventory.vendorParts | `BuildVendorPartsRequest` `0x00467890` | `ParseVendorPartsResponse` `0x004612b0` | ✅ **(2026-07-08: sell/flair + dna; buy no-op by design)** |

**Block sub-parsers (data-model floor, all live-confirmed):** `ParseAccountBlock` `0x00471110`,
`ParseCreaturesBlock` `0x00463990`, `ParseItemsBlock` `0x00463de0` (inbox, not parts),
`ParseDecksBlock` `0x00468980`, `ParseServerTuningBlock` `0x0045f8a0`, `ParsePartEntry` `0x0045f4c0`.
**Verified floor detail:** vendor `transactions` delimiter = `;` (`BuildVendorPartsRequest` joins with
`DAT_00fdb41c`=`0x3B`, each entry `%c%I64d`) — a Ghidra fact, not the C++ `explode_string`.

---

## §4 — Wire-struct floor

The exact on-wire structs the client parses. **Floor = the reflection registrar / parse fn address.**
Only four registrars are genuinely Ghidra-anchored; most reflection field tables are still
**C++-source only** and must be swept.

> **Implementation directive (user, 2026-07-08):** every `AssetData::*` / `AssetType::*` reflection
> class (e.g. `cAIDirector`) is **already mapped by the `AssetData.Parser` lib** — the C# side must
> **derive the field schema from `AssetData.Parser`, not hardcode it**. The Ghidra registrar confirms
> the *wire contract* (which fields, ids, offsets); the lib supplies the schema to serialize against.
> This matches the CLAUDE.md "no DTOs — use generic `AssetValue` navigation" rule.

| Struct | Grade | Client proof | C# parity |
|---|---|---|---|
| Reflection bitmap regime (bm1/bm2/ID+0xFF) | 🟢 | `ParseReflectionMessage` `0x00a22e80` | matches (bm2 LE corrected) |
| SporelabsObject reflection (23 fields) | 🟢 | `RegisterSporelabsObjectFields` `0x00f8ded0` | ⚠️ fields 11/14/15 width dispute (§6) |
| ServerEvent (26 fields) | 🟢 | `RegisterServerEventFields` `0x00f60ea0` (struct 0x98) | ✅ client-verified |
| TeleporterDef (3 fields) | 🟢 | `RegisterTeleporterDefFields` `0x00f97bc0` | — |
| cLabsMarker (38 fields) | 🟢 | registrar `0x00f8b3c0` | partial |
| ActionCommand emit (40B header + subtype table) | 🟢 | `SendActionCommandMsgs` `0x0053be60`, `FillCommandHeader` `0x004e2150`, `GetPayloadSize` `0x00a1ca80` | ⚠️ C# handles 3/4; 5/7–13 depth open |
| ActionCommandResponse (56B) | 🟢 | `OnGmsActionCommandResponse` `0x0053cb10`→`FUN_004d9ba0` | ⚠️ overloads unaudited |
| ObjectDelete (flat u32[]) | 🟢 | `OnGmsObjectDelete` `0x0053ddc0` | ✅ golden-tested |
| cGameObjectCreateData | 🟡 | dispatch `0x0053f550`; fields C++-only | ⚠️ curated subset |
| Objective entry (ObjectivesInit 0xB7) | 🟢 | `OnGmsObjectivesInitForLevel` `0x0053c820` → u8 count + N×56B `{u32 id, 4×u8, 48B blob}` (reader `ReadStreamBytes` `0x00a8d310`) | ✅ 56B parser-faithful; short-read tolerant, no crash |
| cAIDirector (DirectorState 0x8B) | 🟢 | registrar `AssetData::cAIDirector` `0x00f78480` / `AssetType` `0x00f789e0` (class `0x1fb04a19`, 7 fields) | ✅ **DONE 2026-07-09** — `DirectorStatePacket` emits reflection<7> via `AssetReflection.WriteReflection("cAIDirector", …)`, layout driven by AssetData.Parser schema (field order = wire id, verified); golden `DirectorStatePacketTests` |
| Player reflection `<24>` (LabsPlayerUpdate) | 🔴 | none (C++ `Player.cpp`) | ⚠️ fields 9/10/11 missing in C# |
| Character reflection `<124>` | 🔴 | none (C++ `Character.cpp`) | stated match, **unverified** |
| Catalyst reflection `<2>` | 🔴 | none | unverified |
| Locomotion `<23>` (0x94) | 🔴 | none | unaudited |
| ObjectPlayerMove 0x91 (81B) | 🔴 | none | matches C++ (self-attested) |
| ChainData 0x151 (ChainVote) | 🔴 | capture-verified LE only | extra branch, provenance unclear |
| CashOutData 0x2C8 (ChainCashOut) | 🔴 | none; endianness unverified | ❌ not implemented |
| ObjectivesComplete 0xB9 | 🔴 | none | ⚠️ writes wrong 712B CashOut blob |

---

## §5 — Blaze 🔴 (floor entirely unswept)

**Headline:** the Blaze client contract is **not Ghidra-mapped at all.** Every "client-required"
call below is inferred from a **working C++ runtime log** (`output.txt`) that shows the real command
*sequence* — wire/log evidence, not a client-binary address. The only Ghidra artifact anywhere is one
**unconfirmed** candidate: Rooms vtable `~0x00e45e60`, msg table `~0x010e996c` (`[?]`). So Blaze is a
**sweep backlog**, not a status table. C# columns describe divergences already found vs C++/wire.

| Component (id) | Client-required (from working-log) | C# | Floor |
|---|---|---|---|
| Redirector (0x05) | getServerInstance 0x01 | ✅ (union tag differs) | 🔴 |
| Util (0x09) | preAuth 0x07, ping 0x02, postAuth 0x08 | ✅ (CIDS/CONF/QOSS/ports diverge) | 🔴 |
| Auth (0x01) | getLegalDocsInfo, login 0x28, loginPersona 0x6E, getAuthToken 0x24 | ✅ (silent/express envelope divergence) | 🔴 |
| UserSessions (0x7802) | updateNetworkInfo 0x14 (×2) | ✅ (drops QoS/latency/country) | 🔴 |
| Messaging (0x0F) | fetchMessages 0x02 | ⚠️ C# replies `{MCNT=0}` (C++ silent) | 🔴 |
| Rooms (0x15) | selectViewUpdates 0x0A, selectCategoryUpdates 0x0B, **joinRoom 0x14** | ⚠️/✅ (join fixed 2026-05-30) | 🔴 (Rooms vtable candidate only) |
| GameManager (0x04) | resetDedicatedServer 0x19, finalizeGameCreation 0x0F, updateMeshConnection 0x1D | ✅ (request-shape mismatch) | 🔴 |
| Playgroups (0x06) | createPlaygroup 0x01 `[?]` | ⚠️ stub echo | 🔴 (C++ also empty stubs) |
| Association (0x19) | getLists 0x06 | ✅ hardcoded members | 🔴 |
| CensusData (0x0A) | **none on solo path** | ❌ absent | 📖 **documented** (`components/censusdata.md`): subscription **live-population feed** — `NotifyServerCensusData.mCensusDataList` of `GameManagerCensusData`/`PlaygroupCensusData`/`RegionCounts`. Server-browser/matchmaking only; out-of-scope solo |
| GameReporting (0x1C) | **none** (0 log lines) | ❌ | 📖 **documented** (`components/gamereporting.md`): **post-match stats submission** (`submitGameReport`/`submitOfflineGameReport`/`submitTrusted*`; `NotifyGameReportingIdChange` from GameManager; `Arson*`=Blaze boilerplate). Leaderboards only; out-of-scope solo |
| UnknownComponent1 (0x2678) | **none** (identity unknown) | ⚠️ no-op | 🔴 |

**Blaze wire framing (from flow docs, C++-source):** `u16 length | u16 component | u16 command |
u16 error | u32 msg(type:id)`. TDF = 12-byte BE header. These are the *only* solid Blaze facts;
none are client-Ghidra-anchored.

---

## §6 — Ghidra-floor sweep backlog (prioritized)

Where the client floor itself is missing — the honest "cannot measure C# yet" list. **These are the
next mapping targets.** Ordered by leverage for the single-player Dungeon path.

**RakNet**
1. **Chain*/Arena/mode 2nd dispatcher** — *2026-07-08 mechanism located:* Chain ids (kGms 0x29–0x2F =
   0xA9–0xAF) are **not** in the central in-game batch `FUN_0053d2c0` (that registers ids 0xb–0x4d on
   manager `FUN_007bfd40` vtable+0x30). They're registered **per game-state** in state-`OnEnter`
   functions (callers of the register primitive `FUN_00a94e10` at `0x0044a4d0`/`0x0044a6a0`/`0x0044b7e0`
   /`0x00451080`/`0x00454890`/`0x00454f30`), where the kGms id is passed **in a register** (lost in
   pseudocode). *Remaining sweep:* read the raw instructions around each `FUN_00a94e10` call per state
   to recover id→handler fn for 0x29–0x2F, then name `OnGmsChain*`. Blocks the reward/chain flow contract.
2. **LootDropMessage 0xCB layout** — client `GameSimulator` id 76 is a stub; no field parse anywhere.
   *Sweep:* fire a loot drop + hex-log the raw-capture handler, or find a loot registrar.
3. ~~**cAIDirector field map (DirectorState 0x8B)**~~ — **RESOLVED 2026-07-08.** Registrar
   `AssetData::cAIDirector` `0x00f78480` (+ `AssetType::cAIDirector` `0x00f789e0`): class `0x1fb04a19`,
   size 0x4d0, **7 reflection fields** — `mbBossSpawned`(bool@0xd), `mbBossHorde`(0xe),
   `mbCaptainSpawned`(0xf), `mbBossComplete`(0x10), `mbHordeSpawned`(0x48c), `mBossId`(u32@0x14),
   `mActiveHordeWaves`(int@0x47c). C# must emit reflection over these, not a raw mEnabled/mState blob.
4. ~~**ObjectivesInitForLevel 0xB7 width**~~ — **RESOLVED 2026-07-08.** Parser reads `u8 count + N×56B
   {u32 id, 4×u8, 48B blob}` (`0x0053c820`); C# 56B is parser-faithful; reader `ReadStreamBytes`
   `0x00a8d310` is short-read tolerant so it's **not a crash vector**. Only the 48B blob field-roles remain.
5. **SporelabsObject fields 11/14/15 width** — registrar says u64; C# says uint (commit 35a64f8).
   Latent (no sender) but any future sender desyncs the block. *Settle via single-step/capture.*
6. **Modifier/Cooldown/CombatEvent/AgentBlackboard/Loot* outbound layouts** (0x99,0x9A,0xA2–0xA4,0xBA,
   0xC1) — several `DBG`-disabled in C++, no client confirmation. Needed for the Lua combat-feedback phase.
7. **Player `<24>` / Character `<124>` / Catalyst `<2>` reflection tables** — only C++-source; find
   client registrars (only 4 registrars are Ghidra-anchored so far).

**Blaze** — *entire client contract unswept.* Highest-value single target:
8. **GameManager lifecycle** (createGame/joinGame/matchmaking) — C++ hardcodes gameId=1, NotifyGameSetup
   commented. *Sweep:* client `Blaze::GameManager::JoinGameRequest` + `cReplicatedGameData` parser
   (also the `REAS`/`NetworkAddressMember` union tag — Dungeon-entry crash candidate).
9. **Rooms** — only doc with a candidate (`~0x00e45e60` / `~0x010e996c`, `[?]`); trace `joinRoom`
   `RoomData`/`RoomCategoryData` layouts.
10. **CensusData `NotifyServerCensusData` (0x04)** — schema entirely unknown; C++ commented out.
11. Redirector union-tag, Util CIDS/CONF requirements, UserSessions ExtendedData necessity — per-field
    tolerance sweeps.

**Game (server-internal — floor is the S→C surface above, plus these client-logic sweeps)**
12. **AI decision logic** — enemies spawn but `ObjectManager.Update()` is a no-op; no client-observed
    aggro/gambit mapped. #1 gameplay blocker. *Sweep:* runtime trace of enemy decision in-client.
13. **cLuaThread resume loop** — the real Lua coroutine scheduler is **UNLOCATED** (two prior
    mis-IDs were GC internals). Blocks any scheduler-behavior claim.
14. **Lua boot-group hashes 1/3/5/7/8/9** — unresolved FNV hashes in `LuaSystem::Initialize`; need the
    original Darkspore Lua package filenames.
15. **10 Lua stub namespaces** (nBehaviorTree/nScenarioManager/nPhysics/nAgent/nGameDirector/nLevel/
    nJuggernaut/nTuning/nClient + nCondition/nAffix) — native tables/arities not decompiled.
16. **NounDatabase typed assets** — `AIDefinition` read but never consumed; asset schema for AI fields unswept.

**SporeNet / HTTP** — no client floor because the feature is C++-stub:
17. **Vendor economy** — the "grandão": real buy flow. Client parsers exist (§3) but the *offer
    generation / pricing* system has no floor beyond `ParsePartOfferListResponse` + `server_tuning`.
18. **getGame/getReplay history**, **searchAccounts**, **leaderboard** — C++ fixtures; need client
    contract + a game-history store (out of single-player scope).

---

## Keeping this in sync

- One floor grade per row: promote 🔴→🟡→🟢 **only** when a client address is confirmed in the program
  (not from a doc that summarizes Ghidra — re-pull to cite).
- When a §6 sweep lands, move the item into its §1–§5 table with the address, and log the fact in
  `VERIFIED_FACTS.md` + the relevant memory.
- The C# status columns track `PORTING_MATRIX.md`; this doc owns the **floor grade + client address**.
