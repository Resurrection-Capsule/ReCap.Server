# Authentication Component — 0x01

Handles account login, persona selection, auth-token issuance, and legal-doc/TOS
acknowledgement on the Blaze lobby connection (port 42125). On the critical login path:
establishes the user identity (BlazeId / PersonaId / AccountId, name) and the session key
that the rest of the lobby depends on. Login side-effects fire **UserSessions (0x7802)**
notifications (UserAdded / UserUpdated) — see `usersession.md`.

Audit pass 2026-05-29: verified against C++ `Blaze/Component/AuthComponent.cpp`,
`Blaze/Functions.cpp` (TDF struct writers), `SporeNet/User.h`, the C# port
`Adapters/Blaze/Component/AuthenticationComponent.cs`, and a real WORKING C++ runtime log
(`Darkspore\DarksporeBin\Server\output.txt`). Tags: `[V]` = verified in cited source,
`[?]` = unverified / needs Ghidra. Ghidra was **not reachable** this pass (no instance).

---

## Verified login flow (from WORKING C++ log) `[V]`

Ordered command + notification sequence the real client drives (`output.txt`):

1. `Authentication::GetLegalDocsInfo`
2. `Util::PreAuth`, `Util::FetchClientConfig`
3. `Authentication::Login` → fires **UserSessions::UserAdded** (blazeId 1), then **UserSessions::UserUpdated** (blazeId 1)
4. `Authentication::LoginPersona`
5. `Util::PostAuth`
6. `UserSessions::UpdateNetworkInfo` → fires **UserSessions::UserSessionExtendedDataUpdate**
7. `Authentication::GetAuthToken` → fires **UserSessions::UserUpdated**
8. `GameManager (0x64)`, `Messaging::FetchMessages`
9. `UserSessions::UpdateNetworkInfo` (again) → fires **UserSessionExtendedDataUpdate** again
10. `Util::Ping`, `Util::SetClientData`, …

> IMPORTANT divergence vs C++ SOURCE: in the C++ *source*, UserAdded/UserUpdated are fired
> from `LoginPersona` (`AuthComponent.cpp:670-671`), not from `Login`. The WORKING log shows
> them after `Login`. Either the deployed binary differs from this source, or log labels are
> approximate. The C# port fires UserAdded+UserUpdated from `HandleLoginPersona` (matches the
> C++ source, not the log ordering). `[V]` (both), discrepancy `[?]`.

---

## Request/response commands

| Command | Cmd ID | C++ handler | C# handler | Status |
|---|---|---|---|---|
| getAuthToken | 0x24 | `AuthComponent.cpp:533` | `AuthenticationComponent.cs:72` | ✅ |
| login | 0x28 | `AuthComponent.cpp:550` | `AuthenticationComponent.cs:89` | ✅ |
| acceptTos | 0x29 | `AuthComponent.cpp:681` | `AuthenticationComponent.cs:222` | ✅ |
| getTosInfo | 0x2A | `AuthComponent.cpp:686` | `AuthenticationComponent.cs:228` | ✅ |
| getTermsAndConditions | 0x2E | `AuthComponent.cpp:693` | `AuthenticationComponent.cs:234` | ✅ |
| getPrivacyPolicyContent | 0x2F | `AuthComponent.cpp:700` | `AuthenticationComponent.cs:216` | ✅ |
| silentLogin | 0x32 | `AuthComponent.cpp:596` | `AuthenticationComponent.cs:262` | ✅ |
| expressLogin | 0x3C | `AuthComponent.cpp:619` | `AuthenticationComponent.cs:291` | ✅ |
| logout | 0x46 | `AuthComponent.cpp:674` | `AuthenticationComponent.cs:140` | ⚠️ see divergence |
| loginPersona | 0x6E | `AuthComponent.cpp:641` | `AuthenticationComponent.cs:147` | ✅ |
| getLegalDocsInfo / acceptLegalDocs | 0xF1 | — | `AuthenticationComponent.cs:57` | ⚠️ C# only |
| getEmailOptInSettings | 0xF2 | — | `AuthenticationComponent.cs:190` | ⚠️ C# only |
| getTermsOfServiceContent / getLegalDocContent | 0xF6 | — | `AuthenticationComponent.cs:203` | ⚠️ C# only |

C++ enum maps several more commands (createAccount 0x0A, updateAccount 0x14,
listUserEntitlements2 0x1D, createPersona 0x50, listPersonas 0x64, …) but `ParsePacket`
only dispatches the 10 above; rest return `false` (unhandled). `[V]` `AuthComponent.cpp:377-424`

---

## What each handler does (internals)

