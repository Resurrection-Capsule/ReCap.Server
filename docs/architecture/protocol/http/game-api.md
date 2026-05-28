# /game/api — Game API Endpoints

## Overview

This is the primary game REST surface. The client sends most player-facing calls here. All requests go to the single path `/game/api`; the `method=` query parameter selects the handler.

C++ router: `Game/API.cpp:398`. Dispatch chain: `API.cpp:455–510`.  
C# controller: `Adapters/Rest/GameRestController.cs` (`[RestController(Value="/game/api")]`).

**Auth mechanism:** The `token=` parameter carries the session auth token. C++ resolves it to a `SporeNet::User` via `GetUserByAuthToken()` and also sets a `Set-Cookie: token=...` response header. C# resolves it via `accountService.getAccountByAuthToken()`. The `key=` parameter (used by `api.account.auth`) carries `{auth_token}::0`.

**POST body:** C++ parses `multipart/form-data` POST bodies and merges them into URI parameters. C# reads POST body separately via `HTTPHelper.GetParametersFromRequest()`.

## Endpoint Table

| Method / Route | Query `method=` | C++ handler | C# handler | Status | Notes |
|---|---|---|---|---|---|
| GET/POST `/game/api` | `api.account.auth` | `API.cpp:471` → `game_account_auth():1181` | `GameRestController.cs:42` | ✅ | Main login; see response shape below |
| GET/POST `/game/api` | `api.account.setNewPlayerStats` | `API.cpp:455` (aliased to auth) | `GameRestController.cs:249` | ✅ | Redirected to auth handler in both impls |
| GET/POST `/game/api` | `api.account.getAccount` | `API.cpp:474` → `game_account_getAccount():1412` | `GameRestController.cs:117` | ✅ | GET returns account+decks; POST returns account+optional subsets |
| GET/POST `/game/api` | `api.account.logout` | `API.cpp:476` → `game_account_logout():1510` | `GameRestController.cs:194` | ✅ | Invalidates session token |
| GET/POST `/game/api` | `api.account.unlock` | `API.cpp:478` → `game_account_unlock():1517` | `GameRestController.cs:242` | ❌ | C# returns `null` (unimplemented) |
| GET/POST `/game/api` | `api.account.setSettings` | `API.cpp:480` → `game_account_setSettings():1535` | `GameRestController.cs:215` | ✅ | Parses `Key,Val;` pairs; C# persists them to DB |
| GET/POST `/game/api` | `api.account.searchAccounts` | `API.cpp:482` → `game_account_searchAccounts():1563` | `GameRestController.cs:209` | ❌ | C# returns `null` (unimplemented) |
| GET/POST `/game/api` | `api.status.getStatus` | `API.cpp:459` → `game_status_getStatus():982` | `GameRestController.cs:571` | ✅ | See response shape below |
| GET/POST `/game/api` | `api.status.getBroadcastList` | `API.cpp:461` → `game_status_getBroadcastList():1035` | `GameRestController.cs:557` | ✅ | Returns hardcoded broadcast list |
| GET/POST `/game/api` | `api.inventory.getPartList` | `API.cpp:463` → `game_inventory_getPartList():1044` | `GameRestController.cs:417` | ✅ | See response shape below |
| GET/POST `/game/api` | `api.inventory.getPartOfferList` | `API.cpp:465` → `game_inventory_getPartOfferList():1068` | `GameRestController.cs:442` | ⚠️ | C# returns empty parts list; C++ returns vendor offer parts |
| GET/POST `/game/api` | `api.inventory.vendorParts` | `API.cpp:467` → `game_inventory_vendorParts():1086` | `GameRestController.cs:493` | ⚠️ | C# handles `s`/`f` transactions; `w`/`p`/`b` are TODO in both |
| GET/POST `/game/api` | `api.inventory.updatePartStatus` | `API.cpp:469` → `game_inventory_updatePartStatus():1156` | `GameRestController.cs:461` | ✅ | Updates per-part status field |
| GET/POST `/game/api` | `api.creature.getCreature` | `API.cpp:492` → `game_creature_getCreature():1751` | `GameRestController.cs:258` | ✅ | Returns creature with optional abilities/parts |
| GET/POST `/game/api` | `api.creature.getTemplate` | `API.cpp:495` → `game_creature_getTemplate():1775` | `GameRestController.cs:278` | ❌ | C# returns `null` (unimplemented) |
| GET/POST `/game/api` | `api.creature.resetCreature` | `API.cpp:489` → `game_creature_resetCreature():1709` | `GameRestController.cs:293` | ⚠️ | C# does not actually reset creature stats |
| GET/POST `/game/api` | `api.creature.unlockCreature` | `API.cpp:491` → `game_creature_unlockCreature():1732` | `GameRestController.cs:317` | ✅ | Creates creature from template, returns `<creature_id>` |
| GET/POST `/game/api` | `api.creature.updateCreature` | `API.cpp:498` → `game_creature_updateCreature():1798` | `GameRestController.cs:336` | ✅ | Saves gear, parts, stats, base64 PNG blobs |
| GET/POST `/game/api` | `api.deck.updateDecks` | `API.cpp:500` → `game_deck_updateDecks():1866` | `GameRestController.cs:393` | ❌ | C# returns `null` (unimplemented) |
| GET/POST `/game/api` | `api.game.getGame` | `API.cpp:483` → `game_game_getGame():1615` | `GameRestController.cs:405` | ❌ | C# returns `null` (unimplemented) |
| GET/POST `/game/api` | `api.game.getRandomGame` | `API.cpp:485` → `game_game_getRandomGame():1653` | `GameRestController.cs:411` | ❌ | C# returns `null` (unimplemented) |
| GET/POST `/game/api` | `api.game.exitGame` | `API.cpp:487` → `game_game_exitGame():1702` | `GameRestController.cs:399` | ❌ | C# returns `null` (unimplemented) |
| GET/POST `/game/api` | `api.leaderboard.getLeaderboard` | `API.cpp:502` → `game_leaderboard_getLeaderboard():1883` | `GameRestController.cs:551` | ❌ | C# returns `null` (unimplemented) |

