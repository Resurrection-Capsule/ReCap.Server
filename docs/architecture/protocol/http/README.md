# HTTP / REST Surface Overview

ReCap serves a single HTTP server on port **8033** (configurable via `--port`). All game-related REST traffic hits this server. The Blaze login stack lives on separate ports (42127/42125) and is not documented here.

## Route Architecture

### C++ (ground truth)

`Game/API.cpp` — `API::setup()` (line 315) registers every route with a custom Boost.Beast router (`HTTP/Router.cpp`). The `/game/api`, `/bootstrap/api`, `/survey/api`, and `/recap/api` routes are *method-dispatch* endpoints: a single path handles many logical methods, selected by the `method=` query parameter. Routing is a flat `if/else if` chain inside the route lambda.

Static assets (launcher HTML, PNG thumbnails, favicon) are served by `API::responseWithFileInStorage()` which walks the configured `CONFIG_WWW_STATIC_PATH` directory.

### C# (ReCap)

`Adapters/Rest/Api.cs` — `Api.ProcessRequest()` is the entry point. It discovers all classes annotated with `[RestController(Value="/some/path")]` via reflection and dispatches by URI path, then by `[RequestMapping(Name="api.foo.bar")]` on individual methods.

Static files that do not match any controller path are served by `StaticStorageAdapter.GetFile()`.

The C# router does **exact** path matching (`apiPath == dnAttribute.Value`), so regex routes present in C++ (e.g. `/bootstrap/launcher/([...])`, `/web/sporelabs/*`) have no counterpart.

## Route Groups

| Group | Path | C++ handler | C# controller |
|---|---|---|---|
| Bootstrap | `/bootstrap/api` | `API::setup():354` | `BootstrapRestController.cs` |
| Launcher static | `/bootstrap/launcher/` | `API::setup():374-395` | Static fallback |
| Game API | `/game/api` | `API::setup():398` | `GameRestController.cs` |
| Creature PNGs | `/creature_png/` `/template_png/` `/game/service/png` | `API::setup():514-574` | Static fallback / missing |
| Survey | `/survey/api` | `API::setup():577` | `SurveyRestController.cs` |
| ReCap panel | `/recap/api` | `API::setup():331` | `ReCapRestController.cs` |
| QoS | `/qos/qos` `/qos/firewall` `/qos/firetype` | `API::setup():591-706` | **Missing** |
| Web/Sporelabs | `/web/sporelabs*` `/web/sporelabsgame/*` | `API::setup():722-739` | **Missing** |
| Telemetry | `/telemetryevent` | `API::setup():325` | **Missing** |
| Misc static | `/favicon.ico` `/assets/*` `/api` | `API::setup():318-720` | Static fallback |

## Per-Group Detail Sheets

- [bootstrap-api.md](bootstrap-api.md) — launcher config, settings, patches
- [game-api.md](game-api.md) — account, creature, inventory, game, leaderboard, status
- [recap-api.md](recap-api.md) — registration, panel, PNG retrieval, logging
- [survey-api.md](survey-api.md) — survey list
- [static-and-qos.md](static-and-qos.md) — static file serving, PNG endpoints, QoS, web/sporelabs, telemetry

## Common Response Envelope (XML)

All XML responses share a root `<response>` element with these common children (populated by `add_common_keys()` in C++, `ResponseContract` base in C#):

```xml
<response>
  <stat>ok</stat>        <!-- "ok" or "error" -->
  <code>200</code>       <!-- HTTP status integer -->
  <result>1</result>     <!-- 1=success, 0=error -->
  <version>5.3.0.127</version>
  <timestamp>1234567890</timestamp>
  <exectime>1</exectime>
</response>
```

JSON responses (ReCap-specific endpoints) use `{"success":true}` or `{"enablePlayButton":true,"progressLabel":"..."}` shapes without a common envelope.
