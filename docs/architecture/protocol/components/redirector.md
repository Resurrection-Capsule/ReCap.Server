# Redirector Component — 0x05

Handles the initial TLS redirect that points the client from the redirector endpoint (port 42127) to the actual Blaze lobby server (port 42125).

---

## Request/response commands

| Command name | Cmd ID | Direction | C++ handler | C# handler | Status |
|---|---|---|---|---|---|
| getServerInstance | 0x01 | C→S | `Blaze/Component/RedirectorComponent.cpp:194` | `Adapters/Blaze/Component/RedirectorComponent.cs` | ✅ |

## Notifications sent

None defined for this component.

---

## Key TDF fields — getServerInstance (0x01)

The client connects to the TLS redirector first and sends this as the very first Blaze request. The response tells the client the address of the lobby server to connect next.

**Request:** no significant fields consumed by the C++ handler (reads implicitly via the request object).

**Response (ServerInstanceInfo):**

| Tag | Type | Description |
|---|---|---|
| `ADDR` | union (XboxClientAddress active) | Lobby server address |
| `ADDR.VALU.HOST` | string | Hostname or IP of the lobby server |
| `ADDR.VALU.IP` | u32 | IP (set to 0 when HOST is used) |
| `ADDR.VALU.PORT` | u16 | Lobby server port (42125) |
| `SECU` | u32 | Security flag (1 = TLS required on lobby connection) |
| `XDNS` | u32 | Use DNS resolution flag (0 = use IP directly) |

Note: the union's active-member tag is `NetworkAddressMember::XboxClientAddress` (value 0) even though the struct inside is a redirector-specific `IpAddress` (HOST + IP + PORT) — not the game's standard `IpPairAddress`. This is a quirk of the Blaze wire format for the redirector.

---

## Porting gaps

- Only one command exists; both implementations handle it. No porting gaps for command coverage.
- C++ reads the lobby address dynamically from `GetApp().get_blaze_server()`; C# should do the same (confirm that `RedirectorComponent.cs` reads from `ServerConfig` rather than hardcoding).
- The C++ comment block documents an alternative `AMAP`/`NMAP`/`MSGS` response format that is commented out — the active single-field `ADDR` format is authoritative.
