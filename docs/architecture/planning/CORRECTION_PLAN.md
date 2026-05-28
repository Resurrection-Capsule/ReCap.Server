# Correction Plan — umbrella remediation plan

The master plan for fixing **what is missing or divergent today**, organized into workstreams. Born 2026-05-25 after the M7 parity docs ([`PORTING_MATRIX.md`](PORTING_MATRIX.md) + `components/` + `data-model/` + `http/`) made the full gap set visible.

> **Relationship to other plan docs:**
> - [`PORTING_MATRIX.md`](PORTING_MATRIX.md) — the *inventory* (what exists, what's missing, status per class/command/endpoint).
> - [`PORTING_PLAN.md`](PORTING_PLAN.md) — the *gameplay-phase-sequenced* code plan (P1–P7). This umbrella **references** it for the RakNet gameplay path rather than duplicating it.
> - **This file** — the *umbrella*: organizes every gap (Blaze, HTTP, data-model, Core, gameplay) into prioritized workstreams, sets scope, and records what is deliberately deferred.

## Scope decision (2026-05-25)

**Target: single-player offline playable first.** Everything is filtered against that goal.

**In scope:** the gameplay path to "player fights in a dungeon and banks rewards," account/squad/creature persistence, the REST surface a solo client touches, and all outright *divergences* (wrong behavior) regardless of subsystem.

**Deferred — tracked in the [Deferral register](#deferral-register), not abandoned:**
- **`systems/` (combat, Lua, AI)** — even the C++ reference is not robust here; we don't want this progress mirrored into C# yet.
- **Noun / AssetData typed loading** — gets its own strategy driven by future Ghidra deep-dives.
- **Multiplayer** — matchmaking, CreateGame/JoinGame, real Rooms/Playgroups, CensusData, chat relay, broadcast `Send*`.

---

## Priority matrix

| WS | Workstream | Blocks solo play? | Depends on | Effort | Lead model |
|---|---|---|---|---|---|
| [WS-0](#ws-0--frozen-rules-guardrail) | Frozen-rules guardrail | — (reference) | — | none | — |
| [WS-1](#ws-1--gameplay-path-unblock) | Gameplay-path unblock (= PORTING_PLAN P1–P3) | **Yes — hard stall** | WS-3/WS-4 for the *real-squad* part of P3 | M | normal |
| [WS-2](#ws-2--cross-cutting-divergences) | Cross-cutting divergences (TDF varint, GameState enum) | Latent | — | S | normal |
| [WS-3](#ws-3--data-model--persistence-integrity) | Data-model & persistence integrity | Indirectly (no real squad) | — | M | normal |
| [WS-4](#ws-4--rest-surface-completion-solo) | REST surface completion (solo) | Some endpoints (deck save, launcher) | — | M | normal/haiku |
| [WS-5](#ws-5--lobby-stub-sufficiency) | Lobby-stub sufficiency check | Maybe (verify) | runtime client | S | normal |
| [WS-6](#ws-6--qos--login-survival) | QoS / login survival check | Maybe (verify) | runtime client | S | normal |

**Critical spine:** WS-1 is the gameplay stall fix and the highest priority. WS-3 + WS-4 run in **parallel** with WS-1a (they're independent of the wire fixes) and become **prerequisites** for the "real `SetSquad`" sub-item of P3. WS-2/5/6 land opportunistically.

---

## WS-0 — Frozen rules guardrail

**Do not touch. Read before any fix that looks like a C++ misalignment.** Past sessions broke these by "aligning to C++."

| Rule | Source |
|---|---|
| Initial LPU `dataBits = {0,4,5,6,7,8,12,15,16,18,21,22}` (12 bits) — never add `{3,13,14,17}` | `feedback_initial_lpu_divergence`, `phases/06-spaceship.md` |
| `ChainVoteMsgs` 0x151 buffer = LE | `feedback_chainvote_le`, `phases/07-chainvote.md` |
| `GameType = 0` inside 50 ms tick | `feedback_gamestate_type` |
| `LabsPlayerData.DataSetup = false` always | `CLAUDE.md` |
| `ChainCashOut` payload rides wire opcode `0xA9` (not `0xAB`) | `phases/11-chaincashout.md` |
| `ChainData` tail-6-u32 starts at offset `0xEE` (not `0xED`) | `phases/07-chainvote.md` |

Any fix below that appears to contradict these must cite the rule and stop.

---

## WS-1 — Gameplay-path unblock

**This is PORTING_PLAN P1–P3. Do not re-track here — open [`PORTING_PLAN.md`](PORTING_PLAN.md) and work P1→P2→P3 in order.** Summary of what it covers:

- **P1 (Spaceship):** HelloPlayer `0x80` body 2 B → **8 B** (`u8 type, u8 gameplayIndex, u32 BE IPv4, u16 BE Port`). Stall-grade. `UpdateCatalystBonuses` after `SetCatalyst×8`.
- **P2 (PreDungeon 4→8):** GameStart `0xB1` 1 B → **5 B** (`u32 BE levelIndex`); DebugPing `0xCC` empty → **8 B** (`u64 BE timestamp`).
- **P3 (Dungeon entry):** `SwapCharacter(player,1)`; hero `ObjectCreate ×3`; DirectorState/QuickGame body audit. **`SetSquad` from real user data — depends on WS-3 + WS-4** (otherwise stays hardcoded).

**P4 (combat loop) is DEFERRED** — it is the `systems/` subsystem set. P5 cashout `CashOutData` is partially gated on combat; defer the gameplay parts, but the `CashOutData` *struct* + wire shape can be documented/scaffolded any time.

**Stop condition for this umbrella:** once P3's verify gate passes with a *real* squad, the solo pre-combat path is complete and the rest is the deferred `systems/` work.

---

## WS-2 — Cross-cutting divergences

Wrong-today items independent of gameplay phase. Fix opportunistically alongside whatever touches them.

- [ ] **TDF varint sign-bit asymmetry.** C++ decoder drops the varint sign bit; C# encoder emits it. Latent until a signed varint crosses the wire. Source: `BLAZE_TDF.md`. PORTING_PLAN Tier-D row 24.
- [ ] **`GameState` enum 7 → 21 values.** C# auto-numbers 7 states, masked by `GameStatePacket.WireState` switch; C++ `Client.h:14-37` has 21 explicit `= 0xNN`. Risky if a new state slips through unmapped. Add `IsValidStateChange` mirroring `Client.cpp:12-49`. Source: `STATE_MACHINE.md`, PORTING_PLAN Tier-D rows 22–23.

These are not solo-play blockers but are real correctness debt. Land before multiplayer or before any new state is added.

---

## WS-3 — Data-model & persistence integrity

Single-player meaningfully depends on the account having a *real* squad/creatures that persist. Source: [`data-model/`](../data-model/README.md).

- [ ] **`Part.mEquippedToCreatureId` missing** (`data-model/part.md`). C# `CreaturePartModel` cannot track which creature a part is slotted into. Add field + persist. Blocks correct part-equip state.
- [ ] **`Domain/Creature.cs` is a thin stub** (`data-model/creature.md`). Stats/AbilityStats/Parts live only on `CreatureModel`; the domain entity doesn't represent the full creature. Hydrate the domain entity (or document the split as intentional). Needed for `SetSquad` to build real `Character`s in P3.
- [ ] **`Deck.mLocked` default inverted** (`data-model/squad-deck.md`). Verify and correct default. Also: **no deck update path** — ties to WS-4 `api.deck.updateDecks`.
- [ ] **Vendor / DNA economy** (`data-model/vendor.md`). ~10% — static offer list via JSON seed; no buy/sell/buyback. Solo players spend DNA on parts; without this the economy is read-only. Scope: minimal buy/sell, defer buyback.
- [ ] **Settings/Feed persistence** (`data-model/feed.md`, README persistence note). Auth tokens, feed, assoc-lists, room, vendor state are **in-memory only** — reset on restart. For solo, prioritize *account settings* persistence (ties to WS-4 `UserSettingsSave`). Feed/assoc can stay deferred (social).
- [ ] **Typed enums downgraded to strings** (`data-model/creature-template.md`). `CreatureType`/`CreatureClass` are strings in C#. Low priority — works, but loses type safety. Optional.

**Verify gate:** create account → unlock creatures → build a 3-creature squad → restart server → squad + creatures + equipped parts survive. Squad is then usable by P3 `SetSquad`.

---

## WS-4 — REST surface completion (solo)

8 of 24 `/game/api` methods return `null` → `UnimplementedMethodException`. Source: [`http/`](../protocol/http/README.md), `http/game-api.md`.

**Solo-blocking / high-value:**
- [ ] **`api.deck.updateDecks`** — currently null; **squad saves never persist** (pairs with WS-3 deck path). High priority.
- [ ] **`api.game.status` (`/recap/api`)** — launcher progress/play-button state. Launcher UX broken without it.
- [ ] **`api.creature.getTemplate`** — null; creature editor needs it.
- [ ] **`api.creature.resetCreature`** — returns 200 but no creature node; reset logic TODO.
- [ ] **`api.account.unlock`** — null stub.
- [ ] **`/game/service/png` + `creature_png`/`template_png` serving** — UI thumbnails 404. C# has no dedicated handler (static fallback only). Serve template/creature PNG by id+size.
- [ ] **`UtilComponent.FetchClientConfig` + `UserSettingsSave`/`UserSettingsLoadAll`** (Blaze 0x09, `components/util.md`) — client startup config + settings persistence (pairs with WS-3 settings).

**Lower priority (solo-tolerable):**
- [ ] `api.account.searchAccounts`, `api.game.getGame`/`getRandomGame`/`exitGame`, `api.leaderboard.getLeaderboard` — null; mostly social/multiplayer/post-game. Defer unless a solo screen calls them.
- [ ] Launcher HTML templating (`{{host}}`/`{{version}}`) on `/bootstrap/launcher/*` — C# serves raw static. Verify the client tolerates untemplated; fix only if it breaks.

**Verify gate:** launcher shows correct status + play button; squad edits saved via `updateDecks` persist (cross-check WS-3); creature thumbnails render.

---

## WS-5 — Lobby-stub sufficiency check

`Playgroups` (1/11), `Rooms` (2/12), `AssociationLists` (1/8) are minimal stubs (`components/`). The component README claims they "satisfy the client enough to proceed past the lobby screens." **This is an assumption — verify at runtime.**

- [ ] Run solo client through lobby → game-start with current stubs. Capture any component command the client sends that returns an error/empty and stalls a screen.
- [ ] Fill **only** the specific commands that block. Everything else stays deferred (multiplayer).

No code change until a runtime capture proves a stub is insufficient.

---

## WS-6 — QoS / login survival

`QoS::Server` (UDP) and `/qos/*` HTTP routes (`qos`/`firewall`/`firetype`) are entirely absent (`http/static-and-qos.md`, Core matrix). EA clients send QoS probes during connection setup; missing them *may* silently block login or server selection.

- [ ] Run solo client against current C# server with no QoS responder. Determine whether the client completes login + reaches Spaceship anyway (the offline single-server case may not need NAT probing).
- [ ] If it blocks: implement the minimal QoS HTTP XML responses first (cheap), then the UDP `QoS::Server` only if still required.

Verify-first, implement-only-if-blocking — same discipline as WS-5.

---

## Deferral register

Tracked, not abandoned. Each gets its own plan when its gate opens.

| Bucket | Why deferred | When to revisit |
|---|---|---|
| **`systems/` — combat, attributes, Lua VM, abilities, AI, locomotion sim, ServerEvent/CombatEvent** | C++ reference not robust here; don't mirror immature progress. This is PORTING_PLAN P4. | After WS-1 P3 passes and we decide combat is worth porting. Opus-grade docs first (`systems/` doc family). |
| **Noun / NounDatabase / typed AssetData structures** | Needs Ghidra-driven structure recovery; current `AssetDatabase` reads raw nodes. | Separate Ghidra strategy session. Feeds combat (creature stats/AI defs) once started. |
| **Multiplayer: matchmaking, CreateGame/JoinGame/RemovePlayer, real Rooms, Playgroups, CensusData, chat sendMessage, AssociationLists add/remove, broadcast `Send*`** | Single-player-first scope. Solo loop doesn't fence on these. | After solo play is fun. Blaze `GameManager`/`Rooms`/`Playgroups` sheets in `components/` are the starting inventory. |
| **`Scheduler` (AddTask/CancelTask)** | Only needed for timed gameplay events (respawns, cooldown ticks) — i.e. combat. | With `systems/` combat. |
| **Cosmetic / type-safety (typed creature enums, color logging, shared Network base)** | Works as-is. | Cleanup pass, any time. |

---

## How to use this file

1. **WS-1 is the spine** — work [`PORTING_PLAN.md`](PORTING_PLAN.md) P1→P2→P3 in order; that file owns the detailed checkboxes and verify gates.
2. **WS-3 + WS-4 in parallel** with P1/P2 — they don't touch the wire fixes and unblock the *real-squad* part of P3.
3. **WS-2** anytime; **WS-5/WS-6** are verify-first (no code until a capture proves the gap blocks solo play).
4. When a workstream item lands, tick it here AND update the corresponding `PORTING_MATRIX.md` row + coverage estimate in the same turn (memory write-back rule).
5. Anything in the deferral register stays untouched until its gate opens — don't scope-creep into combat or multiplayer.
