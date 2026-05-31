# Porting Plan — C++ → C#

Sequenced plan for porting Darkspore C++ gameplay server to ReCap C#. Born **2026-05-24** at the end of the M1–M5 docs milestone; supersedes the 2026-04-14 `project_evolution_plan` memory (now a pointer to this file).

> **Difference vs ROADMAP:** ROADMAP tracks documentation milestones (✅ closed). This file tracks **code implementation** milestones. Both checkbox-driven, both updated in-same-turn when a sub-item lands.
>
> **Umbrella:** [`CORRECTION_PLAN.md`](CORRECTION_PLAN.md) organizes ALL gaps (Blaze/HTTP/data-model/Core + this gameplay path) into workstreams under a single-player-first scope. This file (P1–P7) IS workstream WS-1 of that plan. P4 (combat loop) is deferred there as `systems/`.

---

## Working principles (carry across sessions)

1. **Gameplay-driven sequencing.** Each P must work end-to-end before moving on. Don't open a later P until current P's verify gate passes. Otherwise bugs stack and the cause is unobservable.
2. **Phase doc + protocol-bugs are the source of truth.** Every code change cites a phase doc section or a Tier-A/B/C/D entry. If neither exists, write the doc first.
3. **Frozen rules are inviolable.** ~~See "Frozen rules — never realign" in `PARITY.md`.~~ See `VERIFIED_FACTS.md`. Past sessions broke them by aligning to C++ and lost a week each time. Note: bit 3 in `SetInitialDataBits` is no longer frozen — see `VERIFIED_FACTS.md`.

> ⚠️ SUPERSEDED 2026-05-31 — see VERIFIED_FACTS.md (PARITY.md deleted; bit-3 FROZEN rule disproved)
4. **Verify gate before commit.** Each P closes with a runtime check (output.log capture + manual client run) AND a logged observation in this file under the relevant P. Code that "builds" is not closed.
5. **C++ Write&lt;T&gt;() = WriteLE in C#.** ~~WriteBE~~ All numeric game data uses `Write<T>()` in C++ (bswap wrapper) which produces **little-endian** on the wire (double-swap on x86). Use `writer.Write()` (LE) in C#. Endianness details in `VERIFIED_FACTS.md`. ~~ENDIANNESS.md deleted.~~

> ⚠️ SUPERSEDED 2026-05-31 — see VERIFIED_FACTS.md (Write&lt;T&gt; is LE on wire, not BE; ENDIANNESS.md deleted)
6. **One commit per Tier-A fix** so bisect works when something regresses.

---

## P1 — Phase 06 unblock (Spaceship)

Goal: client passes the Spaceship loading screen and renders the catalyst UI.

### Prerequisites

- ✅ M4-1 RakNet framing audit (Phase 05) — wire-compatible.
- ✅ M4-4-Item-3 HelloPlayer 8-byte body confirmed via Ghidra `FUN_00a93d50`.

### Tier-A code fixes

- [ ] **HelloPlayer (0x80) body — write 8 B exactly.** `Adapters/RakNet/Packets/HelloPlayerPacket.cs` currently writes 2 B; must write `u8 type, u8 gameplayIndex, u32 BE IPv4, u16 BE Port` derived from the session's `RemoteEndPoint`. Confirmed Tier-A stall-grade bug (`project-protocol-bugs.md` row `HP`). See `phases/06-spaceship.md`.

### Missing impls

- [ ] **`UpdateCatalystBonuses` after `SetCatalyst × 8`.** Currently never invoked in C#. Must set Player dataBit 14 + PlayerBits in `updateBits` (mirror C++ `Player.cpp:515-537`). Required for client to render the catalyst row correctly when re-entering Spaceship after cashout (Phase 11). Cosmetic in P1, but cheap to land while in this file.

### Verify gate

- [ ] Run server (`dotnet run --project ReCap.Server -- --port=9000`).
- [ ] Run client, complete auth, reach Spaceship.
- [ ] Observe `output.log` shows `0x80` packet with body length **8**.
- [ ] Client renders 8 catalyst slots + lobby UI. No "loading" stuck state.
- [ ] Update this row with date + capture filename.

### Notes / past divergences

- Initial LPU `dataBits = {0,3,4,5,6,7,8,12,15,16,18,21,22}` (13 bits — bit 3 re-added 2026-05-31, confirmed safe). Adding `{13,14,17}` not yet tested. See `VERIFIED_FACTS.md`.

