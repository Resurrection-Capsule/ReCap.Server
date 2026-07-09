# Client REST / SporeNet API flow — real contract (Ghidra client)

**Purpose:** map the **real** SporeNet REST flow as the retail client actually drives it — so the C# server implements the authentic system, **not** dalkon's C++ hardcoded fixtures. Mapped 2026-07-08.

**Why not C++:** dalkon's `Game/API.cpp` handlers are reverse-engineered approximations returning hardcoded/fixture data (`getGame` game_id=1, `Vendor` offer list = parts 100-149 with magic stats, empty `settings`/`feed` blocks). "C++ parity" would replicate the fixtures. The **client (Darkspore.exe, Ghidra)** is the real contract.

## Architecture

The client's SporeNet REST layer is in **Darkspore.exe**, class **`SP_App::ApiRequest`** — one request-builder subclass per `api.*` method, each with its own vtable (`PTR_FUN_00fdXXXX`), clustered `0x00460000–0x00468000`. Pattern:
- **Builder** (ctor): sets the method string (`FUN_00402270("api.x.y","")`) + appends named params (`memmove(dst,"<param>",n)` + value). Named `ClientRest::Build<Endpoint>Request`.
- **Response handler** = **vtable slot +0x30** (consistent across all endpoints) — parses the returned XML `<response>` into the client's model. Named `ClientRest::Parse<X>` / `ClientNet::On<X>Response`.

Method-name string cluster: `0x00fda8fc–0x00fdb420`. `getLeaderboard*` (@0x010e2e..) is a **separate Blaze stats component**, not this REST API (social/multiplayer, out of single-player scope).

## Endpoint map (all request builders decompiled + named in Ghidra)

| Method | Builder (`ClientRest::…`) @ | Request params |
|---|---|---|
| `api.account.auth` | `BuildAuthRequest` @0x00464670 | `key`="persona::id", `cookie`, `build`="5.3.0.127", + `include_creatures/decks/feed/settings/server_tuning` (full auth) |
| `api.account.getAccount` | `BuildGetAccountRequest` @0x00464c50 | *(same account fetch; response = OnAccountResponse)* |
| `api.account.logout` | `BuildLogoutRequest` @0x0045fd90 | *(none)* |
| `api.account.setSettings` | `BuildSetSettingsRequest` @0x00465e70 | `settings` = `key,value;key,value;…` |
| `api.account.setNewPlayerStats` | `BuildSetNewPlayerStatsRequest` @0x00465cc0 | `new_player_progress` (int), `new_player_inventory` (int) |
| `api.account.unlock` | `BuildUnlockRequest` @0x00467710 | `unlock_id` (int) |
| `api.creature.updateCreature` | `BuildUpdateCreatureRequest` @0x00466140 | `id, cost, name, points, stats, stats_ability_keyvalues, parts, thumb(+thumb_crc), large(+large_crc)` |
| `api.creature.unlockCreature` | `BuildUnlockCreatureRequest` @0x00465fc0 | `template_id` |
| `api.creature.resetCreature` | `BuildResetCreatureRequest` @0x00464f80 | `id` |
| `api.deck.updateDecks` | `BuildUpdateDecksRequest` @0x00466f90 | `pve_creatures` (9× `%llu`), `pvp_creatures` (9×), `pve_active_slot`, `pvp_active_slot` |
| `api.game.getGame` | `BuildGetGameRequest` @0x00465110 | `game_id` (int64) |
| `api.game.getRandomGame` | `BuildGetGameRequest` (shared) | `replay_version` |
| `api.game.getReplay` | `BuildGetReplayRequest` @0x00465590 | game id (int64) + `round_id` |
| `api.game.exitGame` | `BuildExitGameRequest` @0x0045fcd0 | *(none)* |
| `api.inventory.getPartList` | `BuildGetPartListRequest` @0x00465310 | `count`=10000, `filter` = `creature_id-N;market_status_full-owned;` |
| `api.inventory.getPartOfferList` | `BuildGetPartOfferListRequest` @0x004602c0 | *(none — server returns current offer)* |
| `api.inventory.updatePartStatus` | `BuildUpdatePartStatusRequest` @0x004673c0 | `part_id` (list `%llu`), `status` (list `%llu`), `operator` (optional) |
| `api.inventory.vendorParts` | `BuildVendorPartsRequest` @0x00467890 | `transactions` = list of `<typeChar><partId:int64>`, type ∈ {s,b,w,p,f} (sell/buy/weapon/part/flair) |

