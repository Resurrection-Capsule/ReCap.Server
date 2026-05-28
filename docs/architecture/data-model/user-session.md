# User Session

Aggregates the live runtime state of one logged-in player: identity, auth, room placement, game instance, and association lists. Entirely in-memory; no dedicated persistence table.

## Field Table

| C++ field | C++ type | C# field | C# type | Persisted? | Notes |
|---|---|---|---|---|---|
| `mId` | `uint32_t` | _(none)_ | — | No | RakNet-assigned client slot id. Not modeled in C#; scattered across `Player.Id` (gameplay) and `AccountRepositoryAdapter`. |
| `mState` | `uint32_t` | _(none)_ | — | No | Blaze connection state, managed by `UpdateState()`. Not modeled as a field in C#. |
| `mUsername` | `std::string` | `Account.Email` | `string` | Yes (Account table) | C++ stores on User; C# collapses into Account domain object. |
| `mPassword` | `std::string` | `Account.Password` | `string` | Yes (Account table) | Same collapse. |
| `mName` | `std::string` | `Account.Username` | `string` | Yes (Account table) | Display name; C++ separates from email/username. |
| `mAuthToken` | `std::string` | _(static dict)_ | `Dictionary<string,ulong>` | No | C++ on User instance; C# as static in-memory map in `AccountRepositoryAdapter.cs:22`. |
| `mAccount` | `Account` | `Account` (domain entity) | `Account` | Yes (Account table) | One-to-one; loaded separately in C#. |
| `mCreatures` | `Creatures` | _(service-loaded list)_ | `List<CreatureModel>` | Yes (Creatures table) | C++ owns creature list on User; C# loads on demand via `CreatureService`. |
| `mSquads` | `std::vector<SquadPtr>` | _(service-loaded list)_ | `List<DeckModel>` | Yes (Decks table) | C++ calls them squads; C# calls them decks. |
| `mFeed` | `Feed` | _(none)_ | — | No | Not modeled in C#. |
| `mParts` | `Parts` | _(service-loaded list)_ | `List<CreaturePartModel>` | Yes (CreatureParts table) | Owned by User in C++; loaded per-account in C#. |
| `mAssociationLists` | `std::map<uint32_t, Blaze::ListMembers>` | _(none)_ | — | No | Friend/ignore lists. Not modeled or persisted in C#. |
| `mGame` | `Game::InstancePtr` | `Player.Client` (RakNetClient) | `RakNetClient?` | No | C++ links game instance; C# links the RakNet client socket. Different abstraction level. |
| `mRoom` | `RoomPtr` | _(none)_ | — | No | Blaze lobby room. No Room domain object in C#. |
| `mExtendedData` | `Blaze::UserSessionExtendedData` | _(none)_ | — | No | Blaze extended session metadata. Not modeled in C#. |

## Persistence

No dedicated `UserSession` table or model in C#. The concept is fragmented:
- Identity fields live in `AccountModel` / `Accounts` table.
- Auth token lives in `AccountRepositoryAdapter.idByAuthToken` (static, in-memory only).
- Gameplay state lives in `Domain/Gameplay/Player.cs` and `BasePlayer.cs` (in-memory only).

## Mapper

No mapper for User Session — no domain object exists to map.

## Porting Gaps

- No unified `UserSession` domain object. C++ `User` is the single root that owns account, creatures, squads, parts, feed, room, and game state. In C# these are all separate services and repositories with no shared owner.
- `mId` (RakNet slot id) and `mState` (Blaze connection state) are not tracked in any C# field; game code assigns `Player.Id` which serves a similar but not identical role.
- `mExtendedData` (`Blaze::UserSessionExtendedData`) — Blaze presence/NAT data — not modeled.
- `mRoom` — no Room object in C#; lobby room assignment is not tracked per-session.
- `mAssociationLists` — friend/ignore lists not implemented at all in C# (methods exist in C++ `User.cpp:520–578`).
- `mFeed` — Feed entity absent in C# entirely.
- Auth token map is static per-process, meaning multi-process deployments would fail. C++ has the same limitation.
