# Util Component — 0x09

Handles pre-authentication capability negotiation (`preAuth`), keep-alive pings (`ping`),
post-authentication server-config delivery (`postAuth`), telemetry/ticker/PSS config, and
user-settings persistence on the Blaze lobby connection (port 42125). The first real Blaze
command after the Redirector redirect is `preAuth`; the component delivers the server's
capability blob (component IDs, QoS config, ping period) that the client uses for the rest of
the session. `postAuth` delivers telemetry / PSS / ticker addresses and user telemetry opt
preference. Neither command fires notifications. On the solo login path only `preAuth` → `ping`
→ `postAuth` are exercised; the remaining commands are non-blocking gaps.

Audit pass 2026-05-29: verified against C++ `Blaze/Component/UtilComponent.cpp`,
`Blaze/Component/UtilComponent.h`, `Blaze/Functions.cpp` (TDF struct writers),
`Blaze/Component.cpp` (component dispatch), and the C# port
`Adapters/Blaze/Component/UtilComponent.cs`. Tags: `[V]` = verified in cited source,
`[?]` = unverified / needs Ghidra. Ghidra was **not reachable** this pass (no live instance).

---

## Verified login-flow position (WORKING log) `[V]`

`Redirector::getServerInstance` → **`Util::preAuth`** → `Auth::getLegalDocsInfo` →
`Auth::login` → `Auth::loginPersona` → **`Util::postAuth`** → `UserSessions::updateNetworkInfo`
→ … → **`Util::ping`** (recurring, every `pingPeriod` ms).

---

## Request/response commands

| Command | Cmd ID | C++ handler (file:line) | C# handler (file:line) | Status |
|---|---|---|---|---|
| fetchClientConfig | 0x01 | `UtilComponent.cpp:84,149` (log-only, no reply) | — | ❌ |
| ping | 0x02 | `UtilComponent.cpp:88,153` | `UtilComponent.cs:17,24` | ✅ |
| setClientData | 0x03 | — (enum only, not dispatched) | — | ❌ |
| localizeStrings | 0x04 | — (enum only, not dispatched) | — | ❌ |
| getTelemetryServer | 0x05 | `UtilComponent.cpp:92,160` | — | ❌ C++ only |
| getTickerServer | 0x06 | — (enum only, not dispatched) | — | ❌ |
| preAuth | 0x07 | `UtilComponent.cpp:96,184` | `UtilComponent.cs:18,33` | ✅ |
| postAuth | 0x08 | `UtilComponent.cpp:100,250` | `UtilComponent.cs:19,92` | ✅ |
| userSettingsLoad | 0x0A | — (enum only, not dispatched) | — | ❌ |
| userSettingsSave | 0x0B | `UtilComponent.cpp:104,291` (body commented out) | — | ❌ |
| userSettingsLoadAll | 0x0C | `UtilComponent.cpp:108,310` (body commented out) | — | ❌ |
| filterForProfanity | 0x14 | — (enum only, not dispatched) | — | ❌ |
| fetchQosConfig | 0x15 | — (enum only, not dispatched) | — | ❌ |
| setClientMetrics | 0x16 | `UtilComponent.cpp:112,335` (empty ack) | — | ❌ C++ only |
| setConnectionState | 0x17 | — (enum only, not dispatched) | — | ❌ |
| getPssConfig | 0x18 | — (enum only, not dispatched) | — | ❌ |
| getUserOptions | 0x19 | — (enum only, not dispatched) | — | ❌ |
| setUserOptions | 0x1A | — (enum only, not dispatched) | — | ❌ |
| deleteUserSettings | 0x0E | — | — (name only, `cs:139`) | ❌ |
| suspendUserPing | 0x1B | — | — (name only, `cs:148`) | ❌ |

C++ `ParsePacket` dispatches: `fetchClientConfig`(0x01), `ping`(0x02), `getTelemetryServer`(0x05),
`preAuth`(0x07), `postAuth`(0x08), `userSettingsSave`(0x0B, empty body), `userSettingsLoadAll`(0x0C,
empty body), `setClientMetrics`(0x16). All others return `false`. `[V]` `UtilComponent.cpp:82-121`