> ⚠️ SUPERSEDED 2026-05-31 — see VERIFIED_FACTS.md (bit 3 is safe, vote still fires)
- 🔒 `DataSetup = false` always.
- 🔒 `GameType = 0` in 50 ms tick.
- C# server attaches Game on first `HelloPlayerRequest`; before that, no Game exists for the session.

---

## P2 — Phase 08 unblock (PreDungeon stall status=4 → 8)

Goal: client transitions `PlayerStatusUpdate` from `status=4` (loading) to `status=8` (loaded). Server flips wire state to Dungeon (`0x06`).

### Prerequisites

- P1 verify gate passed (otherwise the client never reaches Phase 08).
- ✅ M4-2 ChainData diff (Phase 07) — buffer is correct on the wire.

### Tier-A code fixes

- [ ] **GameStart (0xB1) body — 1 B → 5 B.** `Adapters/RakNet/Packets/GameStartPacket.cs:17` currently writes `u8 Unk1`. Must write `u32 BE levelIndex` sourced from `Chain.LevelIndex`. Caller in `Game.cs:351` currently hardcodes `0` — replace with `Chain.LevelIndex`. Tier-A row 1.
- [ ] **DebugPing (0xCC) body — empty → 8 B.** `Adapters/RakNet/Packets/DebugPingPacket.cs` writes 0 bytes; C++ writes `u64 BE timestamp`. Add `Timestamp (u64 BE)` field; server-side set to `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()`. Tier-A row 2.
  - **Verify-before-fix option:** before writing the fix, run server + client; hexdump first inbound DebugPing in `output.log`. Confirm client appended `u64 timestamp` after the opcode byte. (Closes M4-4-Item-6 in passing.)

### Missing impls

- [ ] **`GamePrepareForStart` ready-bitfield value.** Currently `1`. C++ comment hints "before this updates its set to 8…". Confirm value via capture if status=8 still stalls after the two Tier-A fixes. See `phases/08-predungeon.md`.

### Verify gate

- [ ] Run server + client. Pick a planet, vote (squad button).
- [ ] Observe sequence in `output.log`:
  - `0xAC` ChainPlayerMsgs (byteCount=6, SquadId BE)
  - `0xB0` GamePrepareForStart (17 B body)
  - `0xA1` LabsPlayerUpdate (Player + 3× Character reflection)
  - `0x88` PlayerStatusUpdate status=2, 4, **8**
  - `0xB1` GameStart (5 B body, levelIndex matches `Chain.Level`)
  - `0xCC` DebugPing (9 B body, BE timestamp)
- [ ] Client renders dungeon load screen and starts streaming markers.
- [ ] Update row with date + capture filename.

### Notes

- 🔒 `ChainVoteMsgs` 0x151 buffer is LE — never flip to BE.
- 🔒 `ChainPlayerMsgs(byteCount=6)` SquadId is BE.

---

## P3 — Phase 09 fill (Dungeon entry + hero deploy)

Goal: hero is visible in the dungeon; client receives the right markers and director state.

### Prerequisites

- P2 verify gate passed (client reaches Dungeon state).
- ✅ M4-3 ObjectCreate dense layout audit — `WriteReflection` paths wire-identical.

### Tier-A code fixes (none — Phase 09 stubs need impl, not bugfix)

### Missing impls

- [ ] **`SwapCharacter(player, 1)` after `OnPlayerStart`.** Mirror `Instance::SwapCharacter` (`Instance.cpp:533+`). Sets `Player.mCurrentDeckIndex = 1`; emits LPU with dataBit `1`, `updateBits |= PlayerBits`. Without this, the LPU never confirms the deck index and the client cannot swap creatures. See `phases/09-dungeon.md` parity row "`SwapCharacter`".
- [ ] **Hero `ObjectCreate` for all 3 squad characters + `ObjectUpdate × 3`.** Currently C# spawns only the deployed character; client errors when trying to swap to an unseen creature. Force-create all three in `OnPlayerStart`. See `phases/09-dungeon.md` parity row "Hero ObjectCreate + ObjectUpdate × 3".
- [ ] **`DirectorState (0x8B)` body audit.** Mirror `cAIDirector::WriteTo`. Body is small (~16 B): boss=0, bbBossSpawned=false. C# `DirectorStatePacket.cs` body pending verification.
- [ ] **`QuickGameMsgs (0xAF)` body audit.** Same — body shape pending verification.
- [ ] **`PlayerStatusUpdate` status=0x20 (BeamOut) wired.** Currently `Game.HandlePlayerStatusUpdate` only handles `status=0x08`. Route 0x20 to `Game.BeamOut(sender)` (the method itself lands in P5). Tier-C row 8.

