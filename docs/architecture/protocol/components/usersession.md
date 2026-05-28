# UserSessions Component — 0x7802

Tracks per-client session state: extended network data, presence, and user lookup, on the Blaze lobby connection (port 42125).

---

## Request/response commands

| Command name | Cmd ID | Direction | C++ handler | C# handler | Status |
|---|---|---|---|---|---|
| fetchExtendedData | 0x03 | C→S | — (enum only) | — | ❌ |
| updateExtendedDataAttribute | 0x05 | C→S | — (enum only) | `Adapters/Blaze/Component/UserSessionsComponent.cs:46` | ⚠️ |
| updateHardwareFlags | 0x08 | C→S | — (enum only) | — | ❌ |
| lookupUser | 0x0C | C→S | `Blaze/Component/UserSessionComponent.cpp:145` | `Adapters/Blaze/Component/UserSessionsComponent.cs:117` | ✅ |
| lookupUsers | 0x0D | C→S | — (enum only) | — | ❌ |
| lookupUsersByPrefix | 0x0E | C→S | — (enum only) | — | ❌ |
| updateNetworkInfo | 0x14 | C→S | `Blaze/Component/UserSessionComponent.cpp:170` | `Adapters/Blaze/Component/UserSessionsComponent.cs:65` | ✅ |
| lookupUserGeoIPData | 0x17 | C→S | — (enum only) | — | ❌ |
| overrideUserGeoIPData | 0x18 | C→S | — (enum only) | — | ❌ |
| updateUserSessionClientData | 0x19 | C→S | `Blaze/Component/UserSessionComponent.cpp:188` | `Adapters/Blaze/Component/UserSessionsComponent.cs:94` | ✅ |
| setUserInfoAttribute | 0x1A | C→S | — (enum only) | `Adapters/Blaze/Component/UserSessionsComponent.cs:102` | ⚠️ |
| resetUserGeoIPData | 0x1B | C→S | — (enum only) | — | ❌ |

## Notifications sent (S→C)

| Notification name | Notify ID | Direction | C++ sender | C# sender | Status |
|---|---|---|---|---|---|
| UserSessionExtendedDataUpdate | 0x01 | S→C | `Blaze/Component/UserSessionComponent.cpp:96` | `UserSessionsComponent.cs:90` | ✅ |
| UserAdded | 0x02 | S→C | `Blaze/Component/UserSessionComponent.cpp:108` | `AuthenticationComponent.cs:171` | ✅ |
| UserRemoved | 0x03 | S→C | — (enum only) | — | ❌ |
| UserSessionDisconnected | 0x04 | S→C | — (enum only) | — | ❌ |
| UserUpdated | 0x05 | S→C | `Blaze/Component/UserSessionComponent.cpp:133` | `AuthenticationComponent.cs:84,183` | ✅ |

---

## Key TDF fields — updateNetworkInfo (0x14)

This command runs immediately after `loginPersona` and is critical for the server knowing the client's network address.

**Request (NetworkInfo / ADDR union):**

| Tag | Type | Description |
|---|---|---|
| `ADDR` | union | Network address union (active member = IpPairAddress) |
| `ADDR.VALU.EXIP.IP` | u32 | External IP (big-endian) |
| `ADDR.VALU.EXIP.PORT` | u16 | External port |
| `ADDR.VALU.INIP.IP` | u32 | Internal IP |
| `ADDR.VALU.INIP.PORT` | u16 | Internal port |

**Response:** empty (ack).

**Notification `UserSessionExtendedDataUpdate` (0x01) fired immediately after:**

| Tag | Type | Description |
|---|---|---|
| `DATA` | struct | `UserSessionExtendedData` (address, QoS, hw flags, attribute bits) |
| `USID` | u64 | User ID |

## Key TDF fields — lookupUser (0x0C)

**Request:**

| Tag | Type | Description |
|---|---|---|
| `USER` | struct | `UserIdentification` — contains the target user ID or name |

**Response:**

| Tag | Type | Description |
|---|---|---|
| `DATA` | struct | `UserSessionExtendedData` |
| `FLGS` | u32 | Online status flags (1=online) |
| `USER` | struct | `UserIdentification` (BUID, NAME, ACCT, LOC) |

---

## Porting gaps

- `fetchExtendedData` (0x03), `updateHardwareFlags` (0x08), `lookupUsers` (0x0D), `lookupUsersByPrefix` (0x0E), `lookupUserGeoIPData` (0x17), `overrideUserGeoIPData` (0x18), `resetUserGeoIPData` (0x1B) — present in C++ enum, not dispatched in either implementation.
- `updateExtendedDataAttribute` (0x05) in C# only sends a `UserUpdated` notification; C++ does not dispatch this command at all.
- `setUserInfoAttribute` (0x1A) in C# is a no-op ack; C++ does not dispatch it either.
- `UserRemoved` (0x03) and `UserSessionDisconnected` (0x04) notifications are defined in C++ but never sent — disconnect cleanup is incomplete in both implementations.
- C++ `NotifyUserAdded` writes the full `UserSessionExtendedData` from the server-side user object; C# writes it from the client's in-memory `ExtendedData` set during `login`/`updateNetworkInfo`.