C# `HandlePacket` dispatches: `ping`(2), `preAuth`(7), `postAuth`(8) only; everything else returns
`false`. `[V]` `UtilComponent.cs:15-21`

---

## Notifications fired

None defined for this component. `[V]` `UtilComponent.cs:153-158` / no notify calls in
`UtilComponent.cpp`.

---

## What each handler does (internals)

### ping (0x02) `[V]`
- **C++**: writes `STIM` = `utils::get_unix_time()` (unix seconds); `request.reply(packet)`.
  `UtilComponent.cpp:153-158`
- **C#**: replies `PingResponse { ServerTime = CurrentUnixTime }` where `CurrentUnixTime` is
  `(uint)DateTime.UtcNow.Subtract(epoch).TotalSeconds`. `UtilComponent.cs:24-30`
- **Match:** identical semantics. `[V]`

### fetchClientConfig (0x01) `[V]`
- **C++**: `std::cout` log only; **sends no reply**. `UtilComponent.cpp:149-151`
- **C#**: absent — falls to `_ => false`. For an unanswered request the client may wait or timeout.
- **Divergence:** C++ at least handles the message (no reply = OK for this command per C++ intent);
  C# drops it entirely (no dispatch entry). Non-blocking on solo path. `[V]`

### getTelemetryServer (0x05) `[V]`
- **C++**: builds a `TelemetryServer` with `anonymous=true`, live addr/port from
  `GetApp().get_telemetry_server()`, same DISA/FILT/NOOK/SDLY/SESS/SKEY/SPCT as postAuth but
  with `ANON=1`. Replies `telemetry.Write(packet)` (top-level, no struct wrapper).
  `UtilComponent.cpp:160-182`
- **C#**: absent. `GetTelemetryServerResponse` class exists (`cs:229`) but no dispatch.
- **Divergence:** not on solo path; non-blocking. `[V]`

### preAuth (0x07) `[V]`
- **C++**: reads `CINF` (client info struct, stored in `clientInfo` local) and `CDAT` (client data:
  `SVCN`, `LANG`, `TYPE`, `IITO`) → persists via `data.Read(request["CDAT"])` into
  `client.data()`. Builds reply: `ASRC`, `CIDS` (9 IDs), `CONF` (map 1 key: `pingPeriod=20000`),
  `INST`=`data.serviceName`, `NASP`, `PILD`, `PLAT`=`clientInfo["PLAT"]`, `QOSS`, `RSRC`, `SVER`.
  `UtilComponent.cpp:184-247`
- **C#**: reads `PreAuthRequest` (CINF + CDAT + FCCR). Sends `PreAuthResponse` with `ASRC`,
  `CIDS` (18 IDs), `CONF` (5 keys), `EEFA=true`, `INST`=`request.ClientData.ServiceName`,
  `MINR=false`, `NASP`, `PILD`, `PLAT="pc"` (hardcoded), `QOSS`, `RSRC`, `SVER`. Does **not**
  persist `ClientData` on the client object. `UtilComponent.cs:33-90`

### postAuth (0x08) `[V]`
- **C++**: builds PSS/TELE/TICK/UROP from live server addresses (`GetApp()` accessors). Calls
  `WritePostAuth(packet, pss, telemetry, tick, options)`. `UtilComponent.cpp:250-289`
- **C#**: builds `PostAuthResponse` with hardcoded `127.0.0.1` addresses, populates TELE with
  extra `STIM`/`SVNM` fields absent from C++, sets `UROP.TMOP=OptOut(0)` + `UROP.UID=client.UserId`
  (C++ writes only `TMOP=OptIn(1)`, no UID). `UtilComponent.cs:92-122`

### setClientMetrics (0x16) `[V]`
- **C++**: `request.reply()` — empty ack, no body. Comment notes `UDEV` (router info) and
  `USTA` (UPnP flag) in request. `UtilComponent.cpp:335-340`