### Verify gate

- [ ] Client renders dungeon environment, sees marker NPCs, and lands hero at spawn marker.
- [ ] `output.log` shows: `0x8B DirectorState`, `0xAF QuickGameMsgs`, `0xB7 ObjectivesInitForLevel`, `0xB8 ObjectiveUpdated × N`, `0x8C ObjectCreate × N`, `0xA7 PlayerCharacterDeploy`, then `0xA1 LabsPlayerUpdate` (small, dataBit 1 set from SwapCharacter).
- [ ] Manually press the character-swap key in client; verify the second character of the squad appears.
- [ ] Update row with date + capture filename.

### Notes

- 🔒 ObjectCreate uses `WriteReflection` (not dense `WriteTo`). Reflection sizes: bm2 BE for createData (10 fields), bmID + `0xFF` for sporelabsObject (23 fields).
- Lua ability preload + real enemy spawning are **out of scope** for P3 — they belong to P4.

---

## P4 — Phase 10 gameplay loop (combat baseline)

Goal: player can move, attack, take damage, and pick up loot. Server is authoritative on positions and HP.

### Prerequisites

- P3 verify gate passed (hero in dungeon, markers spawned).

### Tier-A code fixes (none yet identified)

### Missing impls

- [ ] **`ObjectManager.Update(dt)` body.** Currently stubbed. Mirror `Instance.cpp:452`: drive AI ticks, locomotion, ability cooldowns. Spawn enemies from wanderer markers (out of scope here — track separately).
- [ ] **`OnCrystalDragMessage`.** C++ `Server.cpp:1024` handles client drag-and-drop of catalysts onto creature slots. Absent in C#. Tier-C row "Action: `OnCrystalDragMessage`" in PARITY pre-collapse.
- [ ] **`OnLootDropMessage`.** C++ `Server.cpp:1091`. Absent in C#.
- [ ] **`LocomotionData` update.** `phases/10-gameloop.md` parity row pending.
- [ ] **`MoveObject`** broadcast for multiplayer prep — verify single-player path works first.
- [ ] **`UseAbility`** dispatch.

### Verify gate

- [ ] Player can walk through the dungeon (`0x9C ActionCommandMsgs` → `0x91 ObjectPlayerMove`).
- [ ] Player can attack a marker NPC (`0x9C` with combat action → `0xA8 ActionCommandResponse` → `0x96 AttributeDataUpdate` to drop HP).
- [ ] Pick up a loot crystal: `0x8E ObjectDelete` for the dropped object + `0x96 AttributeDataUpdate` for resource gain.
- [ ] Update row with date + capture filename.

### Notes

- This is the largest P. Expect to split into P4a/P4b/P4c as sub-steps land.
- Lua scripting is the deep end (`LuaFunctions.cpp`) — defer ability scripts until P4 baseline works without them.

---

## P5 — Phase 11 ChainCashOut (terminal phase, success lane)

Goal: completing a dungeon shows the cashout UI with medals/DNA/drops, then sends the player back to ChainVoting for the next mission.

### Prerequisites

- P4 verify gate passed (dungeon clearable end-to-end).
- ✅ M4-4-Item-1 confirms 🔒 wire opcode for cashout = `0xA9`, not `0xAB`.

### Tier-A code fixes

- [ ] **`ObjectivesCompletePacket (0xB9)` body — rewrite.** Currently emits raw 712-B `CashOutData` instead of `count + per-objective + medals u32`. Move the CashOutData payload to a dedicated cashout helper. Tier-A row 3. See `phases/11-chaincashout.md`.

### Missing impls

- [ ] **`Domain/Gameplay/CashOutData.cs` class** — 0x2C8 (712-byte) buffer with absolute-offset writes per `Instance.cpp:30-52`:
  - `0x00`: `u32 planetsCompleted, u32 dna`
  - `0x34`: `u32[4] gold, u32[4] silver, u32[4] bronze`
  - `0x64`: `u32[4] uniqueChances, u32[4] rareChances`
  - `0x84..0x2C7`: zero pad
