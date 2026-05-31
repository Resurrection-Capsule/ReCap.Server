# Redirector Component — 0x05

Handles the initial TLS handshake redirect: client connects to the redirector endpoint (port 42127/TLS),
sends `getServerInstance`, receives the lobby server address (port 42125) in a ServerInstanceInfo
struct, then disconnects and reconnects to the lobby. The single command on the critical path that
establishes the base Blaze server address.

Audit pass 2026-05-29: verified against C++ `Blaze/Component/RedirectorComponent.cpp`
(header `.h` and `.cpp`), `Blaze/Functions.cpp` (TDF writers), the C# port
`Adapters/Blaze/Component/RedirectorComponent.cs`, and `Adapters/Blaze/BlazeServer.cs`
(component wiring). Tags: `[V]` = verified in cited source, `[?]` = unverified / needs Ghidra.
Ghidra was **not reachable** this pass (instance present but project unknown; debugger-proxy only).

---

## Request/response command

| Command | Cmd ID | C++ handler | C# handler | Status |
|---|---|---|---|---|
| getServerInstance | 0x01 | `RedirectorComponent.cpp:194` | `RedirectorComponent.cs:30` | ✅ |

C++ dispatch: `ParsePacket` switch on cmd 0x01 (line 124-127 `.cpp:194`). C# dispatch:
`HandlePacket` switch on cmd 1 (`.cs:19-22`). Both call handler methods immediately and return true.
`[V]`

---

## What the handler does (internals)

### getServerInstance (0x01) `[V]`
- **C++ `GetServerInstance` (line 194–201):** reads `request` (no fields extracted);
  `blazeServer = GetApp().get_blaze_server()`; calls `WriteServerInstanceInfo(packet, blazeServer->get_address().to_string(), blazeServer->get_port())`; replies with built packet.
  `RedirectorComponent.cpp:194-201`
- **C# `HandleGetServerInstance` (line 30–62):** reads `ServerInstanceRequest` (no significant fields used);
  validates HostName and Ip (error 0x10005 if both empty); builds `ServerInstanceInfo` with
  `Address.ActiveMember = ServerAddressMember.IpAddress` and `IpAddress.Hostname`, `IpAddress.Ip`,
  `IpAddress.Port`, `Secure` fields; replies via `client.RespondTo(packet, serverInfo)`.
  `RedirectorComponent.cs:30-62`
- **Request fields `[V]`:** `ServerInstanceRequest` (lines 151–194) includes BSDK, BTIM, CLNT, CLTP,
  CPLT, CSKU, CVER, DSDK, ENV, FPID, LOC, NAME, PLAT, PROF — all read/deserialized by C#
  TDF layer but **not consumed by handler logic**. C++ does not extract any field.

---

## TDF field tables — getServerInstance reply (ServerInstanceInfo)

### C++ response structure `WriteServerInstanceInfo` `[V]` `RedirectorComponent.cpp:136-177`
| Tag | Type | C++ value |
|---|---|---|
| `ADDR` | union, member 1 `XboxClientAddress` | (see nested table below) |
| `ADDR.VALU` | struct (`RedirectorIpAddress`) | (nested) |
| `ADDR.VALU.HOST` | string | `host` parameter = `blazeServer->get_address().to_string()` |
| `ADDR.VALU.IP` | int (u32) | **`0`** (hardcoded, line 142) |
| `ADDR.VALU.PORT` | int (u16) | `port` parameter = `blazeServer->get_port()` |
| `SECU` | int | **`1`** (hardcoded, line 175 — TLS required) |
| `XDNS` | int | **`0`** (hardcoded, line 176) |

Commented-out alt format (lines 148–174): AMAP/NMAP/MSGS lists. **Not used.** `[V]`