- **C#**: absent; falls through to `false`. Likely safe since client does not expect a body.

### userSettingsSave (0x0B) / userSettingsLoadAll (0x0C) `[V]`
- **C++**: bodies are entirely commented out (file-I/O to `./data/<userId>/user_settings`);
  functions are dispatched but do nothing. `UtilComponent.cpp:291-332`
- **C#**: absent.

---

## Key TDF field tables

### ping reply `[V]` `UtilComponent.cpp:153-158` / `UtilComponent.cs:293-297`
| Tag | Type | C++ value | C# value |
|---|---|---|---|
| STIM | u32 | `utils::get_unix_time()` | `CurrentUnixTime` (seconds since epoch) |

### preAuth reply — top-level fields `[V]` `UtilComponent.cpp:205-247` / `UtilComponent.cs:44-90`
| Tag | Type | C++ value | C# value | Match |
|---|---|---|---|---|
| ASRC | string | `"321915"` | `"321915"` | ✅ |
| CIDS | list&lt;int&gt; | 9 IDs: 0x01,0x14,0x04,0x18,0x1C,0x06,0x07,0x7802,0x09 | 18 IDs: 1,25,4,27,28,6,7,9,10,11,30720,30721,30722,30723,20,30725,30726,2000 | ⚠️ divergent set |
| CONF.CONF | map&lt;str,str&gt; | 1 key: `pingPeriod="20000"` | 5 keys: `connIdleTimeout=90s`, `defaultRequestTimeout=80s`, `pingPeriod=20s`, `voipHeadsetUpdateRate=1000`, `xlspConnectionIdleTimeout=300` | ⚠️ divergent (count + value format) |
| EEFA | bool | *(not emitted)* | `true` | C#-only |
| INST | string | `data.serviceName` (echoed from CDAT.SVCN) | `request.ClientData.ServiceName` | ✅ |
| MINR | bool | *(not emitted)* | `false` | C#-only |
| NASP | string | `"cem_ea_id"` | `"cem_ea_id"` | ✅ |
| PILD | string | `""` | `""` | ✅ |
| PLAT | string | `clientInfo["PLAT"].GetString()` (client-mirrored) | `"pc"` (hardcoded) | ⚠️ |
| RSRC | string | `"321915"` | `"321915"` | ✅ |
| SVER | string | `"Blaze 3.9.3.1"` | `"Blaze 3.9.3.1"` | ✅ |

### preAuth — QOSS / QosConfigInfo `[V]` `Functions.cpp:161-179` / `UtilComponent.cs:389-414`
| Tag | Type | C++ value | C# value | Match |
|---|---|---|---|---|
| BWPS | struct (QosPingSiteInfo) | **empty struct** (bandwidthPingSiteInfo is empty, writes no fields) | `{PSA=127.0.0.1, PSP=17502, SNA=ams}` | ⚠️ C# fills it |
| LNP | int | `1` (latencyProbes) | `10` (default `NumLatencyProbes`) | ⚠️ |
| LTPS | map&lt;str,struct&gt; | `{"ams": {PSA=httpQosAddr, PSP=httpQosPort, SNA="ams"}}` | `{"ams": {PSA=127.0.0.1, PSP=17502, SNA=ams}}` | ✅ shape |
| SVID | int | `1161889797` (0x45410805) | `0x45410805u` | ✅ |

QosPingSiteInfo fields (PSA=address string, PSP=port int, SNA=name string).
`[V]` `Functions.cpp:154-158`

### postAuth — PSS (PssConfig) `[V]` `Functions.cpp:130-144` / `UtilComponent.cs:365-387`
| Tag | Type | C++ value | C# value | Match |
|---|---|---|---|---|
| ADRS | string | `pssServer->get_address().to_string()` | `"127.0.0.1"` | ⚠️ hardcoded |
| CSIG | blob | `nullptr,0` (empty) | `TdfBlob` (empty) | ✅ |
| OIDS | list&lt;int&gt; | empty (no oids) | empty | ✅ |
| PJID | string | `"123071"` | `"123071"` | ✅ |
| PORT | int | `pssServer->get_port()` | `42125` | ⚠️ hardcoded |
| RPRT | int | `9` | `9` | ✅ |
| TIID | int | `0` | `0` | ✅ |