- [ ] **`Game.BeamOut(player)` method** — mirror `Instance::BeamOut` (`Instance.cpp:856-880`): set `Chain.Completed=true`, `Chain.Progression=1`, fill default `CashOutData`, send `SendReconnectPlayer(client, ChainVoting)` + `SendDebugPing(client)`.
- [ ] **`ReconnectPlayerPacket (0x81)` class** — 5-byte body: `PacketID(1) + u32 BE newState`. Tier-C row 12.
- [ ] **`Game.HandleDebugPing` ChainCashOut arm** — `sender.SendPacket(new ChainVoteMsgsPacket { Value = 1, CashOutData = ... })`. Note the wire opcode is `0xA9` (ChainVoteMsgs) — that's intentional per M4-4-Item-1 frozen rule. Tier-C row 9.
- [ ] **`ChainVoteMsgsPacket` extension** — currently handles `Value = 0` (vote blob), `Value = 1` (deployment countdown), `Value = 2` (stayInParty). Need an extra branch where `Value = 1` (or whatever value the cashout flow uses) carries the 712-byte `CashOutData` instead of the 4-byte countdown float. **Audit value-byte branching carefully** before changing — the Phase 07 path uses `Value = 1` for the 30-second countdown. Likely the cashout uses a *different* `Value` that's still inside the same `0xA9` wire opcode.

### Open audit items (M4-4 remainder)

- [ ] **CashOutData endianness on the wire** (M4-4-Item-2). Two paths:
  - Ghidra GUI xref on string `"SP_SporeLabs/cChainCashOutState"` (address `0x00fd8778`) to locate the `BindFormatContext` reader, decompile, see if it bswaps.
  - Runtime capture once P5 is wired up — send a known-pattern payload, observe client behaviour. If medals show as garbage, flip endianness.

### Verify gate

- [ ] Clear a dungeon. Client transitions to cashout screen showing DNA / medals / drop chances.
- [ ] Press "next planet" — client returns to ChainVoting state. Next vote works as in P2.
- [ ] `output.log` shows: `0xB9 ObjectivesComplete` (new payload), `0x81 ReconnectPlayer (newState=ChainVoting)`, `0xCC DebugPing`, then `0xA9 ChainVoteMsgs (Value=N, CashOutData blob)`.
- [ ] Update row with date + capture filename.

### Notes

- 🔒 Wire opcode is `0xA9`, **not** `0xAB`. Past sessions almost merged `0xAB` thinking it was a C++ bug.
- 🔒 `mChainSummary[3].playerIndex` is clobbered to 0 by the party-override write at offset `0xD9` — both sides do this. Don't "fix" it; client tolerates.

---

## P6 — Phase 12 GameOver (failure lane, low priority)

Goal: party wipe triggers a clean transition back to Spaceship without leaking server state.

### Prerequisites

- P5 working (success lane proves the state-machine plumbing).
- M4-4-Item-5 (`Goodbye` body) and `ChainGameOverMsgs (0xAE)` body **blocked on Ghidra GUI capture** — defer until those land.

### Missing impls

- [ ] **`GameState.GameOver = 0x0D` + `Quit = 0x0E`** in `Domain/Gameplay/GameplayState.cs`. Today both are absent.
- [ ] **`ChainGameMsgsPacket (0xAD)` class** — 2 B body: `u8 state` (0=fade/return-to-vote, 1=mission failed, 2=observer noop). Helper exists in C++ (`Server.cpp:2334-2348`) but all 4 call sites are commented out — confirm via capture whether the client requires this packet today.
- [ ] **`ChainGameOverMsgsPacket (0xAE)` class** — body unknown. Blocked.
- [ ] **Wipe detection trigger** — server-side party-HP poll. Absent on both sides.

### Verify gate

- [ ] Manually kill all party members in client; verify clean transition without server crash.
- [ ] Update row.

### Notes

- This is **post-fun**. Players completing missions matters more than handling wipes cleanly. Land after P5.

---

## P7 — Phase 13 Disconnect (cleanup, low priority)

Goal: clean RakNet shutdown, no leaked sessions or games.

### Prerequisites

- P5 working (player can complete a session and naturally disconnect).
- M4-4-Item-5 (`Goodbye` body) **blocked on Ghidra**.

### Missing impls

- [ ] **`Game.OnSessionDisconnected` detaches player.** Today `RakNetServer.OnSessionDisconnected` removes the session from `Clients` dict but doesn't tell the `Game` to drop the player. Add `game.DetachPlayer(client)` + impl in `GameService`. Tier-C row 20. See `phases/13-disconnect.md`.
- [ ] **`PlayerDepartedPacket (0x86)` class** — 1-byte body: `u8 mId`. Broadcast to remaining players when one leaves. Tier-C row 18.
- [ ] **`GoodbyePacket (0x83)` parser class** — body shape unknown. Blocked on Ghidra.
- [ ] **SIGINT / SIGTERM handler** in `Program.cs` — clean ordered shutdown chain: drain RakNet sends, broadcast disconnect, close SQLite `DbContext`. Today the process is just killed. Tier-D row 27.
- [ ] **`Instance::RemovePlayer` C# equivalent** in `GameService`. Today no remove API.

