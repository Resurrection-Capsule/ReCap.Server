# ReCap.Server — AssetData.Parser Adoption Plan

Concrete, ordered implementation plan to adopt the post-refactor `AssetData.Parser` in `ReCap.Server`. Companion to [`ASSET_SYSTEM.md`](ASSET_SYSTEM.md) (problem statement + target architecture). This file is the **how/when**, mapped to real files and line numbers in the current `ReCap.Server` tree.

> **Submodule source.** `lib/AssetData.Parser` points to `JeanxPereira/AssetData.Parser` branch `refactor/game-faithful-parser` (commits `58c9f3a..cd84604`). Bump the submodule before running Phase 0.

---

## What changed upstream (and what breaks here)

The parser was rewritten around the game's reflection model (FNV-hash dispatch, lean L1/L2 split). Public surface changes that break the current server:

| Upstream change | Consumer impact |
|---|---|
| `AssetNode` and the `StructNode`/`StringNode`/… hierarchy **deleted** from Core; moved to `AssetData.Parser.Editor.Models` as `EditorNode` | Anything in `ReCap.Server` referencing `AssetData.Parser.AssetNode` / `StringNode` / `NumberNode` / `ArrayNode` no longer compiles. |
| `AssetNodeExtensions` (`.AsUInt32()`, `.AsFloat()`, `.AsVector3()`, `.AsString()`, …) **deleted** | All call sites lose the typed accessors. |
| `AssetNode["name"]` indexer and `.Elements` / `.Children` moved to L2 only | The indexer and the children-enumeration patterns used in the server are not present on the L1 `AssetValue`. |
| `AssetParser.Parse(...)` now returns `AssetData.Parser.Model.AssetValue` instead of `AssetNode` | Wrapper layers stop type-checking. |
| `cPlayerCrystal` → `labsCrystal` rename | Cosmetic; registry now reports 0 unresolved refs. |
| `.NET 10` + central package management in `Directory.Packages.props` | Server already targets net10; no action unless you want to align package versions via CPM here too. |

### Current breaking call sites in `ReCap.Server`

```
Services/AssetDatabase.cs            uses AssetNode + indexer + Parse(...)
Domain/Gameplay/ChainData.cs:42      levelAsset["planetConfig"]?.DisplayValue
Domain/Gameplay/ChainData.cs:50-95   planetConfig["minion"]?.Elements
                                     el["mpNoun"] as AssetData.Parser.StringNode
Domain/Gameplay/Game.cs:365-372      marker["nounDef"]?.AsUInt32()
                                     marker["pos"]?.AsVector3()
                                     marker["scale"]?.AsFloat()
```

That is the complete blast radius (`grep AssetNode|AsUInt|AsFloat|AsVector|\.Elements|\.DisplayValue ReCap.Server` returns nothing else).

---

## Phase 0 — Submodule bump + compile fix (minimum to ship)

Goal: bump the submodule, get `dotnet build` green again with **no behaviour change**. After Phase 0, gameplay works exactly like before — just on the new parser API.

### 0.1 Bump submodule

```
cd lib/AssetData.Parser
git fetch origin
git checkout refactor/game-faithful-parser
cd ../..
git add lib/AssetData.Parser
```

(Defer the commit until 0.2-0.5 are done — single "bump + adopt" commit.)

### 0.2 Replace consumer types

In `Services/AssetDatabase.cs`, `Domain/Gameplay/ChainData.cs`, `Domain/Gameplay/Game.cs`:

