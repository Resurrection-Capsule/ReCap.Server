# Architecture Docs Roadmap

Persistent finishing plan. Tick boxes as work lands. Sessions resume from the lowest un-checked milestone.

> **Update protocol:** when finishing a sub-item, edit this file in the same turn. Stale roadmap = drift. See [CLAUDE.md → memory write-back rule].

---

## Snapshot (2026-05-24)

| Layer | Status |
|---|---|
| Top-level (README, FLOW_CPP, FLOW_CSHARP) | skeletons complete, internally consistent |
| assetdata-system/ (ASSET_SYSTEM, GHIDRA_GROUND_TRUTH, FORMAT_COVERAGE) | complete |
| Phases 00 – 11 | deep-dives written (see per-doc gap count below) |
| Phase 12 GameOver | ✅ M1 (2026-05-23) |
| Phase 13 Disconnect | ✅ M1 (2026-05-23) |
| Cross-cutting refs (TDF, Reflection, Endianness, StateMachine) | ✅ M2 (2026-05-23) |
| `packets/` per-opcode sheets | ✅ Tier-1 (~33) done M3 (2026-05-23); Tier-2/3/4 deferred by design |
| PARITY.md global table | ✅ collapsed to 1-row-per-phase (2026-05-24, M5) |

### Per-phase gap count (❓ tokens in body)

| Phase | ❓ | Real unknowns |
|---|---|---|
| 00 boot | 0 | — |
| 01 redirector | 0 | TLS cert chain (low) |
| 02 blaze-auth | 0 | TDF byte layout (cross-cutting) |
| 03 rest-bootstrap | 0 | XML payload schemas (cross-cutting) |
| 04 blaze-gamemanager | 0 | TDF byte layout (cross-cutting) |
| 05 raknet-connect | 4 | — (M4-1 closed 2026-05-24; 4 ❓ rows are config-defaults audit, lower priority) |
| 06 spaceship | 0 | HelloPlayer 12B field order, Catalyst.WriteReflection byte layout |
| 07 chainvote | 0 | — (M4-2 closed 2026-05-24) |
| 08 predungeon | 0 | GamePrepareForStart 17B order, Character bm size |
| 09 dungeon | 2 | DirectorState body, QuickGame body (M4-3 ObjectCreate closed 2026-05-24) |
| 10 gameloop | 0 | — (large but well-documented) |
| 11 chaincashout | 5 | Wire ID quirk (0xA9 vs 0xAB), CashOutData endianness, ChainData.Completed/Progression fields |

---

## Milestone 1 — Close current lifecycle ✅ (2026-05-23)

Goal: every state transition the client can take has a doc.

- [x] **`phases/12-gameover.md`** — failure lane: party wipe → `ChainGameMsgs(state=1)` → `GameState=0x0D` → transition to Spaceship
- [x] **`phases/13-disconnect.md`** — Goodbye (0x83) graceful, `ID_DISCONNECTION_NOTIFICATION` / `ID_CONNECTION_LOST` RakNet, PlayerDeparted, VoteKickStarted, session cleanup
- [x] Update `README.md` table (rows 12, 13)
- [x] Add Phase 12 + 13 sections to `FLOW_CPP.md` + `FLOW_CSHARP.md`
- [x] Add Phase 12 + 13 parity tables to `PARITY.md`
- [x] Correct `GameState.GameOver = 0x0D` (not 0x0E) across Phase 11 + index docs

## Milestone 2 — Cross-cutting reference docs ✅ (2026-05-23)

Order: lowest dep first so later docs `[[link]]` instead of inlining.

