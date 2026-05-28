# Static Files, PNG Endpoints, QoS, and Misc Routes

## Overview

These routes handle file serving, creature/template image delivery, EA's Quality-of-Service NAT negotiation protocol, the in-game browser pages (`web/sporelabs`), and telemetry reception.

None of these routes use the `method=` dispatch pattern. They are path-matched directly.

C++ implementations: `Game/API.cpp:514-739`.  
C# equivalent: `Api.cs:108-116` — `GetBytesByFilePath()` via `StaticStorageAdapter`, which covers some cases. **QoS, web/sporelabs, and telemetry are entirely absent from C#.**

## Endpoint Table

| Route | C++ handler | C# handler | Status | Notes |
|---|---|---|---|---|
| GET/POST `/template_png/{filename}` | `API.cpp:514` | Static fallback | ⚠️ | C++ logs request; serves from `CONFIG_WWW_STATIC_PATH`. C# serves via `StaticStorageAdapter` if file exists on disk |
| GET/POST `/creature_png/{filename}` | `API.cpp:520` | Static fallback | ⚠️ | C++ serves from `{storage_path}/creature_png/`; C# stores PNG as base64 in DB, retrieves via `/recap/api?method=api.game.getCreatureLargePng` |
| GET/POST `/game/service/png` | `API.cpp:541` | **Missing** | ❌ | Serves template or account PNG; uses `account_id` or `template_id`+`size` params; falls back to `default.png`. Not implemented in C# |
| GET/POST `/bootstrap/launcher/` | `API.cpp:374` | Static fallback | ⚠️ | C++ serves skip-launcher HTML or templated `wrapper.html`; C# falls through to static |
| GET/POST `/bootstrap/launcher/{path}` | `API.cpp:393` | Static fallback | ⚠️ | C++ serves from `CONFIG_WWW_STATIC_PATH`; C# same via static |
| GET/POST `/assets/{path}` | `API.cpp:718` | Static fallback | ⚠️ | C++ serves from `CONFIG_WWW_STATIC_PATH`; C# same |
| GET/POST `/web/sporelabsgame/{path}` | `API.cpp:722` | **Missing** | ❌ | C++ serves from `CONFIG_WWW_STATIC_PATH`; C# has no handler, falls through to static (may work if file exists) |
| GET/POST `/web/sporelabs/alerts` | `API.cpp:726` | **Missing** | ❌ | C++ returns 404 (handler commented out). C# has no handler |
| GET/POST `/web/sporelabs/resetpassword` | `API.cpp:731` | **Missing** | ❌ | C++ returns 404. C# has no handler |
| GET/POST `/web/sporelabs/{path}` | `API.cpp:736` | **Missing** | ❌ | C++ returns 404 (catch-all for any remaining sporelabs path). C# has no handler |
| GET/POST `/qos/qos` | `API.cpp:591` | **Missing** | ❌ | See QoS section below |
| GET/POST `/qos/firewall` | `API.cpp:633` | **Missing** | ❌ | See QoS section below |
| GET/POST `/qos/firetype` | `API.cpp:681` | **Missing** | ❌ | See QoS section below |
| GET/POST `/telemetryevent` | `API.cpp:325` | **Missing** | ❌ | C++ logs receipt and returns 200; C# has no handler |
| GET/POST `/api` | `API.cpp:319` | **Missing** | ❌ | C++ returns 200 and logs; diagnostic catch-all |
| GET/POST `/favicon.ico` | `API.cpp:709` | Static fallback | ⚠️ | C++ serves from `{storage_path}/www/favicon.ico` |

---

## QoS Protocol Detail

The QoS endpoints implement EA's NAT-negotiation protocol. They are used by the client to measure network quality and determine firewall/NAT type before matchmaking. All three are fully implemented in C++ and entirely absent from C#.

### /qos/qos

Query params: `vers` (int32), `qtyp` (int32, 1 or 2), `prpt` (uint16 — client UDP port).

For `qtyp=1` or `qtyp=2`, returns XML. For any other type, returns an empty plain-text body.

```xml
<?xml version="1.0" encoding="UTF-8"?>
<qos>
  <numprobes>2</numprobes>
  <probesize>8</probesize>
  <qosport>{prpt}</qosport>    <!-- echoes client's port -->
  <requestid>{qtyp}</requestid>
  <reqsecret>4919</reqsecret>  <!-- 0x1337 -->
</qos>
```

C++ comment notes a TODO to send UDP probes back to the client port (`prpt`). This is not implemented.

### /qos/firewall

Query params: `nint` (uint32 — number of network interfaces).

```xml
<?xml version="1.0" encoding="UTF-8"?>
<firewall>
  <numinterfaces>{nint}</numinterfaces>
  <ips>
    <!-- repeated nint times -->
    <ips>127.0.0.1</ips>
  </ips>
  <ports>
    <!-- repeated nint times -->
    <ports>{qos_server_port}</ports>
  </ports>
  <requestid>1</requestid>
  <reqsecret>4919</reqsecret>  <!-- 0x1337 -->
</firewall>
```

Uses `GetApp().get_qos_server()->get_address()` and `->get_port()` for the server-side address/port.

### /qos/firetype

Query params: `rqid` (uint32), `rqsc` (uint32), `inip` (uint32 — IPv4 as integer), `inpt` (uint16).

```xml
<?xml version="1.0" encoding="UTF-8"?>
<firetype>
  <firetype>1</firetype>   <!-- Blaze::NatType::Open + 1 = 1 -->
</firetype>
```

Always returns `NatType::Open` (best-case scenario), which means the client concludes it has an open NAT and proceeds with matchmaking.

---

## /game/service/png Detail

Query params: `account_id` (int64) or `template_id` (uint32) + `size` (string).

- If `account_id != 0`: intended to serve a per-account image (body of that branch is commented out in C++).
- If `template_id` is provided: constructs path `{storage}/template_png/{size}/0x{template_id:08x}.png` and serves it. Falls back to `default.png` if missing.

This endpoint is the mechanism the game UI uses to display creature thumbnails in menus. Its absence in C# means the game UI cannot display template images via this path.

---

## Static File Serving Behavior

**C++ (`responseWithFileInStorage`):**
- Appends the URI resource to a base path.
- Handles index.html fallback for directories.
- For `.js` and `.html` files, performs template substitution: `{{isDev}}`, `{{recap-version}}`, `{{host}}`, `{{game-mode}}`, `{{version-lock}}`, etc.
- Uses a special `response.version() |= 0x1000'0000` flag to signal "serve file from disk path" (not from body string).

**C# (`StaticStorageAdapter.GetFile`):**
- Simple file read from a static root directory.
- No template variable substitution.
- Falls back to `{uri}/index.html` if direct path is not found.

The missing template substitution in C# means launcher HTML pages that use `{{host}}` or `{{isDev}}` will receive unexpanded placeholder text if served by C#.

---

## Web/Sporelabs Note

`/web/sporelabs/*` endpoints were the in-game browser overlay pages (alerts, password reset, account management). In C++, all are currently returning 404 with the handler bodies commented out. In C#, these paths are not registered at all and fall through to `StaticStorageAdapter`, which will also 404 unless matching files exist on disk. These pages are not needed for core gameplay.