| Old | New |
|---|---|
| `using AssetData.Parser;` | `using AssetData.Parser;` plus `using AssetData.Parser.Model;` |
| `AssetNode` | `AssetValue` |
| `StringNode` | `StringValue` |
| `NumberNode` | `NumberValue` |
| `ArrayNode` | `ArrayValue` |
| `StructNode` | `StructValue` |
| `node["name"]` | walk `node.Children.FirstOrDefault(c => c.Name == "name")` or add a `FindByName` extension (0.4) |
| `node.Elements` | `(node as ArrayValue)?.Items` |
| `.DisplayValue` (used as primary key in `ChainData.cs:42`) | typed accessor — `(StringValue value).Value`. `DisplayValue` was an editor-only string; using it as a key was incidental. |
| `.AsUInt32()` | `(node as NumberValue)?.Value is double v ? (uint)v : 0u` (or use 0.4 helpers) |
| `.AsFloat()` | `(node as NumberValue)?.Value is double v ? (float)v : 0f` |
| `.AsVector3()` | `node is VectorValue v ? new Vector3(v.X, v.Y, v.Z) : Vector3.Zero` |
| `.AsString()` | `(node as StringValue)?.Value ?? ""` |

### 0.3 `AssetDatabase` shim

The current `AssetDatabase` (`Services/AssetDatabase.cs`) is the only consumer that holds an `AssetParser` directly. After Phase 0 its public signatures change from `AssetNode?` to `AssetValue?`. Keep the class shape; rename return types. The thin wrapper survives until Phase 8.

`GetLevelMarkers` becomes:

```csharp
public IEnumerable<AssetValue> GetLevelMarkers(string levelName)
{
    if (GetAsset(levelName) is not StructValue level) yield break;
    if (level.FindByName("markersets") is not ArrayValue markersets) yield break;

    foreach (var msRef in markersets.Items.OfType<StructValue>())
    {
        var assetId = (msRef.FindByName("markersetAsset") as NumberValue)?.Value;
        if (assetId is null or 0) continue;

        if (GetAssetById((ulong)assetId.Value, "markerset") is not StructValue ms) continue;
        if (ms.FindByName("markers") is not ArrayValue markers) continue;

        foreach (var marker in markers.Items)
            yield return marker;
    }
}
```

`Dictionary<string, AssetNode>` → `Dictionary<string, AssetValue>`. Cache key strategy unchanged.

### 0.4 Add a small `AssetValueExtensions` helper

To minimise call-site churn and re-introduce the typed-accessor ergonomics, add **one file** in `ReCap.Server/Services/Assets/AssetValueExtensions.cs`:

```csharp
using System.Numerics;
using AssetData.Parser.Model;

namespace ReCap.Server.Services.Assets;

internal static class AssetValueExtensions
{
    public static AssetValue? FindByName(this AssetValue? node, string name) =>
        node is null ? null : node.Children.FirstOrDefault(c => c.Name == name);

    public static uint    AsUInt32(this AssetValue? n) => n is NumberValue v ? (uint)v.Value : 0u;
    public static int     AsInt32 (this AssetValue? n) => n is NumberValue v ? (int)v.Value  : 0;
    public static ulong   AsUInt64(this AssetValue? n) => n is NumberValue v ? (ulong)v.Value : 0ul;
    public static float   AsFloat (this AssetValue? n) => n is NumberValue v ? (float)v.Value : 0f;
    public static bool    AsBool  (this AssetValue? n) => n is BoolValue   v && v.Value;
    public static string  AsString(this AssetValue? n) => n switch
    {
        StringValue s          => s.Value,
        LocalizedStringValue l => l.PrimaryValue,
        _ => string.Empty
    };
    public static Vector3 AsVector3(this AssetValue? n) =>
        n is VectorValue v ? new Vector3(v.X, v.Y, v.Z) : Vector3.Zero;
    public static Vector4 AsVector4(this AssetValue? n) =>
        n is VectorValue v ? new Vector4(v.X, v.Y, v.Z, v.W) : Vector4.Zero;
    public static Quaternion AsQuaternion(this AssetValue? n) =>
        n is VectorValue v ? new Quaternion(v.X, v.Y, v.Z, v.W) : Quaternion.Identity;
}
```

This mirrors the old `AssetNodeExtensions` API surface, on the new types, in the server (not in `lib/AssetData.Parser` — those extensions were deleted upstream because they were unused; the server is the one consumer that wanted them).

With this helper, `ChainData.cs:50-95` becomes almost line-identical to today — just `Elements` → `Items` on `ArrayValue`, and the `mpNoun as StringNode` cast becomes `mpNoun as StringValue`.

