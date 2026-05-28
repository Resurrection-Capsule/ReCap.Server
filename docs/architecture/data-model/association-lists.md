# Association Lists

Per-user friend list and ignore list, managed through the Blaze Association component. Entirely in-memory in both C++ and C#; not persisted to any store.

## Field Table

| C++ field | C++ type | C# field | C# type | Persisted? | Notes |
|---|---|---|---|---|---|
| `mAssociationLists` | `std::map<uint32_t, Blaze::ListMembers>` | _(none)_ | — | No | Map from list type id (Friend, Ignore) to member list |
| _(list type key)_ | `Blaze::AssociationListId` (Friend / Ignore) | _(none)_ | — | No | C++ uses `Blaze::AssociationListId::Friend` and `::Ignore` as keys |
| `memberList` (inside `ListMembers`) | `std::vector<MemberInfo>` | _(none)_ | — | No | Each entry has `id.id` (account id int64), `id.name` (string), and `time` (unix timestamp) |

## Persistence

Not persisted in either C++ or C#. In C++, association lists are loaded/saved via Blaze component logic and stored only on the live `User` object (`User.h:199`). No database table, EF model, or JSON seed exists for this data in C#.

## Mapper

No mapper.

## Porting Gaps

- `mAssociationLists` is completely unimplemented in C#. The `User` methods `IsFriend()`, `AddFriendUser()`, `RemoveFriendUser()`, `IsIgnored()`, `AddIgnoredUser()`, `RemoveIgnoredUser()` (`User.cpp:520–578`) have no C# equivalents.
- `Blaze::AssociationListId` enum and `Blaze::ListMembers` / `MemberInfo` structures are not modeled in C# Blaze types (verify `Adapters/Blaze/` for any stub).
- Blaze `AssociationComponent` is referenced in C++ includes (`User.cpp:7`) — the corresponding C# Blaze component would need to implement at minimum `GetLists`, `AddUsersToList`, and `RemoveUsersFromList` notifications.
- Without friend lists, the launcher's social tab will be empty or error. This is low priority for offline single-player but required for any multiplayer scenario.
- Persistence would need a new EF table (e.g., `UserAssociation` with columns: `OwnerAccountId`, `TargetAccountId`, `ListType`, `Timestamp`) if persistence is ever added.
