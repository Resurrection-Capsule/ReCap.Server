# Rooms Component — 0x15

Manages lobby room views, categories, and member presence for the in-game social layer, on the
Blaze lobby connection (port 42125). On the post-login path: client calls `selectViewUpdates`,
`selectCategoryUpdates`, then `joinRoom` to populate its room/membership state before the
`GameManager::resetDedicatedServer` transition.

This component WAS a **top crash suspect**: the client sends `joinRoom` (0x14) during lobby setup
and the C# server silently dropped it (no handler). See Crash Relevance section.

> **Implementation update 2026-05-30.** `joinRoom` (0x14) is now **handled**, and a new
> `RoomManager` (`Adapters/Blaze/Component/RoomManager.cs`) pre-seeds view 1 / categories 1-4 /
> rooms 1-4 (1:1 with C++ `RoomManager()` ctor). `selectViewUpdates` now replies the full
> `SEID/UPRE/USID/VWID` body (was empty) and `selectCategoryUpdates` emits full `RoomCategoryData`
> per category (was a 3-field stub). Builds clean; **awaiting client test** to confirm the crash
> is resolved. The tables below describe the C++ ground truth the port now follows; the "absent in
> C#" notes for these three commands are historical (pre-2026-05-30).

Audit pass 2026-05-29: verified against C++ `Blaze/Component/RoomsComponent.cpp`,
`Blaze/Component/RoomsComponent.h`, `SporeNet/Room.cpp`, `Blaze/Functions.cpp` (TDF struct
writers), the C# port `Adapters/Blaze/Component/RoomsComponent.cs`, and a WORKING C++ runtime
log (`output.txt`). Tags: `[V]` = verified in cited source, `[?]` = unverified / needs Ghidra.
Ghidra was **not reachable** this pass (no loaded instance).

---

## Verified flow position (WORKING log) `[V]`

Post-login lobby sequence (from `output.txt`):
`selectViewUpdates` → NotifyRoomViewAdded → NotifyRoomViewUpdated →
`selectCategoryUpdates` → 4× (NotifyRoomCategoryAdded → NotifyRoomCategoryUpdated) →
`joinRoom` → NotifyRoomAdded → NotifyRoomUpdated → [joinRoom reply] → NotifyRoomMemberJoined
→ `GameManager::resetDedicatedServer` (next step)

`[V]` `output.txt:335-372`. The `joinRoom` exchange is the **last Rooms traffic before
`resetDedicatedServer`**; the client builds its room/membership record from the reply and the
`NotifyRoomMemberJoined` before proceeding.

---

## Request/response commands

| Command | Cmd ID | C++ handler (file:line) | C# handler (file:line) | Status |
|---|---|---|---|---|
| selectViewUpdates | 0x0A | `RoomsComponent.cpp:402` | `RoomsComponent.cs:26` | ✅ content diverges — see §3 |
| selectCategoryUpdates | 0x0B | `RoomsComponent.cpp:441` | `RoomsComponent.cs:51` | ⚠️ body/ids diverge |
| **joinRoom** | **0x14** | **`RoomsComponent.cpp:476`** | **— DROPPED** | **❌ CRASH LEAD** |
| leaveRoom | 0x15 | — (enum only, not dispatched) | — | ❌ |
| kickUser | 0x1F | — (enum only) | — | ❌ |
| transferRoomHost | 0x28 | — (enum only) | — | ❌ |
| createRoomCategory | 0x64 | — (enum only) | — | ❌ |
| removeRoomCategory | 0x65 | — (enum only) | — | ❌ |
| createRoom | 0x66 | — (enum only) | — | ❌ |
| removeRoom | 0x67 | — (enum only) | — | ❌ |
| clearBannedUsers | 0x68 | — (enum only) | — | ❌ |
| unbanUser | 0x69 | — (enum only) | — | ❌ |
| getViews | 0x6D | — (enum only) | — | ❌ |
| createScheduledCategory | 0x6E | — (enum only) | — | ❌ |
| deleteScheduledCategory | 0x6F | — (enum only) | — | ❌ |
| getSchedulesCategories | 0x70 | — (enum only) | — | ❌ |
| lookupRoomData | 0x78 | — (enum only) | — | ❌ |
| listBannedUsers | 0x7A | — (enum only) | — | ❌ |
| setRoomAttributes | 0x82 | — (enum only) | — | ❌ |
| checkEntryCriteria | 0x8C | — (enum only) | — | ❌ |
| toggleJoinedRoomNotifications | 0x96 | — (enum only) | — | ❌ |
| selectPseudoRoomUpdates | 0xA0 | `RoomsComponent.cpp:551` (empty reply) | — | ❌ |