### 0.5 Fix the `DisplayValue`-as-key bug in `ChainData.cs:42`

```csharp
// before
var planetConfigKey = levelAsset["planetConfig"]?.DisplayValue;

// after
var planetConfigKey = (levelAsset.FindByName("planetConfig") as StringValue)?.Value;
```

`StringValue.Value` is the actual asset-reference path. `DisplayValue` happened to round-trip for `StringNode` but was always the wrong contract.

### 0.6 Sanity check

```
dotnet build ReCap.Server
dotnet test  ReCap.Tests
dotnet run --project ReCap.Server -- --assetdata-path=…/AssetData_Binary.package
# verify gameplay smoke: enter Dungeon, markers spawn as before
```

### 0.7 Commit

Single commit: `chore(server): bump AssetData.Parser submodule and adopt new API`. Optional second commit if 0.5 (`DisplayValue` fix) wants its own line in history.

**At this point: same behaviour, new API. Phase 1+ is the actual redesign.**

---

## Phases 1-8 — `ASSET_SYSTEM.md` redesign, concretized

These are the phases already specified in `ASSET_SYSTEM.md` §"Migration path". Below is the execution order with the concrete files each phase touches and a build-and-verify checkpoint per phase.

### Phase 1 — Robust `AssetDatabase` skeleton + warm-up scaffolding

| Action | File |
|---|---|
| Move current `AssetDatabase` to `Services/Assets/LegacyAssetDatabase.cs` (rename class) | `Services/AssetDatabase.cs` → `Services/Assets/LegacyAssetDatabase.cs` |
| Add new `AssetDatabase` facade per `ASSET_SYSTEM.md` §"Public facade" | new `Services/Assets/AssetDatabase.cs` |
| Add `EntryIndex` — `Dictionary<(uint typeHash, uint instanceId), DbpfEntry>` built once | new `Services/Assets/EntryIndex.cs` |
| Boot integration: call `AssetDatabase.WarmUpAsync(ct)` from `Program.cs` after `--assetdata-path` resolution | `Program.cs:~111` |
| DI: register `AssetDatabase` as singleton; `LegacyAssetDatabase` keeps the current registration so existing handlers don't break | `Program.cs` / DI configuration |

Verify: server boots, logs `"AssetDatabase ready, 0 catalogs indexed in N ms"` (warm-up is a no-op until Phase 2+ wires loaders). All existing handlers still go through `LegacyAssetDatabase` and behave identically.

### Phase 2 — `LevelDef` + `MarkerSetDef` + `PlanetConfigDef`

| Action | File |
|---|---|
| Add the three records per `ASSET_SYSTEM.md` §"Typed DTOs" | new `Services/Assets/Defs/{LevelDef,MarkerSetDef,PlanetConfigDef,MarkerDef,NounRef,MarkerSetRef}.cs` |
| Add `LevelLoader` — iterates DBPF entries of type `level`, pulls `planetConfig` + `markersets[*].markersetAsset`, materialises | new `Services/Assets/Loaders/LevelLoader.cs` |
| Wire into `AssetDatabase.WarmUpAsync` | `Services/Assets/AssetDatabase.cs` |
| Switch `ChainData.PopulateFromLevel` to consume `AssetDatabase.GetLevel(name)` instead of raw `AssetValue` walks | `Domain/Gameplay/ChainData.cs` |

After Phase 2: `ChainData.PopulateFromLevel` shrinks to a handful of lines — pull pre-computed `LevelDef.PlanetConfig.Minions` / `.Bosses` / `.Agents`, FNV-hash them into the right slot. No more in-handler tree walks. The `if (!nounNode.Value.Contains("_"))` heuristic moves into the loader.

Verify: same `EnemyNouns`/`LevelNouns` arrays after the first `ChainPlayerMsgs(1)` for each level as before.

### Phase 3 — Real `MarkerSetHash` from `LevelDef` (fixes PreDungeon stall)

