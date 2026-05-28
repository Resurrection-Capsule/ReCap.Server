# Simulation (`n*`) — Ghidra Mapping

Living map of the Darkspore client's **Simulation** subsystem (combat / AI / objects / abilities) reverse-engineered in Ghidra. This is the dominant gameplay gap for ReCap (`PORTING_MATRIX.md`: Game module ~25%). Source of truth: `Darkspore.exe`.

> Method: the **Lua binding registrars** (`LuaFunctions::nXxx`, named by JeanxPereira) are a Rosetta Stone — each pairs a **method-name string** with the **C++ impl function pointer**. They give the entire scriptable API named for free; RE work targets the private impls/helpers below them.

---

## Attack method (validated)

1. `LuaFunctions::RegisterLuaFunctions` (`0x00a0c600`) is the master index — calls every namespace registrar.
2. Each `LuaFunctions::nXxx` registrar decompiles to a clean `{ "MethodName" → ::nXxx::MethodName }` table.
3. **`nXxx::Method` = thin Lua glue** (already named). It calls:
   - a shared **TLS sim-context getter** (`Simulation::GetThreadContext`, `0x009c10e0`), then
   - the **real C++ impl** (`FUN_…`, clustered by class — the actual RE target).
4. Lua stack helpers (`lua_pushnumber`/`tonumber` wrappers) recur everywhere — name once.

> Secondary attack for non-Lua-bound internals: string search (error messages, asset-type names, debug strings).

---

## Scriptable namespace index (26 — from `RegisterLuaFunctions`)

| Cluster | Namespaces |
|---|---|
| **Core objects** | `nGameObject` (0xa08bc0), `nObjectManager` (0xa0bff0), `nGameSimulator` (0xa071f0) |
| **Combat** | `nAbility` (0xa435c0), `nAttribute` (0x9feff0), `nEvent` (0xa0c2e0), `nAffix` (RegisterAffix/IsAffixPointValid/SetAffixPointInvalid) |
| **AI / director** | `nAgent` (0xa05a90), `nBehaviorTree` (GetMyObjectID/GetTargetObjectID), `nGameDirector` (0xa00600), `nScenarioManager` (GetIsClient) |
| **World / movement** | `nLocomotion` (0xa07bc0), `nPhysics` (0xa04420), `nLevel` (DoorOpen/DoorClose), `nObjective` (0xa0c3fb) |
| **Player / infra** | `nPlayer` (0xa06890), `nTimeManager` (GetSimTimeDt/IsSimTimePaused), `nThread` (0xa0ab00), `nThreadData` (0x9f9d60), `nTuning` (0xa01b60), `nUtil` (0xa021a0), `nBit` (0x9fa330), `nMathUtil` (0xa09740), `nDebug` (IsAbilityDebugEnabled), `nClient` (0xa01bd0), `math` (Math::Random) |
| **Cut/stubbed** | `nJuggernaut` (ApplyJuggernaut/RemoveJuggernaut/ScoreJuggernautKill → all `LuaStub`) — disabled PvP "Juggernaut" mode |

---

## Naming convention (enforced by the Ghidra MCP linter)

Discovered during the pilot — **all mapping work must follow this**:

- **Hard rule (blocks):** no name collisions. The linter rejects *token-subset duplicates* (e.g. `GetSimContextFromTLS` rejected for colliding with a function named `Get`). Names need distinguishing tokens.
- **Form:** `Namespace::MethodName` works and gives namespaced display. Use `nXxx::` only for the Lua glue (already done); use the **real C++ class** name for impls (e.g. `ObjectManager::`, `GameObject::`, `Simulation::`).
- **Advisory (warns, does not block):** PascalCase, no underscores, start with a recognized verb (Get/Set/Initialize/Process/Calculate/Find/Check/Handle). With the `::` form the linter mis-parses the prefix and warns anyway — ignore those warnings.
- **Plate comments** want Algorithm / Parameters / Returns sections (advisory).

---

## Architecture findings (pilot: nObjectManager)

- **Thread-local sim context:** `Simulation::GetThreadContext` (`0x009c10e0`) reads `TLS[_tls_index] → +0x08 → +0xCC` = the per-thread simulation root. All `nObjectManager` glue calls it first.
- **ObjectManager layout:** `ctx+0x04` → object container; `container+0x0C` → object count.
- **Pattern confirmed:** `nObjectManager::GetNumGameObjects` (Lua glue) → `ObjectManager::GetNumGameObjects` impl (`0x009d6710`, one-liner `*(mgr+4)+0xC`).

---

## Mapped functions (running log)

