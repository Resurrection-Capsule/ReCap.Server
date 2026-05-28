# Asset Format Coverage — registered formats vs C# `AssetData.Parser`

Per-format porting matrix for the AssetData reflection system. Closes the "~15% gap" noted in [`GHIDRA_GROUND_TRUTH.md`](GHIDRA_GROUND_TRUTH.md): the runtime/parser is mapped, but the per-format stub coverage was never diffed.

> **Source of truth (registered formats):** callers of `AssetTypeRegistry::Register` (`0x009f4b40`) in `Darkspore.exe` — **143 callers**. Each is one `(AssetType::Foo + AssetData::Foo)` format stub.
> **C# side:** `lib/AssetData.Parser/src/Core/Catalog/` — 88 `Structures/*.cs` + 6 `GlobalTypes/*.cs` + `Catalog`/`CatalogEntry` core.
> **Generated:** 2026-05-26 via GhidraMCP.

## Summary

| | Count |
|---|---:|
| Registered formats (Register callers) | 143 |
| — infra (`Catalog`, `CatalogEntry`) | 2 |
| — test-only (`TestAsset`, `TestProcessedAsset`) | 2 |
| **Real gameplay formats** | **139** |
| C# types present | 94 |
| **Missing (real)** | **~45** |
| **Coverage** | **~68%** |

The 32% gap is not random — it is concentrated in the **typed gameplay defs** (creatures, classes, abilities, the `labs*` player/character/crystal triad). This is the same cluster the server-wide [`../PORTING_MATRIX.md`](../planning/PORTING_MATRIX.md) flags as "NounDatabase typed asset loading" — the dominant Game-module blocker.

---

## Missing formats — by family (priority order)

### Tier 1 — gameplay-critical (blocks creatures / combat / deploy)

> **Cross-check with the C++ reference.** dalkon's `NounDatabase` (`Game/Noun.h:695`) is a misnomer — `Noun` is 1 of 143 formats, but he named the DB after the first one he tackled. It hand-rolls **8 per-format loaders**: `Noun`, `NonPlayerClass`, `PlayerClass`, `NpcAffix`, `ClassAttributes`, `AIDefinition`, `CharacterAnimation`, `Phase`. That choice is the strongest signal of *which formats actually drive server behavior* — they all appear in Tier 1/2/3 below. Note ReCap C# correctly mirrors the **client** (one generic parser) instead of dalkon's 8 hand loaders, so these are data-only additions (`Structures/*.cs`), not new loader code.

| Format | Ghidra addr | Why it matters |
|---|---|---|
| `Noun` | `0x00f6d2b0` | Master game-object def. Every creature/NPC/prop. Spawning is hardcoded without it. |
| `PlayerClass` | `0x00f5bcd0` | Hero stat/class def. Needed for real deck loadout (vs hardcoded creature). |
| `NonPlayerClass` | `0x00f5d130` | NPC/enemy class def. |
| `ClassAttributes` | `0x00f5e310` | Base attribute block referenced by classes. |
| `ability` | `0x00f92530` | Ability def. Combat scripting entry point. |
| `Condition` | `0x00f943e0` | Ability/effect condition. |
| `affix` / `NPCAffix` | `0x00fa1d50` / `0x00f7e020` | Loot + elite affix defs. |
| `objective` | `0x00fa19d0` | Level objective def. |
| `labsPlayer` | `0x00f46870` | **The LPU struct.** Player reflection ground truth. |
| `labsCharacter` | `0x00f453f0` | Per-character (deck creature) reflection. |
| `labsCrystal` | `0x00f455e0` | Catalyst/crystal reflection. |
| `sporelabsObject` | `0x00f8f190` | Networked game object base. |
| `cGameObjectCreateData` | `0x00f8f9b0` | ObjectCreate payload (DarksporeGhidra-known struct). |

