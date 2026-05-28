# Data Model

Field-level comparison of every data entity between the C++ reference (`SporeNet/`) and the C# implementation (`ReCap.Server/`).

## Entity Index

| Sheet | Parity | C++ source | C# source |
|---|---|---|---|
| [account.md](account.md) | ~95% — all fields ported; type widening (uint→int) and field-location divergence (email/username on Account vs User) | `SporeNet/User.h:31` | `Domain/Account.cs`, `Models/AccountModel.cs` |
| [user-session.md](user-session.md) | ~20% — no unified session object; identity in Account, gameplay in Player, no room/feed/assoc-lists | `SporeNet/User.h:111` | `Domain/Gameplay/Player.cs`, `Domain/Gameplay/BasePlayer.cs` |
| [creature.md](creature.md) | ~75% — core identity ported; Domain entity is stub (no stats/parts/abilityStats); full data only on EF model | `SporeNet/Creature.h:199` | `Domain/Creature.cs`, `Models/CreatureModel.cs` |
| [creature-template.md](creature-template.md) | ~85% — all ability and damage fields ported; typed enums (CreatureType, CreatureClass) downgraded to strings | `SporeNet/Creature.h:145` | `Models/CreatureTemplateModel.cs` |
| [part.md](part.md) | ~80% — all hash/rarity/status fields ported; `mEquippedToCreatureId` missing; asset ids widened 16→64 bit | `SporeNet/Part.h:31` | `Domain/CreaturePart.cs`, `Models/CreaturePartModel.cs` |
| [squad-deck.md](squad-deck.md) | ~85% — all fields ported; `mLocked` default inverted; creature list changed from fixed[3] to List; no update path | `SporeNet/Squad.h:11` | `Domain/Deck.cs`, `Models/DeckModel.cs` |
| [room.md](room.md) | 0% — Room, RoomCategory, RoomView, RoomManager entirely absent from C# | `SporeNet/Room.h:84` | _(none)_ |
| [vendor.md](vendor.md) | ~10% — static offer list approximated via JSON seed; no buy/sell/buyback logic | `SporeNet/Vendor.h:15` | `Models/CreaturePartTemplateModel.cs` |
| [feed.md](feed.md) | 0% — Feed and FeedItem entirely absent from C# | `SporeNet/User.h:91` | _(none)_ |
| [association-lists.md](association-lists.md) | 0% — friend/ignore lists entirely absent from C# | `SporeNet/User.h:199` | _(none)_ |

## Persistence Overview

ReCap uses **SQLite via Entity Framework Core** (EF Core) as its persistence layer. The `DbContext` subclass is `Config/SqliteConfig.cs`; it declares seven `DbSet<T>` properties that map to SQLite tables:

| Table | Model | Seeded? |
|---|---|---|
| `Accounts` | `AccountModel` | No (user registration) |
| `Creatures` | `CreatureModel` | No (unlocked in-game) |
| `CreatureTemplates` | `CreatureTemplateModel` | Yes — `resources/creature_templates.json` |
| `CreatureParts` | `CreaturePartModel` | No (loot drops) |
| `CreaturePartTemplates` | `CreaturePartTemplateModel` | Yes — `resources/part_templates.json` |
| `Decks` | `DeckModel` | No (created on account setup) |
| `DbSequences` | `DbSequenceModel` | Auto (id generators) |

Seeding happens once at startup in `SqliteConfig.Start()` by checking for a sentinel row before inserting. Static reference data (templates) is read-only after seeding.

The database file path is resolved from `ServerConfig.ServerDatabasePath`. Schema is created via `Database.EnsureCreated()` (no migrations file; schema is derived from EF model annotations).

Auth tokens, association lists, feed data, room state, and vendor state are **not persisted** — they are in-memory only and reset on server restart.
