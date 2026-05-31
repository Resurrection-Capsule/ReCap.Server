# Deck & Squad System — Client/Server Map

> Scope: how a player's **creatures → parts → deck (squad) → in-game deployment** flow works
> end-to-end, C++ reference vs C# port, plus the **client-side deck HUD** that crashes on Dungeon
> entry. The "map it 100%" reference requested 2026-05-28. Single source of truth across all four
> layers: persistence → REST → C# RakNet runtime → C++ reference → **client deck HUD (Ghidra)**.

Cross-refs: [`../flow/phases/09-dungeon.md`](../flow/phases/09-dungeon.md) (deploy packet
sequence), [`../protocol/packets/0xA7-playercharacterdeploy.md`](../protocol/packets/0xA7-playercharacterdeploy.md),
[`../data-model/creature.md`](../data-model/creature.md).

> **Integrity note (2026-05-29).** The Ghidra MCP instance was **open but had no program
> loaded**, so no decompilation could run this session. Everything below is either (a) read from
> the C# code, (b) read from the C++ reference, or (c) **verified in prior sessions** via Cheat
> Engine live breakpoints + static Ghidra (recorded in `memory/deck-squad-consistency-crash.md`).
> Client-RE claims are tagged **[V]** verified / **[?]** not-yet-mapped. Do not promote a **[?]**
> to fact without a live Ghidra confirm.

---

## 0★. Infinite-loading blocker — noun wire-offset regression (FIXED 2026-05-30)

> **This supersedes the symptom the rest of this doc describes.** After the squad de-hardcode
> (§9.4) the client stopped reaching the deck-HUD crash and instead **hung at loading** (status 4 →
> black screen, never status 8). That was a **different, earlier bug** and it is now fixed. The
> GFx-bind crash (§6–§7) is downstream — the level must finish loading before the deck HUD can crash.

**Root cause.** Commit `3f8cf3a` rewrote `LabsCharacterData.WriteTo` (the 0x620 `Character` block
carried in the PreDungeon LPU, inside Player reflection **field 3**) to write `NounId` at wire
offset **`0x004`**, citing the AssetData catalog field `staticData.nounDef @ 0x4`. The C++ ground
truth `Game/Character.cpp:111-112` writes `mNounId` at wire **`0x0B4`**:

```cpp
stream.SetWriteOffset(writeOffset + bytes_to_bits(0x0B4));
Write<uint32_t>(stream, mNounId);
```

With the noun at `0x004`, the client reads **noun = 0** for all 3 squad characters → cannot resolve
the creature assets → hangs at loading. **Fix:** noun written at `0x0B4` again.

**Architecture lesson (important).** The AssetData.Parser `labsCharacter` catalog describes the
client's **in-memory** struct, where `staticData.nounDef` is a *pointer* at struct offset `0x4`.
The **wire** block is a sparse hand-packed copy whose noun-id **hash** lives at `0x0B4`. The two
layouts only coincide for `version`(`0x10`) and `assetID`(`0x8`). **For wire layout, C++
`Character::WriteTo` is authoritative — never derive wire offsets from the catalog.**

### 0★.1 `Character::WriteTo` wire map (0x620 block, LE) — C++ ground truth

| Wire off | Field | Type | C++ (`Character.cpp`) | C# (`LabsPlayerUpdatePacket.cs`) |
|---|---|---|---|---|
| `0x008` | `mAssetId` | u64 | `:108` | ✓ |
| `0x010` | `mVersion` | i32 | `:109` | ✓ |
| **`0x0B4`** | **`mNounId`** | **u32** | **`:112`** | **✓ (was `0x004` — the bug)** |
| `0x0B8`–`0x1E0` | `partAttributes[0x00..0x49]` | f32×74 | `:114-117` | partial (only the few the deploy needs) |
| `0x1E4` | `partAttributes[ImmuneToSleep]` | f32 | `:119-120` | — |
| `0x1EC`–`0x248` | `partAttributes[0x4B..0x61]` | f32×23 | `:122-125` | — |
| `0x24C`–`0x280` | `partAttributes[0x62..0x6E]` | f32×13 | `:127-130` | — |
| `0x3B8` | `mCreatureType` | u32 | `:132-133` | ✓ |
| `0x3C0` | `mDeployCooldown` | u64 | `:135-136` | ✓ |
| `0x3C8` | `mAbilityPoints` | u32 | `:137` | ✓ |
| `0x3CC`–`0x3EF` | `mAbilityRanks[9]` | u32×9 | `:138-140` | ✓ |
| `0x3F0` | health, maxHealth, mana, maxMana, gearScore, gearScoreFlattened | f32×6 | `:142-148` | ✓ |

C# fills only the partAttributes the deployed hero actually divides by
(`MaxHealth 0x0C8`, `MaxMana 0x0CC`, `AttackSpeedScale 0x114`, `CooldownScale 0x118`,
`MinWeaponDamage 0x258`, `MaxWeaponDamage 0x25C`); the rest stay zero (the client tolerates it).

### 0★.2 CreatureType de-hardcoded (correctness, 2026-05-30)

