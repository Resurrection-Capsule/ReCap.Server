# Deck / Squad / Creature System

Complete architecture of the player's deck/squad/creature system, reverse-engineered from the
C++ reference server (ReCapCpp) and the retail `Darkspore.exe` (Ghidra). This is the data the
client edits in the editor/squad UI, persists via REST, and the game deploys into a dungeon.

> **Terminology:** the **client says "deck"**, the **server says "squad"**. They are the same
> thing. `Squad.id == Squad.slot` (1, 2, 3). There is **no separate Deck class** in C++.

## Why this matters (the crash)

The client HUD indexes the player's deck by slot to render the squad bar and bind the deployed
hero (Ghidra: `HUD_PlayerDeck.swf`, `MaxisPlayerDeck`, Lua methods `GetCurrentDeckIndex`,
`GetHealthOfCreatureAtDeckIndex`, `CanDeployCreatureFromCurrentDeck`, `SetCurrentDeckIndex`).
On deploy the client receives `PlayerCharacterDeploy(creatureIndex, objectId)` and looks up its
local deck slot `creatureIndex`. **If that deck slot is empty/inconsistent with what the game
deploys → null lookup → crash** (verified: ACCESS_VIOLATION read 0x0, `this`=NULL in the per-frame
HUD loop — see [[client-crash-diagnosis-method]] / `client-crash-diagnosis-method` memory).

So the REST deck data and the RakNet-deployed squad **must be consistent** (same creature
ids/nouns, non-empty).

## Data model (C++ ground truth)

| Class | File | Key fields |
|-------|------|-----------|
| `Account` | SporeNet/User.h:31 | id, level, xp, dna, avatarId, `defaultDeckPveId`, `defaultDeckPvpId`, unlockPve/PvpDecks |
| `User` | SporeNet/User.h:111 | `mAccount`, `mCreatures` (vector<Creature>, keyed by creature.id), `mSquads` (vector, 3), `mParts` |
| `Squad` (= deck) | SporeNet/Squad.h:11 | `mId`==`mSlot` (1-3), `mName` "Slot N", `mCategory` pve/pvp, `mLocked`, `mCreatureIds[3]` (creature **instance** ids) |
| `Creature` | SporeNet/Creature.h | `mId` (instance), `mTemplate`→`mNoun` (FNV of `.noun`), gearScore, parts, stats, pngs |
| `CreaturePart` | — | rigblock, status |

- `Squad.mCreatureIds[i]` → creature **instance** id → `User.mCreatures.Get(id)` → `creature.mTemplate.mNoun` (the noun deployed in-game).
- `defaultDeckPve/PvpId` = which squad slot is "default" (account setting); the actual game squad comes from the `squadId` in `ChainPlayerMsgs` vote, not this.

## REST API surface (Game/API.cpp)

| Endpoint | file:line | Request | Effect |
|----------|-----------|---------|--------|
| `api.deck.updateDecks` | 1866 | `pve_active_slot` u32, `pve_creatures` CSV, `pvp_active_slot`, `pvp_creatures` | `User::UpdateSquad(slot, csv)` → sets `mCreatureIds`. **No Save() (memory-only)** |
| `api.account.auth` | 1181 | `key`, `include_creatures`, `include_decks`, … | returns `<creatures>` + `<decks>` |
| `api.account.getAccount` | 1412 | `include_*` | account + decks (+creatures if asked) |
| `api.creature.unlockCreature` | 1732 | `template_id` | `mCreatures.Add(templateId)` → seq id. No Save |
| `api.creature.getCreature` | 1751 | `id`, `include_abilities/parts` | full creature |
| `api.creature.updateCreature` | 1798 | id, gear, points, parts, stats, pngs… | `Creature::Update` + save PNGs. No Save |
| `api.creature.getTemplate` | 1775 | id | template |
| `api.creature.resetCreature` | 1709 | id | returns creature |
| `api.inventory.getPartList` | 1044 | filter, count | `<parts>` |
| `api.inventory.vendorParts` | 1086 | transactions | buy/sell parts |
| `api.inventory.updatePartStatus` | 1156 | part_id CSV, status CSV | set status, memory-only |

No `api.deck.getDeck` exists — decks come only via account.auth/getAccount.

### updateDecks wire format (confirmed client + server)