`[V]` C++ `ParsePacket` dispatches **only four** commands: `selectViewUpdates` (0x0A),
`selectCategoryUpdates` (0x0B), `joinRoom` (0x14), `selectPseudoRoomUpdates` (0xA0).
All 18 others exist only in the `PacketID` enum + name-table; `ParsePacket` returns `false`
for them. `RoomsComponent.cpp:187-210`. C# dispatches only 0x0A and 0x0B; default logs
"Unknown command" and returns false. `RoomsComponent.cs:11-23`.

---

## Notifications sent (S→C)

| Notification | Notify ID | C++ sender (file:line) | C# sender | Status |
|---|---|---|---|---|
| NotifyRoomViewUpdated | 0x0A | `RoomsComponent.cpp:213` | `RoomsComponent.cs:41` (inline) | ✅ body diverges |
| NotifyRoomViewAdded | 0x0B | `RoomsComponent.cpp:223` | `RoomsComponent.cs:35` (inline) | ✅ body diverges |
| NotifyRoomViewRemoved | 0x0C | `RoomsComponent.cpp:233` | — | ❌ |
| NotifyRoomCategoryUpdated | 0x14 | `RoomsComponent.cpp:240` | `RoomsComponent.cs:72` (inline) | ⚠️ body truncated |
| NotifyRoomCategoryAdded | 0x15 | `RoomsComponent.cpp:250` | `RoomsComponent.cs:64` (inline) | ⚠️ body truncated |
| NotifyRoomCategoryRemoved | 0x16 | `RoomsComponent.cpp:260` | — | ❌ |
| NotifyRoomUpdated | 0x1E | `RoomsComponent.cpp:267` | — | ❌ |
| NotifyRoomAdded | 0x1F | `RoomsComponent.cpp:277` | — | ❌ |
| NotifyRoomRemoved | 0x20 | `RoomsComponent.cpp:287` | — | ❌ |
| NotifyRoomPopulationUpdated | 0x28 | `RoomsComponent.cpp:294` | — | ❌ |
| NotifyRoomMemberJoined | 0x32 | `RoomsComponent.cpp:308` | — | ❌ |
| NotifyRoomMemberLeft | 0x33 | `RoomsComponent.cpp:316` | — | ❌ |
| NotifyRoomMemberUpdated | 0x34 | `RoomsComponent.cpp:324` | — | ❌ |
| NotifyRoomKick | 0x3C | `RoomsComponent.cpp:332` | — | ❌ |
| NotifyRoomHostTransfer | 0x46 | `RoomsComponent.cpp:340` | — | ❌ |
| NotifyRoomAttributesSet | 0x50 | `RoomsComponent.cpp:348` | — | ❌ |

C# fires 4 of 16 notifications (NotifyRoomViewAdded/Updated + NotifyRoomCategoryAdded/Updated)
with truncated bodies. 12 of 16 never sent from C#. `[V]`

> `[?]` C# `GetNotificationName` also registers 0x5A "MemberAttributesSet" — not present in C++
> enum. May be a newer Blaze protocol version artifact. `RoomsComponent.cs:113`

---

## What each handler does (internals)

### selectViewUpdates (0x0A) `[V]` — `RoomsComponent.cpp:402`
1. Requires a logged-in user (`request.get_user()`), else returns with no reply.
2. Reads `UPDT`. If `UPDT != 0`: `roomViewId = 1`, `add = true`, `update = true`.
3. Replies with `RoomReplicationContext`: `SEID=1`, `UPRE=UserRoomCreated`, `USID=user->get_id()`,
   `VWID=roomViewId`.
4. Fires (only when `UPDT != 0`): `NotifyRoomViewAdded(viewId=1)`, then
   `NotifyRoomViewUpdated(viewId=1)`. Both look up the view in `RoomManager` and serialize
   `roomView->WriteTo(packet)` — only emit if view exists. `RoomsComponent.cpp:427-438`

**C# divergence:** `HandleSelectViewUpdates` sends two `RoomViewNotification` (ViewId=0,
Name="default") as Added then Updated, then calls `client.RespondTo(packet)` with **no reply
body** (no SEID/UPRE/USID/VWID). Order inverted vs C++ (notifications before reply). `cs:26-48`