**Missing from C# entirely (not even a `[RequestMapping]`):** none — all C++ methods have a C# counterpart, but 7 of them return `null`.

---

## Key Response Shapes

### api.account.auth

Content-Type: `text/xml`. Query params: `key={token}::0`, `include_creatures`, `include_decks`, `include_feed`, `include_settings`, `include_server_tuning`, `cookie`, `new_player_progress`.

```xml
<response>
  <stat>ok</stat>
  <version>5.3.0.127</version>
  <timestamp>1748300000000</timestamp>  <!-- Unix ms -->
  <exectime>1</exectime>
  <account>
    <tutorial_completed>0</tutorial_completed>
    <chain_progression>24</chain_progression>
    <creature_rewards>100</creature_rewards>
    <current_game_id>1</current_game_id>
    <current_playgroup_id>1</current_playgroup_id>
    <default_deck_pve_id>1</default_deck_pve_id>
    <default_deck_pvp_id>1</default_deck_pvp_id>
    <level>100</level>
    <avatar_id>0</avatar_id>
    <id>1</id>
    <new_player_inventory>1</new_player_inventory>
    <new_player_progress>9500</new_player_progress>
    <cashout_bonus_time>1</cashout_bonus_time>
    <star_level>10</star_level>
    <unlock_catalysts>1</unlock_catalysts>
    <unlock_diagonal_catalysts>1</unlock_diagonal_catalysts>
    <unlock_fuel_tanks>1</unlock_fuel_tanks>
    <unlock_inventory>1</unlock_inventory>
    <unlock_pve_decks>2</unlock_pve_decks>
    <unlock_pvp_decks>1</unlock_pvp_decks>
    <unlock_stats>1</unlock_stats>
    <unlock_inventory_identify>2500</unlock_inventory_identify>
    <unlock_editor_flair_slots>1</unlock_editor_flair_slots>
    <upsell>1</upsell>
    <xp>10000</xp>
    <grant_all_access>1</grant_all_access>
    <cap_level>0</cap_level>
    <cap_progression>0</cap_progression>
  </account>

  <!-- if include_creatures=true -->
  <creatures>
    <creature>
      <id>1</id>
      <name>CreatureName</name>
      <png_thumb_url>http://...</png_thumb_url>
      <noun_id>12345678</noun_id>
      <version>1</version>
      <gear_score>1.0</gear_score>
      <item_points>0.0</item_points>
    </creature>
  </creatures>

  <!-- if include_decks=true -->
  <decks>
    <deck>
      <name>Deck1</name>
      <category>pve</category>
      <id>1</id>
      <slot>1</slot>
      <locked>0</locked>
      <creatures> ... same shape as creatures ... </creatures>
    </deck>
  </decks>

  <!-- if include_feed=true -->
  <feed/>

  <!-- if include_settings=true -->
  <settings/>

  <!-- if include_server_tuning=true -->
  <server_tuning>
    <itemstore_offer_period>1748300000</itemstore_offer_period>
    <itemstore_current_expiration>1748310800000</itemstore_current_expiration>
    <itemstore_cost_multiplier_basic>1</itemstore_cost_multiplier_basic>
    <itemstore_cost_multiplier_uncommon>1.1</itemstore_cost_multiplier_uncommon>
    <itemstore_cost_multiplier_rare>1.2</itemstore_cost_multiplier_rare>
    <itemstore_cost_multiplier_epic>1.3</itemstore_cost_multiplier_epic>
    <itemstore_cost_multiplier_unique>1.4</itemstore_cost_multiplier_unique>
    <itemstore_cost_multiplier_rareunique>1.5</itemstore_cost_multiplier_rareunique>
    <itemstore_cost_multiplier_epicunique>1.6</itemstore_cost_multiplier_epicunique>
  </server_tuning>
</response>
```