### Tier 2 — level / director / loot data
| Format | Ghidra addr |
|---|---|
| `Level` | `0x00f76db0` |
| `Markerset` | `0x00f74b20` |
| `LevelObjectives` | `0x00f757c0` |
| `ObjectExtents` | `0x00f67770` |
| `Phase` | `0x00f93ef0` |
| `SectionConfig` | `0x00f77ab0` |
| `DirectorTuning` | `0x00f78440` |
| `LootPreferences` | `0x00f5a090` |
| `LootPrefix` | `0x00f54630` |
| `LootRigblock` | `0x00f55480` |
| `LootSuffix` | `0x00f4e510` |
| `ServerEventDef` | `0x00f64310` |

### Tier 3 — tuning / globals (mostly scalar config)
| Format | Ghidra addr |
|---|---|
| `AIDefinition` | `0x00f93770` |
| `AffixTuning` | `0x00fa2010` |
| `CharacterAnimation` | `0x00f604c0` |
| `CharacterType` | `0x00f60de0` |
| `CrystalTuning` | `0x00f7f3a0` |
| `DifficultyTuning` | `0x00f6de10` |
| `EliteNPCGlobals` | `0x00f7e750` |
| `MagicNumbers` | `0x00f95aa0` |
| `NavPowerTuning` | `0x00f74740` |
| `PopupTip` | `0x00ebecb0` |
| `UnlocksTuning` | `0x00ec0280` |
| `WeaponTuning` | `0x00ec0990` |
| `cSplineCameraNodeData` | `0x00f9de10` |

---

## Present (94) — quick reference

**`GlobalTypes/` (6):** `cAssetProperty`, `cAssetPropertyList`, `cAssetQueryString`, `cKeyAsset`, `cLongDescription`, `cSPBoundingBox`.

**`Structures/` (88):** AudioTriggerDef, Cinematic, CollisionVolumeDef, CombatEvent, CombatantDef, CrystalDef, CrystalDropDef, CrystalLevel, EditorPrefs, EventListenerData, EventListenerDef, ExtentsCategory, GameObjectGfxStateTuning, Gfx, GravityForce, InteractableDef, LevelCameraSettings, LevelConfig, LevelKey, LevelMarkerset, LocomotionTuning, LootData, NavMeshLayer, OrbitDef, ProjectileDef, ServerEvent, SharedComponentData, SpaceshipSpawnPointDef, SpaceshipTuning, SpawnPointDef, SpawnTriggerDef, TeleporterDef, TriggerVolumeComponentDef, TriggerVolumeDef, TriggerVolumeEvents, UnlockDef, WeaponDef, DirectorBucket, DirectorClass, cAICondition, cAIDirector, cAINode, cAffixDifficultyTuning, cAgentBlackboard, cAnimatedData, cAnimatorData, cAttributeData, cAudioEventData, cCameraComponent, cCameraComponentData, cCinematicView, cCombatantData, cControllerState, cDecalData, cDoorDef, cEffectEventData, cEliteAffix, cGambitDefinition, cGameObjectGfxStateData, cGameObjectGfxStates, cGfxComponentDef, cGraphicsData, cGrassData, cHardpointInfo, cInteractableData, cLabsMarker, cLayerPrefs, cLineLightData, cLobParams, cLocomotionData, cLootData, cMapCameraData, cNewGfxState, cOccluderData, cParallelLightData, cPointLightData, cPressureSwitchDef, cProjectileParams, cSpaceshipCameraTuning, cSplineCameraData, cSplineCameraNodeBaseData, cSpotLightData, cSwitchDef, cThumbnailCaptureParameters, cToolPos, cVolumeDef, cWaterData, cWaterSimData.

---

## Path to 100%

1. Add the **Tier 1** stubs first — `Noun` + `PlayerClass`/`NonPlayerClass`/`ClassAttributes` + `ability` unlock real creature/combat loading. The `labs*` triad gives byte-level LPU ground truth (cross-check against the FROZEN LPU bits).
2. Each stub is mechanical: decompile `AssetData::Foo` (`0x00f…`) for the field-descriptor table, emit one `Structures/Foo.cs` mirroring field name/type/offset. No new parser code (the generic `DeserializeObject` already handles every wire shape).
3. Tier 2/3 are lower-value scalar config — port on demand when a level/loot feature needs them.