Working-run log order `[V]` (`output.txt:335-338`): reply first, then NotifyRoomViewAdded,
then NotifyRoomViewUpdated.

### selectCategoryUpdates (0x0B) `[V]` — `RoomsComponent.cpp:441`
1. Reads `VWID`.
2. If `VWID == 0`: no-op (pseudo-room categories, unimplemented).
3. If `VWID != 0`: loops `i = 0..3`, `categoryId = i + 1` (**1,2,3,4**). For each fires
   `NotifyRoomCategoryAdded(categoryId)` then `NotifyRoomCategoryUpdated(categoryId)`.
   Each helper looks up `GetRoomCategory(categoryId)` and serializes full `RoomCategoryData`
   via `category->WriteTo(packet)`. Only emits if category exists. `RoomsComponent.cpp:447-454`
4. Replies with `VWID` (echoed). `WriteSelectCategoryUpdates`, `RoomsComponent.cpp:361-363`.

**C# divergence:** `HandleSelectCategoryUpdates` loops `i = 0..3` → ids **0,1,2,3** (off by
one). Sends `RoomCategoryNotification` with only `CTID/NAME/VWID` (3 fields vs 17 in full
`RoomCategoryData`). Category names hardcoded: "General","Help","Trading","LFG". Does not gate
on `VWID==0`. Does not send a reply body. `cs:51-80`

Working-run log order `[V]` (`output.txt:348-363`): reply first, then 4× (Added → Updated).

### joinRoom (0x14) `[V]` — `RoomsComponent.cpp:476` ← CRASH-RELEVANT COMMAND
1. Requires a user, else no reply.
2. Reads: `PASS` (string), `PVAL` (string), `INID` (u64 inviter id), `INVT` (bool invited),
   `CTID` (u32 category id), `RMID` (u32 room id).
3. **Room resolution / on-demand creation:**
   - `RMID == 0`: `room = roomManager.CreateRoom()`, `roomId = room->GetId()`, `newRoom = true`.
   - Else: `room = roomManager.GetRoom(RMID)`. Not found → error `ROOMS_ERR_NOT_FOUND`, return.
4. **Category resolution / on-demand creation:**
   - `CTID == 0`: `roomCategory = roomManager.CreateRoomCategory()`, `newCategory = true`.
   - Else: `roomManager.GetRoomCategory(CTID)`. Not found → error
     `ROOMS_ERR_CREATE_UNKNOWN_CATEGORY`, return.
5. If `roomCategory->GetView()` is null: `roomCategory->SetView(roomManager.CreateRoomView())`
   (guarantees non-null View for serialization).
6. `room->SetCategory(roomCategory)`; `room->AddUser(user)`.
7. If `newRoom`: fire `NotifyRoomAdded(roomId)` then `NotifyRoomUpdated(roomId)` (both serialize
   `room->WriteTo`). `RoomsComponent.cpp:536-539`
8. Reply = `WriteJoinRoom` packet. `RoomsComponent.cpp:543-545`
9. After reply: fire `NotifyRoomMemberJoined(roomId, userId)`. `RoomsComponent.cpp:548`

Wire order from log `[V]` (`output.txt:368-372`): NotifyRoomAdded → NotifyRoomUpdated →
[joinRoom reply] → NotifyRoomMemberJoined.

**C# status: NOT IMPLEMENTED.** `HandlePacket` default case logs debug and returns false.
Client receives no reply to a blocking request → crash/stall before `resetDedicatedServer`.

### selectPseudoRoomUpdates (0xA0) `[V]` — `RoomsComponent.cpp:551`
Empty `request.reply()` with no body. Not dispatched in C#.

---

## RoomManager pre-seeding `[V]` — `Room.cpp:203-217`

