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
| D-003 | Deck HUD (client) | 11 | — (cPlayerDeck::UpdateHud @0x51b860) | Game.cs / LPU | `cPlayerDeck+0x20` (Scaleform movie) null → crash @0x551f47. Root not yet found; insensitive to gameplay data. Needs Ghidra debugger + steps 1-3 wire-diff. | open | |
| D-004 | Player hello dataBits | 5 | Player ctor + Setup() (Player.cpp:58-66, 178-206) | LabsPlayerData.SetInitialDataBits (LabsPlayerUpdatePacket.cs:93) | C# initial dataBit set omitted `13 CrystalData`, `14 CrystalBonuses`, `17 ChainProgression`. C++ hello union = {0,3,4,5,6,7,8,12,13,14,15,16,17,18,21,22}. Aligned C# + golden test to it. | fixed | 54f0e09+1 |
| D-005 | LPU/reflection endianness | 5/11 | Write<T> wrapper (Types.h:218-226) + include-order (Server.h:9/20, Types.h:11) | LabsPlayerUpdatePacket.cs, ReflectionSerializer.cs | A BE re-read of `#define __BITSTREAM_NATIVE_END` was refuted; include order makes it a no-op → wire LE. C# (BinaryWriter) already LE = correct; stale BE comments + 5 golden tests corrected to LE. | verified | 54f0e09 |

| D-006 | REST inventory parts | 3 | Part::WriteApi (Part.cpp:104,110 — `id`/`reference_id` = loop `index`) | GameRestController getPartList + CreaturePartMapper.cs:27 (`id`/`reference_id` = DB row ID) | C++ ids are a 1-based per-response counter; C# uses the persisted DB primary key. Equal to each other on both sides, but counter≠DB-id means part-transaction correlation (vendorParts/updatePartStatus) can mismatch. Off the Dungeon crash path; low priority. | open | |
| D-007 | REST deck/account extras | 3 | User.cpp:611-618 (deck creature node: 6 fields, no png urls); User.cpp:141 (`grant_online_access` commented out) | DeckMapper.cs:36-41 (full creature contract → adds png_large/thumb_url inside deck); AccountContract grant_online_access emitted when non-null | C# emits `png_large_url`/`png_thumb_url` inside `<deck><creatures>` (C++ omits) and emits `grant_online_access` (C++ commented out). Harmless extras; client tolerant. | open | |

**Step-3 (REST account) source-diff done 2026-05-31:** squad-critical fields (`noun_id`, `gear_score`, `id`, `version`) on `<creature>` and `<deck>` nodes match C++ field-for-field. Only the low-risk extras above (D-006/D-007) diverge. This corroborates VERIFIED_FACTS that the deck-HUD crash (D-003) is **game-state/LPU-driven, not REST** — the REST account payload is essentially aligned. Wire-diff of step 3 still pending (HTTP/XML, byte-aligned, no RakNet parser needed).

Seed rows above are from the 2026-05-31 session; expand as the walk proceeds. D-005 verified by C++ source (include-order proof) + matches the existing capture claim; full wire re-confirm pending proper RakNet bit-alignment parse. D-004 client-verify gate pending.