| Address | Name | Notes |
|---|---|---|
| `0x009c10e0` | `Simulation::GetThreadContext` | TLS sim-context getter (`TLS→+8→+0xCC`); shared prologue |
| `0x009d6700` | `ObjectManager::GetObjectById` | id → object ptr (null = invalid) |
| `0x009d6710` | `ObjectManager::GetNumGameObjects` | `*(mgr+4)→+0xC` = count |
| `0x009d6720` | `ObjectManager::GetFirstObjectIndex` | impl |
| `0x009d6730` | `ObjectManager::GetObjectIdByIndex` | impl; returns ptr to object id |
| `0x009d6de0` | `ObjectManager::CreateObjectInternal` | ctx-based low-level spawn |
| `0x009db960` | `ObjectManager::CreateCreature` | builds spawn params (pos/orient/scale/nameHash) → `CreateObjectInternal` |
| `0x009f9740` | `ObjectManager::GetObjectFromLuaArg` | reads id from lua arg (number or userdata) → `GetObjectById` |
| `0x008f6320` | `Lua::GetNumberArg` | shared lua-stack helper (float/number arg) |
| `0x008f64f0` | `Lua::GetObjectIdArg` | shared (integer/id arg) |
| `0x008f65f0` | `Lua::PushNumber` | shared |
| `0x008f67b0` | `Lua::PushBoolean` | shared |

| `0x009d6ac0` | `ObjectManager::RemoveObject` | removal impl (used by all Remove* paths) |
| `0x00a1bd00` | `ObjectManager::DestroyTriggerVolume` | trigger destroy by handle/id |
| `0x00a1c190` | `ObjectManager::CreateTriggerVolumeSphere` | sphere trigger create impl |
| `0x00a1c060` | `ObjectManager::CreateTriggerVolumeBox` | box trigger create impl |
| `0x009d9300` | `ObjectManager::QueryObjectsInRadius` | spatial query → fills id array, returns count |

### nGameObject layout → **Ghidra struct `nGameObject` (created, 720 B / 0x2D0)**

Captured as a real Ghidra data type so decompilation reads `obj->ownerId` instead of `*(undefined4*)(obj+0x14)`. Created as a **partial/growing struct** — known fields named, gaps left `undefined` to fill in as more is mapped.

| Offset | Field (in struct) | Conf. | Evidence |
|---|---|---|---|
| `+0x00` | `uint id` | high | `*objPtr` pushed as id everywhere |
| `+0x04` | `void* pNoun` (type/noun) | high | type-filter via `thunk_FUN_009cdbc0(obj+4)` |
| `+0x44` (`[0x11]`) | `void* pTriggerVolume` | med | `DetachTriggerVolume` frees `obj[0x11]` |
| `+0x50` (`[0x14]`) | `uint ownerId` | high | `GetObjectsOfOwner` matches `obj[0x14]` |
| `+0x2CC` (`[0xB3]`) | `void* pScaleComponent` | med | `CreateCreature` sets `obj[0xB3]+0x40` = float |

**Type-application practice (for the bucket work):** as functions are mapped, set prototypes so the struct propagates — e.g. `GetObjectById` (`0x009d6700`) now returns `nGameObject*`. Each applied prototype makes every consumer's decompilation self-documenting and reveals new field offsets. Refine `pNoun`/`pTriggerVolume`/`pScaleComponent` to concrete struct types once those subsystems are mapped.

### Creature spawn pipeline (decoded)
Lua `nObjectManager.CreateCreature(idTable, "name", x,y,z, [arg6], [tx,ty,tz], [scale])`:
1. glue `nObjectManager::CreateCreature` (`0x00a05500`) marshals args (name → hash via `FUN_00ae4490`, positions via `Lua::GetNumberArg`).
2. → `ObjectManager::CreateCreature` (`0x009db960`) builds the spawn-params struct (position, orientation, scale=`param4`, default-pos fallbacks).
3. → `Simulation::GetThreadContext` + `ObjectManager::CreateObjectInternal` (`0x009d6de0`) = actual insertion.
4. returns object id; optional arg10 sets a float at `object[0xB3]+0x40` (scale/health-like component).

> Still `FUN_` (uncertain, left unnamed by design): `FUN_009c1050` (2nd context/transform getter), `FUN_00ae4490` (string hash — likely FNV, verify), `FUN_009c10b0` (radius-query setup), `thunk_FUN_009cdbc0` (type-id getter from obj+4), `FUN_009dd940`/`FUN_009dd970` (interactable subsystem), `FUN_00a28940`/`FUN_00a28a10` (attach/detach internals).

