# Deck/Squad System — final design (player-driven, no injection)

**Date:** 2026-06-04 · **Status:** approved (user) · **Scope:** ReCap.Server REST deck flow + game-time squad resolution + persistence

## Problem

On a virgin account the server injected 1 random creature into each of the 3 decks. Editing a deck worked in-session, but reopening the game brought the injected creature back in its slot and the client then reported the squad incomplete (missing 1 slot). Three defects conspired:

1. `createDecksForAccount` seeded 1 creature per deck (ported from a C++ SignUp block that C++ itself marks `TODO: remove in the future` — a test hack, not the design).
2. `GameService.ResolveSquad` falls back to "the account's first 3 owned creatures" when a deck resolves empty — a runtime injection that masks deck state.
3. `DeckModel.CreatureIds` (`List<ulong>`) has no explicit EF Core mapping; the edit round-trip through SQLite is unverified, and `updateDecks` stores the client's raw CSV (9 ids: 3 decks × 3 slots, zeros included) into a single deck without ownership validation.

## Decision (user-approved)

Decks are **player-driven**. The server never injects creatures at runtime. Virgin accounts get **3 empty decks**. What the player saves is exactly what persists and exactly what deploys.

## C++ contract (verified 2026-06-04, file:line)

| Fact | Source |
|---|---|
| Squad = fixed `std::array<uint32_t,3>` creature slots; 0 = empty | Squad.h:34 |
| Squad fields: id, slot, name, category ("pve"/"pvp"), locked, creatureIds[3] | Squad.h:34-42 |
| `Squad::Reset(slot)`: id=slot, name="Slot N", locked=false, creatures stay `{0,0,0}` | Squad.cpp:86-91 |
| `ResetSquads()` = 3 empty squads; called ONLY at SignUp | User.cpp:247-258, API.cpp:808 |
| SignUp slot-0 creature fill is a `TODO: remove` test hack | API.cpp:771,808-814 |
| Persistence is verbatim: Write saves all 3 ids (even 0); Read loads exactly, no merge/reset | Squad.cpp:13-54, User.cpp:460-511 |
| `updateDecks` params: `pve_active_slot` + `pve_creatures` CSV (idem pvp); updates only the squad `GetSquadById(active_slot)` | API.cpp:1866-1881 |
| `UpdateSquad` validates each id via `mCreatures.Get(id)` (ownership); skips 0/foreign ids; compacts valid ids into slots via `SetCreatureId(index++)`; sets category | User.cpp:260-289 |
| Serve (`WriteSquadsAPI`): per deck `name,category,id,slot,locked` + `<creatures>`; zero slots silently skipped; per creature `id,name,noun_id,version,gear_score,item_points` | User.cpp:589-620 |
| Game-time: client sends squadId (QuickGame msg case 6) → `GetSquadById`; squad missing → silent drop | Server.cpp:1010-1021,1210-1241 |
| `SetSquad` iterates all 3 slots; missing/0 creature → Character noun=0 → null object. **No 3-creature requirement server-side** | Player.cpp:267-363 |
| Active deck: `defaultDeckPveId/PvpId` only SERVED in the account blob; client picks and sends squadId back. Server never auto-selects | User.h:44-45, API.cpp:786-787 |
| Squad completeness validation is purely client-side | (absence verified across UpdateSquad/SetSquad) |

## Design

### 1. Creation — empty decks
`DeckService.createDecksForAccount(account)`: create 3 decks, slots 1-3, names "Slot N", category "pve", locked=false, `CreatureIds = [0,0,0]` (fixed 3 positional slots, 0 = empty — mirrors `std::array<uint32_t,3>`). Drop the creatures parameter and the 1-creature seeding. `addAllCreatures` still grants the test account every creature; only the decks start empty.

### 2. Update — validated, active-deck-only, positional
`ApplyDeckUpdate` / `DeckService.updateDeck` mirrors `User::UpdateSquad`:
- Parse `pve_creatures` CSV (client sends 9 ids = 3 decks × 3 slots, zeros for empty).
- Target ONLY the deck `Slot == pve_active_slot`.
- Validate every non-zero id belongs to the account (creature lookup by id + AccountID match); skip 0s and foreign ids.
- Write the valid ids compacted into the deck's 3 slots (`index++` like C++), zero-fill the remainder so the deck always holds exactly 3 positional values.
- Set the deck's category from the pve/pvp key.
- Persist via the repository (verified round-trip, see §4).

### 3. Resolution — no fallback, faithful slots
`GameService.ResolveSquad`:
- Find the deck by client-sent squadId. Deck missing → empty squad (mirrors C++ silent drop).
- Map the deck's 3 slots in order. Non-zero id → resolve creature (ownership already guaranteed at write time; still null-checked). Zero/unresolvable → empty slot.
- **Delete the fallback** that injects the account's first 3 creatures.
- `FillSquadCharacters` already emits noun=0 for empty slots (mirrors C++ `SetSquad` null-character behavior) — unchanged.
- `Game.OnPlayerStart` hero spawn loop keeps spawning only the resolved (non-empty) creatures; C++ creates null objects for empty slots, which is wire-equivalent to not spawning for the single-player path already exercised.

### 4. Persistence — verified round-trip
`DeckModel.CreatureIds` must survive insert → update → server restart → read:
- Verify EF Core 9's primitive-collection mapping (List<ulong> → JSON column) actually exists in the created SQLite schema and that `Update + SaveChanges` writes mutations.
- If unreliable, add explicit `OnModelCreating` config: `HasConversion` (List<ulong> ↔ JSON string) + `ValueComparer<List<ulong>>` so EF detects list mutations.
- Add a persistence round-trip test (insert deck → update CreatureIds → new DbContext → read → exact match).
- Note: `Database.EnsureCreated()` does not migrate existing DB files; dev DBs created before the column existed must be recreated (document in the plan; test accounts are disposable).

### 5. Serve — C++ field parity
`DeckMapper.toContract`: emit per deck `name, category, id, slot, locked` and only the resolved non-zero creatures with `id, name, noun_id, version, gear_score, item_points` (C++ order; extra png_* fields already verified benign, keep or drop per D-007 precedent — keep, client-tolerated).

### 6. Out of scope
- Client-side completeness rule (purely client; server stays faithful).
- `defaultDeckPveId` auto-selection (C++ has none).
- PvP decks beyond the same code path (category flag already handled).
- Ghidra client-side deck-UI mapping (not needed: the server contract is fully defined by C++; revisit only if the client misbehaves against a faithful server).

## Testing
- Unit: updateDeck ownership validation (foreign id skipped, zeros skipped, compaction, zero-fill).
- Unit: ResolveSquad — empty deck → empty squad; partial deck → exact slots; no fallback.
- Persistence round-trip test (fresh DbContext re-read).
- Manual gate: virgin account → 3 empty decks in client; build 3-creature squad; relaunch client AND restart server → squad intact; enter dungeon → deploys the built squad; no "random" creature anywhere.

## Migration note
Existing dev accounts carry seeded decks (`[randomId]`) and possibly raw-9-id decks. Test accounts are disposable: recreate. No production data exists.
