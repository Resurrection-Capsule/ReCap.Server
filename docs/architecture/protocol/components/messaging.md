# Messaging Component — 0x0F

Handles in-game chat and system messages between clients, plus a debug command-dispatch hook
in the C++ implementation. Lives on the Blaze lobby connection (port 42125). Component ID
`0x0F` (`MessagingComponent::Id` `[V]` `MessagingComponent.h:14`).

Solo-path relevance: client issues `fetchMessages` once during login (`[V]` working C++ log
`Server/output.txt`; also `auth.md` verified flow step 8). All other commands are not seen on
the solo login→game path. C++ stubs (`cout` only) for fetch/purge/touch/get; C# fills in typed
ACK replies for fetch and purge.

Audit pass 2026-05-29: verified against C++
`Blaze/Component/MessagingComponent.cpp` + `MessagingComponent.h`,
`Blaze/Functions.cpp` (TDF struct writers), `Blaze/Client.cpp` (dispatch / auto-reply
question), `Blaze/Component.cpp` (component registry), and C# port
`Adapters/Blaze/Component/MessagingComponent.cs`. Tags: `[V]` = verified in cited source,
`[?]` = unverified / needs Ghidra. Ghidra **not reachable** this pass.

---

## Request/response commands

| Command | Cmd ID | C++ handler (file:line) | C# handler (file:line) | Status |
|---|---|---|---|---|
| sendMessage | 0x01 | `MessagingComponent.cpp:227-229` → `OnSendMessageResponse` `:110` | — (no case) | ⚠️ C++ full, C# absent |
| fetchMessages | 0x02 | `MessagingComponent.cpp:232-234` (cout only, **no reply**) | `MessagingComponent.cs:33` | ⚠️ C# more correct |
| purgeMessages | 0x03 | `MessagingComponent.cpp:236-238` (cout only, **no reply**) | `MessagingComponent.cs:48` | ⚠️ C# more correct |
| touchMessages | 0x04 | `MessagingComponent.cpp:240-242` (cout only, **no reply**) | — (no case) | ❌ both stub / absent |
| getMessages | 0x05 | `MessagingComponent.cpp:244-246` (cout only, **no reply**) | — (no case) | ❌ both stub / absent |
| sendGlobalMessage | 0x07 | — (not in C++ enum or dispatch) | name in `GetCommandName` only, no case | ❌ absent both |

C++ `ParsePacket` dispatches all five (0x01–0x05) and returns `false` for unknown commands.
`[V]` `MessagingComponent.cpp:81-108`

C++ enum: `SendMessage=0x01, FetchMessages=0x02, PurgeMessages=0x03, TouchMessages=0x04,
GetMessages=0x05`; notification `NotifyMessage=0x01`. `[V]` `MessagingComponent.cpp:39-47`

**No auto-reply in C++ dispatcher.** `Client::ParsePacket` calls `component->ParsePacket(request)`;
handlers that do not call `request.reply()` send **nothing at all** (confirmed `Client.cpp:213-222`
— no fallback empty-reply path). `OnFetchMessages`/`OnPurgeMessages`/`OnTouchMessages`/
`OnGetMessages` all only `cout`-log, never call `request.reply()`. `[V]`

---

## What each handler does (internals)

### fetchMessages (0x02) — solo-critical `[V]`
- **C++** `MessagingComponent.cpp:232-234`: `OnFetchMessages` only does
  `std::cout << "OnFetchMessages" << std::endl`. No `request.reply()` call. Client receives
  **no reply packet** from the C++ server on this path.
- **C#** `MessagingComponent.cs:33-46`: decodes `FetchMessageRequest`; on decode failure replies
  error `0x9000F`; on success replies `FetchMessageResponse { MCNT = 0 }` (well-formed empty
  count). **C# is more correct** — guarantees a typed reply; the C++ implementation sends
  nothing and relies on the client tolerating a missing reply for this command. `[V]`

### purgeMessages (0x03) `[V]`
- **C++** `MessagingComponent.cpp:236-238`: `OnPurgeMessages` only logs; no reply.
- **C#** `MessagingComponent.cs:48-61`: decodes `PurgeMessageRequest`; on decode failure replies
  `0x9000F`; on success replies `PurgeMessageResponse { MCNT = 0 }`. Same pattern as
  fetchMessages — C# is more correct. `[V]`

