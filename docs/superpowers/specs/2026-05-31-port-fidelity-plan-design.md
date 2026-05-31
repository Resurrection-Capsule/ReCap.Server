# Port-Fidelity Plan — design spec

**Date:** 2026-05-31
**Goal:** Reach 1:1 byte-level fidelity between the C# port (`ReCap.Server`) and the C++ ground truth (`ReCapCpp`) for the **single-player Dungeon path**, by enumerating and fixing divergences system-by-system — driven by evidence (C++ source + wire capture), not by the project's past "laws" (which have proven stale).

---

## 1. Target end-state (north star)

**Terminal goal: C# becomes the sole development line.** ReCap.Server (C#) implements *everything the C++ reference does*, verified, so the C++ tree is retired to frozen reference/documentation and all future development (improvements, fixes, eventually features C++ never had) happens exclusively in C#.

"Done" is measurable: `PORTING_MATRIX.md` driven to 100% ✅ (or explicit N/A) + `DIVERGENCE_LEDGER.md` empty + the retail client plays. The **C++ feature set is the definition of "done"** — not an abstract spec.

### Milestone ladder (scales from playable to full parity)

- **M1 — Dungeon single-player path, 1:1 byte-level.** `login → lobby → REST account → HelloPlayer → Spaceship → ChainVoting → vote → PreDungeon → Dungeon entry → frame loop`. Proves the methodology and builds the golden-test harness. Success: client reaches and plays a Dungeon, each wire step matching C++ (modulo session-specific values).
- **M2 — Lua + simulation/combat engine.** The **largest block** (PORTING_MATRIX: Game module ~75% missing): ObjectManager, Attributes/combat math, ability/objective system, AI, loot. **Parity here means client-observable behavior, NOT copying the C++ implementation.** The C++ approach relies on dalkon's *decompiled-and-renamed* Lua scripts — we explicitly do **not** commit to that. Open architecture decision, deferred to M2 kickoff (analyze the system in Ghidra first): a strong candidate is running the game's **original compiled Lua** and making it functional, leaving decompiled scripts for a future modding layer. So M2 matches the engine's *behavior* up to the C++ frontier (currently `UseCharacterAbility → abilityId=0` unresolved), but the *how* is free to diverge and will be designed when M2 starts.
- **M3 — Single-player completion** (cashout, gameover, disconnect, beamout/reconnect) + polish.
- **M4 — Parity certification + C++ retirement.** Systematic audit that C# ≥ C++ for every module; harvest the full golden-capture set; freeze C++ as reference; development continues C#-only.
- **Multiplayer: genuinely deferred** — the C++ reference does not implement real multiplayer yet, so it is not part of parity. It becomes new C#-only work *after* M4.

---

## 2. Phase 0 — Hard reset (clean baseline)

The past "laws" caused regressions because they were asserted without evidence and then treated as inviolable. This session disproved several (see §7). Phase 0 wipes the dogma and reseeds only what is proven.

**Delete / strip the dogma:**
- Remove the **FROZEN VALUES** section from `CLAUDE.md` and the "C++ Gameplay Flow" assertions that are protocol *claims* (keep only the genuinely useful "how to build/run/work" guidance).
- Delete or gut the "laws" docs: `PARITY.md`/`ENDIANNESS.md`-style rule files and the rigid principles in `PORTING_PLAN.md` (e.g. "Write<T> = WriteBE", "Frozen rules inviolable").
- Prune project skills/agents that encode wrong premises: retire `lpu-diff` and `phase`; keep `cpp-trace`/`cpp-ref`/`recap-cpp-tracer` but as **pure search/locate** tools with no embedded protocol dogma; keep `build`/`run-dev`.

**Reseed the truth (`docs/architecture/VERIFIED_FACTS.md`):** a short doc containing ONLY evidence-backed facts, each citing a C++ `file:line` or a capture frame. Initial contents (from this session):
- `Write<T>` wrapper → **little-endian** on the wire (double bswap), not BE.
- RakNexus delivers correctly (sequential datagrams, client ACKs, split-packet reassembly); only divergence is the optional B&AS congestion bit (0x84 vs 0x80) — harmless.
- `Character::WriteTo` is a fixed **0x620** block; offsets verified 1:1 (AssetId@0x008, Version@0x010, NounId@0x0B4, partsAttributes base 0x0B8 stride 4, CreatureType@0x3B8, DeployCooldown@0x3C0, Health…@0x3F0). Bytes 0x000–0x007 are zero (not written).
- C++ ships 3 (default) Character blocks in the HelloPlayer LPU via player dataBit 3; **adding bit 3 does NOT break chain vote** (disproves FROZEN).
- The in-game deck HUD (`cPlayerDeck::UpdateHud` @0x51b860) is **game-state-driven** (reads creature vector from singleton `DAT_0143ffd8+0x710`), not REST; crash = Scaleform movie handle `cPlayerDeck+0x20` null.
- `ChainVoteMsgs` 0x151 buffer is LE (re-verify before trusting).