- [x] **`REFLECTION_SERIALIZER.md`** — bm1 / bm2 / bmID rules + worked examples (Player 24, Character 124, Catalyst). Resolves 06/08/10 cross-references.
- [x] **`ENDIANNESS.md`** — extracted from CLAUDE.md w/ per-field truth table.
- [x] **`STATE_MACHINE.md`** — full `IsValidStateChange` graph (`Client.cpp:12-49`), all 21 GameStates, mermaid + transition matrix. Found: C# enum is incompatible — 7 states vs 21, wire codes mapped through `GameStatePacket.WireState` switch.
- [x] **`BLAZE_TDF.md`** — TDF tag encoding, Map / List / Union framing, varint quirk, used by 01 / 02 / 04. Found: C++ decoder drops varint sign bit while C# encoder emits it — latent bug.

## Milestone 3 — Per-opcode packet wire spec sheets ✅ Tier-1 done (2026-05-23)

**Scope decision:** Tier-1 (~33 gameplay-critical opcodes) per-file. Tier-2/3/4 (~45) tracked in `packets/README.md` index only, sheets deferred.

- [x] `packets/README.md` — opcode index w/ Tier-1 sheets + Tier-2/3/4 inventory
- [x] `packets/0x7F-helloplayerrequest.md`
- [x] `packets/0x80-helloplayer.md`
- [x] `packets/0x81-reconnectplayer.md`
- [x] `packets/0x82-connected.md`
- [x] `packets/0x83-goodbye.md`
- [x] `packets/0x84-playerjoined.md`
- [x] `packets/0x85-partymergecomplete.md`
- [x] `packets/0x86-playerdeparted.md`
- [ ] `packets/0x87-votekickstarted.md` — Tier-2, deferred
- [x] `packets/0x88-playerstatusupdate.md`
- [x] `packets/0x8A-gamestate.md`
- [x] `packets/0x8B-directorstate.md`
- [x] `packets/0x8C-objectcreate.md`
- [x] `packets/0x8D-objectupdate.md`
- [x] `packets/0x8E-objectdelete.md`
- [x] `packets/0x91-objectplayermove.md`
- [x] `packets/0x94-locomotiondataupdate.md`
- [ ] `packets/0x96-attributedataupdate.md` — Tier-3, deferred
- [ ] `packets/0x97-combatantdataupdate.md` — Tier-3, deferred
- [ ] `packets/0x9B-serverevent.md` — Tier-3, deferred
- [x] `packets/0x9C-actioncommandmsgs.md`
- [x] `packets/0xA1-labsplayerupdate.md`
- [x] `packets/0xA7-playercharacterdeploy.md`
- [x] `packets/0xA8-actioncommandresponse.md`
- [x] `packets/0xA9-chainvotemsgs.md`
- [x] `packets/0xAA-chainlevelresultsmsgs.md`
- [x] `packets/0xAB-chaincashoutmsgs.md`
- [x] `packets/0xAC-chainplayermsgs.md`
- [x] `packets/0xAD-chaingamemsgs.md`
- [x] `packets/0xAE-chaingameovermsgs.md`
- [x] `packets/0xAF-quickgamemsgs.md`
- [x] `packets/0xB0-gameprepareforstart.md`
- [x] `packets/0xB1-gamestart.md`
- [x] `packets/0xB7-objectivesinitforlevel.md`
- [x] `packets/0xB8-objectiveupdated.md`
- [x] `packets/0xB9-objectivescomplete.md`
- [x] `packets/0xCC-debugping.md`
- [ ] After all sheets land: refactor phase docs to link `packets/NN-name.md` instead of inlining the byte layout — **deferred** (phase docs still authoritative for sequence + context; per-opcode sheets are byte-spec only)

> **Template per opcode:** PacketID, direction (S→C / C→S / both), body layout table (offset, field, type, BE/LE, notes), C++ writer file:line, C++ reader file:line, C# packet class, C# activator entry, status (✅ / ⚠️ / ❌), open audit items.

## Milestone 4 — Resolve real unknowns

