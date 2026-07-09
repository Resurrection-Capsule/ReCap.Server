# Client REST / SporeNet API flow — real contract (Ghidra client)

**Purpose:** map the **real** SporeNet REST flow as the retail client actually drives it — so the C# server implements the authentic system, **not** dalkon's C++ hardcoded fixtures. Started 2026-07-08.

**Why not C++:** dalkon's `Game/API.cpp` handlers are reverse-engineered approximations — many return hardcoded/fixture data (`getGame` game_id=1, `Vendor` offer list = parts 100-149 with magic stats, empty `settings`/`feed` blocks). Reaching "C++ parity" would replicate the fixtures. The **client (Darkspore.exe, Ghidra)** is the real contract: what it sends, and what fields it reads back.

## Where the client consumes REST

The SporeNet REST layer lives in **Darkspore.exe** (not just webview JS). Each `api.*` method is a **request-builder object** with its own vtable (`PTR_FUN_00fdXXXX`), clustered around `0x00464000–0x00468000`. The builder ctor sets the method string + appends named params; the response XML is parsed by a method on the same object's vtable (response-parse side — mapping in progress).

Method-name string cluster: `0x00fda8fc–0x00fdb420` (ascii). Base host/path handling is upstream of these builders.

### Endpoint → request-builder function (verified via string xref)

| Method | Builder fn | Method string @ |
|---|---|---|
| `api.account.auth` | `FUN_00464670` | 0x00fdad78 |
| `api.account.getAccount` | `FUN_00464c50` | 0x00fdadcc |
| `api.account.logout` | *(string 0x00fda980)* | 0x00fda980 |
| `api.account.setSettings` | *(TBD)* | 0x00fdb17c |
| `api.account.setNewPlayerStats` | *(TBD)* | 0x00fdb110 |
| `api.account.unlock` | *(TBD)* | 0x00fdb3b8 |
| `api.creature.updateCreature` | `FUN_00466140` | 0x00fdb254 |
| `api.creature.unlockCreature` | *(TBD)* | 0x00fdb1a0 |
| `api.creature.resetCreature` | *(TBD)* | 0x00fdaeb0 |
| `api.deck.updateDecks` | `deck_updateDecks_build` (named) | 0x00fdb2f0 |
| `api.game.getGame` | `FUN_00465110` | 0x00fdaed4 |
| `api.game.getRandomGame` | *(TBD)* | 0x00fdaef8 |
| `api.game.getReplay` | *(TBD)* | 0x00fdb024 |
| `api.game.exitGame` | *(TBD)* | 0x00fda8fc |
| `api.inventory.getPartList` | *(TBD)* | 0x00fdaf8c |
| `api.inventory.getPartOfferList` | `FUN_004602c0` | 0x00fdaa0c |
| `api.inventory.updatePartStatus` | *(TBD)* | 0x00fdb354 |
| `api.inventory.vendorParts` | `FUN_00467890` | 0x00fdb420 |

> **Note:** `getLeaderboard*` (strings @0x010e2e..) are a **separate Blaze stats-component** namespace, not this REST API — social/multiplayer, out of the single-player REST flow.

## Verified request contracts (decompiled)

### `api.account.auth` — `FUN_00464670`
The client's login. Params it appends:
- `key` = `"<persona>::<id>"` (fmt `%s::%s`) or `"<persona>::0"` (fmt `%s::0`) when no id.
- `cookie` = *(session cookie)*.
- `build` = `"5.3.0.127"` (game version — the server must accept/expect this).
- **When full auth** (a flag at `+0x24` is 0), also requests these response sub-blocks:
  `include_creatures`, `include_decks`, `include_feed`, `include_settings`, `include_server_tuning` (each a bool param).

**★ Real-contract insight:** the client explicitly asks the auth response to carry
`creatures / decks / feed / settings / server_tuning`. C++ leaves `settings`/`feed` **empty**
(its stub) — the authentic server must **populate** them. `include_server_tuning` is a whole
block the C# server does not emit at all yet. This is the single biggest gap in the login flow
and is client-driven, not a C++ detail.

### `api.inventory.vendorParts` — `FUN_00467890`
The vendor buy/sell transaction. Params:
- `transactions` = a separator-joined list; **per entry `"%c%I64d"` = `<typeChar><partId:int64>`**,
  where `typeChar` ∈ `{s, b, w, p, f}` (sell / buy / weapon / part / flair — matches C++'s
  `'s'`/`'f'`/`'w'`/`'p'` transaction switch, which is itself partial).
- Carries a 64-bit id from a subsystem vtable call (`*(param_1+0x18)` ← `(**(vtable+0x74))()`) —
  likely the current game/session id.