### postAuth — TELE (TelemetryServer) `[V]` `Functions.cpp:115-127` / `UtilComponent.cs:101-111`
| Tag | Type | C++ value | C# value | Match |
|---|---|---|---|---|
| ADRS | string | `telemetryServer->get_address().to_string()` | `"127.0.0.1"` | ⚠️ hardcoded |
| ANON | int | `0` (false for postAuth) | *(not emitted — field absent in C# Telemetry class)* | ⚠️ C# drops it |
| DISA | string | long country-code list | same list | ✅ |
| FILT | string | `""` | *(not set, default `""`)* | ✅ |
| LOC | int | `request.get_client().data().lang` (client lang) | `0x656E5553` ("enUS" hardcoded) | ⚠️ |
| NOOK | string | `"US,CA,MX"` | `"US,CA,MX"` | ✅ |
| PORT | int | `telemetryServer->get_port()` | `42125` | ⚠️ hardcoded |
| SDLY | int | `15000` | `15000` | ✅ |
| SESS | string | `"telemetry_session"` | `"telemetry_session"` | ✅ |
| SKEY | string | `"telemetry_key"` | `"telemetry_key"` | ✅ |
| SPCT | int | `75` | `75` | ✅ |
| STIM | string | *(not emitted)* | `CurrentUnixTime.ToString()` | C#-only |
| SVNM | string | *(not emitted)* | `"BGServ"` | C#-only |

### postAuth — TICK (TickerServer) `[V]` `Functions.cpp:108-112` / `UtilComponent.cs:113-115`
| Tag | Type | C++ value | C# value | Match |
|---|---|---|---|---|
| ADRS | string | `tickServer->get_address().to_string()` | `"127.0.0.1"` | ⚠️ hardcoded |
| PORT | int | `tickServer->get_port()` | `42125` | ⚠️ hardcoded |
| SKEY | string | `"0,{addr}:{port},darkspore-pc,10,50,50,50,50,0,0"` (live addr:port) | `"0,127.0.0.1:8999,darkspore-pc,10,50,50,50,50,0,0"` | ⚠️ port mismatch (8999 vs 42125 for PORT field) |

### postAuth — UROP (UserOptions) `[V]` `Functions.cpp:103-105` / `UtilComponent.cs:116-118`
| Tag | Type | C++ value | C# value | Match |
|---|---|---|---|---|
| TMOP | int | `TelemetryOpt::OptIn` = 1 | `TelemetryOpt.OptOut` = 0 | ⚠️ inverted |
| UID | u64 | *(not emitted)* | `client.UserId` | C#-only |

### getTelemetryServer reply (standalone, not postAuth) `[V]` `UtilComponent.cpp:160-182`
Same field set as postAuth TELE but `ANON=1` (anonymous=true); replies top-level (no `TELE`
struct wrapper). No C# handler. `GetTelemetryServerResponse` class at `cs:229` exists but unused.

---

## Notifications fired

None. `[V]` `UtilComponent.cpp` has no `NotifyX` calls; `UtilComponent.cs:153-158` returns
`"<unknown>"` for all notification IDs.

---

## Divergences (C++ vs C#)

1. **CIDS set.** C++ advertises 9 component IDs (Association=0x01, Auth=0x14, GameManager=0x04,
   Messaging=0x18, Playgroups=0x1C, Redirector=0x06, Rooms=0x07, UserSessions=0x7802,
   Util=0x09). C# advertises 18 IDs (adds 30720-30723, 30725-30726 series, 2000, 10, 11, 20,
   25). Extra IDs tolerated by client empirically. `[V]` `UtilComponent.cpp:185-195` / `cs:54-71`
