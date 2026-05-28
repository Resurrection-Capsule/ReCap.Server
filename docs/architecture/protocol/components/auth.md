# Authentication Component — 0x01

Handles account login, persona selection, and terms-of-service acknowledgement on the Blaze lobby connection (port 42125).

---

## Request/response commands

| Command name | Cmd ID | Direction | C++ handler | C# handler | Status |
|---|---|---|---|---|---|
| getAuthToken | 0x24 | C→S | `Blaze/Component/AuthComponent.cpp:533` | `Adapters/Blaze/Component/AuthenticationComponent.cs:72` | ✅ |
| login | 0x28 | C→S | `Blaze/Component/AuthComponent.cpp:550` | `Adapters/Blaze/Component/AuthenticationComponent.cs:89` | ✅ |
| acceptTos | 0x29 | C→S | `Blaze/Component/AuthComponent.cpp:681` | `Adapters/Blaze/Component/AuthenticationComponent.cs:221` | ✅ |
| getTosInfo | 0x2A | C→S | `Blaze/Component/AuthComponent.cpp:686` | `Adapters/Blaze/Component/AuthenticationComponent.cs:227` | ✅ |
| getTermsAndConditions | 0x2E | C→S | `Blaze/Component/AuthComponent.cpp:692` | `Adapters/Blaze/Component/AuthenticationComponent.cs:232` | ✅ |
| getPrivacyPolicyContent | 0x2F | C→S | `Blaze/Component/AuthComponent.cpp:698` | `Adapters/Blaze/Component/AuthenticationComponent.cs:214` | ✅ |
| silentLogin | 0x32 | C→S | `Blaze/Component/AuthComponent.cpp:596` | `Adapters/Blaze/Component/AuthenticationComponent.cs:261` | ✅ |
| expressLogin | 0x3C | C→S | `Blaze/Component/AuthComponent.cpp:619` | `Adapters/Blaze/Component/AuthenticationComponent.cs:289` | ✅ |
| logout | 0x46 | C→S | `Blaze/Component/AuthComponent.cpp:674` | `Adapters/Blaze/Component/AuthenticationComponent.cs:139` | ✅ |
| loginPersona | 0x6E | C→S | `Blaze/Component/AuthComponent.cpp:641` | `Adapters/Blaze/Component/AuthenticationComponent.cs:146` | ✅ |
| createAccount | 0x0A | C→S | — (not handled in C++ switch) | — | ❌ |
| updateAccount | 0x14 | C→S | — (not handled in C++ switch) | — | ❌ |
| listUserEntitlements2 | 0x1D | C→S | — (not handled in C++ switch) | — | ❌ |
| listEntitlements | 0x20 | C→S | — (not handled in C++ switch) | — | ❌ |
| getPasswordRules | 0x26 | C→S | — (not handled in C++ switch) | — | ❌ |
| createPersona | 0x50 | C→S | — (not handled in C++ switch) | — | ❌ |
| listPersonas | 0x64 | C→S | — (not handled in C++ switch) | — | ❌ |
| acceptLegalDocs | 0xF1 | C→S | — (not handled in C++ switch) | `Adapters/Blaze/Component/AuthenticationComponent.cs:57` | ⚠️ C# only |
| getEmailOptInSettings | 0xF2 | C→S | — (not handled in C++ switch) | `Adapters/Blaze/Component/AuthenticationComponent.cs:189` | ⚠️ C# only |
| getTermsOfServiceContent | 0xF6 | C→S | — (not handled in C++ switch) | `Adapters/Blaze/Component/AuthenticationComponent.cs:203` | ⚠️ C# only |

## Notifications sent

| Notification name | Notify ID | Direction | C++ sender | C# sender | Status |
|---|---|---|---|---|---|
| UserSessionExtendedDataUpdate | 0x7802/0x01 | S→C | via `UserSessionComponent::NotifyUserUpdated` | `AuthenticationComponent.cs:84` | ✅ |
| UserAdded | 0x7802/0x02 | S→C | via `UserSessionComponent::NotifyUserAdded` | `AuthenticationComponent.cs:171` | ✅ |
| UserUpdated | 0x7802/0x05 | S→C | via `UserSessionComponent::NotifyUserUpdated` | `AuthenticationComponent.cs:84,183` | ✅ |
| UserSessionLogoutInfo | 0x7802/0x09 | S→C | — | `AuthenticationComponent.cs:143` | ⚠️ C# only |

---

## Key TDF fields — login (0x28)

**Request:**

| Tag | Type | Description |
|---|---|---|
| `DVID` | u64 | Device ID (purpose unclear) |
| `MAIL` | string | Account email address |
| `PASS` | string | Account password |
| `TOKN` | string | Auth token (alternative auth path) |
| `TYPE` | enum | Token type (0=Unknown, 1=AuthToken, 2=PCLoginToken) |

**Response:**

| Tag | Type | Description |
|---|---|---|
| `NTOS` | bool | Needs terms-of-service acceptance |
| `PCTK` | string | PC login token (hardcoded `"unknown_data"`) |
| `PLST` | list&lt;struct&gt; | Persona list — each entry is a `PDTL` struct |
| `SKEY` | string | Session/telemetry key |
| `SPAM` | bool | Is of legal contact age |
| `UID` | u64 | Blaze user ID |

**PDTL struct fields:**

| Tag | Type | Description |
|---|---|---|
| `DSNM` | string | Display name |
| `LAST` | u32 | Last login unix timestamp |
| `PID` | i64 | Persona ID |
| `STAS` | enum | Persona status (2 = Active) |

## Key TDF fields — loginPersona (0x6E)

**Response (SessionInfo struct):**

| Tag | Type | Description |
|---|---|---|
| `BUID` | i64 | Blaze user ID |
| `FRST` | bool | First login flag |
| `KEY` | string | Session key |
| `LLOG` | i64 | Last login unix timestamp |
| `MAIL` | string | Account email |
| `PDTL` | struct | Persona details (DSNM, LAST, PID, STAS) |
| `UID` | i64 | User ID |

---

## Porting gaps

- `createAccount` (0x0A), `updateAccount` (0x14), `listUserEntitlements2` (0x1D), `listEntitlements` (0x20), `getPasswordRules` (0x26), `createPersona` (0x50), `listPersonas` (0x64) — present in C++ enum but not dispatched in either C++ or C# switch; client does not appear to call these in the normal login flow.
- C++ `logout` (0x46) calls `user->Logout()` to clean up server state; C# version only sends a `UserSessionLogoutInfo` notification — no session cleanup logic.
- C# adds three commands not in C++ (`0xF1`, `0xF2`, `0xF6`) that satisfy newer Blaze client variants.
