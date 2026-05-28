# ReCap Architecture Docs

Detailed flow documentation for the ReCap server (C#) versus the C++ reference implementation (`dalkon/darkspore_server`). The C++ tree is ground truth; every divergence is annotated explicitly.

> **Status:** skeleton — phases will be deepened iteratively.

## Layout

Docs are grouped into five buckets plus two self-contained subsystems:

```
architecture/
├── planning/          living project-management plans + open-question register
├── protocol/          stable wire specs + packets/ + components/ + http/
├── flow/              C++ vs C# event flow + phases/ byte-level deep-dives
├── research/          reverse-engineering notes + primary-source dev testimony
├── data-model/        SporeNet ↔ C# entity sheets (self-contained)
└── assetdata-system/  Asset/AssetData runtime (self-contained)
```

## planning/ — what to do next

| Document | Purpose |
|---|---|
| [`ROADMAP.md`](planning/ROADMAP.md) | Living **documentation** plan (M1–M6 milestones). ✅ M1–M5 closed 2026-05-24. |
| [`PORTING_PLAN.md`](planning/PORTING_PLAN.md) | Living **code-implementation** plan (P1–P7, sequenced by gameplay phase). Picks up where ROADMAP leaves off. Start here when porting work resumes. |
| [`PORTING_MATRIX.md`](planning/PORTING_MATRIX.md) | **Macro parity index** — every C++ module class-by-class vs C#, status ✅/⚠️/❌. The "what's missing / what's wrong" map. Headline: Game module ~25% ported. |
| [`CORRECTION_PLAN.md`](planning/CORRECTION_PLAN.md) | **Umbrella remediation plan** — organizes every gap into prioritized workstreams (WS-0…WS-6), single-player-first scope, with a deferral register. Start here to decide *what to fix next*. |
| [`ARCHITECTURE_OPEN_QUESTIONS.md`](planning/ARCHITECTURE_OPEN_QUESTIONS.md) | Consolidated register of every unresolved architecture question + how to answer each (Ghidra / decision). |

## protocol/ — wire specs

| Document | Purpose |
|---|---|
| [`ENDIANNESS.md`](protocol/ENDIANNESS.md) | BE/LE rules, `Write<T>` wrapper vs raw `BitStream::Write`, per-field truth table. |
| [`REFLECTION_SERIALIZER.md`](protocol/REFLECTION_SERIALIZER.md) | bm1 / bm2 / bmID regimes, C++ `reflection_serializer<N>` + C# mirror, worked examples. |
| [`STATE_MACHINE.md`](protocol/STATE_MACHINE.md) | All 21 `GameState` values, `IsValidStateChange` graph, C# enum divergence. |
| [`BLAZE_TDF.md`](protocol/BLAZE_TDF.md) | Blaze frame header, tag compression, TDF type codes, varint, Map/List/Union framing. |
| [`packets/`](protocol/packets/README.md) | Per-opcode wire spec sheets. ~33 Tier-1 sheets authored; ~45 Tier-2/3/4 indexed only. |
| [`components/`](protocol/components/README.md) | Per-Blaze-component RPC sheets (13). Command-level parity: cmd ID, direction, C++/C# handler, status. |
| [`http/`](protocol/http/README.md) | REST endpoint catalog (5 route groups). Per-route C++/C# handler + status + response shapes. |

## flow/ — event sequence

| Document | Purpose |
|---|---|
| [`FLOW_CPP.md`](flow/FLOW_CPP.md) | Authoritative C++ flow. Event sequence from boot to gameplay loop, with mermaid diagrams and `file:line` citations into `recap_server_develop/`. |
| [`FLOW_CSHARP.md`](flow/FLOW_CSHARP.md) | Current C# flow (`ReCap.Server/`). Same phase structure; gaps and deviations annotated. |
| [`PARITY.md`](flow/PARITY.md) | 1-row-per-phase summary index. Phase docs are the source of truth; this is the fast lookup + frozen-rules table + audit log. Collapsed from 79-row format on 2026-05-24. |
| [`phases/NN-*.md`](flow/phases/00-boot.md) | Byte-level deep-dive per phase. Mermaid sequence, packet layout, BE/LE, bitmap sizes, file:line on both sides. |

## research/ — reverse-engineering & testimony

| Document | Purpose |
|---|---|
| [`SIMULATION_MAP.md`](research/SIMULATION_MAP.md) | Living Ghidra map of the client `n*` Simulation subsystem (combat / AI / objects / abilities) — the dominant gameplay gap. |
| [`CONSOLE_SYSTEM.md`](research/CONSOLE_SYSTEM.md) | `ConsoleClient` = one end of a generic remote command-console built into the retail client, mapped from Ghidra. |
| [`ORIGINAL_SOURCE_TREE.md`](research/ORIGINAL_SOURCE_TREE.md) | Original Darkspore (codename **SporeLabs**) source module layout from a foehammer screenshot, cross-validated against Ghidra namespaces. |
| [`DEV_TESTIMONY_FOEHAMMER.md`](research/DEV_TESTIMONY_FOEHAMMER.md) | Primary-source testimony from foehammer (David Lee Swenson, Darkspore lead engineer): UI stacks, RakNet+Blaze, `.noun`=property system, FNV constants, bmdl/bskl/banm model format, CLI switches, locale. |
| [`DEV_TESTIMONY_XACKERY.md`](research/DEV_TESTIMONY_XACKERY.md) | Xackery research notes + EAWebKit redirection (modded `EAWebKit.dll` Detours hooks, native port map). |

## Self-contained subsystems

| Document | Purpose |
|---|---|
| [`data-model/`](data-model/README.md) | SporeNet ↔ C# entity sheets (11). Field-level parity + persistence (EF Core/SQLite). |
| [`assetdata-system/`](assetdata-system/) | Asset/AssetData runtime: `ASSET_SYSTEM.md` (C# redesign plan), `GHIDRA_GROUND_TRUTH.md` (client runtime reverse-engineered from `Darkspore.exe`), `FORMAT_COVERAGE.md` (per-format C++↔C# porting matrix). |

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

## Tier-A stall bugs — FIXED 2026-05-27

Two Tier-A stall-grade bugs identified via M1–M5 docs + M4-4 Ghidra static analysis, now resolved (verified against C++ `Server.cpp` `SendHelloPlayer`/`SendGameStart`/`SendDebugPing` + a real packet capture):

1. **HelloPlayer (0x80) body** — ✅ already 8 B (`u8 type, u8 gameplayIndex, u32 IPv4, u16 Port`). Port write aligned to BE to match C++ `Write<uint16>`. `HelloPlayerPacket.cs`.
2. **PreDungeon stall (status=4)** — ✅ `GameStart (0xB1)` now writes `u32 BE LevelIndex` (`Chain.LevelIndex`, was 1-byte stub); `DebugPing (0xCC)` now writes `u64 BE` unix time (was empty `WriteTo`). `GameStartPacket.cs`, `DebugPingPacket.cs`, `Game.cs` status=8 path.

Remaining P1 item: `UpdateCatalystBonuses` after `SetCatalyst×8` (not a stall blocker). Fix sequence + verify gates: see [`PORTING_PLAN.md`](planning/PORTING_PLAN.md) P1 and P2.

## Citation conventions

- C++: `Server.cpp:596` resolves to `C:\CodingProjects\Personal\ReCapCpp\darkspore_server\source\RakNet\Server.cpp` line 596.
- C#: `RakNetServer.cs:NN` resolves to `ReCap.Server/Adapters/RakNet/RakNetServer.cs` line NN.
- Endianness: **BE** = big-endian (network / `bswap` wrapper), **LE** = little-endian (`BitStream::Write<T>` raw).
- Bitmap notation: `bm1` = 1 byte, `bm2` = 2 bytes BE, `bmID` = byte-per-field + `0xFF` terminator.
