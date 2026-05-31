# DIVERGENCE LEDGER — C# ↔ C++ field-level

Living, field/byte-level list of every divergence between `ReCap.Server` (C#) and the C++ ground truth (`ReCapCpp`), walked along the single-player Dungeon critical path. The macro index stays in [`PORTING_MATRIX.md`](PORTING_MATRIX.md) (class/handler level); **this is the micro level** (field/byte).

Method & rationale: [`../../superpowers/specs/2026-05-31-port-fidelity-plan-design.md`](../../superpowers/specs/2026-05-31-port-fidelity-plan-design.md). Verified protocol truths: [`../VERIFIED_FACTS.md`](../VERIFIED_FACTS.md).

## How to use
- One row per divergence of a **field/packet** (not a whole class).
- **Status:** `open` (found via source-diff) → `wire-confirmed` (seen in capture) → `fixed` (code changed, one commit) → `verified` (wire-parity + client advances).
- One `fixed` item = **one commit** (bisect-friendly). Cite the commit short hash.
- Refs are `file:line`. C++ root: `…/ReCapCpp/darkspore_server/source`. C# root: `ReCap.Server/`.

## Critical-path steps (walk order)
1 Login/Blaze · 2 Lobby · 3 REST account · 4 RakNet connect/HelloPlayer · 5 Hello LPU · 6 Spaceship→ChainVoting · 7 ChainVoting · 8 Vote→PreDungeon · 9 PreDungeon · 10 Dungeon entry · 11 Frame loop

## Ledger

| ID | System | Step | C++ ref | C# ref | Divergence | Status | Commit |
|----|--------|------|---------|--------|------------|--------|--------|
| D-001 | LabsCharacterData | 5/8 | Character::WriteTo (Character.cpp:99) | LabsPlayerUpdatePacket.cs:194 | `mPartAttributes` only 6 of ~110 filled (rest zero); C++ fills full array from ClassAttributes. Low crash-risk (C++ also sparse) but a real de-hardcode target. | open | |
| D-002 | LabsCharacterData | 8 | Character::WriteTo (Character.cpp:99) | LabsPlayerUpdatePacket.cs:215 | GearScore: C++ wire shows 300.0; C# uses DB value (may differ). Verify intended. | open | |
| D-003 | Deck HUD (client) | 11 | — (cPlayerDeck::UpdateHud @0x51b860) | Game.cs / LPU | `cPlayerDeck+0x20` (Scaleform movie) null → crash @0x551f47. **output.txt 2026-05-31 confirms: client reaches Dungeon (GameState 0x06), spawns 3 heroes, deploys deck=1, runs frame loop ~4s (GameState ×181→×221), does mid-game joinRoom+getPartList (in-game deck/squad UI), then disconnects @16:15:23.** Insensitive to D-004 (dataBits) and to all login/lobby/REST changes — client passes steps 1-10. Root is in-game step 11. Next: Ghidra runtime debugger at 0x551f10 / where cPlayerDeck+0x20 is set. | open | |
| D-004 | Player hello dataBits | 5 | Player ctor + Setup() (Player.cpp:58-66, 178-206) | LabsPlayerData.SetInitialDataBits (LabsPlayerUpdatePacket.cs:93) | C# initial dataBit set omitted `13 CrystalData`, `14 CrystalBonuses`, `17 ChainProgression`. C++ hello union = {0,3,4,5,6,7,8,12,13,14,15,16,17,18,21,22}. Aligned C# + golden test to it. **Verify gate (client, 2026-05-31): crash PERSISTS** — D-004 is a fidelity fix, NOT the crash cause. | fixed | c794012 |
| D-005 | LPU/reflection endianness | 5/11 | Write<T> wrapper (Types.h:218-226) + include-order (Server.h:9/20, Types.h:11) | LabsPlayerUpdatePacket.cs, ReflectionSerializer.cs | A BE re-read of `#define __BITSTREAM_NATIVE_END` was refuted; include order makes it a no-op → wire LE. C# (BinaryWriter) already LE = correct; stale BE comments + 5 golden tests corrected to LE. | verified | 54f0e09 |

| D-006 | REST inventory parts | 3 | Part::WriteApi (Part.cpp:104,110 — `id`/`reference_id` = loop `index`) | GameRestController getPartList + CreaturePartMapper.cs:27 (`id`/`reference_id` = DB row ID) | C++ ids are a 1-based per-response counter; C# uses the persisted DB primary key. Equal to each other on both sides, but counter≠DB-id means part-transaction correlation (vendorParts/updatePartStatus) can mismatch. Off the Dungeon crash path; low priority. | open | |
| D-007 | REST deck/account extras | 3 | User.cpp:611-618 (deck creature node: 6 fields, no png urls); User.cpp:141 (`grant_online_access` commented out) | DeckMapper.cs:36-41 (full creature contract → adds png_large/thumb_url inside deck); AccountContract grant_online_access emitted when non-null | C# emits `png_large_url`/`png_thumb_url` inside `<deck><creatures>` (C++ omits) and emits `grant_online_access` (C++ commented out). Harmless extras; client tolerant. | open | |

**Step-3 (REST account) source-diff done 2026-05-31:** squad-critical fields (`noun_id`, `gear_score`, `id`, `version`) on `<creature>` and `<deck>` nodes match C++ field-for-field. Only the low-risk extras above (D-006/D-007) diverge. This corroborates VERIFIED_FACTS that the deck-HUD crash (D-003) is **game-state/LPU-driven, not REST** — the REST account payload is essentially aligned. Wire-diff of step 3 still pending (HTTP/XML, byte-aligned, no RakNet parser needed).

**Step-1/2 (Blaze login/lobby) source-diff done 2026-05-31 — ALL candidates `open (source-diff, unverified)`.** Critical caveat: `output.txt` 2026-05-31 proves the client **passes login + lobby + reaches the Dungeon**, so none of these are blockers — they are fidelity divergences, NOT the crash. Agent-sourced; verify each against C++ `file:line` before trusting/fixing (prior agents have over-claimed "critical"). Candidates to triage later:

- Step 1 (auth): SilentLogin/ExpressLogin — C++ `WriteFullLogin` wraps `SESS` struct + outer `AGUP/NTOS/PCTK/PRIV/SPAM/THST/TURI` (AuthComponent.cpp:613,636) vs C# bare `SessionInfo` (AuthenticationComponent.cs:287,315); LookupUser tag `EDAT` (Functions.cpp:499) vs C# `DATA` (UserSessionsComponent.cs:401); GetTOSInfo missing `PRIV/THST/TURI` (AuthComponent.cpp:509); `getTelemetryServer 0x05` unhandled in C# (UtilComponent.cs:15); redirector `SECU` int vs C# bool; `TMOP` OptIn(C++) vs OptOut(C#). Client passes login regardless.
- Step 2 (lobby): FinalizeGameCreation reply `{GID}` (GameManagerComponent.cpp:1105) vs C# empty (GameManagerComponent.cs:44); `SelectPseudoRoomUpdates 0xA0` unhandled in C#; NotifyGameSetup REAS inner-struct `"VALU"` label (needs TdfEncoder.cs check); C# extra `QUEU`/`GURL`/`MATR`/`JGS` fields C++ omits. Client reaches Dungeon regardless.

| D-008 | LPU character block | 5/8 | Character::WriteTo (Character.cpp:99) | LabsCharacterData.WriteTo | **EXONERATED via wire-diff.** cpp vs cs 5043B SetSquad LPU (cmp_lpu.py): 0 both-nonzero diffs; 96B = C++ heap-garbage-in-gaps (benign), 38B = C# redundantly filling partAttributes that C++ leaves zero (C++ ships them via AttributeDataUpdate 0x96 instead). Every meaningful field is byte-faithful. LPU is NOT the crash. | verified | (wire 2026-05-31) |
| D-009 | In-game object stream | 10/11 | Server.cpp spawn loop (GetActiveObjects) | Game.OnPlayerStart | **Massive divergence (wire-diff cpp_loopback).** C++ spawns **278+ objects** (0x8C×278 + a companion **0x98×278** + 0xB8×172 + 0x94×13 + 0x90×16 + 0x91×12 + 0xA8×2 + 0xB7); C# spawns **only 3 heroes** and sends **zero** 0x98/0x90/0x91/0x94/0xB8/0xA8/0xB7. Prime crash leads (deck HUD): (a) C# hero ObjectCreate **91B** with extra fields `06`(pos vec3)/`07`/`0x10` vs C++ hero **59B minimal** (no pos); (b) every C++ ObjectCreate has a paired **0x98** companion, C# sends none; (c) ObjectUpdate C# 46B vs C++ 19/21B. Nouns match (squad correct). | open | | D-005 verified by C++ source (include-order proof) + matches the existing capture claim. D-003 is the live crash (in-game step 11); steps 1-10 are empirically passed by the client.
