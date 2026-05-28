# Part (Creature Part)

A loot item that can be equipped to a creature. Defined by rigblock, prefix, and suffix asset references, plus rarity, level, and market metadata.

## Field Table

| C++ field | C++ type | C# field | C# type | Persisted? | Notes |
|---|---|---|---|---|---|
| `mRigblockAssetHash` | `uint32_t` | `RigblockAssetHash` | `ulong` | Yes | FNV1-A hash of rigblock path; C++ 32-bit, C# 64-bit |
| `mPrefixAssetHash` | `uint32_t` | `PrefixAssetHash` | `ulong` | Yes | |
| `mPrefixSecondaryAssetHash` | `uint32_t` | `PrefixSecondaryAssetHash` | `ulong` | Yes | |
| `mSuffixAssetHash` | `uint32_t` | `SuffixAssetHash` | `ulong` | Yes | |
| `mRigblockAssetId` | `uint16_t` | `RigblockAssetId` | `ulong` | Yes | C++ 16-bit, C# 64-bit (widened) |
| `mPrefixAssetId` | `uint16_t` | `PrefixAssetId` | `ulong` | Yes | |
| `mPrefixSecondaryAssetId` | `uint16_t` | `PrefixSecondaryAssetId` | `ulong` | Yes | |
| `mSuffixAssetId` | `uint16_t` | `SuffixAssetId` | `ulong` | Yes | |
| `mTimestamp` | `uint64_t` | `CreationDate` | `ulong` | Yes | Unix seconds; named `mTimestamp` in C++, `CreationDate` in C# |
| `mCost` | `uint32_t` | `Cost` | `int` | Yes | C++ unsigned, C# signed |
| `mLevel` | `uint16_t` | `Level` | `int` | Yes | |
| `mRarity` | `PartRarity` (enum) | `Rarity` | `CreaturePartRarity` | Yes | Enums match (`Basic/Uncommon/Rare/Epic/Unique/RareUnique/EpicUnique`) |
| `mMarketStatus` | `uint8_t` | `MarketStatus` | `int` | Yes | |
| `mStatus` | `uint8_t` | `Status` | `int` | Yes | |
| `mUsage` | `uint8_t` | `Usage` | `int` | Yes | |
| `mIsFlair` | `bool` | `IsFlair` | `bool` | Yes | |
| `mEquippedToCreatureId` | `uint32_t` | _(none)_ | — | No | **Missing in C#.** C++ tracks which creature has this part equipped. |
| _(none)_ | — | `AccountId` | `ulong` | Yes | C# only; C++ parts are owned by the `User.mParts` collection, not by per-item field |
| _(none)_ | — | `CreatureId` | `int?` | Yes | C# only; intended to replace `mEquippedToCreatureId` but uses signed nullable int vs unsigned uint |
| _(enum)_ | `PartUsage` (empty enum) | — | — | — | C++ has a `PartUsage` enum defined but it has no members; irrelevant |

## Persistence

EF Core model: `CreaturePartModel` (`Models/CreaturePartModel.cs`).
Table: `CreatureParts` (DbSet in `Config/SqliteConfig.cs:14`).
Repository: `Adapters/Persistence/SQLite/CreaturePartRepositoryAdapter.cs`.

There is also a `CreaturePartTemplateModel` (`Models/CreaturePartTemplateModel.cs`) backed by the `CreaturePartTemplates` table, seeded from `resources/part_templates.json`. This is a read-only loot table; it has no C++ direct equivalent (C++ generates vendor parts procedurally).

Hash computation for `*AssetHash` fields is done in `Mappers/CreaturePartMapper.cs:31–63` via FNV1-A over generated asset paths.

## Mapper

`Mappers/CreaturePartMapper.cs` — AutoMapper plus manual helpers:
- `CreaturePart` → `CreaturePartModel` (Domain to persistence).
- `CreaturePartModel` → `CreaturePartContract` (persistence to REST response; sets `ReferenceID = ID`).
- `toCreaturePartModel(CreaturePartTemplateModel, bool)` — constructs a `CreaturePartModel` from a loot-table template with computed asset hashes.

## Porting Gaps

- `mEquippedToCreatureId` (`Part.h:79`) is missing. C# has `CreatureId` (`int?`) which covers the same concept, but it is signed and nullable where C++ is `uint32_t`. No logic currently sets or uses `CreatureId` consistently.
- `AccountId` on `CreaturePartModel` is a C#-only addition; C++ derives ownership from the `User` who holds the part in their `mParts` collection.
- Asset hash fields widened from `uint32_t` (C++) to `ulong` (C#) — safe for storage but may diverge if wire protocol expects 32-bit hashes.
- Asset ID fields widened from `uint16_t` (C++) to `ulong` (C#) — same concern on wire usage.
- `PartUsage` enum (`Part.h:15–17`) is empty in C++; the `Usage` field is treated as raw byte. C# mirrors this as `int Usage`.
- C++ `Vendor::RefreshPartOfferList()` generates parts procedurally (`Vendor.cpp:46–66`); C# uses a pre-seeded `CreaturePartTemplates` JSON table instead — different data source, equivalent purpose.