| Action | File |
|---|---|
| Add `MarkerSetRef.NameHash` from the level XML (already in the `LevelDef` from Phase 2) | already done in Phase 2 |
| Replace `Chain.MarkerSet => FnvHash($"{LevelName}_ai_1.Markerset")` with `_db.GetLevel(LevelName)?.MarkerSets.First(m => m.Name.EndsWith("_ai_1")).NameHash ?? 0` | `Domain/Gameplay/ChainData.cs:101` |
| Pass that into `GamePrepareForStart` | `Domain/Gameplay/Game.cs:313` |

Verify: capture the `MarkerSet` bytes the C++ build emits for the same `--assetdata-path` and compare. If they match, PreDungeon stall closes.

### Phase 4 — `NounDef` catalog + typed `OnPlayerStart`

| Action | File |
|---|---|
| Add `NounDef` record per `ASSET_SYSTEM.md` §"Typed DTOs" | new `Services/Assets/Defs/NounDef.cs` |
| Add `NounLoader` — iterates DBPF entries of type `noun`, materialises | new `Services/Assets/Loaders/NounLoader.cs` |
| `AssetDatabase.GetNoun(uint id)` / `GetNounByName(string name)` | facade |
| Switch `Game.OnPlayerStart` markers loop to: pull marker positions from the already-indexed `LevelDef.MarkerSets[..].Loaded.Markers` and per-marker noun metadata from `_db.GetNoun(marker.Noun)` | `Domain/Gameplay/Game.cs:358-400` |

Verify: object count + noun IDs per dungeon entry match the current build.

### Phase 5 — `ClassAttributesDef` + `NonPlayerClassDef` + `PlayerClassDef` + `NpcAffixDef`