---

### api.status.getStatus

Query params: `include_broadcasts` (bool).

```xml
<response>
  <stat>ok</stat>
  <version>5.3.0.127</version>
  <timestamp>1</timestamp>
  <exectime>1</exectime>
  <status>
    <api>
      <health>1</health>
      <revision>1</revision>
      <version>1</version>
    </api>
    <blaze>
      <health>1</health>
    </blaze>
    <gms>
      <health>1</health>
    </gms>
    <nucleus>
      <health>1</health>
    </nucleus>
    <game>
      <health>1</health>
      <countdown>90</countdown>
      <open>1</open>
      <throttle>1</throttle>
      <vip>1</vip>
    </game>
  </status>

  <!-- if include_broadcasts=true -->
  <broadcasts>
    <broadcast>
      <id>16</id>
      <end>17</end>
      <start>18</start>
      <type>19</type>
      <message>Bananas for sale! ...</message>
      <tokens>12345678</tokens>
    </broadcast>
  </broadcasts>
</response>
```

---

### api.inventory.getPartList

Query params: `token`, `count` (C# only; ignored in C++), `filter` (C# only).

```xml
<response>
  <stat>ok</stat>
  <version>5.3.0.127</version>
  <timestamp>1</timestamp>
  <exectime>1</exectime>
  <parts>
    <part>
      <id>1</id>
      <reference_id>0</reference_id>
      <creature_id>0</creature_id>      <!-- 0 = unequipped -->
      <creation_date>1748300000</creation_date>
      <cost>100</cost>
      <level>5</level>
      <rarity>2</rarity>               <!-- 0=basic 1=uncommon 2=rare 3=epic 4=unique -->
      <market_status>1</market_status> <!-- 0=unknown 1=full-owned 2=unknown 3=buyback -->
      <status>0</status>
      <usage>1</usage>
      <is_flair>0</is_flair>
      <rigblock_asset_id>868969257</rigblock_asset_id>
      <prefix_asset_id>0</prefix_asset_id>
      <prefix_secondary_asset_id>0</prefix_secondary_asset_id>
      <suffix_asset_id>0</suffix_asset_id>
    </part>
  </parts>
</response>
```

**C# note:** The C# implementation only returns parts where `CreatureId is null` (unequipped inventory items). Equipped parts are excluded. This may differ from C++ behavior which returns all user parts.

---

## Unimplemented in C# (return null)

The following methods have `[RequestMapping]` handlers that return `null`, causing the C# dispatcher to throw `UnimplementedMethodException`:

- `api.account.unlock`
- `api.account.searchAccounts`
- `api.creature.getTemplate`
- `api.deck.updateDecks`
- `api.game.getGame`
- `api.game.getRandomGame`
- `api.game.exitGame`
- `api.leaderboard.getLeaderboard`
