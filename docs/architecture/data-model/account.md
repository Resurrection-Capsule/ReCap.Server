# Account

Stores player account data: progression, unlocks, and identity. Persisted per-user in SQLite.

## Field Table

| C++ field | C++ type | C# field | C# type | Persisted? | Notes |
|---|---|---|---|---|---|
| `id` | `int64_t` | `Id` | `ulong` | Yes | PK; C++ uses `int64_t`, C# uses `ulong` (unsigned — potential sign divergence for very large ids) |
| `tutorialCompleted` | `bool` | `tutorialCompleted` | `bool` | Yes | Serialized as `"Y"/"N"` in C++ XML; same in AccountMapper contract |
| `grantAllAccess` | `bool` | `grantAllAccess` | `bool` | Yes | |
| `grantOnlineAccess` | `bool` | `grantOnlineAccess` | `bool?` | Yes | Nullable in C#; C++ serializes as int (0/1); commented out in C++ `Write()` |
| `chainProgression` | `uint32_t` | `chainProgression` | `int` | Yes | C# uses signed int for an unsigned field |
| `creatureRewards` | `uint32_t` | `creatureRewards` | `int` | Yes | |
| `currentGameId` | `uint32_t` | `currentGameId` | `int` | Yes | Default 1 |
| `currentPlaygroupId` | `uint32_t` | `currentPlaygroupId` | `int` | Yes | Default 1 |
| `defaultDeckPveId` | `uint32_t` | `defaultDeckPveId` | `int` | Yes | Default 1 |
| `defaultDeckPvpId` | `uint32_t` | `defaultDeckPvpId` | `int` | Yes | Default 1 |
| `level` | `uint32_t` | `level` | `int` | Yes | |
| `xp` | `uint32_t` | `xp` | `int` | Yes | |
| `dna` | `uint32_t` | `dna` | `int` | Yes | Currency; spent by `UnlockUpgrade()` in C++ |
| `avatarId` | `uint32_t` | `avatarId` | `int` | Yes | C++ clamps 0–16 on read |
| `newPlayerInventory` | `uint32_t` | `newPlayerInventory` | `int` | Yes | |
| `newPlayerProgress` | `uint32_t` | `newPlayerProgress` | `int` | Yes | |
| `cashoutBonusTime` | `uint32_t` | `cashoutBonusTime` | `int` | Yes | |
| `starLevel` | `uint32_t` | `starLevel` | `int` | Yes | |
| `unlockCatalysts` | `uint32_t` | `unlockCatalysts` | `int` | Yes | |
| `unlockDiagonalCatalysts` | `uint32_t` | `unlockDiagonalCatalysts` | `int` | Yes | |
| `unlockInventory` | `uint32_t` | `unlockInventory` | `int` | Yes | |
| `unlockFuelTanks` | `uint32_t` | `unlockFuelTanks` | `int` | Yes | |
| `unlockPveDecks` | `uint32_t` | `unlockPveDecks` | `int` | Yes | |
| `unlockPvpDecks` | `uint32_t` | `unlockPvpDecks` | `int` | Yes | |
| `unlockStats` | `uint32_t` | `unlockStats` | `int` | Yes | |
| `unlockInventoryIdentify` | `uint32_t` | `unlockInventoryIdentify` | `int` | Yes | |
| `unlockEditorFlairSlots` | `uint32_t` | `unlockEditorFlairSlots` | `int` | Yes | |
| `upsell` | `uint32_t` | `upsell` | `int` | Yes | |
| `capLevel` | `uint32_t` | `capLevel` | `int` | Yes | |
| `capProgression` | `uint32_t` | `capProgression` | `int` | Yes | |
| _(none)_ | — | `Email` | `string` | Yes | C# only; not in C++ `Account` struct (lives on `User`) |
| _(none)_ | — | `Username` | `string` | Yes | C# only; not in C++ `Account` struct (lives on `User`) |
| _(none)_ | — | `Password` | `string` | Yes | C# only; not in C++ `Account` struct (lives on `User`) |
| _(none)_ | — | `settings` | `Dictionary<string,string>` | No (`[NotMapped]`) | No C++ equivalent; not written to DB |

## Persistence

EF Core model: `AccountModel` (`Models/AccountModel.cs`).
Table: `Accounts` (DbSet in `Config/SqliteConfig.cs:12`).
Repository: `Adapters/Persistence/SQLite/AccountRepositoryAdapter.cs`.
Auth tokens are stored only in-memory (`static Dictionary<string,ulong> idByAuthToken`); not persisted across restarts.

## Mapper

`Mappers/AccountMapper.cs` — AutoMapper, two maps:
- `Account` → `AccountModel` (Domain to persistence).
- `AccountModel` → `AccountContract` (persistence to REST response; maps `tutorialCompleted` bool → `"Y"/"N"` string).

## Porting Gaps

- All numeric fields use `int` (signed) in C# where C++ uses `uint32_t`. Overflow for values > `int.MaxValue` would silently corrupt data.
- `Email`, `Username`, `Password` are merged into `Account` in C# but belong to `User` in C++. C++ `Account` struct has no authentication identity fields at all.
- `settings` dictionary (`Domain/Account.cs:50`) is `[NotMapped]` — never written to or read from SQLite; populated from nowhere at present.
- Auth token map is static in-memory (`AccountRepositoryAdapter.cs:22`). Survives restarts only while process runs. C++ also keeps tokens in-memory (on `User`).
- C++ `grantOnlineAccess` is commented out in `Write()` (`User.cpp:141`), so it is never serialized to XML. C# persists it.
- No `unlockInventory` / `unlockInventoryIdentify` linkage logic ported (C++ `UnlockUpgrade()` derives `unlockInventoryIdentify = 180 + 30 * unlockInventory` dynamically — C# stores both as raw values only).
