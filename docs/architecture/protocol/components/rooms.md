# Rooms Component — 0x15

Manages lobby room views, categories, and member presence for the in-game social layer, on the Blaze lobby connection (port 42125).

---

## Request/response commands

| Command name | Cmd ID | Direction | C++ handler | C# handler | Status |
|---|---|---|---|---|---|
| selectViewUpdates | 0x0A | C→S | `Blaze/Component/RoomsComponent.cpp:402` | `Adapters/Blaze/Component/RoomsComponent.cs:26` | ✅ |
| selectCategoryUpdates | 0x0B | C→S | `Blaze/Component/RoomsComponent.cpp:441` | `Adapters/Blaze/Component/RoomsComponent.cs:50` | ⚠️ |
| joinRoom | 0x14 | C→S | `Blaze/Component/RoomsComponent.cpp:476` | — | ❌ |
| leaveRoom | 0x15 | C→S | — (enum only) | — | ❌ |
| kickUser | 0x1F | C→S | — (enum only) | — | ❌ |
| transferRoomHost | 0x28 | C→S | — (enum only) | — | ❌ |
| createRoom | 0x66 | C→S | — (enum only) | — | ❌ |
| removeRoom | 0x67 | C→S | — (enum only) | — | ❌ |
| getViews | 0x6D | C→S | — (enum only) | — | ❌ |
| lookupRoomData | 0x78 | C→S | — (enum only) | — | ❌ |
| setRoomAttributes | 0x82 | C→S | — (enum only) | — | ❌ |
| selectPseudoRoomUpdates | 0xA0 | C→S | `Blaze/Component/RoomsComponent.cpp:551` | — | ❌ |

## Notifications sent (S→C)

| Notification name | Notify ID | Direction | C++ sender | C# sender | Status |
|---|---|---|---|---|---|
| NotifyRoomViewUpdated | 0x0A | S→C | `Blaze/Component/RoomsComponent.cpp:213` | `RoomsComponent.cs:44` (inline) | ✅ |
| NotifyRoomViewAdded | 0x0B | S→C | `Blaze/Component/RoomsComponent.cpp:223` | `RoomsComponent.cs:36` (inline) | ✅ |
| NotifyRoomViewRemoved | 0x0C | S→C | `Blaze/Component/RoomsComponent.cpp:233` | — | ❌ |
| NotifyRoomCategoryUpdated | 0x14 | S→C | `Blaze/Component/RoomsComponent.cpp:240` | — | ❌ |
| NotifyRoomCategoryAdded | 0x15 | S→C | `Blaze/Component/RoomsComponent.cpp:250` | — | ❌ |
| NotifyRoomCategoryRemoved | 0x16 | S→C | `Blaze/Component/RoomsComponent.cpp:260` | — | ❌ |
| NotifyRoomUpdated | 0x1E | S→C | `Blaze/Component/RoomsComponent.cpp:267` | — | ❌ |
| NotifyRoomAdded | 0x1F | S→C | `Blaze/Component/RoomsComponent.cpp:277` | — | ❌ |
| NotifyRoomRemoved | 0x20 | S→C | `Blaze/Component/RoomsComponent.cpp:287` | — | ❌ |
| NotifyRoomMemberJoined | 0x32 | S→C | `Blaze/Component/RoomsComponent.cpp:308` | — | ❌ |
| NotifyRoomMemberLeft | 0x33 | S→C | `Blaze/Component/RoomsComponent.cpp:316` | — | ❌ |
| NotifyRoomMemberUpdated | 0x34 | S→C | `Blaze/Component/RoomsComponent.cpp:324` | — | ❌ |
| NotifyRoomKick | 0x3C | S→C | `Blaze/Component/RoomsComponent.cpp:332` | — | ❌ |
| NotifyRoomHostTransfer | 0x46 | S→C | `Blaze/Component/RoomsComponent.cpp:340` | — | ❌ |
| NotifyRoomAttributesSet | 0x50 | S→C | `Blaze/Component/RoomsComponent.cpp:348` | — | ❌ |

---

## Key TDF fields — selectViewUpdates (0x0A)

**Request:**

| Tag | Type | Description |
|---|---|---|
| `UPDT` | u32 | Non-zero = request view updates |

**Response:**

| Tag | Type | Description |
|---|---|---|
| `SEID` | u32 | Session/subscription event ID |
| `UPRE` | enum | Update reason (UserRoomCreated, etc.) |
| `USID` | i64 | User ID |
| `VWID` | u32 | View ID |

**Followed by notifications:** `NotifyRoomViewAdded` + `NotifyRoomViewUpdated` if `UPDT != 0`.

**RoomViewData struct fields (inside notifications):**

| Tag | Type | Description |
|---|---|---|
| `DISP` | string | Display name |
| `NAME` | string | View name |
| `VWID` | u32 | View ID |
| `MXRM` | u32 | Max rooms |
| `USRM` | u32 | User-created room limit |

---

## Porting gaps

- `joinRoom` (0x14) is fully implemented in C++ (creates rooms/categories on demand, fires `NotifyRoomAdded`, `NotifyRoomMemberJoined`); absent in C#. Client sends this to enter a lobby room after `selectCategoryUpdates`.
- `selectCategoryUpdates` (0x0B) in C++ fires `NotifyRoomCategoryAdded` + `NotifyRoomCategoryUpdated` for four categories; C# sends an empty ack, so the client sees no room categories.
- 13 of 15 defined notifications are not sent from C#.
- The `RoomData`, `RoomCategoryData`, and `RoomViewData` TDF structs are fully modeled in C++ but have no C# counterparts.
- `selectPseudoRoomUpdates` (0xA0) in C++ is an empty ack; not dispatched in C#.