### login (0x28) `[V]`
- C++: reads `MAIL`,`PASS`,`TOKN`,`TYPE`,`DVID`; `UserManager.Login()` resolves/creates user;
  **seeds the session's `UserSessionExtendedData`** (country=US, hardwareFlags=1,
  userAttributes=3, 2 BlazeObjectIds {4,1,0}+{5,1,0}, 5x latency=1161889797, QoS dbps=128000
  NAT=Open ubps=2); `set_user(user)`; replies `WriteLogin` (Login struct, NOT SESS).
  `AuthComponent.cpp:550-594`
- C# `HandleLogin`: reads `LoginRequest`; `getAccountByEmailAndPassword`; sets
  `client.UserId/Username`; `InitializeClientExtendedData` (same seed values 1:1); replies
  `LoginResponse` with one `PersonaDetails` in `PLST`. `AuthenticationComponent.cs:89-138`,
  seed `:319-337`.
- **C# does NOT fire any notification from Login** (C++ source also doesn't here). The log
  shows UserAdded/UserUpdated after Login - C# fires them from LoginPersona instead. `[V]`

### loginPersona (0x6E) `[V]`
- C++: builds `SessionInfo` (blazeId=uid, firstLogin=false, key="telemetry_key",
  lastLogin=now, uid=uid; PDTL name/last/id/status=Active), `sessionInfo.Write(packet)` as the
  **top-level reply (no SESS wrapper)**, then fires `NotifyUserAdded` + `NotifyUserUpdated(Connected)`.
  `AuthComponent.cpp:641-672`
- C# `HandleLoginPersona`: `getAccountById(client.UserId)`; replies `BuildSessionInfo`
  (SessionInfo top-level); then builds `NotifyUserAdded` (copies extended data + address +
  UserInfo) -> `Notify(...,0x7802,2)`; then `UserStatus{Connected}` -> `Notify(...,0x7802,5)`.
  `AuthenticationComponent.cs:147-188`

### getAuthToken (0x24) `[V]`
- C++: `set_auth_token(to_string(user id))`; replies `AUTH` string; fires
  `NotifyUserUpdated(Authenticated)`. `AuthComponent.cpp:533-548`
- C#: `client.AuthToken ??= UserId.ToString()`; persists via `setAccountAuthToken`; replies
  `GetAuthTokenResponse{AUTH}`; fires `UserStatus{Authenticated}` -> `Notify(...,0x7802,5)`.
  `AuthenticationComponent.cs:72-87` - **matches C++.**

### silentLogin (0x32) / expressLogin (0x3C) `[V]`
- C++: resolve user by auth token (silent) / email (express); reply `WriteFullLogin`
  (AGUP,NTOS,PCTK,PRIV, **SESS struct**, SPAM,THST,TURI). `AuthComponent.cpp:596-639`
- C#: resolve account; reply **`BuildSessionInfo` (bare SessionInfo, NOT the FullLogin
  envelope)**. Divergence - see below. `AuthenticationComponent.cs:262-317`

### logout (0x46) `[V]`
- C++: `user->Logout()` (server-side session cleanup). `AuthComponent.cpp:674-679`
- C#: only fires `UserSessionLogoutInfo` notification (0x7802/9); **no session cleanup**.
  `AuthenticationComponent.cs:140-145`

### TOS / legal `[V]`
- C++ `getTermsAndConditions`: LDVC="Something", TCOL=len, TCOT="Hello this is something".
  `getPrivacyPolicy`: LDVC="Something2". `getTosInfo`: EAMC/PMC/PRIV/THST/TURI. `AuthComponent.cpp:509-531,686-705`
- C# mirrors: `getTermsAndConditions` returns the same placeholder text;
  `getTosInfo`/`getEmailOptInSettings` return EAMC/PMC. `AuthenticationComponent.cs:228-243`

---

## Key TDF field tables

### login reply - C++ `WriteLogin` (Login struct) `[V]` `AuthComponent.cpp:430-472`
| Tag | Type | C++ value | C# (`LoginResponse`) |
|---|---|---|---|
| NTOS | bool | 0 | NeedsLegalDoc=false |
| PCTK | string | "unknown_data" | "unknown_data" |
| PLST | list&lt;PDTL&gt; | 1 persona (name/last/id/status=Active) | 1 `PersonaDetails` |
| PRIV | string | "" | "" |
| SKEY | string | "telemetry_key" | "telemetry_key" |
| SPAM | bool | 0 | IsOfLegalContactAge=false |
| THST/TURI | string | "" | "" |
| UID | i64 | **persona id (= user id)** | UserId = account.Id |

> C# `LoginResponse` adds `ANON`,`UNDR` fields not in C++. C++ `WriteLogin` has a commented
> alternative `UID = client.get_id()`. Both use the user/account id for UID. `[V]`