## Response contracts

### `auth` / `getAccount` → `ClientNet::OnAccountResponse` @0x00469190 (vtable +0x30)
Reads `<response>` in order:

| Node | Parser | Into |
|---|---|---|
| `timestamp` | inline | — |
| `account` | `ClientRest::ParseAccountBlock` @0x00471110 | obj+0x64 |
| `creatures` | `ClientRest::ParseCreaturesBlock` @0x00463990 | obj+0x58 |
| `decks` | `FUN_00468980` (deck list; not yet field-mapped) | obj+0x5c |
| `items` (parts) | `ClientRest::ParseItemsBlock` @0x00463de0 | obj+0x60 |
| `settings` | inline — `key,value` pairs; values `"on"`/`"off"`→bool else `_wtol`→int | settings store |
| `server_tuning` | `ClientRest::ParseServerTuningBlock` @0x0045f8a0 (OPTIONAL) | obj+0x68 |

**★ `include_feed` is requested but NO `feed` node is parsed here** (feed handled elsewhere or ignored — verify before implementing).

### `server_tuning` block → `ClientRest::ParseServerTuningBlock` @0x0045f8a0 — **item-store economy config**
- `itemstore_offer_period` (int), `itemstore_current_expiration` (unix time)
- `itemstore_cost_multiplier_{basic,uncommon,rare,epic,unique,rareunique,epicunique}` (7× float)

(A *different* server_tuning consumer, `FUN_0046bdd0`, reads `throttle`/`blaze_service_name` — likely Blaze/connection config, not the item store.)

### `getPartOfferList` → `ClientRest::ParsePartOfferListResponse` @0x00460380 (vtable +0x30)
`timestamp` + `expires` (int64 unix — offer expiry, obj+0x58) + `parts` (list → `FUN_0045f4c0`, obj+0x78).

### Remaining response handlers (mechanical follow-up)
Each endpoint's response = **its builder object's vtable slot +0x30**; most reuse the block sub-parsers (`ParseCreaturesBlock`/`ParseItemsBlock`/deck-list) or return `timestamp` + the updated entity. To field-map any: read the builder's `PTR_FUN_00fdXXXX` vtable, take slot +0x30, decompile. Endpoints still to field-map: `getGame`/`getReplay` (game-history XML — needs a server-side history store for real data), `updateCreature`/`updateDecks`/`unlock`/`getPartList`/`updatePartStatus`/`setSettings` (mostly ack + updated entity).

## Real-vs-C++ implementation insights (the whole point)
1. **`settings` + `server_tuning` are mandatory** — client parses them; C++ leaves `settings` empty and never emits `server_tuning`. Biggest login-flow gap.
2. **Vendor is server-driven** — the offer set + `expires` come from `getPartOfferList`; per-rarity pricing + refresh timing come from `server_tuning.itemstore_*`. The C++ hardcoded 100-149 fixture is NOT the system. `vendorParts` submits a `<type><partId>` transaction batch.
3. **Creature save is heavy** — `updateCreature` uploads rendered `thumb`/`large` PNGs + CRCs + stats + parts. The server stores & serves these (ties to `/game/service/png`).
4. **Decks** = 9 creatures per pve/pvp + active slot each (matches the rebuilt C# deck system).
5. **`include_feed` requested, not parsed** in the account response — resolve before building feed.

## Status
- ✅ Architecture + class (`SP_App::ApiRequest`) + builder/response(+0x30) pattern.
- ✅ **All 18 request contracts decompiled + named in Ghidra** (`ClientRest::Build*Request`).
- ✅ Response contracts: full `account`/`auth` structure + `server_tuning` (item-store economy) + `getPartOfferList` (offer+expires+parts). Named + plate-commented.
- ⏳ Field-maps of block sub-parsers (`ParseAccountBlock`, `ParseCreaturesBlock`, `ParseItemsBlock`, deck-list) + the remaining per-endpoint +0x30 response handlers — pinpointed, mechanical.