Removes hardcoded HP=200 / GearScore=300 defaults from `LabsPlayerUpdatePacket.cs` and `Game.cs:178-201`. Loaders mirror Phase 4. Materialise from `cClassAttributes`, `cNonPlayerClass`, `cPlayerClass`, `cNpcAffix` if those are present in the DBPF (check `FORMAT_COVERAGE.md` for current C# stub coverage).

If any of the source structs lack a stub in `lib/AssetData.Parser/src/Core/Catalog/`, that becomes a parallel TODO in the parser repo before Phase 5 can complete — track in [`FORMAT_COVERAGE.md`](FORMAT_COVERAGE.md).

### Phase 6 — `AIDefinitionDef` + minimal `ObjectManager.Update(delta)`

| Action | File |
|---|---|
| `AIDefinitionDef` loader | new `Services/Assets/Loaders/AIDefinitionLoader.cs` |
| `ObjectManager` scaffold — owns the live object dict, drives per-tick AI | new `Domain/Gameplay/ObjectManager.cs` |
| Wire from the game loop | `Domain/Gameplay/Game.cs` |

Out of scope: actual combat AI. The point of Phase 6 is the AI **plumbing** (load the definitions, attach to objects); behaviours land later.

### Phase 7 — Ability registry (Lua bridge or stub)

Per `ASSET_SYSTEM.md` open question #4 — either port the C++ Lua VM or stub abilities until a different mechanism replaces it. Recommended initial path: **stub** — `AbilityRegistry.GetAbility(id)` returns an `AbilityDef` placeholder with metadata only (name, cooldown, damage type), no execution. Real execution comes when combat lands.

### Phase 8 — Delete `LegacyAssetDatabase`

Once every consumer (`ChainData`, `Game.OnPlayerStart`, any new gameplay handler) routes through the typed `AssetDatabase` facade:

- Delete `Services/Assets/LegacyAssetDatabase.cs`
- Delete `Services/Assets/AssetValueExtensions.cs` (the Phase 0.4 helper) if nothing typed still uses it
- The DI graph now exposes only the robust `AssetDatabase`

---

## Sequencing summary

| Order | Phase | Risk | Effect |
|---|---|---|---|
| 1 | Phase 0 — submodule bump + compile fix | low | green build, same behaviour |
| 2 | Phase 1 — facade + EntryIndex + warm-up boot | low | new code, no consumers yet |
| 3 | Phase 2 — `LevelDef` | medium | `ChainData` rewritten, gameplay unchanged |
| 4 | Phase 3 — real `MarkerSetHash` | medium | **likely unblocks PreDungeon stall** |
| 5 | Phase 4 — `NounDef` | medium | `OnPlayerStart` rewritten |
| 6 | Phase 5 — `ClassAttributes` / `Npc*` | medium | hardcoded HP/GearScore replaced with real data |
| 7 | Phase 6 — `AIDefinition` plumbing | medium | enables future AI work |
| 8 | Phase 7 — ability registry stub | low | enables future combat work |
| 9 | Phase 8 — delete `LegacyAssetDatabase` | low | drops the thin wrapper |

Phase 0 is the gate; everything after that is incremental and independently shippable. Phases 3 and 5 are the highest-impact gameplay wins.

---

## Acceptance criteria

Reuses [`ASSET_SYSTEM.md` §"Acceptance criteria for robust"](ASSET_SYSTEM.md). Phase-by-phase gates here:

- **Phase 0**: `dotnet build` + `dotnet test` green; PreDungeon spawns the same set of markers (count + nouns) as before.
- **Phase 1**: boot log shows `"AssetDatabase ready, …"`; existing handlers still go through `LegacyAssetDatabase`.
- **Phase 2**: `ChainData.PopulateFromLevel` drops below 20 lines; level loads cached at warm-up; second `ChainPlayerMsgs(1)` for the same level returns in microseconds (no parser work).
- **Phase 3**: `MarkerSet` byte sequence in `GamePrepareForStart` matches the C++ build for the same `--assetdata-path` (capture both and `diff`).
- **Phase 4**: `OnPlayerStart` marker count + per-marker noun IDs unchanged vs Phase 0; the hardcoded path is gone.
- **Phase 5**: search `Game.cs LabsPlayerUpdatePacket.cs` for `200f` and `300f` — should return no matches (was the HP/GearScore default).
- **Phase 8**: `grep -r LegacyAssetDatabase ReCap.Server` returns no matches.

---

## Open questions (defer to design time)

These weren't resolved upstream and matter at Phase 4-5:

1. **Does the binary `noun` asset embed pointers to `NonPlayerClass`/`PlayerClass`/`AIDefinition` via raw `assetId` or via name strings?** If `assetId`, cross-reference resolution at warm-up is trivial (`EntryIndex` lookup). If name string, the resolver has to FNV-hash each one and the warm-up loop has one more pass. Resolve by inspecting a parsed `noun` tree once Phase 4 starts.
2. **`AssetDatabase` lifecycle on missing `--assetdata-path`.** Should boot proceed with `IsReady == false` and degraded handlers (current behaviour: `Chain.EnemyNouns` keeps hardcoded values), or refuse to start? Document the chosen contract in `Program.cs`.
3. **Locale**: catalog file is `catalog_{lang}.bin`. Currently `131 = en_us` works for retail. If we ever support multiple locales, decide whether to load all and index by locale, or follow a runtime config knob. Out of scope for Phase 0; revisit before Phase 5.
4. **Ability loading mechanism** (Lua bridge vs C# script vs static table). Decide before Phase 7.

---

## File-touch summary (Phase 0 only)

Files that change in Phase 0:

```
.gitmodules                                        (already pointing at refactor branch — verify)
lib/AssetData.Parser                               (submodule pointer bumped)
ReCap.Server/Services/AssetDatabase.cs             (rename types AssetNode → AssetValue, etc.)
ReCap.Server/Services/Assets/AssetValueExtensions.cs  NEW
ReCap.Server/Domain/Gameplay/ChainData.cs          (StringNode → StringValue, indexer → FindByName, DisplayValue fix)
ReCap.Server/Domain/Gameplay/Game.cs               (3 lines in OnPlayerStart — AsUInt32/AsFloat/AsVector3 from new helper)
```

That's the entire blast radius. After Phase 0, you can stop and ship — gameplay is identical, parser is new. Everything else is the redesign on top.
