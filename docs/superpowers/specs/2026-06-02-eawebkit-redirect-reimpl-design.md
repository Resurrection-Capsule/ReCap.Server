# EAWebKit redirect reimplementation — design

> Status: design, 2026-06-02. Reimplements Xackery's client-redirect mods in the
> EAWebKit source we now build ourselves. Xackery never released his modded DLL's
> source; we rebuild the redirect from the stock tree we compiled (VS2008, 0 errors,
> `Distribution/pc/9.0.21022/dev-opt/bin/EAWebkit.dll`).

## Problem

Darkspore's web UI engine is `EAWebKit.dll`. The retail client dials EA's live
hosts (`config.darkspore.com`, `api.darkspore.com`, …). To run against ReCap, the
web layer must hit `127.0.0.1` (or a remote ReCap), and the engine's DirtySDK
ProtoSSL must stop rejecting ReCap's cert.

Xackery solved this with a recompiled `EAWebKit.dll` (stock 1.21 + Microsoft Detours
hooks on `gethostbyname`/`connect`/SSL-verify). The Detours approach is fragile:
byte-signatures are version-specific (his were built for demo 5.3.0.103, broke on
retail 127 and on Wine, needing fixed-address fallbacks). His source was never shared.

We now build the EAWebKit tree from source, so we can reimplement the redirect
**directly in the source** at the DirtySDK seams — no Detours, no byte-signatures.

## Scope

