# /recap/api — ReCap Control Endpoints

## Overview

This endpoint group is ReCap-specific (not part of the original Darkspore server protocol). It covers user registration (called from the ReCap launcher web UI), client-side log relay, creature PNG retrieval, and an admin panel API.

C++ router: `Game/API.cpp:331`. Dispatch chain: `API.cpp:336–350`.  
C# controller: `Adapters/Rest/ReCapRestController.cs` (`[RestController(Value="/recap/api")]`).

The C# controller **does not** set `ContentType` at the class level — each method specifies its own or leaves it null. The `api.game.log` method explicitly uses `application/json`.

## Endpoint Table

| Method / Route | Query `method=` | C++ handler | C# handler | Status | Notes |
|---|---|---|---|---|---|
| GET/POST `/recap/api` | `api.game.registration` | `API.cpp:337` → `recap_game_registration():752` | `ReCapRestController.cs:38` | ✅ | Creates account + seeds all creatures/parts; returns `{"success":true}` |
| GET/POST `/recap/api` | `api.game.status` | `API.cpp:339` → `recap_game_status():833` | **Missing** | ❌ | C++ returns `{"enablePlayButton":bool,"progressLabel":"..."}` based on `Installer::isRunning()`; C# has no handler |
| GET/POST `/recap/api` | `api.game.log` | `API.cpp:341` → `recap_game_log():842` | `ReCapRestController.cs:30` | ✅ | Relays browser console.log to server stdout/logger; body is plain text |
| GET/POST `/recap/api` | `api.game.getCreatureLargePng` | *(not in C++)* | `ReCapRestController.cs:63` | ⚠️ | C#-only endpoint; serves stored base64 PNG as binary; no C++ equivalent |
| GET/POST `/recap/api` | `api.game.getCreatureThumbPng` | *(not in C++)* | `ReCapRestController.cs:72` | ⚠️ | C#-only endpoint; serves stored base64 thumb PNG as binary |
| GET/POST `/recap/api` | `api.panel.listUsers` | `API.cpp:343` → `recap_panel_listUsers():848` | **Missing** | ❌ | C++ body is fully commented out (stub); not implemented in C# either |
| GET/POST `/recap/api` | `api.panel.getUserInfo` | `API.cpp:345` → `recap_panel_getUserInfo():875` | **Missing** | ❌ | C++ body is fully commented out (stub); not implemented in C# either |
| GET/POST `/recap/api` | `api.panel.setUserInfo` | `API.cpp:347` → `recap_panel_setUserInfo():901` | **Missing** | ❌ | C++ body is fully commented out (stub); not implemented in C# either |

## Response Shapes

### api.game.registration

Content-Type: `application/json`

```json
{"success": true}
```

On error (C++ only, via exception):
```json
{"message": "error text"}
```

The C# implementation always returns `{"success":true}` and does not surface errors to the client; exceptions propagate to the global exception handler.

Both implementations follow the same seeding logic on registration:
- Create account with maxed-out test values (level 100, all unlocks, 10M DNA)
- Add all template creature parts
- Unlock all creatures from template database
- Create starter squads (first 3 templates)

### api.game.status (C++ only)

Content-Type: `application/json`

```json
{
  "enablePlayButton": true,
  "progressLabel": ""
}
```

`enablePlayButton` is `!Installer::isRunning()`. Used by the launcher UI to show/hide the Play button while asset installation is in progress. **Not implemented in C#.**

### api.game.getCreatureLargePng / api.game.getCreatureThumbPng (C# only)

Content-Type: raw binary (PNG)

Query params: `id` (creature ID as integer).

Returns the base64-decoded PNG blob stored in the creature's `LargePngBase64` / `ThumbPngBase64` field. These URLs are written into `creature.LargePngUrl` / `creature.ThumbPngUrl` during `api.creature.updateCreature`.

In C++, PNG files are written to disk under `{storage_path}/creature_png/{id}_{version}_large.png` and served via the `/creature_png/` static route instead.

### api.game.log

Content-Type: `application/json` (declared by `[RequestMapping]`)

Request body: plain text (browser `console.log` output from the launcher WebKit context).  
Response: empty body (`new byte[]{}` in C#, no body set in C++).

## Panel API (Commented Out in Both Implementations)

`api.panel.listUsers`, `api.panel.getUserInfo`, and `api.panel.setUserInfo` are fully commented out in C++ (`API.cpp:848-930`) and have no C# equivalents. They were designed as a local admin panel for inspecting and editing user data. The C++ commented code suggests they would return JSON with `{"stat":"ok","users":[...]}` shapes.
