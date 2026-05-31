# AssociationLists Component — 0x19

Manages friend and ignore lists for the client's social graph on the Blaze lobby connection
(port 42125). Called on the login path (client requests lists after auth); also handles
add/remove mutations that fire `NotifyUpdateListMembership` to the requesting session.
Not on the critical crash path but present in every normal lobby flow.

Audit pass 2026-05-29: verified against C++ `Blaze/Component/AssociationComponent.cpp`,
`Blaze/Component/AssociationComponent.h`, `Blaze/Functions.cpp`, `Blaze/Functions.h`,
`Blaze/Types.h`, and C# port `Adapters/Blaze/Component/AssociationListsComponent.cs`.
Tags: `[V]` = verified in cited source, `[?]` = unverified / needs Ghidra. Ghidra was
**not reachable** this pass (no instance).

---

## Request/response commands

| Command | Cmd ID | C++ handler (file:line) | C# handler (file:line) | Status |
|---|---|---|---|---|
| addUsersToList | 0x01 | `AssociationComponent.cpp:217` | — | ❌ absent in C# |
| removeUsersFromList | 0x02 | `AssociationComponent.cpp:247` | — | ❌ absent in C# |
| clearLists | 0x03 | — (enum only, not dispatched) | — | ❌ |
| setUsersToList | 0x04 | — (enum only, not dispatched) | — | ❌ |
| getListForUser | 0x05 | — (enum only, not dispatched) | — | ❌ |
| getLists | 0x06 | `AssociationComponent.cpp:277` | `AssociationListsComponent.cs:23` | ✅ (hardcoded) |
| subscribeToLists | 0x07 | — (enum only, not dispatched) | — | ❌ |
| unsubscribeFromLists | 0x08 | — (enum only, not dispatched) | — | ❌ |
| getConfigListsInfo | 0x09 | — (absent from C++ enum) | name-map only `cs:93` | ⚠️ C# name-map only |

C++ `ParsePacket` dispatches only `addUsersToList`(0x01), `removeUsersFromList`(0x02),
`getLists`(0x06); rest return `false` (unhandled). `[V]` `AssociationComponent.cpp:134-152`

C# dispatches only `getLists`(0x06); all others fall to `default` (returns false). `[V]` `cs:13-20`

---

## Notifications (server → client)

| Notification | ID | C++ sender (file:line) | C# sender | Status |
|---|---|---|---|---|
| NotifyUpdateListMembership | 0x19/1 | `AssociationComponent.cpp:191` (called from add/remove) | — | ❌ never sent from C# |

---

## What each handler does (internals)

### getLists (0x06) `[V]`
- **C++** (`AssociationComponent.cpp:277-326`):
  - Reads `MXRC`, `OFRC`, iterates `ALST` list of `ListInfo`.
  - For each requested list, mirrors the request `ListInfo` back into the reply entry, sets `ofrc` from request, `toct=0`.
  - type==4 (Ignore): adds one hardcoded member — `id=user->get_id()`, `name=user->get_name()`.
  - type==5 (Friend): adds two hardcoded members — `{id=100, name="Dalkon"}`, `{id=102, name="test"}`.
  - Replies `LMAP` list via `WriteLists`. No persistence layer.
  - **Commented-out follow-up calls:** `PlaygroupsComponent::NotifyJoinPlaygroup` + `NotifyUpdateListMembership` — both disabled. `cpp:323-325`