At server start, `RoomManager()` ctor creates (in order): **RoomView id 1** (name "Lobby View
#1"); **categories 1,2,3,4** each with `SetView(view 1)` (names "Lobby Category #1..4");
**rooms 1,2,3,4** each with `SetCategory(category i+1)` (names "Lobby #1..4"). `Room.cpp:203-216`

Every category id 1-4 and room id 1-4 exists with a non-null view/category before any client
packet. This is why C++ `selectCategoryUpdates` can emit full `RoomCategoryData` immediately and
why `joinRoom` with `RMID` 1-4 resolves an existing room without on-demand creation.

**C# has no equivalent seeding.** No `RoomManager`, no pre-seeded views/categories/rooms.
Consequence: even if `joinRoom` were added to `HandlePacket`, category/room lookup would fail
and the error path would fire. Pre-seeding must land alongside the handler.

ID scheme `[V]` (`Room.cpp:224-243`): when called with `id==0`, new id = (last id in map) +1,
or 1 if empty. Same for `CreateRoomCategory`, `CreateRoomView`.

---

## TDF field tables

All struct layouts verified from actual `WriteTo`/`Write` bodies in `SporeNet/Room.cpp` and
`Blaze/Functions.cpp`. Field emit order = exactly source order. Values shown are concrete
defaults the C++ server emits.

### RoomReplicationContext (selectViewUpdates reply) `[V]` — `RoomsComponent.cpp:419-425`

| Tag | TDF type | Value |
|---|---|---|
| `SEID` | integer | `1` |
| `UPRE` | enum/integer | `RoomViewUpdate::UserRoomCreated` |
| `USID` | integer | `user->get_id()` |
| `VWID` | integer | `roomViewId` (1 when UPDT≠0, else 0) |

### selectCategoryUpdates reply `[V]` — `RoomsComponent.cpp:361-363`

| Tag | TDF type | Value |
|---|---|---|
| `VWID` | integer | echoed from request |

### RoomViewData (VDAT / NotifyRoomViewAdded/Updated body) `[V]` — `Room.cpp:67-82`

| # | Tag | TDF type | Value emitted |
|---|---|---|---|
| 1 | `DISP` | string | `"hello"` (literal placeholder) |
| 2 | `GMET` | map&lt;str,str&gt; | empty map |
| 3 | `META` | map&lt;str,str&gt; | empty map |
| 4 | `MXRM` | integer | `1` |
| 5 | `NAME` | string | `mName` (default: `"Lobby View #<id>"`) |
| 6 | `USRM` | integer | `0` |
| 7 | `VWID` | integer | `mId` (the view's id) |

**C# `RoomViewNotification` body:** same 7 tags, but emits `DISP=""`, `NAME` from constructor
arg (hardcoded `"default"`), `VWID=0`. `DISP` diverges (`""` vs `"hello"`), `NAME` and `VWID`
diverge from real manager values. `RoomsComponent.cs:133-155`

### RoomCategoryData (CDAT / NotifyRoomCategoryAdded/Updated body) `[V]` — `Room.cpp:91-124`

| # | Tag | TDF type | Value emitted |
|---|---|---|---|
| 1 | `CAPA` | integer | `10` |
| 2 | `CMET` | map&lt;str,str&gt; | empty map |
| 3 | `CRIT` | map&lt;str,str&gt; | empty map |
| 4 | `CTID` | integer | `mId` (category id) |
| 5 | `DESC` | string | `mDescription` (default `""`) |
| 6 | `DISP` | string | `""` |
| 7 | `DISR` | string | `""` |
| 8 | `EMAX` | integer | `10` |
| 9 | `EPCT` | integer | `0` |
| 10 | `FLAG` | integer | `0` |
| 11 | `GMET` | map&lt;str,str&gt; | empty map |
| 12 | `LOCL` | string | `""` |
| 13 | `NAME` | string | `mName` (default `"Lobby Category #<id>"`) |
| 14 | `NEXP` | integer | `1` |
| 15 | `PASS` | string | `mPassword` (default `""`) |
| 16 | `UCRT` | integer | `1` |
| 17 | `VWID` | integer | `viewId` (owning view id, or 0 if no view) |

Note `[V]`: `CMET`/`CRIT`/`GMET` are **empty maps** (push_map/pop), not strings despite header
byte-type comment 0x54. `EPCT` and `NEXP` use `put_integer`, not float.

**C# `RoomCategoryNotification` body:** only `CTID`, `NAME`, `VWID` (3 of 17 fields). Missing
14 fields: CAPA, CMET, CRIT, DESC, DISP, DISR, EMAX, EPCT, FLAG, GMET, LOCL, NEXP, PASS, UCRT.
Ids sent are 0-based (0,1,2,3) vs correct 1-based (1,2,3,4). `RoomsComponent.cs:157-168`

### RoomData (RDAT / NotifyRoomAdded/Updated body) `[V]` — `Room.cpp:150-200`

`population` is computed inline by iterating `mUsers`, pruning expired weak-ptrs; each live
user increments `population` and writes its id into `BLST`. At joinRoom time, the joining user
has already been added via `room->AddUser(user)` (`RoomsComponent.cpp:533`), so `BLST` contains
the member id and `POPU` = 1 on a freshly-joined single-user room.

| # | Tag | TDF type | Value emitted |
|---|---|---|---|
| 1 | `AREM` | integer | `1` (auto-remove) |
| 2 | `ATTR` | map&lt;str,str&gt; | empty map `[V]` (`Room.cpp:166`) |
| 3 | `BLST` | list&lt;int&gt; | **member ids** (iterates `mUsers`; empty if no members) |
| 4 | `CAP`  | integer | `mCapacity` (default `1`) |
| 5 | `CNAM` | string | category name (default `"Lobby Category #<id>"`) |
| 6 | `CRET` | integer | `0` |
| 7 | `CRIT` | map&lt;str,str&gt; | empty map |
| 8 | `CRTM` | integer | `0` |
| 9 | `CTID` | integer | category id |
| 10 | `ENUM` | integer | `1` |
| 11 | `HNAM` | string | `"Lobby"` |
| 12 | `HOST` | integer | `1` |
| 13 | `NAME` | string | `mName` (default `"Lobby #<id>"`) |
| 14 | `POPU` | integer | live member count (same iteration as BLST) |
| 15 | `PSWD` | string | `mPassword` (default `""`) |
| 16 | `PVAL` | string | `""` |
| 17 | `RMID` | integer | `mId` (room id) |
| 18 | `UCRT` | integer | `1` |

Note `[V]`: `ATTR` **IS** emitted (as an empty map) despite earlier doc note claiming otherwise.
`Room.cpp:166-167`. `BLST` doubles as the member-id list and the `population` accumulator — it
is NOT a separate banned-user list (that is the header comment label, but the body code writes
live user ids). `CNAM` fallback is `"Unknown category"` if `mCategory` is null.

### joinRoom reply — `WriteJoinRoom` `[V]` — `RoomsComponent.cpp:365-399`

| Tag | TDF type | Contents |
|---|---|---|
| `CRIT` | string | `""` |
| `VERS` | integer | `1` |
| `CDAT` | struct | `RoomCategoryData` — `category->WriteTo(packet)` (17 fields above) |
| `RDAT` | struct | `RoomData` — `room->WriteTo(packet)` (18 fields above) |
| `VDAT` | struct | `RoomViewData` — `view->WriteTo(packet)` (7 fields above) |
| `MDAT` | struct | `RoomMemberData` — `memberData.Write(packet)` (2 fields below) |

Emit order: `CRIT`, `VERS`, then the four nested structs in order `CDAT`→`RDAT`→`VDAT`→`MDAT`.
`RoomsComponent.cpp:378-399`

### RoomMemberData (MDAT) `[V]` — `Functions.cpp:771-774`

| # | Tag | TDF type | Value |
|---|---|---|---|
| 1 | `BZID` | integer | `memberId` = `user->get_id()` |
| 2 | `RMID` | integer | `roomId` = `room->GetId()` |

Struct definition: `{ int64_t memberId; uint32_t roomId; }` (`Functions.h:440-443`).
`WriteJoinRoom` default-constructs it, sets both fields, calls `Write`. `RoomsComponent.cpp:371-374`

### NotifyRoomMemberJoined body `[V]` — `RoomsComponent.cpp:308-313`

| Tag | TDF type | Value |
|---|---|---|
| `BZID` | integer | `memberId` = `userId` |
| `RMID` | integer | `roomId` |

### NotifyRoomViewRemoved body `[V]` — `RoomsComponent.cpp:233-237`

| Tag | TDF type | Value |
|---|---|---|
| `VWID` | integer | `viewId` |

### NotifyRoomCategoryRemoved body `[V]` — `RoomsComponent.cpp:260-264`

| Tag | TDF type | Value |
|---|---|---|
| `CTID` | integer | `categoryId` |

### NotifyRoomRemoved body `[V]` — `RoomsComponent.cpp:287-291`

| Tag | TDF type | Value |
|---|---|---|
| `RMID` | integer | `roomId` |

### NotifyRoomPopulationUpdated body `[V]` — `RoomsComponent.cpp:294-306`

| Tag | TDF type | Value |
|---|---|---|
| `POPA` | map&lt;str,str&gt; | empty (admin population?) |
| `POPM` | map&lt;str,str&gt; | empty (member population?) |

### NotifyRoomMemberLeft / NotifyRoomKick / NotifyRoomHostTransfer body `[V]`

All three use `MBID` (member id) + `RMID` (room id). `RoomsComponent.cpp:316-345`

### NotifyRoomMemberUpdated body `[V]` — `RoomsComponent.cpp:324-330`

| Tag | TDF type | Value |
|---|---|---|
| `BZID` | integer | memberId |
| `RMID` | integer | roomId |

### NotifyRoomAttributesSet body `[V]` — `RoomsComponent.cpp:348-358`

| Tag | TDF type | Value |
|---|---|---|
| `ATTR` | map&lt;str,str&gt; | empty (TODO in C++) |
| `RMID` | integer | roomId |

---

## Crash relevance — joinRoom verdict

`[V]` **Confirmed dropped. Strong crash suspect.**

- In a working C++ run, `joinRoom` is fully handled: NotifyRoomAdded → NotifyRoomUpdated →
  [reply with CDAT/RDAT/VDAT/MDAT] → NotifyRoomMemberJoined. `output.txt:368-372`
- In C#, `joinRoom` falls through to `default`, logs "Unknown command", returns false. Client
  receives **no reply** to a blocking request command.
- The client builds its local room/membership record from `RDAT`/`MDAT` in the reply and from
  `NotifyRoomMemberJoined`. Without them, the room object remains null/partial.
- Ordering evidence: `joinRoom` is the last Rooms traffic before `GameManager::resetDedicatedServer`.
  Client almost certainly dereferences the room record during or after that transition.

`[?]` Exact fault address (which client struct deref faults) not proven from server source alone.
Needs Ghidra: `Blaze::Rooms::JoinRoomRequest` / `RoomData` layout and the `OnGms*` / lobby
state machine to confirm which field triggers the null deref.

---

## How to implement joinRoom in C# (faithful)

Add to `HandlePacket` switch: `case 0x14: return HandleJoinRoom(client, packet);`.

Faithful sequence mirroring `RoomsComponent.cpp:476-548`:
1. Parse TDF: `PASS`(str), `PVAL`(str), `INID`(u64), `INVT`(u32 bool), `CTID`(u32), `RMID`(u32).
2. Resolve/create room: `RMID==0` → create (newRoom flag); else look up; missing → error
   `ROOMS_ERR_NOT_FOUND`.
3. Resolve/create category: `CTID==0` → create; else look up; missing → error
   `ROOMS_ERR_CREATE_UNKNOWN_CATEGORY`.
4. Ensure category has a view (create if null).
5. `room.Category = category; room.AddUser(user)`.
6. If newRoom: `Notify(RoomData, 0x15, 0x1F)` (NotifyRoomAdded) then
   `Notify(RoomData, 0x15, 0x1E)` (NotifyRoomUpdated). Both carry full `RoomData` struct.
7. Reply with: `CRIT=""`, `VERS=1`, `CDAT`=RoomCategoryData(17 fields), `RDAT`=RoomData(18
   fields), `VDAT`=RoomViewData(7 fields), `MDAT`=RoomMemberData(BZID=userId, RMID=roomId).
8. After reply: `Notify(0x15, 0x32)` NotifyRoomMemberJoined `BZID`=userId, `RMID`=roomId.

**Also required:** pre-seed a `RoomManager` equivalent at server startup:
1. Create RoomView id=1 (name "Lobby View #1").
2. Create categories 1-4 (names "Lobby Category #1..4"), each `SetView(view 1)`.
3. Create rooms 1-4 (names "Lobby #1..4"), each `SetCategory(category i+1)`.

This mirrors `RoomManager()` ctor `Room.cpp:203-216`. Without pre-seeding, category/room lookup
on `joinRoom` returns null and the error reply fires even with a correct handler.

Also fix `selectCategoryUpdates` to use category ids 1-4 (not 0-3) and emit full
`RoomCategoryData` (17 fields) instead of the truncated 3-field `RoomCategoryNotification`.

Also fix `selectViewUpdates` to reply with SEID/UPRE/USID/VWID first, then send notifications
carrying the real `RoomViewData` (7 fields, VWID=1, DISP="hello") rather than a stub.

---

## Divergences (C++ vs C#)

1. `[V]` **joinRoom (0x14) DROPPED in C#.** C# `HandlePacket` dispatches only 0x0A and 0x0B.
   `joinRoom` gets no handler; client receives no reply. **Top crash suspect.** `RoomsComponent.cs:11-23`

2. `[V]` **No RoomManager / no pre-seeding in C#.** C++ `RoomManager()` ctor pre-creates view 1,
   categories 1-4, rooms 1-4 at startup. C# has no equivalent. `joinRoom` lookup would fail
   even if a handler were added. `Room.cpp:203-216`

3. `[V]` **selectViewUpdates body.** C++ replies with `RoomReplicationContext` (SEID/UPRE/USID/
   VWID=1), then fires notifications that serialize real `RoomViewData` (7 fields, DISP="hello",
   VWID=1). C# sends two notifications with stub `RoomViewNotification` (DISP="", NAME="default",
   VWID=0) then calls `RespondTo` with no body. Order is also inverted (notifications before reply
   in C#, reply before notifications in C++). `RoomsComponent.cpp:402-438` vs `cs:26-48`

4. `[V]` **selectCategoryUpdates category ids off-by-one.** C++ loops `categoryId = i+1` → ids
   1,2,3,4. C# loops `CategoryId = i` → ids 0,1,2,3. `RoomsComponent.cpp:449-450` vs `cs:62-63`

5. `[V]` **selectCategoryUpdates body truncated.** C++ serializes full 17-field `RoomCategoryData`
   per category. C# sends only 3 fields (CTID/NAME/VWID) in `RoomCategoryNotification`.
   `Room.cpp:91-124` vs `cs:157-168`

6. `[V]` **selectCategoryUpdates does not gate on VWID==0.** C++ skips when `viewId==0`. C#
   always sends all 4 categories regardless. `RoomsComponent.cpp:444-447` vs `cs:51-80`

7. `[V]` **13 of 16 notifications never sent from C#** (only RoomView Added/Updated +
   RoomCategory Added/Updated are emitted, with truncated bodies). No category/room/member-data
   structs reach the client from the working 4.

8. `[V]` **selectPseudoRoomUpdates (0xA0) not dispatched in C#.** C++ replies empty.
   `RoomsComponent.cpp:551-554`

9. `[V]` **ATTR field in RoomData.** Existing doc note incorrectly stated ATTR is not emitted;
   it IS emitted as an empty map. `Room.cpp:166-167`

10. `[V]` **BLST semantics.** Header comment labels `BLST` as "banned users list", but
    `Room::WriteTo` iterates `mUsers` into `BLST` (live member ids) and derives `population`
    from the same loop. On join, BLST contains the joining user's id; it is not a ban list.
    `Room.cpp:169-180`

---

## Open questions / Ghidra TODO

- `[?]` **Client-side joinRoom fault proof:** read `Blaze::Rooms::JoinRoomRequest`, `RoomData`,
  `RoomCategoryData`, `RoomViewData` class layouts in `Darkspore.exe`. Confirm which reply tag
  the client dereferences null on when joinRoom is unanswered. RoomComponent vtable candidate
  ~0x00e45e60, message table ~0x010e996c. Ghidra instance was not reachable this pass.
- `[?]` **resetDedicatedServer dependency:** confirm GameManager `resetDedicatedServer` assumes
  prior room membership (sequence in `output.txt` around the joinRoom block).
- `[?]` **RoomViewUpdate enum values:** exact integer value of `UserRoomCreated` not traced.
  Verify against `Blaze/Component/RoomsComponent.h` enum or Ghidra.
- `[?]` **TDF type-byte precision:** header comment block (0x1C/0x28/0x3C/0x40/0x50/0x54/0x58)
  is documentary only; actual `WriteTo` uses `put_integer`/`put_string`/`push_map`/`push_list`.
  Some comment annotations ("float"/"bool") disagree with the wire type. Verify precise
  `TDF::Type` enum values from `Blaze/TDF.cpp` if exact wire type bytes are needed.
- `[?]` **Category semantic meaning:** what ids 1-4 represent for Darkspore's lobby (matchmaking
  buckets?) and whether the client requires specific NAME/CAPA/FLAG metadata per id.
- `[?]` **BLST on joinRoom reply:** client sees member id(s) in BLST — confirm it reads this as
  the room's current member list and whether a stale/empty BLST breaks room state display.