Client builder: `SP_App::ApiRequest::deck_updateDecks_build` (Ghidra 0x00466f90). Sends form params:
- `pve_creatures` = CSV of creature **instance** ids (`%llu`, comma-joined), e.g. `"42,16,75"`
- `pvp_creatures` = CSV
- `pve_active_slot` = u32 squad slot (only if non-zero)
- `pvp_active_slot` = u32 (only if non-zero)

Server `User::UpdateSquad(slot, csv)` (User.cpp:260): `GetSquadById(slot)`, split CSV, `mCreatures.Get(id)`, `SetCreatureId(index, id)`.

### Deck contract returned to client (auth/getAccount `<decks>`)

`User::WriteSquadsAPI` (User.cpp:589): per squad `<deck>`: `name, category, id, slot, locked`, nested
`<creatures>` per id: `id, name, noun_id, version, gear_score, item_points`.

## Persistence

User data is XML at `{storage}/user/{username}.xml` (`<creatures>`, `<squads>`). `User::Save()`
(User.cpp:494) is called **only** on `Logout()` (User.cpp:444), SignUp, and registration.
`updateDecks`/`updateCreature`/`unlockCreature` mutate **in-memory only** — so even C++ loses edits
on a non-graceful disconnect. Within a session they're correct (in-memory).

## Squad → game deploy flow

1. Client edits deck UI → `api.deck.updateDecks` → server updates `mSquads[slot].mCreatureIds`.
2. Client enters spaceship → `api.account.auth?include_decks` → client rebuilds its deck model.
3. Chainvote: client sends `ChainPlayerMsgs` byteCount=6 with `squadId`.
4. Server `PrepareGameStart` → `GetSquadById(squadId)` → `mCreatureIds[3]` → `SetSquad` →
   per creature `GetCreatureById(id)` → `Character.SetNoun(creature.GetNoun())`.
5. Dungeon: `PlayerCharacterDeploy(creatureIndex, objectId)`; client maps creatureIndex→its deck
   slot, binds the live object. **Deployed nouns must == deck creatures' nouns.**

## Client side (Ghidra)

- UI: `HUD_PlayerDeck.swf`, `SP_UI/cPlayerDeck`, `MaxisPlayerDeck(.OnCreatureClicked/.OnCreatureCardsLoaded)`.
- Lua-callable HUD methods: `GetCurrentDeckIndex`, `SetCurrentDeckIndex`, `CanDeployCreatureFromCurrentDeck`,
  `Get{Health,MaxHealth,Mana,MaxMana}OfCreatureAtDeckIndex` — index creatures by deck slot.
- LPU reflection fields (`labsPlayer`): `mCurrentDeckIndex`, `mQueuedDeckIndex`, `mDeckScore`, `mLockedDeckIndexMin`.
- Request builder named: `SP_App::ApiRequest::deck_updateDecks_build` (0x00466f90).

## ReCap status & gaps

| Piece | ReCap status |
|-------|--------------|
| `api.deck.updateDecks` | ❌ **STUB** (returns null, GameRestController.cs:393) — deck edits dropped → revert |
| `api.account.auth/getAccount` decks | ✅ returns decks (but they're empty since updateDecks never persists) |
| DB decks | seeded EMPTY (`DeckService.createDecksForAccount` → `CreatureIds=[]`) |
| Game squad | ❌ **hardcoded** `Game.SquadCreatureNouns` (ignores squadId/deck) |

### Fix plan (faithful, de-hardcoded)

1. **Implement `api.deck.updateDecks`**: parse `pve_active_slot` + `pve_creatures` (CSV instance
   ids), `DeckService.UpdateDeck(account, slot, ids)`, **persist to SQLite** (better than C++'s
   memory-only — survives restart).
2. **Game squad from deck**: in PrepareGameStart (ChainPlayerMsgs byteCount=6 with squadId),
   resolve `squadId` → player's deck `CreatureIds` → each creature's `TemplateID` (= noun) →
   build the squad/LPU characters from those. Removes `SquadCreatureNouns` hardcode (ties to the
   dalkon-hardcode cleanup task).
3. Ensure REST deck creatures and deployed nouns/assetIds agree (same creature instances).

Until (1)+(2): temp DB hack `Decks[1].CreatureIds='[42,16,75]'` keeps deck⇄deploy consistent for
testing (instances whose templates match the hardcoded squad). See [[deck-squad-consistency-crash]].