### Verify gate

- [ ] Kill client mid-dungeon. Confirm server logs cleanup, no stale entries in `Clients` dict, no orphan Game in `GameService`.
- [ ] Trigger `Ctrl+C` on server; confirm clean shutdown sequence (no half-written SQLite, no leftover RakNet sessions).
- [ ] Update row.

---

## Cross-cutting items (any time)

Not phase-sequenced. Land alongside whichever P touches them.

### Tier-D architectural

- [ ] **Re-align C# `GameState` enum** to byte values matching C++ `Client.h:14-37`. Today 7 values, auto-numbered; should be 21 values explicit `= 0xNN`. Currently masked by `GameStatePacket.WireState` switch — risky if any new state slips through unmapped. Tier-D row 22.
- [ ] **`IsValidStateChange` helper** mirroring `Client.cpp:12-49`. Wire all `State = X` assignments in `Game.cs` through it. Tier-D row 23.
- [ ] **TDF varint sign-bit asymmetry.** Encoder/decoder pick different conventions for negatives. Latent until signed-varint crosses the wire. Tier-D row 24.
- [ ] **Add `Scheduler` equivalent** (Tier-D row 25). Only needed if delayed-task gameplay features land.

### Aux Blaze ports (M4-4-Item-4)

- [ ] **Confirm client survives without PSS/Tick/Telemetry/QoS/HTTP-Telemetry/HTTP-QoS ports.** Run client against current C# server without those ports open. If client stalls at any specific auth step, log it. Implement only the ports that block startup. Tier-D row 26.

### Tier-E dead code (no fix needed)

Documented for awareness; touching these is pure busywork unless multi-player lands:
- `SendPlayerJoined (0x84)` — zero call sites
- `SendPlayerDeparted (0x86)` — zero call sites (until P7)
- `SendChainGame (0xAD)` — all call sites commented out (until P6)
- `ChainLevelResultsMsgs (0xAA)`, `ChainGameOverMsgs (0xAE)` — declared, no helper
- `VoteKickStarted (0x87)`, `GameAborted (0x89)` — declared, no helper

---

## Frozen rules — never realign

These intentionally diverge from C++. Past sessions broke them by "aligning to C++":

| Rule | Where |
|---|---|
| Initial LPU `dataBits = {0,3,4,5,6,7,8,12,15,16,18,21,22}` (13 bits) — bit 3 safe (verified 2026-05-31); `{13,14,17}` not yet tested | `VERIFIED_FACTS.md`, `phases/06-spaceship.md` |

> ⚠️ SUPERSEDED 2026-05-31 — see VERIFIED_FACTS.md (bit 3 no longer frozen)
| `ChainVoteMsgs` 0x151 buffer = LE | `feedback_chainvote_le.md`, `phases/07-chainvote.md` |
| `GameType = 0` inside 50 ms tick | `feedback_gamestate_type.md` |
| `LabsPlayerData.DataSetup = false` always | `CLAUDE.md` |
| `ChainCashOut` payload rides wire opcode `0xA9` (not `0xAB`); value byte distinguishes vote vs cashout | M4-4-Item-1, `phases/11-chaincashout.md` |
| `HelloPlayer (0x80)` wire body = 8 B exactly | M4-4-Item-3, `phases/06-spaceship.md` |
| `ChainData` tail-6-u32 starts at offset `0xEE` (not `0xED`) | M4-2, `phases/07-chainvote.md` |

---

## Done log

Append-only. One line per closed P or cross-cutting item.

- 2026-05-24 — `PORTING_PLAN.md` authored. Skeleton seeded from M1-M5 audits.

---

## How to use this file

1. Pick the **lowest open P** — never skip ahead.
2. Within that P: complete all Tier-A fixes, then missing impls.
3. Run the Verify gate. Capture `output.log` to `ReCap.Tests/captures/PN-YYYYMMDD.log`.
4. Tick the boxes and add a one-line note in the **Done log** at the bottom of this file.
5. If the verify gate fails: do not move on. Diagnose, file a new Tier-A entry in `project-protocol-bugs.md` if needed, fix, retry.
6. Update CLAUDE.md and the relevant phase doc if the fix reveals a new constraint.
