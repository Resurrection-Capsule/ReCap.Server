# Vendor

Manages the part shop: a rotating offer list of purchasable parts and a per-user buyback list. Entirely in-memory and stateless between restarts.

## Field Table

| C++ field | C++ type | C# field | C# type | Persisted? | Notes |
|---|---|---|---|---|---|
| `mPartOfferList` | `Parts` | _(none — approximated by `CreaturePartTemplates` table)_ | — | No | C++ generates procedurally on construction; C# uses a pre-seeded JSON loot table instead |
| `mBuybackMap` | `std::unordered_map<UserPtr, Parts>` | _(none)_ | — | No | Per-user parts sold back to vendor; not modeled in C# at all |

## Persistence

Not persisted. No `Vendor` domain class exists in C#. The offer list concept is approximated by the `CreaturePartTemplates` table (seeded from `resources/part_templates.json`), which `CreaturePartRepositoryAdapter` and `CreaturePartMapper.toCreaturePartModel()` use to construct offer responses.

The buyback list (`mBuybackMap`) has no C# equivalent whatsoever.

## Mapper

No mapper — no C# Vendor domain object.

## Porting Gaps

- No `Vendor` class or service in C#. The `CreaturePartTemplateRepositoryAdapter` covers the static offer list but not dynamic refresh or buyback.
- `Vendor::RefreshPartOfferList()` generates random parts (ids 100–149) with hardcoded stats (`Vendor.cpp:46–66`). C# `CreaturePartTemplates` is a static JSON seed — no random rotation.
- `Vendor::OnBuy()` and `Vendor::OnSell()` transaction logic is absent in C#.
- `mBuybackMap` (per-user sell-back inventory) is not modeled; a player cannot recover sold parts.
- `PartRarity::Rare` hardcoded default in `RefreshPartOfferList()` (`Vendor.cpp:57`) is not replicated in C# loot table logic.
