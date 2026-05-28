# Creature

A player-owned creature instance. Holds identity, gear score, visual URLs, stats, and ability data. Backed by the `Creatures` SQLite table.

## Field Table

| C++ field | C++ type | C# field | C# type | Persisted? | Notes |
|---|---|---|---|---|---|
| `mId` | `uint32_t` | `ID` | `ulong` | Yes | PK; C++ 32-bit, C# 64-bit (widened) |
| `mVersion` | `uint32_t` | `Version` | `int` | Yes | Edit version counter |
| `mTemplate` | `TemplateCreaturePtr` | `TemplateID` + `TemplateName` | `ulong` + `string` | Yes | C++ stores live pointer; C# stores foreign key id + name |
| `mCreatorId` | `int64_t` | `AccountID` | `ulong` | Yes | Owner account; C++ calls it `creatorId`, C# calls it `AccountID` |
| `mGearScore` | `float` | `GearScore` | `double` | Yes | C++ float, C# double (widened) |
| `mItemPoints` | `float` (default 300) | `ItemPoints` | `double` | Yes | |
| `mPngLargeUrl` | `std::string` | `LargePngUrl` | `string?` | Yes | |
| `mPngThumbUrl` | `std::string` | `ThumbPngUrl` | `string?` | Yes | |
| `mAbilityStats` | `std::vector<AbilityStat>` | `AbilityStats` | `List<CreatureModelAbilityStat>` | Yes (serialized to string in model) | C# model stores as semicolon-delimited string in DB; parsed by `setAbilityStatsWithString()` |
| `mStats` | `std::vector<Stats>` | `Stats` | `List<CreatureModelStat>` | Yes (serialized to string in model) | Same pattern: semicolon-delimited string |
| _(none)_ | — | `Cost` | `ulong` | Yes | No C++ equivalent on `Creature`; may reflect part costs |
| _(none)_ | — | `Parts` | `List<ulong>` | Yes (serialized to string) | C++ stores parts as `Parts mEquippedParts` (commented out in `Creature.h:237`); C# stores part id list |
| _(none)_ | — | `LargePngBase64` / `LargeCrc` | `string?` | Yes | C# model extras; no C++ equivalent |
| _(none)_ | — | `ThumbPngBase64` / `ThumbCrc` | `string?` | Yes | C# model extras; no C++ equivalent |

## Persistence

EF Core model: `CreatureModel` (`Models/CreatureModel.cs`).
Table: `Creatures` (DbSet in `Config/SqliteConfig.cs:13`).
Repository: `Adapters/Persistence/SQLite/CreatureRepositoryAdapter.cs`.

`Stats` and `AbilityStats` are stored as serialized strings in `CreatureModel` (not separate columns); deserialized at load time via helper methods.

`Parts` is stored as a comma-delimited string via `getPartsAsString()` / `setPartsWithString()` (`Models/CreatureModel.cs:33–39`).

## Mapper

`Mappers/CreatureMapper.cs` — AutoMapper, three maps:
- `Creature` → `CreatureModel` (Domain to persistence; maps `Domain.Creature` which lacks stats/ability fields — those only exist on `CreatureModel`).
- `CreatureModel` → `CreatureContract` (persistence to REST response).
- `CreatureTemplateModel` → `GetCreatureResponseContract` (template to REST detail response, with creature fields overlaid manually in `toGetCreatureContract()`).

## Porting Gaps

- `Domain/Creature.cs` is a thin stub — it has no `Stats`, `AbilityStats`, or `Parts` fields. All three exist only on `CreatureModel`. The Domain entity and the EF Model are structurally diverged: the Domain is not a true representation of the data.
- `mEquippedParts` is commented out in C++ `Creature.h:237`; C# stores part IDs on `CreatureModel.Parts` but the `Creature` domain entity has no `Parts` field.
- `mCreatorId` semantic mismatch: C++ means the creator (Spore editor user), C# uses `AccountID` (the owning player). For this game they are equivalent, but the naming divergence could cause confusion.
- C++ `Creature` derives stats from a `TemplateCreature` reference at runtime (calling `mTemplate->GetAbility()`, etc.). C# flattens everything into persisted model strings; there is no runtime derivation from template.
- `Cost` field on `CreatureModel` has no C++ `Creature` counterpart — provenance unclear.
- `CreatureType` and `CreatureClass` enums defined in `SporeNet/Creature.h:62–78` are not modeled as typed enums in C#; stored as raw strings (`elementType`, `classType`) in `CreatureTemplateModel`.
