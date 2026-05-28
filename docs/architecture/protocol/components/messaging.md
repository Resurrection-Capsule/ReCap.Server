# Messaging Component — 0x0F

Handles in-game chat messages between clients, with a debug command-dispatch hook in the C++ implementation, on the Blaze lobby connection (port 42125).

---

## Request/response commands

| Command name | Cmd ID | Direction | C++ handler | C# handler | Status |
|---|---|---|---|---|---|
| sendMessage | 0x01 | C→S | `Blaze/Component/MessagingComponent.cpp:227` | — | ❌ |
| fetchMessages | 0x02 | C→S | `Blaze/Component/MessagingComponent.cpp:232` | `Adapters/Blaze/Component/MessagingComponent.cs:33` | ⚠️ |
| purgeMessages | 0x03 | C→S | `Blaze/Component/MessagingComponent.cpp:237` | `Adapters/Blaze/Component/MessagingComponent.cs:48` | ⚠️ |
| touchMessages | 0x04 | C→S | `Blaze/Component/MessagingComponent.cpp:241` | — | ❌ |
| getMessages | 0x05 | C→S | `Blaze/Component/MessagingComponent.cpp:244` | — | ❌ |

## Notifications sent (S→C)

| Notification name | Notify ID | Direction | C++ sender | C# sender | Status |
|---|---|---|---|---|---|
| NotifyMessage | 0x01 | S→C | `Blaze/Component/MessagingComponent.cpp:135` | `MessagingComponent.cs:27` (stub) | ⚠️ |

---

## Key TDF fields — sendMessage (0x01)

**Request (ClientMessage):**

| Tag | Type | Description |
|---|---|---|
| `ATTR` | map&lt;u32,string&gt; | Message attributes (key 0xFF02 = message text) |
| `TARG` | struct/ObjectId | Target (component ID + local ID identifying game or user) |
| `TYPE` | u32 | Message type (0=Tell, 7=Party, 8=Game, 9=Lobby, 14=System, 16=SporeNetBroadcast) |

**Response:**

| Tag | Type | Description |
|---|---|---|
| `MGID` | u32 | Message group ID |
| `MIDS` | list&lt;u32&gt; | Message IDs assigned |

**NotifyMessage (0x01) body (ServerMessage):**

| Tag | Type | Description |
|---|---|---|
| `FLAG` | u32 | Server flags |
| `MGID` | u32 | Message ID |
| `NAME` | string | Sender name |
| `PYLD` | struct | Original ClientMessage payload |
| `TIME` | u32 | Unix timestamp |

---

## Porting gaps

- `sendMessage` (0x01) is the primary command; C++ implements full routing (game messages → `GameManager` target, user messages → `UserSession` target) including a debug `!packet`/`!lua` command parser; C# has no equivalent.
- `fetchMessages` and `purgeMessages` in C++ are stub-logged (`std::cout`) with no reply; C# versions send empty responses, which is slightly more correct.
- `touchMessages` (0x04) and `getMessages` (0x05) are unimplemented in both C++ and C#.
- `NotifyMessage` in C# is a bare stub that sends an empty `ServerMessage`; it is not wired to any inbound `sendMessage` dispatch.
