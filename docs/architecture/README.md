# ReCap Architecture Docs

Detailed flow documentation for the ReCap server (C#) versus the C++ reference implementation (`dalkon/darkspore_server`). The C++ tree is ground truth; every divergence is annotated explicitly.

> **Status:** skeleton — phases will be deepened iteratively.

## How to read

| Document | Purpose |
|---|---|
| [`ROADMAP.md`](ROADMAP.md) | Living **documentation** plan (M1–M6 milestones). ✅ M1–M5 closed 2026-05-24. |
| [`PORTING_PLAN.md`](PORTING_PLAN.md) | Living **code-implementation** plan (P1–P7, sequenced by gameplay phase). Picks up where ROADMAP leaves off. Start here when porting work resumes. |
| [`FLOW_CPP.md`](FLOW_CPP.md) | Authoritative C++ flow. Event sequence from boot to gameplay loop, with mermaid diagrams and `file:line` citations into `recap_server_develop/`. |
| [`FLOW_CSHARP.md`](FLOW_CSHARP.md) | Current C# flow (`ReCap.Server/`). Same phase structure; gaps and deviations annotated. |
| [`PARITY.md`](PARITY.md) | 1-row-per-phase summary index. Phase docs are the source of truth; this is the fast lookup + frozen-rules table + audit log. Collapsed from 79-row format on 2026-05-24. |
| [`ENDIANNESS.md`](ENDIANNESS.md) | BE/LE rules, `Write<T>` wrapper vs raw `BitStream::Write`, per-field truth table. |
| [`REFLECTION_SERIALIZER.md`](REFLECTION_SERIALIZER.md) | bm1 / bm2 / bmID regimes, C++ `reflection_serializer<N>` + C# mirror, worked examples. |
| [`STATE_MACHINE.md`](STATE_MACHINE.md) | All 21 `GameState` values, `IsValidStateChange` graph, C# enum divergence. |
| [`BLAZE_TDF.md`](BLAZE_TDF.md) | Blaze frame header, tag compression, TDF type codes, varint, Map/List/Union framing. |
| [`ASSET_SYSTEM.md`](ASSET_SYSTEM.md) | C# `AssetDatabase` (generic `AssetNode`) replacing C++ `NounDatabase`. |
| [`packets/`](packets/README.md) | Per-opcode wire spec sheets. ~33 Tier-1 sheets authored; ~45 Tier-2/3/4 indexed only. |
| `phases/NN-*.md` | Byte-level deep-dive per phase. Mermaid sequence, packet layout, BE/LE, bitmap sizes, file:line on both sides. |

## Glossary

| Term | Definition |
|---|---|
| **Blaze** | EA's proprietary login/lobby protocol over TCP. Powers auth, matchmaking, telemetry. |
| **TDF** | Tagged Data Format — Blaze payload encoding. |
| **RakNet** | UDP game-networking library (v3.92). Darkspore uses it for real-time state. |
| **Noun** (C++) | Game-object definition (creature, item, marker). C# exposes the same data as `AssetNode` through `AssetData.Parser`. |
| **AssetDatabase** (C#) | Generic system that replaces C++ `NounDatabase`. Reads `AssetData_Binary.package`. |
| **Catalyst** | Equippable crystal/buff (8 slots). |
| **Squad** | Player's deck of three creatures. |
| **Chain** | Co-op progression mode (vote → predungeon → dungeon → cashout → repeat). |
| **dataBits / updateBits** | Bitsets inside `LabsPlayerUpdate` that gate which fields hit the wire each tick. |
| **Reflection serializer** | C++ encoder that emits a presence bitmap followed by present fields. 1 byte for ≤8 fields, 2 bytes BE for 9–16, byte-ID + `0xFF` terminator for >16. |

## Flow phases

| # | Phase | Wire state | Summary |
|---|---|---|---|
| 00 | Boot | — | Server startup (Blaze, RakNet, HTTP), config load, AssetData / NounDatabase warmup. |
| 01 | Redirector | — | Client opens TLS to :42127, receives Blaze lobby address. |
| 02 | Blaze Auth | — | Login on :42125 — Auth / UserSessions / Util components, ticker, session token. |
| 03 | REST Bootstrap | — | HTTP launcher: `/bootstrap/api?method=*`, surveys, asset URLs. |
| 04 | Blaze GameManager | — | CreateGame / JoinGame, NotifyGamePlayerStateChange, RakNet handoff. |
| 05 | RakNet Connect | — | UDP :42000, `ID_NEW_INCOMING_CONNECTION`, `SendConnected`. |
| 06 | Spaceship | `0x02` | `HelloPlayerRequest` → catalysts init → `HelloPlayer` + `PartyMergeComplete` + first LPU. |
| 07 | ChainVoting | `0x0B` | `DebugPing` (Spaceship→ChainVoting), `ChainPlayerMsgs(2)` → `ChainVoteMsgs` (0x151 B LE). |
| 08 | PreDungeon | `0x05` | `ChainPlayerMsgs(6)` (vote) → `PrepareGameStart` + `SetSquad` + `GamePrepareForStart` → `PlayerStatusUpdate` 2/4/8. |
| 09 | Dungeon | `0x06` | `GameStart` + `DebugPing` (Dungeon) → `DirectorState` + `QuickGame` + `OnPlayerStart` + `ObjectCreate` + `PlayerCharacterDeploy`. |
| 10 | Gameplay loop | (active) | `Instance::Update()` 50 ms tick, `SendLabsPlayerUpdate` per-player, `SendGameState` broadcast, action handlers (movement, abilities, loot). |
| 11 | ChainCashOut | `0x0C` | `BeamOut` → `ReconnectPlayer(ChainVoting)` → client jumps to `ChainCashOut` → `DebugPing` → `ChainCashOutMsgs(value=1, CashOutData 0x2C8)`. |
| 12 | GameOver | `0x0D` | Failure lane parallel to 11: party wipe → `ChainGameMsgs(state=1)` → client transitions to `GameOver` → exit to `Spaceship`. Spec only — not exercised in C++ or C#. |
| 13 | Disconnect | — | `Goodbye (0x83)` / `ID_DISCONNECTION_NOTIFICATION` / `ID_CONNECTION_LOST` → `RemoveClient`. `SendPlayerDeparted (0x86)` planned but never invoked. |

## Current bug (2026-05-24)

Two Tier-A stall-grade bugs identified via M1–M5 docs + M4-4 Ghidra static analysis:

1. **HelloPlayer (0x80) body** — C# writes 2 B, client `FUN_00a93d50` requires 8 B (`u8 type, u8 gameplayIndex, u32 BE IPv4, u16 BE Port`). Blocks Phase 06 cleanly entering the gameplay loop.
2. **PreDungeon stall (status=4)** — even if Phase 06 is fixed, `GameStart (0xB1) 1 B vs 5 B` and empty `DebugPing` body block status=4 → status=8 transition.

Fix sequence + verify gates: see [`PORTING_PLAN.md`](PORTING_PLAN.md) P1 and P2.

## Citation conventions

- C++: `Server.cpp:596` resolves to `recap_server_develop/darkspore_server/source/RakNet/Server.cpp` line 596.
- C#: `RakNetServer.cs:NN` resolves to `ReCap/ReCap.Server/Adapters/RakNet/RakNetServer.cs` line NN.
- Endianness: **BE** = big-endian (network / `bswap` wrapper), **LE** = little-endian (`BitStream::Write<T>` raw).
- Bitmap notation: `bm1` = 1 byte, `bm2` = 2 bytes BE, `bmID` = byte-per-field + `0xFF` terminator.