Outcome: `CLAUDE.md` becomes a lean working guide; all protocol truth lives in `VERIFIED_FACTS.md` + the ledger, each line with a citation.

---

## 3. The DIVERGENCE_LEDGER (living artifact)

`docs/architecture/planning/DIVERGENCE_LEDGER.md` — one row per field/packet-level divergence:

| ID | System | Flow step | C++ ref (file:line) | C# ref (file:line) | Divergence | Status | Commit |
|----|--------|-----------|---------------------|---------------------|------------|--------|--------|

- **Status:** `open → wire-confirmed → fixed → verified`.
- **Indexing:** `PORTING_MATRIX.md` stays the macro index (class/handler level); the ledger is the micro level (field/byte). Cross-link both ways.
- Each `fixed` item is exactly **one commit** (bisect-friendly). The verify gate (§6) promotes it to `verified`.

---

## 4. Detection loop (hybrid method)

Per flow step (§5), in order:

1. **Source-diff (sweep)** — compare the C# handler/`WriteTo` against C++ source field-by-field (via `recap-cpp-tracer`). File every divergence in the ledger as `open`.
2. **Wire-diff (confirm)** — capture C++ vs C# for that step (dumpcap on Npcap Loopback; game conv only), byte-diff; catch anything source-reading missed; mark `wire-confirmed`.
3. **Fix** — one divergence, one commit.
4. **Verify gate (§6)** — wire-parity for that step AND client advances → `verified`.
5. **Ghidra debugger (pontual)** — only when a step stalls with no obvious wire divergence (e.g. a null client-side bind), attach and inspect runtime state.

---

## 5. Critical-path walk order

Walk in sequence; a step opens only after the previous passes its verify gate. (The deck-HUD crash is insensitive to gameplay data, so steps 1–3 — never wire-diffed yet — are prime suspects.)

1. **Login/Blaze** — redirector → lobby → auth → REST token
2. **Lobby** — Rooms (joinRoom), GameManager (resetDedicatedServer / NotifyGameSetup / finalizeGameCreation)
3. **REST account** — `api.account.getAccount` (decks+creatures), `getPartList` — *prime suspect for the deck-HUD null*
4. **RakNet connect** → HelloPlayer (0x80) → PartyMergeComplete
5. **Hello LPU** — player + 3 characters + catalysts
6. **Spaceship → ChainVoting** (DebugPing)
7. **ChainVoting** — ChainPlayerMsgs(2) → ChainVoteMsgs 0x151 blob
8. **Vote → PreDungeon** — ChainPlayerMsgs(6) → PrepareGameStart + SetSquad LPU
9. **PreDungeon** — PlayerStatusUpdate 2/4/8 → GameStart
10. **Dungeon entry** — DirectorState, QuickGame, ObjectivesInit, ObjectCreate×N, per-hero updates, PlayerCharacterDeploy, InteractableData
11. **Frame loop** — GameState, LPU, deck HUD bind (current crash), ActionCommandMsgs movement

---

## 6. Verify gates — two layers