- **C#** (`AssociationListsComponent.cs:23-82`):
  - Reads `GetListsRequest` (ALST/MXRC/OFRC). On null request replies error `0x5E0001`.
  - type==4: one hardcoded member `{ID=101, Name="Ignoredude"}`.
  - type==5: two hardcoded members `{ID=100, Name="Dalkon"}`, `{ID=102, Name="test"}`.
  - Replies `GetListsResponse` containing `LMAP` + spurious `GRP/LVL/STAT/XTRA` fields (see divergence #2).
  - Also has commented-out follow-up calls (identical to C++). `cs:80-81`
  - **Ignore list member differs:** C++ uses the live user's id/name; C# hardcodes `ID=101 / "Ignoredude"`.

### addUsersToList (0x01) — C++ only `[V]` `AssociationComponent.cpp:217-244`
- Reads `BIDL` list of `ListMemberInfoUpdate`; reads `LID`.
- For each entry: constructs `ListMemberInfo` (id from `BIDL`, `time=now`), also builds `ListMemberInfoUpdate{info, type=Add(1)}`.
- Reply: `UpdateListMembersResponse` → `LMID` list + empty `REM` list (add path: `REM` skipped).
- Fires `NotifyUpdateListMembership` with `BIDL` (updates) + `LID` struct. `[V]`

### removeUsersFromList (0x02) — C++ only `[V]` `AssociationComponent.cpp:247-274`
- Reads `BIDL` list; reads `LID`.
- For each entry: constructs `ListMemberId` (for `REM`), builds `ListMemberInfoUpdate{info, type=Remove(2)}`.
- Reply: `UpdateListMembersResponse` → empty `LMID` list + `REM` list.
- Fires `NotifyUpdateListMembership`. `[V]`

### NotifyUpdateListMembership (0x19/1) — C++ implementation `[V]` `AssociationComponent.cpp:191-214`
- Early-returns if `listMemberInfoUpdate` is empty or user is null.
- Writes `BIDL` list (each element: `LMID` struct + `LUPT` int) + `LID` struct.
- Sends to requesting client via `request.notify(packet, Id=0x19, NotifyUpdateListMembership=0x01)`.

---

## Key TDF field tables

### getLists request `[V]` `AssociationComponent.cpp:79-84`
| Tag | Type | C++ reads | C# reads |
|---|---|---|---|
| ALST | list&lt;struct&gt; | list of `ListInfo` structs | `TdfStructVector<BlazeList>` |
| MXRC | u32 | `request["MXRC"].GetUint()` | `GetListsRequest.MXRC` |
| OFRC | u32 | `request["OFRC"].GetUint()` | `GetListsRequest.OFRC` |

### ListInfo (request entry / INFO echo) — `ListInfo::Write` `[V]` `Functions.cpp:338-348`
| Tag | Type | C++ | C# (`BlazeList`) |
|---|---|---|---|
| BOID | object_id | blazeObjectId | `BlazeObjectId` |
| FLGS | u32 | flags | `Flags` |
| LID  | struct | `ListIdentification` (LNM+TYPE) | `ListIdentification` (LNM+TYPE) |
| LMS  | u32 | lms | `LMS` (ulong) ⚠️ width |
| PRID | u32 | prid | `PRID` (ulong) ⚠️ width |

### getLists response — `WriteLists` / `ListMembers::Write` `[V]` `Functions.cpp:177-203,387-403`
| Tag | Type | C++ | C# (`GetListsResponse` / `GetListsResponseList`) |
|---|---|---|---|
| LMAP | list&lt;struct&gt; | list of `ListMembers` | `TdfStructVector<GetListsResponseList>` |
| INFO | struct | `ListInfo` (echo from request) | `BlazeList` Info |
| MEML | list&lt;struct&gt; | list of `ListMemberInfo` | `TdfStructVector<GetListsResponseListMember>` |
| OFRC | u32 | `ofrc` (echoed from request) | `OFRC` |
| TOCT | u32 | `toct` (=0) | `TOCT` (=0) |

> C# `GetListsResponse` adds top-level fields `GRP=0xFF`, `LVL=0xCC`, `STAT=0x02`, `XTRA=0xAA`
> not present in C++ `WriteLists`. These match the `PresenceInfo` tags documented in the C++ comment
> block (`AssociationComponent.cpp:69-73`) but are never written by any C++ handler. C++ only
> documents them as a struct description. `[V]` `cs:157-167` vs `cpp:277-326`

### ListMemberId (LMID inner struct) — `ListMemberId::Write` `[V]` `Functions.cpp:362-367`
| Tag | Type | C++ | C# (`GetListsResponseListMemberId`) |
|---|---|---|---|
| BLID | i64 | id (int64) | `ID` (uint) ⚠️ width |
| PNAM | string | name | `Name` |
| XREF | u64 | externalReference | `ExternalReference` (uint) ⚠️ width |
| XTYP | enum | externalReferenceType | `ExternalReferenceType` (uint) |

### ListMemberInfo (MEML entry) — `ListMemberInfo::Write` `[V]` `Functions.cpp:370-376`
| Tag | Type | C++ | C# (`GetListsResponseListMember`) |
|---|---|---|---|
| LMID | struct | `ListMemberId::Write` (BLID+PNAM+XREF+XTYP) | `GetListsResponseListMemberId` |
| TIME | i64 | time | `TIME` (long) |

### addUsersToList / removeUsersFromList reply — `UpdateListMembersResponse` `[V]` `AssociationComponent.cpp:155-175`
| Tag | Type | Written when |
|---|---|---|
| LMID | list&lt;struct&gt; | non-empty `memberInfoList` (add path); each entry = `ListMemberInfo::Write` (LMID+TIME) |
| REM  | list&lt;struct&gt; | non-empty `removeList` (remove path); each entry = `ListMemberId::Write` (BLID+PNAM+XREF+XTYP) |

### NotifyUpdateListMembership body `[V]` `AssociationComponent.cpp:201-214`
| Tag | Type | Contents |
|---|---|---|
| BIDL | list&lt;struct&gt; | Each entry: `LMID` struct (`ListMemberInfo::Write`) + `LUPT` int (`ListUpdateType`: Add=1, Remove=2) `[V]` `Types.h:200-202`, `Functions.cpp:379-385` |
| LID  | struct | `ListIdentification::Write` (LNM + TYPE) |

---

## Divergences (C++ vs C#)

1. **addUsersToList / removeUsersFromList absent.** C++ dispatches both (`:217`, `:247`);
   C# drops all commands except 0x06 (`cs:13-20`). Clients cannot mutate friend/ignore lists
   in C#. `[V]`
2. **Spurious PresenceInfo fields in getLists response.** C# `GetListsResponse` emits `GRP`,
   `LVL`, `STAT`, `XTRA` at the top level (`cs:157-167`). C++ `WriteLists` writes only `LMAP`
   (`cpp:182-188`). These fields appear in the C++ comment block as `PresenceInfo` struct tags
   but are never written by any association handler. `[V]`
3. **Ignore list member differs.** C++ uses the authenticated user's live `id`/`name`
   (`cpp:298-300`); C# hardcodes `ID=101 / "Ignoredude"` (`cs:47-53`). Any client logic
   reading the ignore-list owner identity gets a wrong result. `[V]`
4. **NotifyUpdateListMembership never sent.** C++ fires it after every add/remove
   (`cpp:244,274`); C# has no notification path for association changes. `[V]`
5. **BLID / XREF integer width.** C++ `ListMemberId` uses `int64_t id` / `uint64_t externalReference`
   (`Functions.h:210-215`); C# uses `uint ID` / `uint ExternalReference` (`cs:198,202`).
   BLID truncated to 32-bit on wire from C#. `[V]`
6. **LMS / PRID width in ListInfo.** C++ `ListInfo::lms/prid` are `uint32_t`
   (`Functions.h:202-205`); C# maps both to `ulong` (`cs:146,149`). `[V]`
7. **Hardcoded data — no persistence.** Both C++ and C# use hardcoded friend entries (same
   Dalkon/test pair). Neither reads from a real social graph. C++ adds the ignore-list member
   dynamically from session; C# does not. `[V]`

---

## Open questions / Ghidra TODO

- `[?]` Does the client read the spurious `GRP`/`LVL`/`STAT`/`XTRA` fields in the getLists
  response, or are they silently ignored? Confirm via `Blaze::AssociationLists::GetListsResponse`
  class layout in Ghidra (no instance this pass).
- `[?]` Does the single-player login flow ever call `addUsersToList`/`removeUsersFromList`? If not,
  the missing handlers are not a crash cause but a completeness gap.
- `[?]` Is `NotifyUpdateListMembership` sent proactively at login in any deployed build, or only
  reactively after add/remove? The commented-out call in `GetLists` (`cpp:323-324`) hints it
  was intended but disabled.
- `[?]` Confirm `BLID` is truly i64 on the wire (C++ writes `put_integer` with int64 source);
  if so C# uint truncation would corrupt any user id > 0xFFFFFFFF.
- `[?]` `getConfigListsInfo` (0x09) appears in C# name-map only (`cs:93`) — not in any C++
  enum or handler. Verify whether Darkspore client ever sends it.
