# Darkspore AssetData Reflection System

How `Darkspore.exe` describes, hashes, and registers every asset/data type at
runtime. Reverse-engineered from the retail client in Ghidra. All addresses are
in the `Darkspore.exe` image (image base `0x00400000`).

The companion file [`REFLECTION_SCHEMA_DUMP.txt`](./REFLECTION_SCHEMA_DUMP.txt)
is the raw extraction: every reflection field name, in order, for all 488
registrar functions (6292 hashed strings).

---

## 1. Overview

Each reflectable C++ type (assets, components, tuning blocks, network structs)
has a generated **field-descriptor builder** function, already named in Ghidra
as `AssetData::<Type>` (e.g. `AssetData::Noun`, `AssetData::cGameObjectCreateData`,
`AssetData::cAssetProperty`). These builders run once at program startup (they
are C++ global constructors invoked through the CRT init list) and populate a
static array of **field descriptors** for their type.

A descriptor records, per field: the field's **type**, its **name** (and the
FNV hash of that name), its **byte offset** inside the C++ struct, its
**container strategy**, and a **default value**. The hashed field name is the
key the engine uses to bind serialized property data (`.prop` / packaged
AssetData) to concrete struct members.

This is the authoritative schema for the binary asset formats — it is what a
faithful re-implementation of the asset parser must mirror.

---

## 2. The hash function — `HashFunctionn` @ `0x00adf410`

Every field/type name is hashed by `HashFunctionn(name, basis, mode)`:

```
hash = basis
for each byte b of name:
    hash = (hash * 0x01000193) ^ xlat[b]      // 32-bit
```

- This is **FNV-1** (multiply *then* XOR), **not** FNV-1a.
- `basis` is the FNV-1 32-bit offset basis `0x811c9dc5` at every call site.
- `mode` selects a 256-entry byte translation table applied to each input byte:
  - `mode 0` — raw byte (identity).
  - `mode 1` — translate via table at `0x01191e30` (case-folding; used by every
    AssetData field/type registration).
  - `mode 2` — translate via table at `0x01191f30`.

Because mode 1 case-folds, field-name lookups are case-insensitive.

---

## 3. Field-descriptor builders — `AssetData::<Type>`

Enumerate the reflection system by listing the callers of `HashFunctionn`
(~488 functions). Almost all are already named with their type.

Each type appears as a **pair**:

1. A large builder that hashes every field name and writes the descriptor
   records (the full schema). Example field counts: `Noun` = 256, `labsCharacter`
   = 372, `ability` = 149, `labsPlayer` = 72, `cGameObjectCreateData` = 30.
2. A one-field function that only hashes the type's own name — this
   **self-registers the type name** into the global type registry.

### Descriptor record layout

Decompiling `AssetData::cAssetProperty @ 0x00f8f9f0` shows the per-field record
(~`0x60` bytes), with fields written to a contiguous global descriptor array:

| Offset | Meaning                                                            |
|--------|-------------------------------------------------------------------|
| +0x00  | type-name string pointer (e.g. `"char"`, `"guid"`)                |
| +0x04  | `HashFunctionn(typeName, …)`                                      |
| +0x08  | type id / flags                                                   |
| +0x0c  | field-name string pointer (e.g. `"name"`)                         |
| +0x10  | `HashFunctionn(fieldName, …)`                                     |
| +0x14  | member byte offset inside the struct (e.g. `name@0x50`, `type@0x54`, `value@0x58`) |
| +0x18  | `ContainerStrategy::Get(...)` — scalar / list / array kind        |
| +0x1c… | default value string + hash, type-enum flags                      |

---

## 4. Reading `REFLECTION_SCHEMA_DUMP.txt`

For each registrar the dump lists the hashed strings in source order. Within a
builder these appear as repeating triples:

```
<type>, <fieldName>, <default>
```

Examples decoded from the dump:

```
cGameObjectCreateData (30):
    noun (Noun), position (cSPVector3), rotXDegrees/rotYDegrees/rotZDegrees (float),
    assetId (uint64_t), scale (float), team (uint8_t),
    hasCollision (bool), playerControlled (bool)

cAssetProperty (12):
    name (char), value (char), key (guid), type (guid)

cAssetPropertyList (3):
    array mpAssetProperties of cAssetProperty

CrystalDef (9):
    modifier (Key), type (enum), rarity (enum)

labsCrystal:
    crystalNoun (Noun), level (uint16_t)
```

Entries with 0–1 fields are **not** asset schemas: they are either runtime
single-key lookups or App config "Property" registrations (e.g. `ShowRadar`,
`ShowFriendsChat`, `TooltipHoverTime`) — a simpler flavor of the same hash-based
system used for tunable UI/config variables.

---

## 5. What this is NOT (two common traps)

- **The `0x00FD0000` table is not a reflection dispatch table.** It is the
  standard MSVC CRT static-initializer pointer array (`_initterm` / `.CRT$XCU`):
  1000+ `void(void)` thunks run at startup. The `AssetData::<Type>` builders are
  *among* the functions these thunks invoke, but the table itself is generic C++
  global-ctor machinery.

- **`FUN_00e0c8c0` is unrelated to reflection.** It validates a `uint16_t` index
  against a vector: returns `0` when the index is in range and `0xFD0004` (a
  magic error sentinel) when out of range or on the `0xFFFF` sentinel mismatch.
  The constant `0xFD0004` happens to equal the init-table address, so Ghidra
  mislabels the immediate as `&PTR_LAB_00fd0004`; the two are unrelated.

---

## 6. Relevance to ReCap

This schema is the ground truth for `AssetData.Parser`: it defines, per format,
the exact field names (and their FNV-1 hashes), declared types, container
strategies, and struct offsets used by the retail client. The 8 formats loaded
by the in-game asset database are the porting priority; the dump covers all of
them plus every other reflectable type.

## 7. How the dump was regenerated

1. Set `GHIDRA_MCP_ALLOW_SCRIPTS=1` in the environment of the **Ghidra** process
   (the Python MCP bridge ignores this variable — the gate is enforced
   Ghidra-side) and restart Ghidra.
2. Run an inline Ghidra script that, for every caller of `HashFunctionn`
   (`0x00adf410`), walks the function's instructions and captures the string
   pushed immediately before each `CALL` to it, in order.
3. If inline scripts fail with a `GhidraPlaceholderBundle cannot be cast to
   GhidraSourceBundle` error (caused by a hard-killed Ghidra leaving a corrupt
   OSGi cache), delete
   `%APPDATA%\ghidra\ghidra_12.1_PUBLIC\osgi\{felixcache,compiled-bundles}` and
   restart.