In scope — **only the EAWebKit web/HTTP layer**:
- DNS redirect (where the engine's HTTP/HTTPS goes).
- HTTP port remap (80 → ReCap REST port).
- SSL cert/host verification bypass (accept ReCap's self-signed cert).

Explicitly OUT of scope:
- Blaze (42127 redirector / 42125 lobby) and RakNet (42000) — those are the
  `Darkspore.exe`'s own sockets (`nSporeNet`), NOT EAWebKit's DirtySDK. They are
  redirected by other means (hosts file / exe hostname patch) and untouched here.
- A clean CA-cert path (`ProtoSSLSetCACert`) — deferred; bypass now (team decision).
- The modern-engine shim (`ReCap.WebShim`) — separate side-quest.

## Key requirement (team debate, 2026-04-02)

VitorMM: the EAWebKit solution "should receive parameters, since we need to be able
to connect to both local and remote servers… the Hub should be able to launch
Darkspore both pointing at local or remote, with the same installation." So the
redirect target **must be configurable**, not hardcoded.

## Decisions

| Topic | Decision |
|---|---|
| Mechanism | Source-level edits in the DirtySDK package — no Detours. |
| SSL | Bypass verify (replicate Xackery), gated by a config flag. |
| Target config | External config file next to the DLL/exe. |
| DNS scope | Force ALL `gethostbyname` → `config.host` (replicate Xackery; the engine only dials the game backend anyway). |
| Port remap | Dest port 80 → `config.http_port`. |
| Config location | Folder of `EAWebKit.dll` (= `Darkspore.exe` folder), resolved via `GetModuleFileName` — not CWD. |
| ReCap REST | HTTP only, `http://*:8033/` (`Api.cs:41`, `DEFAULT_PORT=8033`). No TLS server side → web traffic is HTTP; SSL bypass only covers residual HTTPS attempts. |

## Architecture

Four pieces, all compiled into `EAWebkit.dll` via the existing DirtySDK project:

### 1. Config loader — new `recapredirect.{c,h}`
Added to the DirtySDK package source/include (compiled into the DLL). Public C API:

```c
typedef struct RecapRedirectConfig {
    unsigned int uHostAddr;   /* config.host assembled like SocketLookupThread: (a<<24)|(b<<16)|(c<<8)|d, e.g. 0x7F000001 for 127.0.0.1 */
    unsigned int uHttpPort;   /* config.http_port, default 8033 */
    int          bSslBypass;  /* config.ssl_bypass, default 1 */
} RecapRedirectConfig;

const RecapRedirectConfig* RecapRedirectGet(void);  /* lazy-loads on first call, thread-safe-once */
```

- Resolves its own module path via `GetModuleHandleEx(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS, &RecapRedirectGet, …)` + `GetModuleFileNameA`, strips the filename, appends `recap.cfg`.
- Parses a minimal INI: lines `key=value`, `#` comments, trims whitespace. Keys:
  `host` (dotted IPv4 or hostname → resolved once via real `gethostbyname`), `http_port`, `ssl_bypass`.
- Missing file / missing keys → defaults `127.0.0.1` / `8033` / `1`.
- Loads exactly once (guard flag); never throws; on any parse error falls back to defaults and continues.

`recap.cfg` shipped next to the DLL:
```ini
# ReCap EAWebKit redirect
host=127.0.0.1
http_port=8033
ssl_bypass=1
```

### 2. DNS redirect — `dirtynetwin.c::SocketLookupThread` (~line 804)
Replace `winhost = gethostbyname(host->name);` resolution result with
`host->addr = RecapRedirectGet()->uHostAddr;` (host byte order as the surrounding code
expects — it builds `host->addr` from the 4 octets; we set it directly), `host->done = 1`.
No real DNS call. Comment cites this design doc.

### 3. Port remap — `dirtynetwin.c::_MapAddress` (~line 219)
After the existing name/addr map loop, before returning: if
`SockaddrInGetPort(pAddr) == 80`, write `SockaddrInSetPort(pTemp, RecapRedirectGet()->uHttpPort)`
on the returned temp sockaddr (and ensure the addr is the config host). Leave all
other ports untouched (Blaze/RakNet never reach here, but harmless).

### 4. SSL bypass — `protossl.c`
- `_VerifyCertificate` (~1334): if `RecapRedirectGet()->bSslBypass`, return success (0)
  before the modulus/signature checks.
- Host wildcard check (~1715, `_WildcardMatchNoCase(pState->strHost, …)`): if bypass,
  treat as match (skip the host-mismatch failure path).
Both gated so `ssl_bypass=0` restores stock verification.

## Data flow

```
Darkspore.exe (EAWebKit web engine)
  → resolve "config.darkspore.com"  → SocketLookupThread → config.host (127.0.0.1)
  → connect(host:80)                → _MapAddress        → config.host:8033
  → TLS handshake (if any)          → _VerifyCertificate → accepted (bypass)
  → HTTP GET /bootstrap/api…        → ReCap REST :8033 (BootstrapRestController)
```

## Error handling

- Config loader never fails hard: file/parse errors → defaults, DLL keeps working.
- If `recap.cfg` absent, behavior == hardcoded `127.0.0.1:8033` + bypass (Xackery parity).
- Edits are guarded by the config flag, so a stock build is recoverable by config alone
  (except DNS force-all, which is unconditional — acceptable: the engine only dials the
  game backend).

## Testing / gate

1. Rebuild the EAWebKit VS2008 solution → expect 0 errors (baseline already green).
2. Back up the working `EAWebKit.dll` next to `Darkspore.exe`.
3. Drop the rebuilt `EAWebkit.dll` + a `recap.cfg` (host=127.0.0.1).
4. Start ReCap (`dotnet run`, REST on 8033).
5. Launch Darkspore.
6. **PASS:** the login web UI loads (served from ReCap 8033), no cert rejection,
   client reaches the login flow as it does with Xackery's DLL today.
7. **Remote check:** set `host=<remote ReCap IP>` in `recap.cfg`, relaunch → connects
   to the remote server, same install (validates VitorMM's requirement).

## Files touched

- NEW `EAWebKitSupportPackages/DirtySDKEAWebKit/local/core/{source/dirtysock,include}/recapredirect.{c,h}`
- EDIT `.../source/dirtysock/pc/dirtynetwin.c` (SocketLookupThread, _MapAddress)
- EDIT `.../source/proto/protossl.c` (_VerifyCertificate, host-match)
- EDIT the DirtySDK `.vcproj` to compile `recapredirect.c`
- NEW `recap.cfg` template (ship next to the DLL)

## Open items

- Confirm `host` config accepting a hostname (not just dotted IP) is worth the extra
  resolve-once code, or restrict to dotted IPv4 for v1. (Lean: dotted IPv4 only for v1.)
- Verify `SockaddrInSetPort`/`SockaddrInGetPort` helper names exist in this DirtySDK
  vintage (grep before editing).
