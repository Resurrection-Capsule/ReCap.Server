# AssociationLists Component — 0x19

Manages friend and ignore lists for the client's social graph on the Blaze lobby connection (port 42125).

---

## Request/response commands

| Command name | Cmd ID | Direction | C++ handler | C# handler | Status |
|---|---|---|---|---|---|
| addUsersToList | 0x01 | C→S | `Blaze/Component/AssociationComponent.cpp:217` | — | ❌ |
| removeUsersFromList | 0x02 | C→S | `Blaze/Component/AssociationComponent.cpp:247` | — | ❌ |
| clearLists | 0x03 | C→S | — (enum only) | — | ❌ |
| setUsersToList | 0x04 | C→S | — (enum only) | — | ❌ |
| getListForUser | 0x05 | C→S | — (enum only) | — | ❌ |
| getLists | 0x06 | C→S | `Blaze/Component/AssociationComponent.cpp:277` | `Adapters/Blaze/Component/AssociationListsComponent.cs:15` | ✅ |
| subscribeToLists | 0x07 | C→S | — (enum only) | — | ❌ |
| unsubscribeFromLists | 0x08 | C→S | — (enum only) | — | ❌ |

## Notifications sent (S→C)

| Notification name | Notify ID | Direction | C++ sender | C# sender | Status |
|---|---|---|---|---|---|
| NotifyUpdateListMembership | 0x01 | S→C | `Blaze/Component/AssociationComponent.cpp:191` | — | ❌ |

---

## Key TDF fields — getLists (0x06)

Primary command on the login path. Client requests its friend/ignore lists by sending a set of `ListInfo` descriptors.

**Request:**

| Tag | Type | Description |
|---|---|---|
| `ALST` | list&lt;struct&gt; | Requested lists — each entry is a `ListInfo` containing `LID` (name + type) and flags |
| `MXRC` | u32 | Max member result count |
| `OFRC` | u32 | Member result offset |

**List types (LID.TYPE):**

| Value | Meaning |
|---|---|
| 4 | Ignore list |
| 5 | Friend list |

**Response:**

| Tag | Type | Description |
|---|---|---|
| `LMAP` | list&lt;struct&gt; | One `ListMembers` entry per requested list |

**ListMembers struct:**

| Tag | Type | Description |
|---|---|---|
| `INFO` | struct | `ListInfo` (BOID, FLGS, LID, LMS, PRID) |
| `MEML` | list&lt;struct&gt; | Member list — each entry has `ListMemberInfo` (LMID, TIME) |
| `OFRC` | u32 | Offset echo |
| `TOCT` | u32 | Total count |

**ListMemberId (LMID) fields:**

| Tag | Type | Description |
|---|---|---|
| `BLID` | i64 | Blaze ID |
| `PNAM` | string | Persona name |
| `XREF` | u64 | External reference ID |
| `XTYP` | enum | External type |

---

## Porting gaps

- `addUsersToList` (0x01) and `removeUsersFromList` (0x02) are implemented in C++ (read `BIDL` list, fire `NotifyUpdateListMembership`); absent in C# — clients cannot modify their lists.
- `NotifyUpdateListMembership` (0x01) is never sent from C#.
- Both C++ and C# `getLists` responses use hardcoded/placeholder friend entries (C++ hardcodes Dalkon+test; C# hardcodes Ignoredude+SomeFriend). No persistence layer connects these to real account data.
- `subscribeToLists` (0x07) and `unsubscribeFromLists` (0x08) are unimplemented in both; the client does not appear to call these in the normal single-player flow.
- `clearLists` (0x03), `setUsersToList` (0x04), `getListForUser` (0x05) are undefined in both implementations.
