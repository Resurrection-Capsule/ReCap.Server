# UserSessions Component — 0x7802

Manages per-connection session state, network/geo info, and extended user data
(`UserSessionExtendedData`). Owns the notifications (UserAdded / UserUpdated /
UserSessionExtendedDataUpdate) that other components (Auth, GameManager) fire to publish user
identity and address to the client right after login. On the critical login path.

Audit pass 2026-05-29: verified against C++ `Blaze/Component/UserSessionComponent.cpp`,
`Blaze/Functions.cpp` (TDF writers), `SporeNet/User.h`, C# port
`Adapters/Blaze/Component/UserSessionsComponent.cs`, and a WORKING C++ runtime log. Tags:
`[V]` verified in cited source, `[?]` needs Ghidra. Ghidra not reachable this pass.

---

## Verified flow position (WORKING log) `[V]`
`Login` → (UserAdded, UserUpdated) → `LoginPersona` → `PostAuth` → **`UpdateNetworkInfo`** →
**UserSessionExtendedDataUpdate** → `GetAuthToken` → UserUpdated → … → **`UpdateNetworkInfo`
(2nd)** → UserSessionExtendedDataUpdate. UpdateNetworkInfo is called **twice**. `[V]` `output.txt`

---

## Request/response commands

| Command | Cmd ID | C++ handler | C# handler | Status |
|---|---|---|---|---|
| updateExtendedDataAttribute | 0x05 | — (not dispatched) | `UserSessionsComponent.cs:46` | ⚠️ C# only |
| lookupUser | 0x0C | `UserSessionComponent.cpp:145` | `UserSessionsComponent.cs:117` | ✅ |
| updateNetworkInfo | 0x14 | `UserSessionComponent.cpp:170` | `UserSessionsComponent.cs:65` | ✅ |
| updateUserSessionClientData | 0x19 | `UserSessionComponent.cpp:188` | `UserSessionsComponent.cs:94` | ✅ |
| setUserInfoAttribute | 0x1A | — (not dispatched) | `UserSessionsComponent.cs:102` | ⚠️ C# only |
| fetchExtendedData | 0x03 | — | — | ❌ |
| updateHardwareFlags | 0x08 | — (enum only) | — | ❌ |

C++ `ParsePacket` dispatches only `lookupUser`(0x0C), `updateNetworkInfo`(0x14),
`updateUserSessionClientData`(0x19). `[V]` `UserSessionComponent.cpp:75-94`

---

## Notifications (server → client) — all implemented in C# `[V]`

| Notification | ID | C++ sender | C# sender | Purpose |
|---|---|---|---|---|
| UserSessionExtendedDataUpdate | 0x7802/1 | `NotifyUserSessionExtendedDataUpdate` `cpp:96` | `cs:90` | Push refreshed extended data (DATA + USID) |
| UserAdded | 0x7802/2 | `NotifyUserAdded` `cpp:108` | `AuthenticationComponent.cs:179` | Add user to roster (DATA + USER) |
| UserRemoved | 0x7802/3 | enum only | — | Remove user |
| UserSessionDisconnected | 0x7802/4 | enum only | — | Session dropped |
| UserUpdated | 0x7802/5 | `NotifyUserUpdated` `cpp:133` | `AuthenticationComponent.cs:80`,`183` | Update session state (FLGS + ID) |

> C++ enum also lists notification ids 1-5 (`UserSessionComponent.cpp:26-30`). C# adds id 8
> (UserAuthenticated) / id 9 (UserUnauthenticated/logout) in `GetNotificationName`; only 9 is
> actually emitted (from Auth logout). `[V]`

---

## What each handler does (internals)

### updateNetworkInfo (0x14) — the important one `[V]`
- **C++**: `extendedData.ip.Read(request["ADDR"]["VALU"])` — stores the **client-reported**
  IpPair address into the session's extended data, replies empty, then
  `NotifyUserSessionExtendedDataUpdate(userId, extendedData)` writing the **full** struct.
  `UserSessionComponent.cpp:170-186`
- **C#**: reads `NetworkInfo`, replies empty, then builds a `UserSessionExtendedDataUpdate`
  whose DATA carries **only** ADDR (from the *actual UDP endpoint*, not the reported address) +
  `UATT=0x4000000000000000` ("disable multiple-locations popup"). It **drops** the QoS / latency
  / country / BlazeObjectIdList that login seeded. `UserSessionsComponent.cs:65-92`

### lookupUser (0x0C) `[V]`
- C++: reads request UserData, sets statusFlags|=1 (online); only fills extendedData if the
  looked-up name == "test"; replies UserData (EDAT+FLGS+USER). `cpp:145-168`
- C#: `Server.FindClientByUserId`; on miss replies error 0x5E0001; else builds
  `LookupUserResponse` from the *target's* extended data + UserInfo(AID/BlazeId/NAME).
  `cs:117-162` — more complete than C++.

### updateUserSessionClientData (0x19) `[V]`
- C++: reads `PresenceInfo` from CVAR, replies an integer-list `CVAR=[1]`. `cpp:188-202`
- C#: replies `CVAR=[1]`, ignores request body. `cs:94-100` — matches.

### updateExtendedDataAttribute (0x05) — C#-only `[V]`
- C# reads the attribute request, fires `UserUpdated{FLGS=3}`, replies empty. No C++ handler. `cs:46-63`

### setUserInfoAttribute (0x1A) — C#-only `[V]`
- C# logs ATTV/MASK, replies empty. No C++ handler. `cs:102-115`

---

## Key TDF field tables