### SESS struct (loginPersona / fullLogin) - `SessionInfo::Write` `[V]` `Functions.cpp:290-302`
| Tag | Type | C++ value | C# (`SessionInfo`) |
|---|---|---|---|
| BUID | i64 | user id | account.Id |
| FRST | bool | 0 | false |
| KEY  | string | "telemetry_key" | "telemetry_key" |
| LLOG | i64 | now (unix) | CurrentUnixTime |
| MAIL | string | account email | account.Email |
| PDTL | struct | persona details | PersonaDetails |
| UID  | i64 | user id | account.Id |

### PDTL - `PersonaDetails::Write` `[V]` `Functions.cpp:280-287`
| Tag | Type | C++ | C# (`PersonaDetails`) |
|---|---|---|---|
| DSNM | string | `user->get_name()` | **`account.Username`** <- player name source |
| LAST | u32 | now | CurrentUnixTime |
| PID  | i64 | user id | **`client.UserId`** (login) / `account.Id` (persona resp.) |
| STAS | enum | Active(2) | Active |
| XREF | u64 | 0 | 0 |
| XTYP | enum | 0 | ConnectionProfileType.Invalid |

> **Player-name finding:** DSNM is sourced from `account.Username` in C#, from `user->get_name()`
> (the `mName` field, separate from username/email) in C++. If the C# seed maps email->Username,
> the lobby shows the email/login as the display name. The C# `User`/`Account` model has no
> distinct display-name vs login distinction on this path. `[V]` `AuthenticationComponent.cs:130,349`

### AUTH - getAuthToken reply `[V]`
| Tag | Type | Value |
|---|---|---|
| AUTH | string | C++: `to_string(user id)`; C#: `client.UserId.ToString()` |

---

## Notifications fired (all are UserSessions component 0x7802)

| Notification | ID | Fired by (C++) | Fired by (C#) |
|---|---|---|---|
| UserAdded | 0x7802/2 | `LoginPersona` `AuthComponent.cpp:670` | `HandleLoginPersona` `cs:179` |
| UserUpdated (Connected) | 0x7802/5 | `LoginPersona` `:671` | `cs:181-185` |
| UserUpdated (Authenticated) | 0x7802/5 | `GetAuthToken` `:547` | `GetAuthToken` `cs:80-84` |
| UserSessionLogoutInfo | 0x7802/9 | - (C++ uses `user->Logout()`) | `HandleLogout` `cs:143` (C#-only) |

C# matches the C++-source notification set. (Log shows them earlier in the flow - see flow note.) `[V]`

---

## Divergences (C++ vs C#)

1. **silentLogin / expressLogin envelope.** C++ wraps the SessionInfo inside the FullLogin
   envelope (`AGUP,NTOS,PCTK,PRIV,SESS,SPAM,THST,TURI`). C# returns a **bare `SessionInfo`**
   (same as loginPersona). If the client expects the FullLogin wrapper for these, silent/express
   login replies are malformed. `[V]` `AuthComponent.cpp:474-507,613-616,635-638` vs `cs:287,315`
2. **logout cleanup.** C++ calls `user->Logout()`; C# only sends a logout notification, no
   server-side session teardown. `[V]`
3. **Display name source.** C++ DSNM = `user->get_name()` (dedicated name); C# DSNM =
   `account.Username`. Player-name bug origin. `[V]`
4. **PDTL.PID inconsistency in C#.** login uses `account.Id`; loginPersona response uses
   `client.UserId` for `PersonaDetails.PersonaId` but `account.Id` for SessionInfo.UID/BUID. All
   equal today (single account, id==1) but conceptually PersonaId != BlazeId in real Blaze. `[V]` `cs:132,346,351`
5. **C#-only commands** 0xF1/0xF2/0xF6 satisfy newer Blaze client legal-doc/opt-in flow absent
   from C++. `[V]`
6. **Login token** is trivial/predictable (`token = id`), shared as SKEY + AUTH + REST bearer. `[V]`

---

## Open questions / Ghidra TODO
- `[?]` Why does the WORKING log fire UserAdded/UserUpdated after **Login** while both C++ source
  and C# fire them from **LoginPersona**? Confirm with deployed-binary trace or packet capture.
- `[?]` Does the client require the FullLogin envelope (SESS-wrapped) for silentLogin/expressLogin,
  or is a bare SessionInfo accepted? (C# returns bare.)
- `[?]` Confirm the client reads UID/BUID as PersonaId or BlazeId - verify against
  `Blaze::Authentication::*` reply class in Ghidra (no instance loaded this pass).
- `[?]` Confirm PDTL.XTYP/XREF requirements and whether DSNM must be a distinct display name vs login.
- `[?]` Verify whether logout must tear down the UserSessions entry (C# doesn't).