Automated tests verify the **server produces correct bytes** (C# == C++ / == golden). They do **NOT** guarantee the client works — the client is a black box; the only absolute proof of "plays" is running it. But because the C++ reference demonstrably works with the client, "C# bytes == C++ bytes" is a strong *proxy* and catches ~90% of divergences without opening the client.

**Layer 1 — Automated golden tests (the workhorse, every change).** `ReCap.Tests` is the harness; recorded C++ captures are the fixtures. Per phase, capture the C++ wire once → extract the application-level packet payloads (reassemble RakNet fragments *once*, at fixture-creation time, not per test) → assert C# `WriteTo`/handler output is byte-identical to the fixture (modulo session-specific values). This is also the **golden oracle** that makes C#-only development safe after C++ retirement — the recorded bytes remain the ground truth when C++ is gone.

**Layer 2 — Manual client run (milestone gate, not every commit).** The only proof the client actually plays. Run at milestone boundaries.

Per critical-path step: (a) **wire-parity** — C# bytes match C++/golden, discounting legitimate session values (IP, port, timestamps, object IDs, RNG); (b) **client-progress** — client advances. **Final gate (M1):** the client plays a full single-player Dungeon. "Builds" is never "done" — every closed step has a logged runtime observation and, where applicable, a golden test.

**Golden-capture is a continuous workstream:** capture each phase's golden fixture when that phase passes its gate (not all up-front), so effort tracks progress. By M4 the full set exists and C++ can be retired.

---

## 7. De-hardcode & improve-beyond-C++ (opportunistic, with rules)

**De-hardcode rule:** when the walk touches a system that hardcodes a value the C++ derives from data, the fix replaces the hardcode with the real AssetData/DB-driven value **as part of closing that divergence**. No hardcode that merely "happens to match." Known targets: `SetSquad` real loadout, spawn noun IDs, ClassAttributes full `mPartAttributes` array (currently only maxHealth/maxMana extracted), gearscore, level/markerset loading.

**Improve-beyond-C++ rule (with brake):** where C++ is incomplete or dirty (e.g. uninitialized pointer garbage in `Character` block gaps; `abilityId=0` unresolved), implement the *correct* thing rather than copying the garbage — **only** when it is invisible to the client (the client tolerates C++'s garbage) or strictly better. Mother rule: match the **contract the client requires**, not C++'s byte-garbage.

**Superseded-laws changelog:** every disproven "law" removed in Phase 0 gets a one-line entry (what it claimed, the evidence that killed it) so future sessions don't resurrect it.

---

## 8. Model-tiering policy (cost control)

Opus is expensive; reserve it. Assign the cheapest tier that does the job:
- **Haiku** — mechanical: file reads, single-field source lookups, capture byte extraction, ledger-row formatting, scaffolding.
- **Sonnet** — the bulk: per-system source-diff sweeps, wire-diff analysis, multi-file fixes, subagent fan-outs (cpp-tracer-style locators, divergence enumeration).
- **Opus** — only hard reasoning: ambiguous protocol divergences, design/synthesis decisions, crash diagnosis where cheaper tiers stall.

Pass the `model` override when spawning agents. Default sub-agent work to Sonnet; drop to Haiku for trivial steps; escalate to Opus only on a stuck reasoning problem.

---

## 10. M1 kickoff — first concrete steps (for a fresh session)

Read first: this spec, `docs/architecture/VERIFIED_FACTS.md`, `docs/architecture/planning/DIVERGENCE_LEDGER.md`, and memory `port-fidelity-plan-phase0`. Use Sonnet for the sweeps/captures; Opus only if reasoning stalls.

1. **Golden-harness scaffold** (`ReCap.Tests`): a helper that loads a captured application-payload fixture (hex) and asserts a produced byte sequence equals it, with a mask for session-specific values (IP/port/timestamp/objId). Unit-test target = the packet `WriteTo` methods. Start with one known packet (e.g. the deploy LPU `A1 00 00 10 01 01 00 00 00 FF`) to prove the harness.
2. **Capture C++ golden for steps 1–3** (login/Blaze/REST), the never-wire-diffed prime suspects: dumpcap on Npcap Loopback for Blaze/RakNet, and save the C++ server's REST responses (`api.account.getAccount`, `getPartList`). These become fixtures + the regression oracle.
3. **Source-diff + wire-diff steps 1–3** vs C++: Blaze handshake/auth, lobby (Rooms/GameManager), and especially the REST account/deck/creature payloads the menu loads. File every divergence in the ledger (`open` → `wire-confirmed`).
4. **Fix divergences**, one commit each, each with a golden test (Layer 1). Re-test.
5. **Verify gate:** client run — did the deck-HUD movie bind (D-003) change? If steps 1–3 were clean and it still crashes, attach the Ghidra debugger at `0x551f10` / where `cPlayerDeck+0x20` is set, and inspect runtime state.
6. Continue the walk: steps 4–5 (HelloPlayer 8B body + hello LPU), then 6–11.

Known open ledger items to fold in: D-001 (ClassAttributes full `mPartAttributes` de-hardcode), D-002 (gearscore), D-003 (deck-HUD crash).

## 9. Out of scope (deferred register)

- Multiplayer (matchmaking, CreateGame/JoinGame, real Rooms/Playgroups, CensusData, chat, broadcast `Send*`).
- Combat/Lua/AI beyond the C++ frontier.
- Typed Noun/AssetData loading strategy beyond what the Dungeon path needs.