**★ Real-contract insight:** vendor is transaction-batched (`<type><partId>` list), not a single
buy. The authentic server processes each transaction and must return the updated inventory/gold
state (response-parse side TBD). C++'s hardcoded 100-149 offer list is a fixture, not the system.

## Method to complete the map (main-session Ghidra only)

Ghidra `decompile_function` is reachable only from the main session (subagents can't). For each endpoint:

1. **Request:** `decompile_function <builder fn>` → read the `FUN_00402270("api.x.y","")` method set + every param append (`memmove(dst,"<paramName>",n)` + `FUN_00402fb0`/`FUN_00402270` value). That is the exact request contract.
2. **Response:** read the builder's vtable (`PTR_FUN_00fdXXXX` set at `*param_1`) → the response-handler slot parses the returned XML into the client's model. Decompile it to list the exact response fields/tags the client reads (this is what the server MUST emit; anything the client doesn't read is fixture noise to drop).
3. Record request + response here; then implement the C# to satisfy the **read** fields, dropping C++ hardcode the client never consumes.

### Priority (single-player value)
1. `auth` / `getAccount` response parse — the `include_*` blocks (settings + server_tuning are the real gaps). Highest leverage: the whole session bootstraps here.
2. `vendorParts` + `getPartOfferList` response — the parts economy (real single-player progression).
3. `updateCreature` / `updateDecks` / `updatePartStatus` — persistence round-trips (mostly working; verify the read fields).
4. `getGame` / `getRandomGame` / `getReplay` — game-history/replay; needs a history store for real data (lower priority, launcher feature).

## Verified response contracts (decompiled)

### `api.account.auth` / `getAccount` response — `ClientNet::OnAccountResponse` @0x00469190
(vtable slot +0x30 of the auth request object). Walks `<response>` and reads, in order:

| Node | Parser (renamed in Ghidra) | Into |
|---|---|---|
| `timestamp` | inline | — |
| `account` | `ClientRest::ParseAccountBlock` @0x00471110 | obj+0x64 |
| `creatures` | `ClientRest::ParseCreaturesBlock` @0x00463990 | obj+0x58 |
| `decks` | `FUN_00468980` (generic list parser; not yet isolated) | obj+0x5c |
| `items` (parts) | `ClientRest::ParseItemsBlock` @0x00463de0 | obj+0x60 |
| `settings` | inline — key/value; values `"on"`/`"off"` → bool, else `_wtol` → int | settings store |
| `server_tuning` | `ClientRest::ParseServerTuningBlock` @0x0045f8a0 (OPTIONAL, presence-guarded) | obj+0x68 |

**★ Real gaps vs C++:** the client parses `settings` (on/off/int) and `server_tuning` — C++ leaves
`settings` empty and never emits `server_tuning`. `include_feed` is requested but **no `feed` node is
parsed** in OnAccountResponse (feed handled elsewhere or ignored — verify before implementing feed).

### `server_tuning` block — `ClientRest::ParseServerTuningBlock` @0x0045f8a0
**This block is the item-store (vendor) economy config** — the vendor is **server-driven pricing**, not
the C++ hardcoded 100-149 fixture. Wide-string child fields the client reads:
- `itemstore_offer_period` (int) — offer refresh period
- `itemstore_current_expiration` (unix time) — current offer expiry
- `itemstore_cost_multiplier_basic|uncommon|rare|epic|unique|rareunique|epicunique` (7× float) — per-rarity pricing

→ To build the authentic vendor: the server emits these tuning values in the account response; the client
prices/refreshes the store from them. `getPartOfferList` supplies the actual offered parts; `vendorParts`
submits the buy/sell transaction batch. (`throttle`/`blaze_service_name` are a *different* server_tuning
consumer — `FUN_0046bdd0` — likely the Blaze/connection config, not the item store.)

## Status
- ✅ API-client location + per-endpoint builder pattern identified.
- ✅ Request contracts: `auth`, `vendorParts`.
- ✅ Response contract: full `account`/`auth` top-level structure + `server_tuning` (item-store economy) fields.
- ✅ Ghidra annotated: `ClientRest::Build{Auth,GetAccount,GetGame,GetPartOfferList,UpdateCreature,VendorParts}Request`, `ClientRest::Parse{Account,Creatures,Items,ServerTuning}Block`, plate comments on `OnAccountResponse` + `ParseServerTuningBlock`.
- ⏳ Sub-parser field maps: `ParseAccountBlock` (account fields), `ParseCreaturesBlock`, `ParseItemsBlock`, `FUN_00468980` (decks), `ParseServerTuningBlock` done.
- ⏳ Remaining endpoints' request+response: getGame/getRandomGame/getReplay, getPartOfferList response, updateCreature/updateDecks/updatePartStatus response, setSettings, unlock, logout.