2. **CONF map.** C++ sends 1 key `pingPeriod="20000"` (ms). C# sends 5 keys with
   `pingPeriod="20s"` (different format). Value format difference `[?]` (client parser tolerance
   unknown). `[V]` `UtilComponent.cpp:216-218` / `cs:73-77`
3. **PLAT mirroring.** C++ echoes `clientInfo["PLAT"]` back; C# hardcodes `"pc"`. Functionally
   identical for Windows clients. `[V]` `UtilComponent.cpp:225` / `cs:49`
4. **QOSS.BWPS.** C++ writes an empty struct (bandwidthPingSiteInfo collection is empty →
   no fields). C# populates BWPS with `127.0.0.1:17502`. `[V]` `Functions.cpp:162-165` / `cs:79-81`
5. **QOSS.LNP.** C++ = 1; C# = 10. `[V]` `UtilComponent.cpp:232` / `cs:395`
6. **postAuth TELE.ANON absent in C#.** C++ writes `ANON=0` (anonymous=false) for postAuth.
   C# `GetTelemetryServerResponse` class has no `ANON` field → field missing from wire. `[V]`
   `Functions.cpp:117` / `cs:229-275` (field absent).
7. **postAuth TELE.LOC.** C++ uses `client.data().lang` (client-reported). C# hardcodes
   `0x656E5553` ("enUS"). `[V]` `UtilComponent.cpp:269` / `cs:104`
8. **postAuth TELE extras.** C# emits `STIM` (server time string) and `SVNM="BGServ"` not
   present in C++ `TelemetryServer::Write`. `[V]` `Functions.cpp:115-127` / `cs:110-111`
9. **postAuth UROP.TMOP inverted.** C++ OptIn=1; C# OptOut=0. `[V]` `Functions.cpp:104` / `cs:118`
10. **postAuth UROP.UID.** C++ writes only `TMOP`; C# also writes `UID=client.UserId`.
    `[V]` `Functions.cpp:103-105` / `cs:117`
11. **postAuth TICK.SKEY port.** C++ formats `0,{addr}:{port},...` with live tick port. C#
    hardcodes port `8999` in the key string but `42125` for the `PORT` field. `[V]`
    `UtilComponent.cpp:280` / `cs:115`
12. **ClientData not persisted.** C++ stores `CDAT` on `client.data()` (lang, type, serviceName,
    iito) for later use (e.g. `LOC` in telemetry). C# reads `PreAuthRequest.ClientData` but does
    not persist it on the `Client` object. `[V]` `UtilComponent.cpp:202-203` / `cs:35-36`
13. **preAuth C#-only fields.** `EEFA=true` and `MINR=false` emitted by C# but absent from C++
    reply. `[V]` `UtilComponent.cs:337-344` vs `UtilComponent.cpp:205-247`.

---

## Open questions / Ghidra TODO

- `[?]` Which `CIDS` entries does the client actually require? Does advertising 30720-series or
  missing Association/Rooms change client behavior?
- `[?]` Does the client parse `CONF.pingPeriod` as raw milliseconds (`"20000"`) or as a duration
  string (`"20s"`)? C++ and C# use conflicting formats.
- `[?]` Does the client read `QOSS.BWPS`? C++ leaves it empty; C# fills it. Could cause
  mismatch if the client validates the BWPS/LTPS pair.
- `[?]` Does omitting `TELE.ANON` in the postAuth reply cause client-side telemetry to default
  to anonymous mode?
- `[?]` Does `UROP.TMOP=0` (OptOut, C#) vs `1` (OptIn, C++) affect any gameplay behavior, or
  is it telemetry-only?
- `[?]` Does the client read `TELE.STIM` or `TELE.SVNM` (C#-only fields)?
- `[?]` Does the client read `UROP.UID` (C#-only field)?
- `[?]` Confirm whether `fetchClientConfig` (0x01) must be acked or if no-reply is safe. C++
  explicitly handles it with no reply; client behavior on timeout unknown.
- `[?]` Verify `TICK.SKEY` format requirements (port 8999 vs 42125 inconsistency in C#).