### nObjectManager — status: CLOSED (pilot complete)
- **15 functions named** across `ObjectManager::` (lifecycle, spatial, triggers) + `Simulation::`/`Lua::` shared helpers.
- Lifecycle (CreateObject/CreateCreature/RemoveObject*/RemoveAll/RemoveOfType), iteration (First/Next/ById/Count), validity, spatial (QueryObjectsInRadius), triggers (sphere/box create, destroy) all mapped.
- `nObjectManager::CreateObject` and `::CreateCreature` glue both route to the shared spawn core `ObjectManager::CreateCreature` (`0x009db960`) → `CreateObjectInternal` (`0x009d6de0`).
- Remaining glue (GetObjectsOfType, GetObjectsInRadius_Sorted*, GetObjectsAlongLine, GetInteractableObjects, Attach/CreateTriggerVolume) are **variants reusing the mapped query/iterator/spawn impls** — no new core to map.

**Pilot conclusion:** workflow, naming convention, and Rosetta Stone all validated end-to-end on a full namespace. Ready to scale to the remaining buckets.

---

## Bucket 1 — Core objects (in progress)

### nObjectManager — ✅ CLOSED (see above)

### nGameObject — API harvested (125 methods), accessors being mapped
The full per-object gameplay API (`LuaFunctions::nGameObject` `0x00a08bc0`, **0x7D=125 methods**, all glue named by JeanxPereira). Method families:
- **Transform:** GetPosition/SetPosition, GetOrientation/SetOrientation, GetLinearVelocity/SetLinearVelocity, GetScale/SetScale, GetFacing, GetCurrentSpeed, GetFootprintRadius, GetGroundPosition, GetCenterPoint…
- **Combat/vitals:** GetHitPoints/GetMaxHitPoints, GetManaPoints/SetManaPoints/GetMaxManaPoints, GetWeaponDamage, TakeDamage, HealDamage, KillObject, FullHealObject, IsInCombat, IsAlive, IsVaporized, GetRecentDamage…
- **AI/aggro/identity:** GetTeam/SetTeam, GetOwnerID/SetOwnerID, GetTargetID/SetTargetID, AddAggroForObject/GetAggroForObject, AlertObject, GetNPCRank, GetNPCType, GetCreatureType…
- **Animation:** SetAnimationState(+ToDeath/Aggro/PassiveIdle…), overlay/override states, emote/ability anim getters…
- **Effects/modifiers/attributes:** AddEffect/RemoveEffect, IsModifierActive, IsChanneling, IsStimulusActive, Get/SetAttributeSnapshot, MirrorBaseAttributes, SetAttributeDelegate…
- **Loot/interactable:** IsDNALoot/IsPartLoot, DropObject, DNAPickup, HasInteractableUsesLeft, MarkCantDropLoot, MakeElite…

**Named accessor impls (`GameObject::`):**
| Address | Name | Reveals |
|---|---|---|
| `0x009dde90` | `GameObject::GetHitPoints` | HP via combatant component |
| `0x009ddd20` | `GameObject::GetMaxHitPoints` | (also clamps starting HP in CreateCreature) |
| `0x009ddeb0` | `GameObject::GetManaPoints` | |
| `0x009ddd70` | `GameObject::GetMaxManaPoints` | |
| `0x009e8d60` | `GameObject::GetTargetId` | |
| `0x008f65d0` | `Lua::PushFloat` | shared (distinct from `Lua::PushNumber`) |

> Health/mana live in the **combatant component** (`obj+0x2CC`, `+0x40` = current HP), not at the object root — accessed via the `009ddexx`/`009dddxx` getter cluster. Remaining 119 nGameObject glue methods → impls are the next grind (transform, animation, effects, aggro), each filling more of the struct/components.

### nGameObject struct — updated
Added `bTeam @ 0x54` (u8); renamed `pScaleComponent` → `pCombatant` (`@0x2CC`, holds HP at +0x40). Confirmed `ownerId @ 0x50`.

### nGameSimulator — API harvested (10 methods)
`0x00a071f0`: GetGameTime, GetGameObjectiveCompletionTime, GetDifficulty/GetMajorDifficulty/GetMinorDifficulty, SendActionCancelMessage, StartCinematic, IsGameSidekicking, IsChainGame, SetFirstLootPickedUp. Impls = next.

---

## Scaling plan (after pilot)

Per-namespace agent workflow:
1. Decompile `LuaFunctions::nXxx` → harvest `{name → impl addr}` table.
2. For each impl `FUN_`: decompile, name as `RealClass::Method` (per convention), set plate comment, set prototype.
3. Name shared helpers once (TLS context, lua stack helpers).
4. Cross-reference dalkon's C++ `Game/` (ObjectManager, Locomotion, Ability, Noun) as Rosetta Stone #2.
5. Return a compact report → appended to the "Mapped functions" log here.

Partition (5 buckets): core-objects · combat · AI/director · world/movement · player/infra. Execution per `ARCHITECTURE_OPEN_QUESTIONS.md` Q2 — pilot done; choose parallel strategy next.
