# Creature Template

Static read-only definition of a creature archetype: stats, abilities, element type, class, and geometry constraints. Seeded from JSON at startup; never mutated by gameplay.

## Field Table

| C++ field | C++ type | C# field | C# type | Persisted? | Notes |
|---|---|---|---|---|---|
| `mNoun` | `uint32_t` | `id` | `ulong` | Yes (PK) | Noun hash = unique template identifier |
| `mNameLocaleId` | `std::string` | `nameLocaleId` | `string` | Yes | |
| `mTextLocaleId` | `std::string` | `descLocaleId` | `string` | Yes | C++ name `mTextLocaleId`, C# name `descLocaleId` — same field |
| `mName` | `std::string` | `name` | `string` | Yes | |
| `mType` | `CreatureType` (enum) | `elementType` | `string` | Yes | C++ typed enum (`Bio/Cyber/Plasma/Necro/Chrono/All/Unknown`); C# raw string |
| `mClass` | `CreatureClass` (enum) | `classType` | `string` | Yes | C++ typed enum (`Ravager/Sentinel/Tempest/All/Unknown`); C# raw string |
| `mEquipableParts` | `CreatureParts` (enum) | `hasHands` + `hasFeet` | `bool` + `bool` | Yes | C++ single enum (`All/NoHands/NoFeet/Unknown`); C# decomposed into two booleans |
| `mBaseWeaponDamageMin` | `double` | `weaponMinDamage` | `double` | Yes | |
| `mBaseWeaponDamageMax` | `double` | `weaponMaxDamage` | `double` | Yes | |
| `mGearScore` | `float` | `gearScore` | `double` | Yes | C++ float, C# double |
| `mAbility[0]` | `uint32_t` | `abilityPassive` | `int` | Yes | Index 0 of C++ `mAbility[5]` array |
| `mAbility[1]` | `uint32_t` | `abilityBasic` | `int` | Yes | Index 1 |
| `mAbility[2]` | `uint32_t` | `abilityRandom` | `int` | Yes | Index 2 |
| `mAbility[3]` | `uint32_t` | `abilitySpecial1` | `int` | Yes | Index 3 |
| `mAbility[4]` | `uint32_t` | `abilitySpecial2` | `int` | Yes | Index 4 |
| `mAbilityStats` | `std::vector<AbilityStat>` | `statsTemplateAbilityKeyvalues` | `string` | Yes | C# stores as delimited string |
| `mStats` | `std::vector<Stats>` | `statsTemplate` | `string` | Yes | C# stores as delimited string |
| _(none)_ | — | `statsTemplateAbility` | `string` | Yes | C# only; no direct C++ counterpart as a stored field |

## Persistence

EF Core model: `CreatureTemplateModel` (`Models/CreatureTemplateModel.cs`).
Table: `CreatureTemplates` (DbSet in `Config/SqliteConfig.cs:13`).
Seeded at startup from `resources/creature_templates.json` (`Config/SqliteConfig.cs:33–38`).
Repository: `Adapters/Persistence/SQLite/CreatureTemplateRepositoryAdapter.cs` (read-only; no insert/update operations).

## Mapper

`Mappers/CreatureTemplateMapper.cs` — AutoMapper, one map:
- `CreatureTemplateModel` → `GetCreatureTemplateResponseContract`.

Also used in `CreatureMapper.toGetCreatureContract()` to overlay template fields onto the REST creature detail response.

## Porting Gaps

- `CreatureType` and `CreatureClass` are typed C++ enums (`Creature.h:62–78`) with serialization helpers (`from_string` / `to_string`). C# stores these as raw strings in `elementType` and `classType`; no corresponding C# enum types are defined anywhere in the project.
- `mEquipableParts` enum semantics (`NoHands`, `NoFeet`) are lost — C# uses two independent booleans that don't map cleanly to the tri-state enum.
- `mAbilityStats` is a vector of structured `AbilityStat` records (key/token/value) in C++. In C# it is serialized as a delimited string; no structured deserialization to a typed object exists on the template (unlike on `CreatureModel`).
- `CreatureID` enum in `Creature.h:22–60` (listing all known noun hashes per creature) has no C# equivalent; noun IDs are just raw `ulong` values.
- Template is strictly read-only; no update or delete path in C# (matching C++ behavior).
