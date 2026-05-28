# Room

A Blaze lobby room that groups connected players for matchmaking. Composed of a hierarchy: RoomView → RoomCategory → Room. All data is in-memory and ephemeral.

## Field Table

### Room

| C++ field | C++ type | C# field | C# type | Persisted? | Notes |
|---|---|---|---|---|---|
| `mId` | `uint32_t` | _(none)_ | — | No | Not modeled in C# |
| `mName` | `std::string` | _(none)_ | — | No | Auto-set to `"Lobby #N"` |
| `mPassword` | `std::string` | _(none)_ | — | No | |
| `mCapacity` | `uint32_t` (default 1) | _(none)_ | — | No | |
| `mCategory` | `RoomCategoryPtr` | _(none)_ | — | No | Parent category link |
| `mUsers` | `std::map<uint64_t, UserPtr::weak_type>` | _(none)_ | — | No | Live user set with weak references |

### RoomCategory

| C++ field | C++ type | C# field | C# type | Persisted? | Notes |
|---|---|---|---|---|---|
| `mId` | `uint32_t` | _(none)_ | — | No | |
| `mName` | `std::string` | _(none)_ | — | No | Default `"Lobby Category #N"` |
| `mDescription` | `std::string` | _(none)_ | — | No | |
| `mPassword` | `std::string` | _(none)_ | — | No | |
| `mView` | `RoomViewPtr` | _(none)_ | — | No | Parent view link |

### RoomView

| C++ field | C++ type | C# field | C# type | Persisted? | Notes |
|---|---|---|---|---|---|
| `mId` | `uint32_t` | _(none)_ | — | No | |
| `mName` | `std::string` | _(none)_ | — | No | Default `"Lobby View #N"` |

## Persistence

Not persisted. Room, RoomCategory, and RoomView are entirely in-memory, managed by `RoomManager` (`SporeNet/Room.cpp:203–319`). No C# equivalent exists at all.

## Mapper

No mapper — no C# domain object to map.

## Porting Gaps

- No `Room`, `RoomCategory`, `RoomView`, or `RoomManager` domain object or service exists in C#.
- `User.mRoom` (the per-session room assignment, `SporeNet/User.h:212`) has no C# counterpart.
- The Blaze `RoomManager` is initialized at startup with 4 views, 4 categories, and 4 rooms (`Room.cpp:203–216`). C# Blaze component handlers exist (`Adapters/Blaze/`) but do not manage Room objects.
- `Room.WriteTo()` serializes the full Blaze TDF room packet including user list, capacity, category name, and password (`Room.cpp:150–200`). This packet path is not implemented in C#.
- `RoomCategoryFlags` enum (`Room.h:21–24`) — `Pseudo=4`, `Unknown=8` — is not modeled in C#.