### sendMessage (0x01) — not ported `[V]`
- **C++** `MessagingComponent.cpp:227-229`: `OnSendMessage` logs then immediately calls
  `OnSendMessageResponse` (`:110-133`):
  - Reads `ClientMessage` from request (`Functions.cpp:406-429`).
  - Builds reply `{ MGID=first_attr_id, MIDS=[attr_ids] }`, calls `request.reply(packet)` (`:129`).
  - Calls `NotifyMessage(request, message)` (`:132` → `:135-225`).
- `NotifyMessage` `:135-225` builds a `ServerMessage`, resolves sender name via target component
  id (GameManager=`0x64` or UserSessions=`0x7802`), checks for `!`-prefixed debug commands
  (attribute `0xFF02`) and — if name is non-empty — writes the `ServerMessage` and calls
  `request.notify(packet, Id, NotifyMessage=0x01)` (`:223`). `[V]`
- **C#**: no `case 0x1` in `HandlePacket`. `NotifyMessage(Client)` exists at
  `MessagingComponent.cs:26-31` but constructs `new ServerMessage()` whose `Encode` throws
  `NotImplementedException` (`:168-175`). Would crash if called; nothing calls it currently.
  Debug-command hook not ported. Not gameplay-critical for solo path. `[V]`

### touchMessages (0x04) / getMessages (0x05) `[V]`
- **C++** `MessagingComponent.cpp:240-242`, `:244-246`: log-only, no reply.
- **C#**: no case for either command. `HandlePacket` returns `false` → caller logs "Unknown command".
  Neither command appears on the solo login path. `[V]`

---

## Key TDF field tables

### FetchMessageRequest (client → server) `[V]` `MessagingComponent.cs:96-130`

| Tag | Type | C# field | Notes |
|---|---|---|---|
| FLAG | u32 | `Flags` | filter flags |
| MGID | u64 | `MessageId` | specific message id to fetch (0 = all) |
| PIDX | u32 | `PageIndex` | pagination index |
| PSIZ | u32 | `PageSize` | page size |
| SMSK | u32 | `StatusMask` | status bitmask filter |
| SORT | enum | `OrderBy` (MessageOrder) | `Default=0, TimeAsc=1, TimeDesc=2` |
| SRCE | BlazeObjectId | `Source` | source filter |
| STAT | u32 | `Status` | status filter |
| TARG | BlazeObjectId | `Target` | target filter |
| TYPE | u32 | `Type` | message type filter |
| TYPL | vector&lt;u32&gt; | `TypeList` | type list filter |

> C++ does not implement `OnFetchMessages` body — no C++ `Read` counterpart for this struct. `[V]`

### FetchMessageResponse (server → client) `[V]` `MessagingComponent.cs:132-136`

| Tag | Type | C++ | C# value |
|---|---|---|---|
| MCNT | u32 | — (no reply sent) | 0 |

### PurgeMessageRequest (client → server) `[V]` `MessagingComponent.cs:138-157`

| Tag | Type | C# field |
|---|---|---|
| FLAG | u32 | `Flags` |
| MGID | u64 | `MessageId` |
| SMSK | u32 | `StatusMask` |
| SRCE | BlazeObjectId | `Source` |
| STAT | u32 | `Status` |
| TYPE | u32 | `Type` |

### PurgeMessageResponse (server → client) `[V]` `MessagingComponent.cs:159-163`

| Tag | Type | C++ | C# value |
|---|---|---|---|
| MCNT | u32 | — (no reply sent) | 0 |

### sendMessage reply — `OnSendMessageResponse` `[V]` `MessagingComponent.cpp:121-129`

| Tag | Type | C++ value |
|---|---|---|
| MGID | u32 | first attribute id from ClientMessage (0 if attrs empty) |
| MIDS | list&lt;u32&gt; | all attribute ids from ClientMessage |

### ClientMessage (read from sendMessage request) `[V]` `Functions.cpp:406-442`

| Tag | Type | Notes |
|---|---|---|
| ATTR | map&lt;u32→string&gt; | message attributes; key `0xFF02` = message text |
| FLAG | u32 | flags |
| STAT | u32 | status |
| TAG | u32 | tag |
| TARG | BlazeObjectId | target: componentId + unknown + localId |
| TYPE | u32 | message type (0=Tell,7=Party,8=Game,9=Lobby,14=System,15=Warning,16=SporeNetBroadcast) |

### NotifyMessage — ServerMessage `[V]` `Functions.cpp:446-457`

| Tag | Type | C++ value | Notes |
|---|---|---|---|
| FLAG | u32 | 0 | always zero |
| MGID | u32 | 1 | always 1 |
| NAME | string | resolved sender name | empty → notification not sent |
| PYLD | struct | ClientMessage (ATTR,FLAG,STAT,TAG,TARG,TYPE) | embedded message payload |
| SRCE | BlazeObjectId | `source` (zero-initialized) | source object id |
| TIME | u32 | `utils::get_unix_time()` | server timestamp |

