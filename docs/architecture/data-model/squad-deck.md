# Squad / Deck

A named slot holding three creature IDs that the player takes into a game session. Called "Squad" in C++ source and in the XML wire format; called "Deck" in C# source. The terms are interchangeable.

## Field Table

| C++ field | C++ type | C# field | C# type | Persisted? | Notes |
|---|---|---|---|---|---|
| `mId` | `uint32_t` | `ID` | `ulong` | Yes | C++ 32-bit, C# 64-bit |
| `mSlot` | `uint32_t` | `Slot` | `int` | Yes | 1-based slot index (1, 2, or 3) |
| `mName` | `std::string` | `Name` | `string` | Yes | Default `"Slot N"` in C++ |
| `mCategory` | `std::string` | `Category` | `string?` | Yes | `"pve"` or `"pvp"`; nullable in C# |
| `mLocked` | `bool` | `Locked` | `bool` | Yes | Default `true` in C++ ctor; default `false` in C# — **inverted default** |
| `mCreatureIds[3]` | `std::array<uint32_t, 3>` | `CreatureIds` | `List<ulong>` | Yes | C++ fixed array of 3 slots; C# variable-length list |
| _(none)_ | — | `AccountID` | `ulong` | Yes | C# only; C++ squads are owned by `User.mSquads` container |

## Persistence

EF Core model: `DeckModel` (`Models/DeckModel.cs`).
Table: `Decks` (DbSet in `Config/SqliteConfig.cs:16`).
Repository: `Adapters/Persistence/SQLite/DeckRepositoryAdapter.cs`.

`CreatureIds` is stored as a `List<ulong>` in the model. EF Core will serialize this using the JSON column type or a value converter depending on provider configuration (SQLite requires explicit conversion; verify `SqliteConfig.cs` has this or check migration).

## Mapper

`Mappers/DeckMapper.cs` — AutoMapper plus manual contract builder:
- `Deck` → `DeckModel` (Domain to persistence).
- `toContract(DeckModel, List<CreatureModel>)` — builds `DeckContract` from deck + creature list. Contains a temporary workaround (marked `// TODO`) that ignores `CreatureIds` and instead pulls creatures by slot-index arithmetic (`deck.Slot*3 + 0/1/2`), bypassing the stored id list entirely (`DeckMapper.cs:37–41`).

## Porting Gaps

- `mLocked` default is `true` in C++ (`Squad.h:41`) but `false` in C# (`Domain/Deck.cs:12`). A newly created squad in C# will immediately appear unlocked, which is the opposite of C++ behavior.
- C++ fixed `mCreatureIds[3]` guarantees exactly 3 slots; C# `List<ulong>` is variable-length with no enforcement of exactly 3 entries.
- `AccountID` field is a C#-only addition to support FK lookup without traversing the User object.
- `DeckMapper.toContract()` has a hardcoded TODO workaround: creatures are selected by `Slot * 3 + index` from the full creature list instead of by the actual `CreatureIds`. This will produce incorrect results if creatures are not stored in contiguous slot order.
- No update path in `DeckRepositoryAdapter` — only `insertDeck` exists. Modifying a deck's creature list or lock state after creation has no persistence operation.
- C++ `User::UpdateSquad()` validates creature existence before assigning IDs (`User.cpp:276–288`). No equivalent validation in C#.
- C++ always initializes 3 squads on `ResetSquads()` (`User.cpp:247–258`). C# does not guarantee three decks exist for a new account.