- [x] **RakNexus framing audit** ✅ (2026-05-24) — wire-compatible with RakNet 3.92 / protocol 13. MTU 1492, UDP header 28, payload 1464 B. `INCLUDE_TIMESTAMP_WITH_DATAGRAMS=0` (sliding-window mode) — both sides omit timestamp. Datagram header byte, ACK/NAK, RangeList, InternalPacket field order (incl. split trio: count → id → index) all byte-identical. Two cosmetic divergences flagged: (a) RakNexus does not downconvert `*_WITH_ACK_RECEIPT` reliability bits — harmless; (b) RakNexus splits at hard-coded `mtu - hdr - 50` vs upstream's exact `GetMaxMessageHeaderLengthBits/8` — conservative, never under-splits. Findings in `phases/05-raknet-connect.md`.
- [x] **ChainData 0x151 byte-by-byte diff** ✅ (2026-05-24) — per-offset table in `phases/07-chainvote.md`. One real divergence: tail-6-u32 starts at `0xED` in C++ vs `0xEE` in C# (Branch A only). LE rule says C# offset is canonical. Both paths otherwise structurally aligned (modulo BE/LE). Logged tail-clobber finding (`playerIndex` of `mChainSummary[3]` overwritten by party-override write at `0xD9` — both sides).
- [x] **ObjectCreate dense layout audit** ✅ (2026-05-24) — wire-identical. `cGameObjectCreateData::WriteReflection` (10 fields, bm2 BE) + `sporelabsObject::WriteReflection` (23 fields, bmID + `0xFF` terminator). All multi-byte fields BE; quat element order `x,y,z,w`. Dense `WriteTo` (sizes `0x70` / `0x308`) also match offset-by-offset on both sides but unused — `WriteReflection` is the only wire path. Findings in `phases/09-dungeon.md`.
- [ ] **Wire capture / static-analysis session** to settle:
  - [x] ChainCashOut opcode ✅ (2026-05-24, Ghidra static) — **not 0xAB**. C++ `SendChainCashOutMessages` writes `0xA9` intentionally. Client `FUN_0053d2c0` subscription table (38 GMS IDs) omits both `0x2A` (ChainVote) and `0x2C` (ChainCashOut); both ride the `BinaryReader::BindFormatContext` path inside `cChainVotingState`/`cChainCashOutState`. CashOut differs from Vote only by `value != 0`. Doc: `phases/11-chaincashout.md`.
  - [ ] CashOutData endianness (BE default → fall back to LE) — still open; needs Ghidra GUI xref on `"SP_SporeLabs/cChainCashOutState"` or runtime capture once Phase 08 stall fixed.
  - [x] HelloPlayer 12B field order ✅ (2026-05-24, Ghidra static via `FUN_00a93d50`) — **8 B** actual: `u8 type, u8 gameplayIndex, u32 BE IPv4, u16 BE Port`. C++ writes 12 B (4 B padding ignored); C# writes only 2 B (broken). Fix Tier-A: `HelloPlayerPacket.cs`. Doc: `phases/06-spaceship.md`.
  - [ ] Goodbye (0x83) body shape — still open; needs Ghidra GUI (client only receives, never sends, so runtime won't surface it cheaply).
  - [ ] DebugPing body — does client consume `u64 timestamp`? Still open. Trivial via runtime: output.log already captures inbound DebugPing — hexdump first session and read len/bytes.
  - [ ] Whether the client survives missing PSS/Tick/Telemetry/QoS ports — runtime only.

## Milestone 5 — Sync PARITY.md ✅ (2026-05-24)

- [x] Decision point resolved: **collapse to 1-row-per-phase summary**. Phase docs remain source of truth; top-level PARITY is now a fast index. 79 ❓ rows eliminated.
- [x] Each phase row carries one-line key-divergences + link to phase doc. Frozen-rules section preserved. New "Audit log" + "How to keep this in sync" sections added.

## Milestone 6 — Alt modes (low priority)

- [ ] `phases/A0-arena.md` — ArenaLobby / ArenaRoundResults / Arena packets
- [ ] `phases/A1-juggernaut.md` — JuggernautLobby / JuggernautResults
- [ ] `phases/A2-killrace.md` — KillRaceLobby / KillRaceResults
- [ ] `phases/B0-editor.md` — Editor / LevelEditor / Replay / Spectator / Observer (read-only flows)

## Milestone 7 — Full both-system parity docs (2026-05-25)

Goal shift: the gameplay-flow docs (phases/packets) cover the RakNet trail. M7 documents **every C++ module class-by-class** so porting gaps are visible. Drives `PORTING_PLAN.md`.

- [x] **`PORTING_MATRIX.md`** — class/handler parity for all 8 C++ modules (Blaze, Game, SporeNet, RakNet, HTTP, Core/QoS/Network). Status ✅/⚠️/❌/❓ per row. Generated 2026-05-25 via 6-agent fan-out. **Headline: Game module ~25% ported — combat engine absent.**
- [x] **`components/`** — 13 sheets (11 active Blaze components + 2 C#-only) + README index. Command-level parity tables. Done 2026-05-25. Finding: Auth 10/10 ported; GameManager 3/10; Util 3/18; Playgroups 1/11; CensusData absent.
- [x] **`data-model/`** — 11 sheets + README. SporeNet ↔ C# field tables + persistence. Done 2026-05-25. Finding: Room/Feed/AssociationLists 0% ported; `Domain/Creature.cs` is a thin stub (Stats/Parts live only on Model).
- [x] **`http/`** — 6 sheets (README + 5 route groups) with complete endpoint catalog. Done 2026-05-25. Finding: 8/24 `/game/api` methods return null → `UnimplementedMethodException`; `/qos/*`, `/game/service/png`, `/telemetryevent` absent.
- [ ] **`systems/`** — Game-module subsystem deep-dives. **Opus-grade** (combat math, Lua VM, attribute interplay, AI):
  - [ ] `systems/object-manager.md` — Object/cGameObject lifecycle, spatial query, ObjectManager.Update
  - [ ] `systems/attributes-combat.md` — 114-attribute array, TakeDamage/Heal/crit/distribution
  - [ ] `systems/abilities-lua.md` — Lua VM, coroutines, Ability/Objective execution (~3.2k LOC LuaFunctions)
  - [ ] `systems/ai-director.md` — AI inner class, AgentBlackboard, gambits, cAIDirector
  - [ ] `systems/noun-database.md` — typed Noun/NPC/PlayerClass/AIDefinition asset loading
  - [ ] `systems/locomotion-physics.md` — Locomotion sim, projectile/lob/roll, Collision, OctTree
  - [ ] `systems/loot-events.md` — LootData, ServerEvent/ClientEvent/CombatEvent, Level/LevelConfig/CashOutData
- [ ] Tier-2/3/4 packet sheets (~45) — fold into PORTING_MATRIX RakNet rows or author on demand

---

## Working principles

1. **One milestone at a time.** Don't fork.
2. **Update this file in the same turn** when a sub-item lands.
3. **Phase docs are the source of truth.** Top-level docs link; they don't duplicate.
4. **`file:line` citations resolve into the actual tree.** When a cited line moves, update the doc — don't leave dangling refs.
5. **Mark unknowns explicitly.** `❓` w/ a one-line "what would close this" note. Empty cells are forbidden.
6. **Caveman in chat, normal English in docs.** Authored prose stays readable for someone reading without context.

---

## Out of scope

- Implementation work (this roadmap is **documentation only**) — tracked separately in [`PORTING_PLAN.md`](PORTING_PLAN.md) (P1–P7, sequenced by gameplay phase)
- Performance tuning, dependency upgrades
- Code refactors triggered by doc findings — file those as Tier-A/B/C/D rows in `project-protocol-bugs` memory, then surface via the relevant P in PORTING_PLAN