`mCreatureType` is the creature's **element**, written as the C++ `SporeNet::CreatureType` ordinal
(`Creature.cpp` `from_string`): **Bio=0, Cyber=1, Plasma=2, Necro=3, Chrono=4, All=5, Unknown=6**.
This is **not** the AssetData `PlayerClass.creatureType` enum (technology/spacetime/life/…) — that
one has different names/ordinals; do not use it here. `GameService.ToSquadCreature` now resolves
`CreatureTemplateModel.elementType` (`"BIO"`/`"CYBER"`/…) via `CreatureElement.ToWireType`
(`Domain/Gameplay/SquadCreature.cs`) instead of the old hardcoded `0`. Not a loading blocker
(0=Bio is valid), but it makes each hero show its real element.

---

## 0. TL;DR

- "Squad" (C++ / XML wire) == "Deck" (C#). Same thing: a named slot of **3 creature IDs**.
- **The C++ reference server works.** With it the client enters the level, walks, and attacks
  normally — no deck-HUD crash. **(Corrected 2026-05-29; supersedes the earlier "C++ crashes too"
  claim, which was from a period when dalkon's then-broken WIP was being repaired — see §7.)**
- Therefore the crash is **server-side reachable**: the bug is in **what our C# server does
  differently from C++**, not an unconditional client bug. Find the divergence → fix the crash.
- The fault itself: `cPlayerDeck`'s show-animation fires a **GFx::Value invoke on an unbound
  movieclip** → null `this` → AV `read 0x0 @ 0x00551f47`. **[V]** It fires from a **per-frame
  deck-HUD update**, not a packet handler. **[V]** But because C++ avoids it, **something the C++
  server sends/does (data, ordering, or an extra packet) lets the client bind that movieclip and
  we don't.**
- Individual server pieces look byte-perfect in isolation (Wireshark `api.account.auth`;
  ObjectCreate wire-identical). The divergence is therefore likely **sequence/timing/coverage**
  of the deck-show flow, or a value the cards/show path consumes — not a single malformed packet.
- **Next:** diff our deploy/HUD flow against C++ (§5 vs §4) with the C++ reference running + extra
  logs, and map the show-trigger head in Ghidra (§9). The decisive comparison is C++-vs-C# server
  behavior around the deck show, since C++ provably makes the client work.

---

## 1. Vocabulary & conceptual model

| Term | Meaning |
|---|---|
| **Creature** | One playable hero instance owned by an account. Has parts, stats, a TemplateID(=noun). |
| **Part** | Equippable item on a creature (weapon/armor/etc). Modifies stats. |
| **Deck** | C# name for a saved group of 3 creatures (a "squad"). |
| **Squad** | C++/wire name for the same 3-creature group. |
| **Catalyst** | Crystal/buff slot (8 per player), separate from creatures. |
| **Noun** | Asset definition hash; a creature's TemplateID doubles as its noun. |

| Concept | C++ name | C# name | Wire/REST | Notes |
|---|---|---|---|---|
| Slot of 3 creatures | `Squad` | `Deck` | `<deck>` | 3 squads/decks per account, slots 1–3 (1-based) |
| Member | `mCreatureIds[3]` | `CreatureIds` | `<creatures>` | TemplateID(=noun) of each creature |
| Owner | `User.mSquads` | `Deck.AccountID` | account | C++ container-owned; C# FK |
| In-game active | `mCurrentDeckIndex` | `CurrentDeckIndex` | LPU dataBit 1 | which of 3 is deployed |

The deck stores creature **instance IDs**; the game deploys the creature's **noun** as a world
object. "Squad" in C++ source and the XML wire format == "Deck" in C# source — interchangeable.

---

## 2. Data model & persistence (server)

### 2.1 Field table (C++ ↔ C#)

| C++ field | C++ type | C# field | C# type | Persisted? | Notes |
|---|---|---|---|---|---|
| `mId` | `uint32_t` | `ID` | `ulong` | Yes | C++ 32-bit, C# 64-bit |
| `mSlot` | `uint32_t` | `Slot` | `int` | Yes | 1-based slot index (1, 2, or 3) |
| `mName` | `std::string` | `Name` | `string` | Yes | Default `"Slot N"` in C++ |
| `mCategory` | `std::string` | `Category` | `string?` | Yes | `"pve"` or `"pvp"`; nullable in C# |
| `mLocked` | `bool` | `Locked` | `bool` | Yes | C++ ctor default `true`; C# default `false` — **inverted** |
| `mCreatureIds[3]` | `std::array<uint32_t,3>` | `CreatureIds` | `List<ulong>` | Yes | C++ fixed 3 slots; C# variable list |
| _(none)_ | — | `AccountID` | `ulong` | Yes | C# only; C++ squads owned by `User.mSquads` |

### 2.2 Persistence

EF Core model `DeckModel` (`Models/DeckModel.cs`) → table `Decks` (`Config/SqliteConfig.cs`).
Repo `Adapters/Persistence/SQLite/DeckRepositoryAdapter.cs` has both `insertDeck` and
`updateDeck` (`:31,:40`). `CreatureIds` persisted as `List<ulong>` (JSON/value-converter column).

### 2.3 Mapper

`Mappers/DeckMapper.cs` — AutoMapper + manual contract builder. `toContract(DeckModel,
List<CreatureModel>)` uses the deck's **actual** `CreatureIds` (`creatures.Find(c => c.ID == id)`,
skipping ids not found). The old `Slot*3+i` index hack was removed.

### 2.4 Porting gaps (open)

- `mLocked` default inverted (C++ `true` `Squad.h:41` vs C# `false` `Domain/Deck.cs:12`).
- C# `List<ulong>` not length-enforced to exactly 3 (C++ `mCreatureIds[3]` guarantees 3).
- `AccountID` is a C#-only FK addition.
- C++ `User::UpdateSquad()` validates creature existence before assigning ids (`User.cpp:276–288`);
  no equivalent in C#.
- C++ always initializes 3 squads on `ResetSquads()` (`User.cpp:247–258`); C# does not guarantee
  three decks for a new account.

### 2.5 C++ class model (ground truth) & persistence

| Class | File | Key fields |
|---|---|---|
| `Account` | `SporeNet/User.h:31` | id, level, xp, dna, avatarId, `defaultDeckPveId`, `defaultDeckPvpId`, unlockPve/PvpDecks |
| `User` | `SporeNet/User.h:111` | `mAccount`, `mCreatures` (vector, keyed by creature.id), `mSquads` (3), `mParts` |
| `Squad` (= deck) | `SporeNet/Squad.h:11` | `mId`==`mSlot` (1–3), `mName` "Slot N", `mCategory` pve/pvp, `mLocked`, `mCreatureIds[3]` (creature **instance** ids) |
| `Creature` | `SporeNet/Creature.h` | `mId` (instance), `mTemplate`→`mNoun` (FNV of `.noun`), gearScore, parts, stats, pngs |

- `Squad.mCreatureIds[i]` → creature **instance** id → `User.mCreatures.Get(id)` →
  `creature.mTemplate.mNoun` (the noun deployed in-game).
- `defaultDeckPve/PvpId` = which squad slot is "default" (account setting); the **actual game
  squad comes from the `squadId` in the `ChainPlayerMsgs` vote**, not this default.
- **C++ persistence is memory-only per-edit.** User XML at `{storage}/user/{username}.xml`
  (`<creatures>`, `<squads>`); `User::Save()` (`User.cpp:494`) runs **only** on `Logout()`
  (`User.cpp:444`), SignUp, registration. `updateDecks`/`updateCreature`/`unlockCreature` mutate
  in-memory only — even C++ loses edits on a non-graceful disconnect. C# **persists each edit to
  SQLite** (stronger than C++ — survives restart).

---

## 3. REST layer

Two endpoints matter for the deck:

### `api.account.auth` (login)
`GameRestController.loginPlayerAccount`. Serves `<creatures>` (all owned) + `<decks>` (3).
- Creature block: `{id, name, png_large_url, png_thumb_url, noun_id, version, gear_score,
  item_points}` — matches C++ `Creature::WriteApi` 1:1 (Wireshark-verified byte-perfect).
- `png_*_url`: **root-relative** `/template_png/{TemplateID}_thumb.png`
  (`CreatureMapper.CreaturePngUrl`). C++ sends these **empty** and the client self-builds the
  path; an absolute `http://localhost/...` (old commit 32d7c87) **breaks** the client's own
  fetch. Root-relative is the corrected form. **Not the crash cause** (client issues 0
  template_png requests on the login→menu flow regardless — the fetch is screen/render-triggered).

### `api.deck.updateDecks` (squad edit)
`GameRestController.updateDecks` (`GameRestController.cs:395`) — **persists**. Reads
`pve_active_slot` + `pve_creatures` (CSV of creature ids) and the pvp pair, then
`ApplyDeckUpdate` → `deckService.updateDeck(account, slot, creatureIds)` →
`DeckRepositoryAdapter.updateDeck`. `DeckMapper.toContract` serves the deck's **actual**
`CreatureIds`.

### 3.1 C++ Game/API.cpp endpoint surface (reference)

| Endpoint | C++ `:line` | Request | Effect |
|---|---|---|---|
| `api.deck.updateDecks` | 1866 | `pve_active_slot` u32, `pve_creatures` CSV, `pvp_active_slot`, `pvp_creatures` | `User::UpdateSquad(slot, csv)` → sets `mCreatureIds` (no Save) |
| `api.account.auth` | 1181 | `key`, `include_creatures`, `include_decks` | `<creatures>` + `<decks>` |
| `api.account.getAccount` | 1412 | `include_*` | account + decks (+creatures) |
| `api.creature.unlockCreature` | 1732 | `template_id` | `mCreatures.Add` → seq id |
| `api.creature.getCreature` | 1751 | `id`, `include_abilities/parts` | full creature |
| `api.creature.updateCreature` | 1798 | id, gear, points, parts, stats, pngs | `Creature::Update` + save PNGs |
| `api.creature.getTemplate` | 1775 | id | template |
| `api.inventory.getPartList` | 1044 | filter, count | `<parts>` |
| `api.inventory.vendorParts` | 1086 | transactions | buy/sell parts |
| `api.inventory.updatePartStatus` | 1156 | part_id CSV, status CSV | set status |

There is **no `api.deck.getDeck`** — decks come only via `account.auth` / `getAccount`.

### 3.2 `updateDecks` wire format (confirmed client + server)

Client builder `SP_App::ApiRequest::deck_updateDecks_build` (Ghidra `0x00466f90`) sends form params:
- `pve_creatures` = CSV of creature **instance** ids (`%llu`, comma-joined), e.g. `"42,16,75"`
- `pvp_creatures` = CSV
- `pve_active_slot` = u32 squad slot (only emitted if non-zero); `pvp_active_slot` = u32 (idem)

Server C++ `User::UpdateSquad(slot, csv)` (`User.cpp:260`): `GetSquadById(slot)`, split CSV,
`mCreatures.Get(id)`, `SetCreatureId(index, id)`.

### 3.3 Deck contract returned in `<decks>`

C++ `User::WriteSquadsAPI` (`User.cpp:589`): per squad `<deck>`: `name, category, id, slot,
locked`, nested `<creatures>` per id: `id, name, noun_id, version, gear_score, item_points`.

---

## 4. C# RakNet runtime — deploy flow

File: `Domain/Gameplay/Game.cs`. Current state of play (2026-05-29):

```
ChainPlayerMsgs(byteCount=6, squadId)   →  State=PreDungeon
  FillSquadCharacters(playerData)            (3× LabsCharacterData)
  dataBits {1,2,3,23}; updateBits = PlayerBits | CharacterMask
  GamePrepareForStart(level, markerSet, 1, levelIndex)
  SendLabsPlayerUpdate

PlayerStatusUpdate(status=8)            →  State=Dungeon
  GameStart(levelIndex)  +  DebugPing
  SendLabsPlayerUpdate

DebugPing (Dungeon)                     →  OnPlayerStart:
  DirectorState, QuickGameMsgs
  ObjectivesInitForLevel
  ResolveSpawnPosition (CameraSpawnPoint marker, fallback 44,0.47,17.5)
  for each of 3 squad chars:
     ObjectCreate → ObjectUpdate → CombatantData → AttributeData
  SwapCharacter(client, player, DeployedDeckIndex=1, objId):   (Game.cs:574)
     CurrentDeckIndex=1; dataBit 1; PlayerCharacterDeploy(slot, deckIndex, objId); LPU
```

### Still hardcoded / divergent (does NOT cause the crash, but ports remain incomplete)
- **`SquadCreatureNouns = {1667741389, 749013658, 3591937345}`** — squad is hardcoded, not
  resolved from the player's persisted deck. De-hardcode = resolve `squadId → deck.CreatureIds
  → creature.TemplateID(noun)` and plumb into `Game`. (Tracked: dalkon-hardcode cleanup.)
- `SpawnLevelMarkers=false` — marker NPC spawn gated off (greedy spawn of non-object design
  markers caused a *different*, earlier null-deref; re-enable only with noun-type filtering).
- No Lua ability preload; no enemy/obelisk spawn; objectives are a static default.

---

## 5. C++ reference — authoritative deploy/HUD flow

Source: `C:\CodingProjects\Personal\ReCapCpp\darkspore_server\source`.

```
OnChainPlayerMsgs(byteCount=6) → PrepareGameStart(client, unknown, squadId):   Server.cpp:1208
  squad = user->GetSquadById(squadId)        // from user XML (updateDecks-populated)
  state = PreDungeon
  player->SetSquad(squad):                    Player.cpp:267
     for 3 creatureIds: build Character (ctor sets all 13 dataBits), SetCharacter(i)
     dataBits {1,2,23}; updateBits = PlayerBits | CharacterMask
  SendGamePrepareForStart                      Server.cpp:1996  (17B)

OnPlayerStatusUpdate(status=8):               Server.cpp:643
  state = Dungeon; SendGameStart; SendDebugPing

OnDebugPing (Dungeon):                         Server.cpp:1186
  SendDirectorState (empty cAIDirector)
  SendQuickGame
  mGame.OnPlayerStart(player)                  Instance.cpp:308  (obelisks/enemies/objectives/objects/3 chars)
  mGame.SwapCharacter(player, 1)               → PlayerCharacterDeploy + LPU
```

**C++ NEVER hardcodes the squad** — it resolves from the user's saved squad. The deployed
creature ids and the REST-served deck ids therefore always agree. The C# port must reach the
same place by de-hardcoding (§4). Full packet/parity table: [`../flow/phases/09-dungeon.md`].

> Crucially: **the C++ reference server makes the client work** — enter level, walk, attack, no
> deck-HUD crash. So whatever C++ sends/does around the deck-show + card-load flow is exactly what
> lets the client bind the deck movieclip. C++ is the oracle for the fix (§7.2, §8).

---

## 6. Client deck HUD — `cPlayerDeck` (`Darkspore.exe` @ base 0x00400000)

`cPlayerDeck` is built by the in-game HUD factory `FUN_00423f60` (which also builds
cHUDBase/cActionBar/cCatalysts). **[V]** Object size `0x88B`, vtable `0x00fd4768`. Deck init
`FUN_00420bf0` (called per element by the factory) does two **async** things:
1. **[V]** `RegisterCallback("MaxisPlayerDeck.OnCreatureCardsLoaded", LAB_0041fdc0)` +
   `"MaxisPlayerDeck.OnCreatureClicked"` (`FUN_0041bb50`).
2. **[V]** `LoadMovie("HUD_PlayerDeck.gfx", completion_cb = FUN_0041c4f0)` (the UI is a
   Scaleform `.gfx`, see §9).

### 6.1 Struct fields

| Offset | Meaning | Status |
|---|---|---|
| `+0x20` | **GFx::Value** of the deck show-animation target. `+0x24` = type tag byte (forwarder checks `AND AL,0x8f; CMP AL,8`); `+0x28` = the value's object/movieclip ptr (`*(deck+0x20+8)`) | **[V]** |
| `+0x28` | object/movieclip ptr — **NULL at crash** | **[V]** |
| `+0x40` | deck animation state (`!= -1` while a show/hide transition is in flight) | **[V]** |
| `+0x44` | deck animation timer (f32; the timer branch fires the event when it expires) | **[V]** |
| readiness flags (which offsets set `+0x40`/start the show, and gate it on cards+gfx loaded) | — | **[?]** not mapped this session |

### 6.2 Functions (with status)

- **[V] `FUN_00423f60`** — in-game HUD factory; allocates `cPlayerDeck` (0x88B, vtable
  `0x00fd4768`) with base cUIElement init, then runs the per-element deck init.
- **[V] `FUN_00420bf0`** — deck init: registers the two callbacks + `LoadMovie HUD_PlayerDeck.gfx`.
- **[V] `FUN_0041c4f0`** — GFx-load completion callback. Confirmed to fire (CE: 2× before crash).
  Observed to copy strings (`FUN_00401820` = EASTL string assign); it does **not** by itself bind
  the `deck+0x20` movieclip.
- **[V] `LAB_0041fdc0`** — `OnCreatureCardsLoaded` callback. Confirmed to fire before the crash.
- **[V] `FUN_0051b860`** — `cPlayerDeck::UpdateHud`, the per-frame tick. Timer branch at
  `0x0051b9b8`: `COMISS XMM0,[ESI+0x44]; JC 0x0051b9db` → when the timer (deck+0x44) ≤ 0 and the
  state (deck+0x40) `!= -1`, `LEA ECX,[ESI+0x20]` then calls `FUN_004015b0(deck+0x20)`.
- **[V] `FUN_004015b0`** — GFx::Value invoke forwarder. Disasm:
  `4015b1 MOV EAX,[ECX+4]; 4015b4 AND AL,0x8f; 4015b6 CMP AL,8; 4015d5 MOV EDX,[ECX+8];
  4015d8 MOV ECX,[ECX]; 4015dc CALL FUN_00551f10`. `ECX = deck+0x20`; it dereferences the
  value's object and dispatches into `FUN_00551f10`.
- **[V] `FUN_00551f10`** (faults `MOV ECX,[EDI]` @ `0x00551f47`, `EDI(this)=0`) — the dispatched
  method, invoked with a null `this` because the `deck+0x20` GFx::Value has no object bound
  (`deck+0x28 == 0`).
- **[?] show-trigger** — the function(s) that set `deck+0x40` to start the show and gate it on
  "cards loaded + gfx loaded" are **not yet decompiled** (needs live Ghidra).

### 6.3 Trigger chain — verified tail, unmapped head

```
[?] show-trigger (NOT MAPPED this session)
        |   sets deck+0x40 (state) + deck+0x44 (timer) to start the show animation
        v
[V] FUN_0051b860  cPlayerDeck::UpdateHud   (per-frame; timer branch @0x0051b9b8, ret 0x0051b9db)
        |   timer (deck+0x44) expires, state (deck+0x40) != -1 ; LEA ECX,[ESI+0x20]
        v
[V] FUN_004015b0  GFx::Value invoke forwarder (ret 0x004015e1)
        |   ECX = deck+0x20 ; MOV ECX,[ECX] ; CALL
        v
[V] FUN_00551f10  dispatched method, this = (object of deck+0x20 GFx::Value) = 0
        |   deck+0x28 == 0  (no movieclip bound)
        v
   AV: read 0x0 @ 0x00551f47            // crash
```

The **tail** (UpdateHud → forwarder → crash) is instruction-verified. The **head** (what starts
the show animation, and why `deck+0x20` is never bound) requires a live Ghidra pass to map.

### 6.4 Client UI surface (names & Lua interface)

- UI objects: `SP_UI/cPlayerDeck`, Scaleform widget `MaxisPlayerDeck`
  (`.OnCreatureClicked` / `.OnCreatureCardsLoaded` callbacks), movie `HUD_PlayerDeck.gfx`.
- Lua-callable HUD methods (index creatures by deck slot): `GetCurrentDeckIndex`,
  `SetCurrentDeckIndex`, `CanDeployCreatureFromCurrentDeck`,
  `Get{Health,MaxHealth,Mana,MaxMana}OfCreatureAtDeckIndex`.
- LPU reflection fields (`labsPlayer`): `mCurrentDeckIndex`, `mQueuedDeckIndex`, `mDeckScore`,
  `mLockedDeckIndexMin`.
- Client API request builder: `SP_App::ApiRequest::deck_updateDecks_build` (`0x00466f90`).

> Earlier crash theory (the deck-data-consistency angle): the client maps the deployed
> `PlayerCharacterDeploy(creatureIndex)` to its local deck slot, so an empty/inconsistent deck
> slot was suspected of the null lookup. That was **disproven** — the deck data is byte-perfect
> (§7) and the crash reproduces even with consistent data. The real fault is the GFx bind (§6.3,
> §7). Deck⇄deploy consistency still matters for correctness, just not for *this* crash.

---

## 7. The crash — root cause

### 7.1 What faults (verified)

`cPlayerDeck`'s show animation fires a **GFx::Value invoke** (`deck+0x20`) whose bound
object/movieclip (`deck+0x28`) is **null** → null `this` → AV `read 0x0 @ 0x00551f47`. Verified:
- **Cheat Engine (live)**: HW breakpoints captured `EIP=0x00551f47, EDI(this)=0`; stack
  `0x0051b9d6 → FUN_004015b0 → FUN_00551f10`. `*(deck+0x28)==0` confirmed.
- **CE live (2026-05-29)**: the GFx-load cb + `OnCreatureCardsLoaded` both fired ~2s **before**
  the crash, yet `deck+0x28` stayed null → not an asset-missing / load-never-completes problem;
  the **bind** itself never happens (in our C# session).

### 7.2 Why it is server-side reachable (the key correction)

**The C++ reference server makes the client work** — enter level, walk, attack, no crash
(confirmed 2026-05-29 by the project owner; the reference build is fully playable). So the unbound
movieclip is **not unconditional**: under C++ the client *does* bind `deck+0x20`. The difference is
**what our C# server provides/does that C++ does** — data, packet sequence/timing, or an extra
packet around the deck-show / card-load flow. Fixing that divergence fixes the crash.

> **OUTDATED claim removed (2026-05-29).** A prior version said "the C++ reference server hits the
> same crash (Splitwirez), 2nd login sometimes works → client bug, server-independent." That was
> wrong: the Splitwirez report dates to a period when **dalkon's WIP server was itself broken**
> (dalkon had disappeared and others were repairing his code). It does **not** describe the
> current, working reference server. The crash is therefore **not** a server-independent client
> bug. See `memory/deck-squad-consistency-crash.md` (lines marked OUTDATED 2026-05-29).

### 7.3 Still ruled out (not the cause)

- Auth/creature **data** in isolation (Wireshark: `api.account.auth` byte-perfect; client parses
  it fully). Creature PNG url/host. A missing UI asset (the `.gfx` + cards load fine).
- These being correct is *why* the divergence is most likely **sequence/timing/coverage** of the
  deck-show flow rather than one malformed packet.

---

## 8. Finding the C++↔C# divergence (the actual fix path)

Since C++ provably makes the client bind `deck+0x20` and we don't, the fix is to **find what C++
does that we don't** around the deck-show / card flow.

1. **Run the C++ reference server with the client and add logs** around the full deploy/HUD-show
   sequence (every packet + timing from squad deploy → SwapCharacter → whatever drives the deck
   show). Capture the exact order and contents.
2. **Diff against our C# flow** (§4 vs §5): a packet C++ sends that we don't; a different ordering
   (SwapCharacter / PlayerCharacterDeploy / ObjectCreate); a field/value the cards or show path
   consumes that we leave zero/empty; the 3-character force-create C++ does; objectives/director
   content differences.
3. **Map the show-trigger head in Ghidra** (§9 item 0) to learn which inbound state/data gates the
   movieclip bind — that tells you which server difference matters.
4. **Capture both with Wireshark** (C++ session vs C# session) and compare the post-`status=8`
   byte streams directly.

Hypotheses — now *testable* against C++ (the oracle), not speculative:

| Hypothesis | How C++ would differ | How to confirm |
|---|---|---|
| Missing/extra packet around deck show | C++ sends something in the Dungeon-entry burst we omit | log+diff packet list after status=8 |
| Wrong ordering of deploy vs HUD-show | C++ orders SwapCharacter/ObjectCreate/Deploy differently | timeline diff |
| A consumed value left empty (cards/deck) | C++ fills a field the AS reads to bind the clip | field-level diff of LPU/ObjectUpdate/deck contract |
| Hardcoded squad matters | C++ deploys real deck creatures; we deploy hardcoded nouns | de-hardcode (§9.3) then retest |

### 8.1 Runtime capture from the WORKING C++ reference (2026-05-29)

Ran the instrumented C++ reference with the client (`DarksporeBin/Server/output.txt`). The full
post-`status=8` deploy sequence and the `[DECK]`/`[LPU]`/`[DEPLOY]` values:

```
[LPU] id=0 updateBits=0x17f8 deckIndex=0          (Spaceship first tick)
OnChainPlayerMsgs(6): 1
[DECK] PrepareGameStart squadId=1 creatures=[4,9,6]
[DECK] SetCharacter i=0 noun=0x6367b6cd assetId=0x0 objId=1 gs=0
[DECK] SetCharacter i=1 noun=0xd6189d41 assetId=0x0 objId=2 gs=0
[DECK] SetCharacter i=2 noun=0x2ca50a9a assetId=0x0 objId=3 gs=0
Sent GamePrepareForStart
status 2/1 → [LPU] updateBits=0x1007 deckIndex=0
status 2/0 → [LPU] updateBits=0x1000
status 4/1 → [LPU] updateBits=0x1000
status 8/1 → Sent GameStart, DebugPing, [LPU] updateBits=0x1000
DebugPing(Dungeon) → DirectorState, QuickGameMsgs, Player Start, ObjectivesInitForLevel
[DEPLOY] playerId=0 creatureIndex=1 objId=2
Sent PlayerCharacterDeploy, [LPU] updateBits=0x1000 deckIndex=1
```

This **refutes several earlier hypotheses** and reframes the whole investigation:

- ❌ **D1 (assetId=0) — REFUTED.** The working C++ server sends **`assetId=0x0`** for all three
  squad characters (lines `SetCharacter i=0..2`). assetId=0 is therefore *fine*; it is **not** the
  cause. (C++ only sets a non-zero assetId when the level/noun XML provides one; for these
  creatures it's 0, and the client still works.)
- ❌ **gearScore — not it.** C++ sends `gs=0`; we hardcode `300`. C++ works with 0, so this is
  cosmetic, not the cause.
- ✅ **The deploy sequence + updateBits match what we already send.** `0x17f8` → `0x1007` →
  `0x1000`… → deploy `creatureIndex=1 objId=2` → `0x1000 deckIndex=1`. Our C# flow mirrors this.
- ⚠️ **C++ deploys `creatureIndex=1`** (the *middle* deck slot, objId=2), exactly like our
  `DeployedDeckIndex=1`. Match.

**Net:** the deck/deploy *packet path* is NOT where C++ and C# differ. The divergence is upstream,
in **Blaze lobby player data** (§8.2) — the only concrete server-data difference found so far.

> Note: the C++ reference in this capture does **not render players** (missing Lua/level scripts,
> `scaldron_4.level.xml` not found) but **does not crash**. Our C# *does* render the player. So the
> crash is specific to our build's data, and §8.2 is the first hard lead.

### 8.2 Blaze player-name divergence — FOUND & FIXED (2026-05-29)

User observed: in C# the player shows as **"HelloDarkspore"** (loading screen + briefly as the
nickname); in C++ it is consistently the real persona **"JeanxPereira"** everywhere.

Root cause: `GameManagerComponent.SendGameSetup` (`GameManagerComponent.cs`) hardcoded
`"HelloDarkspore"` in **two** places:
- `GameData.GameAttribs["GameOwnerName"] = "HelloDarkspore"` (line 151)
- `GamePlayer.PlayerName = "HelloDarkspore"` (line 161)

C++ uses the real persona name in both equivalents: `GNAM = user->get_name()`
(`GameManagerComponent.cpp:39`) and per-player `NAME = user->get_name()` (`:60`).

**Fix applied:** both now use `client.Username` (`= Account.Username`, "JeanxPereira").
`game.Name` was already correct (`"{client.Username}'s Game"`).

**Why this matters beyond cosmetics:** `NotifyGameSetup` establishes the **player roster** the
client binds its lobby + in-game HUD to. That C# struct is riddled with `// TODO: Add the rest of
the fields` — the placeholder name is a symptom that the **GamePlayer/GameData TDF is incomplete**
vs C++ `SendNotifyGameSetup` (which sets `BLOB, EXID, GID, NAME` per player and `APRS, CRIT, GID,
GNAM, GSET=287, GSTA=2, IGNO, NTOP, PROS` on the game). An incomplete roster entry is a plausible
upstream cause of the HUD/deck binding failing. **Next: full field-level diff of NotifyGameSetup
C++ vs C# (§9).**
- **Fix:** resolve each noun's DBPF asset id from `AssetDatabase` and set it on (a) the
  `LabsCharacterData.AssetId`, (b) the `GameObject`, (c) `GameObjectCreateData.AssetId`.

> **§8.1 supersedes the suspicion ordering below.** The runtime capture proved D1 (assetId) and
> gearScore are NOT the cause (C++ sends 0 for both and works). D2/D4 below are still real
> structural ports worth doing, but they are **not** the crash lead — §8.2 (Blaze player data) is.

**D2 — CharacterObject creation timing (real port, but NOT the crash — C++ sends assetId=0 too).**
- C++ creates the 3 squad world objects **in PreDungeon** inside `SetSquad → SetCharacter →
  ObjectManager::Create` (`Player.cpp:329`), binding noun/health/team/collision *before* the first
  PreDungeon LPU. The Dungeon deploy then just *references* existing objects (objId 1/2/3 in the
  capture). C# creates them only later in `OnPlayerStart`. Worth aligning for correctness, but the
  capture shows the client is happy with the C++ ordering whose assetId is still 0.

**D3 — `GamePrepareForStart` — NOT a divergence (cleared 2026-05-29).**
- C++ `SendGamePrepareForStart` (`Server.cpp:2064-2078`, the `start==true` branch) writes
  **4× u32 = 16-byte body** (`// Packet size: 0x10`): `chainData.GetLevel()`,
  `chainData.GetMarkerSet()`, `1`, `chainData.GetLevelIndex()`.
- C# `GamePrepareForStartPacket` writes the same 4× u32: `LevelHash, MarkerSetHash, 1, LevelIndex`.
  **They match.** (An earlier note here claimed 6×u32/24B — that misread a long comment block +
  the unused `else` branch above the real code. Retracted.)
- Caveat: `09-dungeon.md`/CLAUDE.md still call this "17 B" — the real body is 16 B (4×u32) + 1 B
  packet id = 17 B total, so "17 B" is the on-wire total. Consistent. Nothing to fix here.

**D4 — Hardcoded squad + creature fields.**
- C++ pulls each character's `noun / version / creatureType / gearScore / gearScoreFlattened`
  from the **real owned creature** (`Player.cpp:286-292`). C# hardcodes nouns
  `{1667741389, 749013658, 3591937345}`, `CreatureType` from a static `{2,0,3}`, `GearScore=300`.
- De-hardcoding (resolve squad → deck → creature → noun) aligns C# with C++ and is correct
  regardless. Note the capture: C++'s real squad creatures still came through with `gs=0` and
  `assetId=0`, so de-hardcoding alone won't change those — it's about *correctness*, not the crash.

**Verdict (updated 2026-05-29 after runtime capture):** D1 (assetId) and gearScore are **refuted**
— the working C++ server sends `assetId=0`/`gs=0` and the client is fine. D3 was a misread
(cleared). D2/D4 are real ports but not the crash. The **only concrete C++↔C# server-data
divergence found is the Blaze lobby player data** (§8.2: hardcoded "HelloDarkspore" name + an
incomplete `NotifyGameSetup` GamePlayer/GameData struct). That is now the lead.

---

## 9. Open items

0. **Full field-level diff of `NotifyGameSetup` (C++ vs C#) — THE LEAD (§8.2).** The C# struct is
   full of `// TODO: Add the rest of the fields`. Diff per-player (C++ sets `BLOB, EXID, GID, LOC,
   NAME, PID, SID, SLOT, STAT, TIDX, TIME, UGID, UID`) and per-game (`APRS, GID, GNAM, GPVH, GSET,
   GSID, GSTA, HNET, HSES, IGNO, MCAP, NQOS, NRES, NTOP, PGID, PRES, PSAS, SEED, UUID, VOIP, VSTR`)
   against ours. The `GameManagerComponent.cpp` `/* GOOD: */` comment block (lines ~20-98) is a
   captured reference dump — use it as the target. A malformed/incomplete roster entry is the
   prime suspect for the HUD/deck bind failing.
1. **Player name — FIXED 2026-05-29.** `GameOwnerName` + `GamePlayer.PlayerName` now use
   `client.Username` (populated at login from `account.Username`, was never assigned → null →
   client fell back to placeholder "HelloDarkspore"). Verify in-game shows the real persona.
2. **Map the show-trigger head (needs live Ghidra).** Open `Darkspore.exe` in the Ghidra instance
   *before* connecting, then decompile the deck tick that sets `deck+0x40` and the writer of
   `deck+0x20` (what actually binds the movieclip).
3. **Read `HUD_PlayerDeck.gfx` ActionScript** (loose on disk:
   `Darkspore\Data\FlashUI\animations~\HUD_PlayerDeck.gfx`, 15,794 B; `DeckEditor.gfx` alongside).
   Decompile (gfxexport / SWF-GFx decompiler) to see exactly what data the deck movieclip consumes.
4. **De-hardcode the squad (D4, correctness)** — resolve squad→deck→creature→noun. NOT the crash
   (C++ works with assetId=0/gs=0) but aligns C# with C++ and is needed for real creatures.
5. Remove the `[AUTH-DUMP]` debug line in `GameRestController.loginPlayerAccount` when done.
6. Phase-09 server gaps (Lua preload, enemy/obelisk spawn, objectives) — for combat, not the crash.

> Dropped from this list (resolved/refuted): "Fix AssetId=0" (refuted — C++ sends 0 and works),
> "Fix GamePrepareForStart shape" (was a misread; packets match), "create squad objects in
> PreDungeon" (D2, real port but not the crash — kept under §8.1 D2 as correctness work).

---

## 10. Address appendix (`Darkspore.exe`, base 0x00400000)

All **[V]** (verified, prior CE + Ghidra sessions; see `memory/deck-squad-consistency-crash.md`).

| Address | Symbol (functional) | Role |
|---|---|---|
| `0x00fd4768` | `cPlayerDeck` vtable | object size 0x88B |
| `0x00423f60` | in-game HUD factory | constructs cPlayerDeck (+ cHUDBase/cActionBar/cCatalysts) |
| `0x00420bf0` | deck init | RegisterCallback(OnCreatureCardsLoaded/OnCreatureClicked) + LoadMovie HUD_PlayerDeck.gfx |
| `0x0041c4f0` | GFx-load completion cb | fired before crash; copies strings (FUN_00401820); does not bind deck+0x20 |
| `0x0041fdc0` | `OnCreatureCardsLoaded` cb | fired before crash |
| `0x0041bb50` | `OnCreatureClicked` cb | registered by deck init |
| `0x0051b860` | `cPlayerDeck::UpdateHud` | per-frame tick; timer branch @0x0051b9b8 → FUN_004015b0(deck+0x20) |
| `0x004015b0` | GFx::Value invoke forwarder | `ECX=deck+0x20; MOV ECX,[ECX]; CALL`; ret 0x004015e1 |
| `0x00551f10` | dispatched GFx method | faults `MOV ECX,[EDI]` @ `0x00551f47`, this=0 (deck+0x20 value unbound, deck+0x28=0) |

**Not in this table (not mapped this session, need live Ghidra):** the show-trigger function,
the cards/gfx readiness flags and their offsets, and the writer of `deck+0x20`.
