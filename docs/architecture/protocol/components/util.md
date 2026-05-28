# Util Component — 0x09

Handles pre-authentication capability negotiation, keep-alive pings, post-authentication server config delivery, and user settings persistence, on the Blaze lobby connection (port 42125).

---

## Request/response commands

| Command name | Cmd ID | Direction | C++ handler | C# handler | Status |
|---|---|---|---|---|---|
| fetchClientConfig | 0x01 | C→S | `Blaze/Component/UtilComponent.cpp:149` (stub) | — | ❌ |
| ping | 0x02 | C→S | `Blaze/Component/UtilComponent.cpp:153` | `Adapters/Blaze/Component/UtilComponent.cs:24` | ✅ |
| setClientData | 0x03 | C→S | — (enum only) | — | ❌ |
| localizeStrings | 0x04 | C→S | — (enum only) | — | ❌ |
| getTelemetryServer | 0x05 | C→S | `Blaze/Component/UtilComponent.cpp:160` | — | ❌ |
| getTickerServer | 0x06 | C→S | — (enum only) | — | ❌ |
| preAuth | 0x07 | C→S | `Blaze/Component/UtilComponent.cpp:184` | `Adapters/Blaze/Component/UtilComponent.cs:34` | ✅ |
| postAuth | 0x08 | C→S | `Blaze/Component/UtilComponent.cpp:250` | `Adapters/Blaze/Component/UtilComponent.cs:92` | ✅ |
| userSettingsLoad | 0x0A | C→S | — (enum only) | — | ❌ |
| userSettingsSave | 0x0B | C→S | `Blaze/Component/UtilComponent.cpp:291` (commented-out) | — | ❌ |
| userSettingsLoadAll | 0x0C | C→S | `Blaze/Component/UtilComponent.cpp:310` (commented-out) | — | ❌ |
| filterForProfanity | 0x14 | C→S | — (enum only) | — | ❌ |
| fetchQosConfig | 0x15 | C→S | — (enum only) | — | ❌ |
| setClientMetrics | 0x16 | C→S | `Blaze/Component/UtilComponent.cpp:335` | — | ❌ |
| setConnectionState | 0x17 | C→S | — (enum only) | — | ❌ |
| getPssConfig | 0x18 | C→S | — (enum only) | — | ❌ |
| getUserOptions | 0x19 | C→S | — (enum only) | — | ❌ |
| setUserOptions | 0x1A | C→S | — (enum only) | — | ❌ |

## Notifications sent

None defined for this component.

---

## Key TDF fields — preAuth (0x07)

First Blaze command the client sends. Response tells the client which component IDs are active and supplies QoS configuration.

**Response:**

| Tag | Type | Description |
|---|---|---|
| `ASRC` | string | Auth source (`"321915"`) |
| `CIDS` | list&lt;u16&gt; | Active component IDs (Auth, GameManager, Messaging, Playgroups, Redirector, Rooms, UserSessions, Util, …) |
| `CONF` | struct → map | Server config strings (`pingPeriod`, `connIdleTimeout`, etc.) |
| `INST` | string | Service instance name (from client's CDAT.SVCN) |
| `NASP` | string | Namespace (`"cem_ea_id"`) |
| `PLAT` | string | Platform string (mirrored from client CINF.PLAT) |
| `QOSS` | struct | QoS config — contains `BWPS` (bandwidth ping site), `AQPJ` (alias→QosPingSiteInfo map) |
| `RSRC` | string | Registration source (`"321915"`) |
| `SVER` | string | Blaze version string (`"Blaze 3.9.3.1"`) |

## Key TDF fields — postAuth (0x08)

Sent immediately after `preAuth`. Delivers telemetry/ticker/PSS server addresses used by the client.

**Response:**

| Tag | Type | Description |
|---|---|---|
| `PSS` | struct | PSS config (ADDR, PJID, PORT, RPRT, TIID) |
| `TELE` | struct | Telemetry server (ADRS, PORT, ANON, DISA, FILT, NALE, PILD, SDLY, SESS, SKEY, SPCT) |
| `TICK` | struct | Ticker server (ADRS, PORT, SKEY) |
| `UROP` | struct | User options (ULVL = TelemetryOpt enum) |

---

## Porting gaps

- `getTelemetryServer` (0x05) is fully implemented in C++ but absent in C#; client may call this independently of `postAuth`.
- `setClientMetrics` (0x16) in C++ is an ack stub; absent in C# (silently falls to default/false path).
- `userSettingsSave` (0x0B) and `userSettingsLoadAll` (0x0C) have commented-out file-I/O code in C++ and are absent in C#; no user setting persistence in either implementation.
- C++ `preAuth` reads `CDAT` (client data including service name and type) and stores it on the client object; C# reads `ServiceName` from `ClientData` in the `PreAuthRequest` TDF but does not persist `ClientType`.
