# PARITY — C++ ↔ C# Phase Summary

One row per phase. **Phase docs are the source of truth** — this file is a fast index into them. For per-handler / per-packet detail, click through to the linked phase doc.

> Collapsed from 79 per-handler rows on 2026-05-24 (ROADMAP M5). Rationale: phase docs already carry per-phase parity tables and were drifting from this top-level. Top-level now points at them instead of duplicating.

Legend:

| Symbol | Meaning |
|---|---|
| ✅ | Phase audited; parity confirmed or divergences frozen by design |
| ⚠️ | Phase partially audited; known divergences logged, not yet fixed |
| ❌ | Phase largely absent on C# side or has known stall-grade bugs |
| 🔒 | Intentionally diverges from C++; do not realign |

---

## Phase summary

| # | Phase | Status | Audit doc | Key divergences / decisions |
|---|---|---|---|---|
| 00 | Boot | ⚠️ | [`phases/00-boot.md`](phases/00-boot.md) | C++ has central `Scheduler` (`Main.cpp:110`) — C# absent. NounDatabase preload at boot in C++ vs lazy `AssetDatabase` in C#. No SIGINT/SIGTERM handler in C#. |
| 01 | Redirector | ⚠️ | [`phases/01-redirector.md`](phases/01-redirector.md) | TLS :42127 both sides; cert chain unverified. ServerInstanceRequest/Reply TDF layout pending. |
| 02 | Blaze Auth | ⚠️ | [`phases/02-blaze-auth.md`](phases/02-blaze-auth.md) | CensusData + Stats components absent in C# (verify client tolerance). TDF byte layout pending capture. PSS/Tick/Telemetry/QoS ports not opened in C# (M4-4 runtime check open). |
| 03 | REST Bootstrap | ⚠️ | [`phases/03-rest-bootstrap.md`](phases/03-rest-bootstrap.md) | api.account.* + api.config.* XML payload schemas pending verification. Static `/web/sporelabsgame/*` served from `resources/static`. |
| 04 | Blaze GameManager | ⚠️ | [`phases/04-blaze-gamemanager.md`](phases/04-blaze-gamemanager.md) | TDF attributes per Create/JoinGame pending. `NETWORK_QOS_DATA` flow unverified. |
| 05 | RakNet Connect | ✅ | [`phases/05-raknet-connect.md`](phases/05-raknet-connect.md) | **M4-1 closed 2026-05-24**: RakNexus wire-compatible w/ RakNet 3.92 protocol 13. MTU 1492, UDP hdr 28, datagram bit-pack identical, RangeList byte-identical, InternalPacket field order incl. split trio matches. Cosmetic: no `*_WITH_ACK_RECEIPT` downconvert + hardcoded 50 B split-margin. Dead code: `DatagramHeader.Serialize/Deserialize`. |
| 06 | Spaceship | ❌ | [`phases/06-spaceship.md`](phases/06-spaceship.md) | **HelloPlayer (0x80) Tier-A stall bug** — C# writes 2 B body; client requires 8 B (`u8 type, u8 gameplayIndex, u32 BE IPv4, u16 BE Port`). M4-4-Item-3 confirmed via Ghidra `FUN_00a93d50`. Initial LPU dataBits 🔒 12 bits `{0,4,5,6,7,8,12,15,16,18,21,22}` (frozen — never add `{3,13,14,17}`). Catalyst constants hardcoded. `UpdateCatalystBonuses` never invoked. |
| 07 | ChainVoting | ✅ | [`phases/07-chainvote.md`](phases/07-chainvote.md) | **M4-2 closed 2026-05-24**: `ChainData` 0x151 buffer byte-by-byte audited. `🔒` LE encoding (C# is canonical wire form). One structural divergence in Branch A: C++ tail-6-u32 at `0xED`, C# at `0xEE` (LE rule says C# wins). `mChainSummary[3].playerIndex` clobber bug present both sides. Defaults: C++ seeds 6 enemies + 2 levelNouns; C# seeds 5 enemies + 0 levelNouns (closed by `PopulateFromLevel` only when `--assetdata-path` provided). |
| 08 | PreDungeon | ❌ | [`phases/08-predungeon.md`](phases/08-predungeon.md) | **Phase 08 stall at `status=4`** — known 4-bug cluster. `GameStart (0xB1)` 1 B vs 5 B (Tier-A). `DebugPing` body empty vs `u64 BE timestamp` (Tier-A). `ObjectivesCompletePacket` writes wrong payload (Tier-A). `GamePrepareForStart` ready-bitfield value pending. Synchronous LPU after vote ✅ applied. |
| 09 | Dungeon | ⚠️ | [`phases/09-dungeon.md`](phases/09-dungeon.md) | **M4-3 closed 2026-05-24**: `ObjectCreate (0x8C)` wire-identical. Reflection bm sizes match (bm2 BE for 10-field createData; bmID + `0xFF` for 23-field sporelabsObject). Rest of phase heavily stubbed: missing Lua ability preload, marker scanning, enemy spawning, objective init, `SwapCharacter(player,1)`, hero force-create. `DirectorState` + `QuickGameMsgs` bodies pending audit. |
| 10 | Gameplay loop | ⚠️ | [`phases/10-gameloop.md`](phases/10-gameloop.md) | 50 ms cadence matches. `GameType=0` 🔒 in loop. `OnCrystalDragMessage` + `OnLootDropMessage` absent in C#. `ObjectManager.Update(dt)` stubbed. |
| 11 | ChainCashOut | ❌ | [`phases/11-chaincashout.md`](phases/11-chaincashout.md) | **M4-4-Item-1 closed 2026-05-24**: 🔒 `ChainCashOut` payload rides wire opcode `0xA9` (ChainVoteMsgs) with `value != 0` — **not 0xAB**. Client `FUN_0053d2c0` subscription table omits both `0x2A` and `0x2C`; both ride `BinaryReader::BindFormatContext`. CashOutData endianness still open (Ghidra GUI xref needed, or runtime once Phase 08 fixed). Entire C# implementation absent: no `BeamOut`, `ReconnectPlayer`, `ChainCashOutMsgs`, `CashOutData`. |
| 12 | GameOver | ❌ | [`phases/12-gameover.md`](phases/12-gameover.md) | `GameState.GameOver = 0x0D` + `Quit = 0x0E` missing from C# `GameplayState.cs`. `ChainGameMsgs (0xAD)` helper exists in C++ but all 4 call sites commented out. No wipe-detection on either side. C# `ChainGameMsgsPacket` / `ChainGameOverMsgsPacket` stubbed in `PacketActivator.cs`, no class. |
| 13 | Disconnect | ⚠️ | [`phases/13-disconnect.md`](phases/13-disconnect.md) | Both sides leak: `RemoveClient` does not call `DetachPlayer`. `Goodbye (0x83)` parser stub on C#; body shape unknown (M4-4-Item-5 open — needs Ghidra GUI). `PlayerDeparted (0x86)` helper exists in C++ but never called. `VoteKickStarted`/`GameAborted` reserved, no impl. Clean SIGINT/SIGTERM absent in C#. |

---

## Known frozen divergences (do not "fix")

| Item | Reason | Reference |
|---|---|---|
| Initial LPU dataBits = 12 (not 16) | Adding `{3,13,14,17}` breaks chain vote | [feedback_initial_lpu_divergence.md] / CLAUDE.md |
| `ChainVoteMsgs` 0x151 buffer = LE | Flipping to BE breaks levels / enemies UI | [feedback_chainvote_le.md] / CLAUDE.md |
| `GameType = 0` inside the loop | `mStateData` is zero-initialised in C++ | [feedback_gamestate_type.md] |
| `LabsPlayerData.DataSetup = false` | `true` triggers demo / capped mode bugs | CLAUDE.md |
| `ChainCashOut` payload uses opcode `0xA9`, not `0xAB` | Client bypasses subscription table for Chain* messages; rides `BindFormatContext` in `cChainCashOutState` | M4-4-Item-1 (2026-05-24) / `phases/11-chaincashout.md` |
| `HelloPlayer (0x80)` wire body = 8 B exactly | Client `FUN_00a93d50` parser reads `u8 type, u8 gameplayIndex, u32 BE IPv4, u16 BE Port` | M4-4-Item-3 (2026-05-24) / `phases/06-spaceship.md` |
| `ChainData` 0x151 buffer tail starts at offset `0xEE` (not `0xED`) in C# | C++ tail at `0xED` via cursor continuation; C# at `0xEE` via hardcoded `tailOffset`. C# offset confirmed canonical via working-client observation | M4-2 (2026-05-24) / `phases/07-chainvote.md` |

---

## Audit log

| Date | Milestone | What landed |
|---|---|---|
| 2026-05-23 | M1 | Phase 12 (GameOver) + Phase 13 (Disconnect) doc closure |
| 2026-05-23 | M2 | Cross-cutting refs: REFLECTION_SERIALIZER, ENDIANNESS, STATE_MACHINE, BLAZE_TDF |
| 2026-05-23 | M3 | Per-opcode packet sheets (Tier-1) |
| 2026-05-24 | M4-1 | RakNexus framing audit ↔ RakNet 3.92 protocol 13 — wire-compatible |
| 2026-05-24 | M4-2 | ChainData 0x151 byte-by-byte diff |
| 2026-05-24 | M4-3 | ObjectCreate dense layout audit |
| 2026-05-24 | M4-4 partial | Items 1 (ChainCashOut opcode) + 3 (HelloPlayer 8B) via Ghidra static analysis |
| 2026-05-24 | M5 | Collapsed top-level PARITY to 1-row-per-phase summary |

---

## Where per-handler detail lives

Each phase doc carries a **Parity table (Phase NN)** section at the end with per-handler rows: `C++ file:line`, `C# file:line`, status, notes. Click the phase doc above to find that table.

If you need to cross-reference packet wire layouts independent of phase, see `packets/README.md` for the Tier-1 opcode sheet index.

---

## How to keep this in sync

When a phase audit closes (or new finding lands), update **two places**:

1. The phase doc's own parity table (source of truth).
2. The relevant row above — bump status, add divergence note one-liner. Do not duplicate per-handler detail here.

Add a new row to the **Audit log** if the milestone changed.
