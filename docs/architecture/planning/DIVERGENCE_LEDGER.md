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

Seed rows above are from the 2026-05-31 session; expand as the walk proceeds (steps 1–3 not yet wire-diffed).