### C# response structure `ServerInstanceInfo` `[V]` `RedirectorComponent.cs:202-224`
| Field | C# TDF tag | C# value | vs C++ |
|---|---|---|---|
| `Address` | `ADDR` | union, member 0 `IpAddress` | ⚠️ UNION MEMBER TAG DIFFERS (see divergence #1) |
| `Address.IpAddress` | (nested) | `RedirectorIpAddress` struct | ✅ same struct type |
| `Address.IpAddress.Hostname` | `HOST` | set from `HostName` property | ⚠️ C++ sends actual host string; C# sends empty string (see divergence #2) |
| `Address.IpAddress.Ip` | `IP` | set from `Ip` property | ⚠️ C++ hardcodes 0; C# receives from config (see divergence #2) |
| `Address.IpAddress.Port` | `PORT` | set from `Port` property (value 42125) | ✅ C++ uses dynamic port, C# is hardcoded 42125 |
| `Secure` | `SECU` | `IsSecure` property (value false) | ⚠️ C++ hardcodes 1; C# sends false (see divergence #3) |
| `DefaultDNSAddress` | `XDNS` | **0** (default) | ✅ |

### Component wiring — C# initialization `[V]` `BlazeServer.cs:49-54`
```
if (isSecure) {
    AttachComponent(new RedirectorComponent{
        HostName = HostName,        // <-- passes HostName parameter
        Ip = 0,                      // <-- always 0; config.GetServerIPNumber() NOT used here
        Port = 42125                 // <-- hardcoded; NOT from BlazeServer.Port parameter
    });
}
```
`HostName` param comes from BlazeServer ctor (line 33), default from config in Program.cs.
`Ip` forced to 0 (not `ServerConfig.GetServerIPNumber()`). `Port` hardcoded 42125.
`IsSecure` inherited from BlazeServer.IsSecure (line 51 implicit, set via property during object init).
Actually `IsSecure` (C# `[TdfField("SECU", false)]` line 219 default **false**, not true).

---

## Notifications

None. Redirector fires no notifications. `[V]`

---

## Divergences (C++ vs C#)

1. **Union active member tag.** C++ uses `NetworkAddressMember::XboxClientAddress` (enum value 1).
   C# uses `ServerAddressMember.IpAddress` (enum value 0). The union payload is identical
   (`RedirectorIpAddress`/VALU struct), but member ID differs. **Impact depends on client:**
   if client checks member tag, may reject C#. `[V]` `RedirectorComponent.cpp:137` vs
   `RedirectorComponent.cs:54`

2. **Address field split.** C++ sends `HOST=<real-host-string>`, `IP=0`. C# sends `HOST=""` (empty),
   `IP=<config-number>`. Both strategies point to same lobby IP, but via different TDF fields.
   **C# HostName always empty** (line 51: `new RedirectorComponent{ HostName = HostName,` where
   `HostName` param is never used elsewhere; it is set but C# handler fills the ServerInstanceInfo
   from component property `HostName` line 55). Actually, the component is attached with HostName,
   but the handler reads `this.HostName` (which is empty after init on line 10). Tracing:
   BlazeServer ctor HostName param → passed in line 51 → sets component.HostName → handler reads
   component.HostName at line 55 → writes to serverInfo.Address.IpAddress.Hostname. So C# **should**
   write the HostName passed to BlazeServer. But actual runtime shows empty string in replies
   (previous doc). **Further testing needed.** For now code shows correct flow. `[V]` `BlazeServer.cs:51,33`

3. **SECU (TLS flag).** C++ hardcodes 1 (TLS required). C# property defaults to false (line 219
   `[TdfField("SECU", false)]`), and component is never assigned IsSecure explicitly, so it
   remains false. **C# sends SECU=0.** Client either ignores SECU (respects EAWebKit patch only),
   or accepts non-TLS. Since login succeeds in C# deployment, SECU=0 is accepted by patched
   client. `[V]` `RedirectorComponent.cpp:175` vs `RedirectorComponent.cs:219` + `BlazeServer.cs:49-54`

4. **Port sourcing.** C++ calls `blazeServer->get_port()` (dynamic). C# hardcodes 42125 in component
   init (line 53). If C++ redirector points to a non-standard port, C# will still say 42125. `[V]`
   `RedirectorComponent.cpp:198` vs `BlazeServer.cs:53`

---

## Open questions / Ghidra TODO

- `[?]` Does the client validate the union member tag (0 vs 1) and reject if mismatched? Or does it
  read VALU regardless of tag?
- `[?]` What does the client prefer when `HostName` and `Ip` both present, or both absent?
  Darkspore.exe client-side union decoder in Ghidra (nSporeNet handler for Redirector packets).
- `[?]` Does SECU=0 on redirector affect the subsequent lobby connection TLS state, or is TLS
  negotiated independently by the client? (EAWebKit patch + modded SSL verify → non-TLS; SECU may
  be advisory only.)
- `[?]` Verify C# HostName assignment: does BlazeServer.HostName (from config) actually reach the
  reply, or is it always empty due to initialization order?