### UserSessionExtendedData (DATA) — `UserSessionExtendedData::Write` `[V]` `Functions.cpp:190-257`
| Tag | Type | Meaning | C++ | C# (`UserSessionExtendedData`) |
|---|---|---|---|---|
| ADDR | union | network address (Unset or IpPairAddress→VALU) | yes | yes (`NetworkAddress`) |
| BPS  | string | best ping-site alias (default "gva", index 1) | yes | yes (default "") ⚠️ |
| CMAP | map u32→i32 | client attributes | yes | `ClientAttributes` |
| CTY  | string | country | yes | `Country` |
| CVAR | int list | client vars (empty) | yes | `ClientData` (Tdf?) ⚠️ type differs |
| DMAP | map u32→i64 | data map | yes | `DataMap` |
| HWFG | u32 | hardware flags | yes | `HardwareFlags` |
| PSLM | i32 list | ping-site latency list | yes | `LatencyList` |
| QDAT | struct | NetworkQosData (DBPS/NATT/UBPS) | yes | `QosData` |
| UATT | int | user attributes | yes (u32-ish) | `UserInfoAttribute` (u64) ⚠️ width |
| ULST | objectId list | BlazeObjectId list | yes | `BlazeObjectIdList` |

> Field set matches 1:1. Watch: C# `BPS` default "" vs C++ "gva"; C# `CVAR` is a nested Tdf,
> C++ is an empty integer list; C# `UATT` is u64 vs C++ smaller int. `[V]`

### UserSessionExtendedDataUpdate notification `[V]`
| Tag | Type | C++ (`cpp:96-105`) | C# (`cs:218-228`) |
|---|---|---|---|
| DATA | struct | full extendedData | extendedData (ADDR+UATT only on UpdateNetworkInfo) |
| SUBS | bool | — (absent) | present (false) ⚠️ C#-only |
| USID | u64 | userId | userId |

### UserAdded notification — `NotifyUserAdded` `[V]` `cpp:108-131`
| Tag | Type | Contents |
|---|---|---|
| DATA | struct | UserSessionExtendedData |
| USER | struct | UserIdentification |

### UserIdentification (USER) — `UserIdentification::Write` `[V]` `Functions.cpp:470-477`
| Tag | Type | C++ | C# (`UserIdentification`) |
|---|---|---|---|
| AID  | i64 | id | AccountId |
| ALOC | u32 | localization (client lang) | AccountLocale (C# hardcodes 0x656E5553 "enUS") |
| EXBB | blob | empty | ExternalBlob |
| EXID | u64 | 0 | ExternalId |
| ID   | i64 | id (BlazeId) | BlazeId |
| NAME | string | `user->get_name()` | **`account.Username`** ← roster display name |
| (ORIG) | — | — | C# adds ORIG (OriginId) |

### UserUpdated notification — `NotifyUserUpdated` `[V]` `cpp:133-143`
| Tag | Type | C++ | C# (`UserStatus`) |
|---|---|---|---|
| FLGS | int | SessionState | StatusFlags |
| ID   | i64 | user id | BlazeId |

---

## Divergences (C++ vs C#)

1. **updateNetworkInfo data loss.** C++ pushes the *full* extended data in the
   ExtendedDataUpdate; C# pushes only ADDR + UATT, dropping QoS/latency/country/ULST that login
   seeded. Functionally the client may already hold those from UserAdded, but the update is
   lossy vs C++. `[V]` `cs:80-90` vs `cpp:170-186`
2. **Address source.** C++ stores the client-reported `ADDR.VALU`; C# uses the real UDP
   endpoint (ignores reported address). For NAT/loopback this can disagree. `[V]`
3. **SUBS field** in C# ExtendedDataUpdate not present in C++. `[V]`
4. **Name source.** USER.NAME = `account.Username` (C#) vs `user->get_name()` (C++) — same
   player-name issue as Auth DSNM. `[V]`
5. **ALOC hardcoded** to enUS in C#; C++ uses the client-reported lang. `[V]` `cs:175` / `cpp:117`
6. **C#-only commands** 0x05, 0x1A and notifications 8/9 absent from C++. `[V]`
7. **lookupUser** is richer in C# (resolves live client) vs C++ (echoes request, only fills data
   for name=="test"). `[V]`

---

## Crash / login-stability relevance
- Empty acks for updateNetworkInfo + the UserAdded/UserUpdated/ExtendedDataUpdate notifications
  are what let the client finish login and populate its own session/roster. The C# set matches
  C++ here, which is why login completes. `[V]`/`[?]`
- The **wrong player name** traces to USER.NAME / PDTL.DSNM both being `account.Username` rather
  than a distinct display name (see Auth doc). Fixing the Account display-name mapping fixes both. `[V]`
- The lossy ExtendedDataUpdate (divergence #1) is a latent multiplayer issue (peers may not learn
  a session's QoS/address fully) but not a current single-player crash. `[?]`

---

## Open questions / Ghidra TODO
- `[?]` Does the client require the *full* extended-data struct on each ExtendedDataUpdate, or is
  ADDR+UATT sufficient? (C# sends the reduced form and login still works.)
- `[?]` Confirm UserAdded `DATA`/`USER` field order + that ALOC must equal the client-reported
  lang (C# hardcodes enUS) — verify `Blaze::UserSessions::*` classes in Ghidra (no instance this pass).
- `[?]` Is `BPS` required to be a real alias ("gva") or is "" tolerated? (C# default is "".)
- `[?]` Does the client read `SUBS`? (C# emits it; C++ doesn't.)
- `[?]` Why two updateNetworkInfo round-trips — is the 2nd mandatory or just a client refresh?
