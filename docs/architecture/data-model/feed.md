# Feed

A per-user news feed of social events: friend adds, creature unlocks, and upgrades. Used by the launcher to show recent activity.

## Field Table

### FeedItem

| C++ field | C++ type | C# field | C# type | Persisted? | Notes |
|---|---|---|---|---|---|
| `accountId` | `uint32_t` | _(none)_ | — | No | The account that generated the event |
| `id` | `uint32_t` | _(none)_ | — | No | Item id |
| `messageId` | `FeedMessageType` (enum) | _(none)_ | — | No | `Friend=1`, `Creature=2`, `Upgrade=3` |
| `metadata` | `std::string` | _(none)_ | — | No | Content varies by type: friend account id, `creatureId;nameId`, or upgrade id |
| `name` | `std::string` | _(none)_ | — | No | Display name of the user who triggered the event |
| `timestamp` | `uint64_t` | _(none)_ | — | No | Unix seconds |

### Feed (container)

| C++ field | C++ type | C# field | C# type | Persisted? | Notes |
|---|---|---|---|---|---|
| `mItems` | `std::vector<FeedItem>` | _(none)_ | — | No | Owned by `User`; persisted to per-user XML in C++, not modeled in C# |

## Persistence

Not persisted in C#. In C++ the Feed is serialized to per-user XML (`User.cpp:147–194`) as part of `User::Save()` / `User::Load()`. No C# model, table, or service exists for Feed.

## Mapper

No mapper.

## Porting Gaps

- `Feed` and `FeedItem` are completely absent from C#. No domain class, model, service, or REST contract handles feed data.
- `FeedMessageType` enum (`User.h:24–28`) has no C# equivalent.
- The REST launcher typically polls `/api/feed` or similar endpoint. If that endpoint is not stubbed, the launcher may show an empty feed or error — verify Blaze/REST handlers.
- Feed items reference `accountId` + `name` of the triggering user, requiring cross-user lookups. The in-memory `UserManager` in C++ supports `GetUserById()`; C# has no equivalent user registry.