> Notification only fired when `serverMessage.name` is non-empty (`MessagingComponent.cpp:219`).
> C++ resolves name from GameManager player (`cpp:146-159`) or UserSession auth-token lookup
> (`cpp:209-215`). `[V]`

---

## Notifications fired

| Notification | ID | C++ sender | C# sender | Purpose |
|---|---|---|---|---|
| NotifyMessage | 0x0F/0x01 | `MessagingComponent::NotifyMessage` `cpp:135-225` (on sendMessage only) | `MessagingComponent.NotifyMessage(client)` — **throws NotImplementedException** `cs:26-31` | Push `ServerMessage` to target |

> C++ fires the notification only when name resolution succeeds (non-empty name, `cpp:219`).
> C# implementation is a latent crash — `ServerMessage.Encode` throws `NotImplementedException`
> (`cs:168-175`). Nothing in the current codebase calls `NotifyMessage(client)`. `[V]`

---

## Divergences (C++ vs C#)

1. **fetchMessages sends no reply in C++.** `OnFetchMessages` is a `cout`-only stub; the
   dispatcher (`Client.cpp:213-222`) has no fallback empty-reply path. C++ client tolerates
   missing reply for this command. C# sends `{MCNT=0}` — more correct, prevents client hang.
   `[V]` `MessagingComponent.cpp:232-234` vs `MessagingComponent.cs:44`
2. **purgeMessages sends no reply in C++.** Same stub pattern as fetchMessages. C# sends
   `{MCNT=0}`. `[V]` `MessagingComponent.cpp:236-238` vs `MessagingComponent.cs:59`
3. **sendMessage entirely absent in C#.** C++ implements full reply + `NotifyMessage` dispatch
   including `!`-command debug hook. C# has no `case 0x1`. Not solo-blocking; no chat relay
   needed. `[V]` `MessagingComponent.cpp:110-229` vs `MessagingComponent.cs:10-24`
4. **C# `ServerMessage.Encode` throws.** `NotifyMessage(Client)` in C# constructs a
   `ServerMessage` whose `Encode` is `throw new NotImplementedException()`. Latent crash if
   anything calls `MessagingComponent.NotifyMessage(client)`. Currently dead code. `[V]`
   `MessagingComponent.cs:166-175`
5. **touchMessages / getMessages absent in C#.** C++ dispatches both (stub log only); C#
   `HandlePacket` returns false for both. Neither on solo path. `[V]`
   `MessagingComponent.cpp:240-246` vs `MessagingComponent.cs:10-24`
6. **sendGlobalMessage (0x07) C#-named only.** Appears in `GetCommandName` switch as a name
   string; never dispatched in either C++ (absent from enum/dispatch) or C#. `[V]`
   `MessagingComponent.cs:71`
7. **Debug command hook not ported.** C++ `!packet`, `!lua`, `!debug` commands via chat attribute
   `0xFF02` (GameManager target path). Absent in C#. Development aid, not gameplay-critical. `[V]`
   `MessagingComponent.cpp:163-204`

---

## Open questions / Ghidra TODO

- `[?]` Confirm the client's `Blaze::Messaging::FetchMessages` reply handler tolerates
  `{MCNT=0}` with no `MSGS` list field when count is zero. C# sends this and login proceeds,
  but the client reader may expect an empty list TDF (`MSGS=[]`) to be present. (Ghidra not
  reachable this pass.)
- `[?]` Confirm `Blaze::Messaging::FetchMessages` reply handler tolerates **no reply at all**
  (C++ behavior) — does the client have a timeout path or does it block? Login proceeds with C++
  too, implying tolerance of missing reply.
- `[?]` Confirm the `NotifyMessage` struct in Ghidra: specifically whether `SRCE` is required,
  whether `MGID` must start at 1, and whether the PYLD embedded `ClientMessage` fields are all
  required when count=0 in a server-originated notify.
- `[?]` Determine whether `sendMessage` / `NotifyMessage` is needed for the Dungeon chat bar
  (visible in-game UI) — if so, C# `ServerMessage.Encode` must be implemented before Dungeon
  UI is considered complete.
- `[?]` Is `sendGlobalMessage` (0x07) a valid Blaze command the client ever sends, or is it only
  a server→client notification variant? Neither C++ enum nor dispatch includes it.
