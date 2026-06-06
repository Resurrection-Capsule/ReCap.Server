# LUA_REGISTRAR_TABLES

Extracted 2026-06-05 from Darkspore.exe retail via Ghidra MCP.
Image base 0x400000. All addresses include the base.

---

## 1. Boot Group Order

`LuaSystem::Initialize` @0x00a0c810 builds a 10-element stack array `local_34[0..9]` and
iterates it in index order (0→9), issuing one `AssetCatalog::GetInstance` vtable query
per element to fetch all Lua scripts belonging to that group.

| Index | Hash (LE u32) | Resolved name | Source |
|-------|--------------|---------------|--------|
| 0 | 0x3681d755 | `lua` | FNV verified |
| 1 | 0xda09176b | **UNRESOLVED** | — |
| 2 | 0xfc0ff8f5 | `modifiers` | FNV verified |
| 3 | 0xd2fcb262 | **UNRESOLVED** | — |
| 4 | 0x7153bbb1 | `abilities` | FNV verified |
| 5 | 0xd79fa88c | **UNRESOLVED** | — |
| 6 | 0xc130a42a | `behaviors` | FNV verified |
| 7 | 0xb2a79c5c | **UNRESOLVED** | — |
| 8 | 0xee84d09a | **UNRESOLVED** | — |
| 9 | 0x24f78aa1 | **UNRESOLVED** | — |

**FNV algorithm** (client variant — multiply THEN xor, NOT canonical FNV-1a):

```
hash = 0x811C9DC5
for each char c in s.toLower():
    hash = hash * 0x1000193
    hash ^= (byte)c
```

Reference: `lib/AssetData.Parser/src/Core/TypeModel/WireHash.cs::Fnv1a`.

The 6 unresolved hashes are asset-catalog group IDs. No binary string data matches them.
They cannot be resolved without the original Darkspore Lua asset package filenames.

---

## 2. RegisterLuaNamespace Call Sites

`RegisterLuaNamespace` @0x008f3620 signature: `(lua_State* L, const char* namespace, LuaModuleReg* regs, int count)`.
`LuaModuleReg` = 8 bytes: `char* name` + `void* fn`.

All namespaces registered via `LuaFunctions::RegisterLuaFunctions` @0x00a0c600 or called from it.
Arrays are stack-allocated (not static data).

### Registration order in `LuaFunctions::RegisterLuaFunctions`

1. `math` (inline, 1 fn)
2. `nThreadData` (via `nThreadData()` call @0x009f9d60)
3. `nTimeManager` (inline, 2 fns)
4. `nThread` (via `nThread()` call @0x00a0ab00)
5. `nBehaviorTree` (inline, 2 fns)
6. `nScenarioManager` (inline, 1 fn)
7. `nAbility` + `nModifier` + `nCondition` (via `nAbility()` @0x00a435c0)
8. `nPhysics` (via `nPhysics()` @0x00a044b0)
9. `nUtil` (via `nUtil()` @0x00a02220)
10. `nBit` (via `nBit()` @0x009fa3b0)
11. `nGameObject` (via `nGameObject()` @0x00a08bc0)
12. `nAttribute` (via `nAttribute()` @0x009ff090)
13. `nLocomotion` (via `nLocomotion()` @0x00a07bc0)
14. `nObjectManager` (via `nObjectManager()` @0x00a0bff0)
15. `nPlayer` (via `nPlayer()` @0x00a06890)
16. `nEvent` (via `nEvent()` @0x00a0c2e0)
17. `nAgent` (via `nAgent()` @0x00a05a90)
18. `nDebug` (inline, 1 fn)
19. `nMathUtil` (via `nMathUtil()` @0x00a09740)
20. `nGameDirector` (via `nGameDirector()` @0x00a00600)
21. `nGameSimulator` (via `nGameSimulator()` @0x00a071f0)
22. `nLevel` (inline, 2 fns)
23. `nObjective` (via `nObjective()` @0x00a0c3fb)
24. `nAffix` (inline, 3 fns)
25. `nJuggernaut` (inline, 3 fns — all bound to `LuaStub`)
26. `nTuning` (via `nTuning()` @0x00a01bc0)

Additionally, called from `LuaSystem::Initialize` @0x00a0c810 before `RegisterLuaFunctions`:
- `nClient` (via `nClient()` @0x00a01bd0)

---

## 3. Per-Namespace Function Tables

### math (1 fn) — inline in RegisterLuaFunctions

| # | Name |
|---|------|
| 1 | random |

### nThreadData (8 fns) — @0x009f9d60

| # | Name |
|---|------|
| 1 | GetInt |
| 2 | SetInt |
| 3 | GetGUID |
| 4 | SetGUID |
| 5 | GetFloat |
| 6 | SetFloat |
| 7 | CreatePrivateTable |
| 8 | GetPrivateTable |

### nTimeManager (2 fns) — inline in RegisterLuaFunctions

| # | Name |
|---|------|
| 1 | GetSimTimeDt |
| 2 | IsSimTimePaused |

### nThread (23 fns) — @0x00a0ab00

| # | Name |
|---|------|
| 1 | WaitForHitpointsAbove |
| 2 | WaitForControlOfAgent |
| 3 | WaitForJumpComplete |
| 4 | WaitForNearGoal |
| 5 | MoveTowardPoint |
| 6 | MoveTowardObject |
| 7 | WaitForXSeconds |
| 8 | WaitUntilTime |
| 9 | WaitForever |
| 10 | WaitForProjectile |
| 11 | WaitForLobbedProjectile |
| 12 | WaitForPlayerInRadius |
| 13 | WaitForPlayerOutOfRadius |
| 14 | CheckForPlayersNear |
| 15 | WaitForFadeOutInXSeconds |
| 16 | CreateThreadForObject |
| 17 | GetCurrentThreadID |
| 18 | GetDurationElapsed |
| 19 | WaitForNotAlive |
| 20 | WaitForValidTargetWithinRange |
| 21 | WaitForNotValidTarget |
| 22 | Sleep |
| 23 | WakeUp |

Count matches known value (0x17 = 23). ✓

### nBehaviorTree (2 fns) — inline in RegisterLuaFunctions

| # | Name |
|---|------|
| 1 | GetMyObjectID |
| 2 | GetTargetObjectID |

### nScenarioManager (1 fn) — inline in RegisterLuaFunctions

| # | Name |
|---|------|
| 1 | GetIsClient |

### nAbility (46 fns) — @0x00a435c0, count 0x2e

| # | Name |
|---|------|
| 1 | GetAbilityID |
| 2 | GetAbilityInstanceID |
| 3 | GetAbilityNamespace |
| 4 | GetAgentID |
| 5 | GetTargetID |
| 6 | GetTargetPosition |
| 7 | RegisterAbility |
| 8 | MarkForDelete |
| 9 | BindAgent |
| 10 | ReleaseAgent |
| 11 | CreateAbilityEvent |
| 12 | SendAbilityEvent |
| 13 | SetAbilityEventIntData |
| 14 | GetAbilityEventIntData |
| 15 | SetAbilityEventFloatData |
| 16 | GetAbilityEventFloatData |
| 17 | SetAbilityEventGUIDData |
| 18 | GetAbilityEventGUIDData |
| 19 | GetAbilityEventType |
| 20 | RequestAbility |
| 21 | IsAbilityRunning |
| 22 | IsControlledByAbility |
| 23 | GetRank |
| 24 | GetDescriptors |
| 25 | CheckDescriptors_AllMatch |
| 26 | CheckDescriptors_AnyMatch |
| 27 | CheckForModifierDescriptor |
| 28 | CheckForModifierDebuffDescriptor |
| 29 | IsAbilityInRange |
| 30 | IsAbilityAbleToHit |
| 31 | GetAgentAttributeSnapshot |
| 32 | RetakeAgentSnapshot |
| 33 | GetAbilityAttributeValue |
| 34 | PayCooldownAndMana |
| 35 | ResetAbilityCooldown |
| 36 | AddCooldownTime |
| 37 | RemoveCooldownTime |
| 38 | ScaleCooldownTime |
| 39 | GetProperty |
| 40 | TargetInRangeAtStart |
| 41 | CallFunctionInContext |
| 42 | PreloadAsset |
| 43 | PreloadModifier |
| 44 | PreloadAnimation |
| 45 | GetAnimationSequenceIndex |
| 46 | PlayAnimationSequence |

Count matches known value (0x2e = 46). ✓

### nModifier (32 fns) — @0x00a435c0 (second block), count 0x20

| # | Name |
|---|------|
| 1 | GetModifierInstanceID |
| 2 | IsModifierRunning |
| 3 | RequestModifier |
| 4 | MarkForDelete |
| 5 | GetFirstModifierByGUID |
| 6 | AgentHasModifierMatchingGUID |
| 7 | GetMyAgentID |
| 8 | GetMyInitiatorID |
| 9 | SetInitiatorID |
| 10 | GetInitiatorID |
| 11 | RegisterModifier |
| 12 | GetRank |
| 13 | GetModifierID |
| 14 | GetProperty |
| 15 | ResetDuration |
| 16 | IncrementStackCount |
| 17 | SetStackCount |
| 18 | GetMyStackCount |
| 19 | GetStackCount |
| 20 | GetFloatProperty |
| 21 | CheckDescriptors_AllMatch |
| 22 | CheckDescriptors_AnyMatch |
| 23 | CheckForModifierDescriptor |
| 24 | CheckForModifierDebuffDescriptor |
| 25 | GetModifiersMatchingDescriptor |
| 26 | GetModifiersMatchingDebuffDescriptor |
| 27 | GetModifierGUID |
| 28 | GetDuration |
| 29 | GetRemainingDuration |
| 30 | GetInitiatorAttributeSnapshot |
| 31 | CallFunctionInContext |
| 32 | PreloadAsset |

Count matches known value (0x20 = 32). ✓

### nCondition (2 fns) — @0x00a435c0 (third block), count 2

| # | Name |
|---|------|
| 1 | RegisterCondition |
| 2 | GetProperty |

### nPhysics (8 fns) — @0x00a044b0

| # | Name |
|---|------|
| 1 | AddPhysicsForObject |
| 2 | RemovePhysicsForObject |
| 3 | ApplyImpulse |
| 4 | IsDynamic |
| 5 | SetObjectAsCollidable |
| 6 | ForceClientUpdate |
| 7 | IsInLineOfSight |
| 8 | DistanceToTerrain |

### nUtil (7 fns) — @0x00a02220

| # | Name |
|---|------|
| 1 | SPID |
| 2 | ToGUID |
| 3 | GetBoolProperty |
| 4 | GetIntProperty |
| 5 | GetStringProperty |
| 6 | GetDataDirectory |
| 7 | GetAsset |

### nBit (7 fns) — @0x009fa3b0

| # | Name |
|---|------|
| 1 | Or |
| 2 | And |
| 3 | Not |
| 4 | Xor |
| 5 | Mask |
| 6 | LShift |
| 7 | RShift |

### nGameObject (125 fns) — @0x00a08bc0, count 0x7d

| # | Name |
|---|------|
| 1 | GetType |
| 2 | GetAssetID |
| 3 | GetAssetNameWithType |
| 4 | GetMarkerID |
| 5 | GetCenterPoint |
| 6 | GetPosition |
| 7 | GetGroundPosition |
| 8 | SetPosition |
| 9 | GetOverheadPosition |
| 10 | GetOrientation |
| 11 | SetOrientation |
| 12 | GetBaseOrientation |
| 13 | GetLinearVelocity |
| 14 | SetInitialDirection |
| 15 | SetLinearVelocity |
| 16 | GetAngularVelocity |
| 17 | SetAngularVelocity |
| 18 | GetCurrentSpeed |
| 19 | GetModifiedMoveSpeed |
| 20 | GetFacing |
| 21 | SetTargetPosition |
| 22 | GetRightDirection |
| 23 | GetUpDirection |
| 24 | GetFootprintRadius |
| 25 | GetScale |
| 26 | SetScale |
| 27 | GetGraphicsScale |
| 28 | IsInCombat |
| 29 | AddEffect |
| 30 | RemoveEffect |
| 31 | RemoveEffectIndex |
| 32 | SetAnimationState |
| 33 | SetOverlayAnimationState |
| 34 | SetAnimationStateToDeath |
| 35 | SetAnimationStateToAggro |
| 36 | SetAnimationStateToEnterPassiveIdle |
| 37 | SetAnimationStateToPreAggroIdle |
| 38 | SetOverrideMoveAnimationState |
| 39 | GetRandomAbilityAnimationState |
| 40 | GetDanceEmoteAnimationState |
| 41 | GetTauntEmoteAnimationState |
| 42 | ResetAnimationState |
| 43 | ResetOverlayAnimationState |
| 44 | ResetOverrideMoveAnimationState |
| 45 | SetGraphicsState |
| 46 | GetObjectDistance |
| 47 | PressSwitch |
| 48 | ToggleDoor |
| 49 | GetObjectDirection |
| 50 | GetObjectAngleRad |
| 51 | GetObjectAngleDeg |
| 52 | GetNPCRank |
| 53 | GetTargetID |
| 54 | SetTargetID |
| 55 | GetHitPoints |
| 56 | GetMaxHitPoints |
| 57 | GetWeaponDamage |
| 58 | TakeDamage |
| 59 | HealDamage |
| 60 | KillObject |
| 61 | FullHealObject |
| 62 | GetManaPoints |
| 63 | SetManaPoints |
| 64 | GetMaxManaPoints |
| 65 | MarkForDelete |
| 66 | IsMarkedForDelete |
| 67 | ValidateHostileTarget |
| 68 | ValidateFriendlyTarget |
| 69 | IsCombatant |
| 70 | IsAlive |
| 71 | IsVaporized |
| 72 | SetCorpseFadingAway |
| 73 | IsCorpseFadingAway |
| 74 | SetIsVisible |
| 75 | GetPositionWithinRangeOfObject |
| 76 | AddAggroForObject |
| 77 | GetAggroForObject |
| 78 | AlertObject |
| 79 | OverrideFirstAggro |
| 80 | IgnoreFirstAggro |
| 81 | GetTeam |
| 82 | SetTeam |
| 83 | GetOwnerID |
| 84 | SetOwnerID |
| 85 | SetAggroDelegate |
| 86 | GetAggroDelegate |
| 87 | SetAttributeDelegate |
| 88 | MirrorBaseAttributes |
| 89 | IsStimulusActive |
| 90 | IsModifierActive |
| 91 | IsChanneling |
| 92 | GetIsStealthed |
| 93 | SetStealthType |
| 94 | GetIsTargetable |
| 95 | SetIsTargetable |
| 96 | GetNPCType |
| 97 | GetRecentDamage |
| 98 | IncrementNumTimesUsed |
| 99 | HasInteractableUsesLeft |
| 100 | GetInteractibleUserData |
| 101 | DropObject |
| 102 | SetGravityForce |
| 103 | DropStuffForObject |
| 104 | TriggerFalseCollision |
| 105 | SetDeathTimer |
| 106 | GetCreatureType |
| 107 | HasAbilityWithDescriptor |
| 108 | SetAttributeSnapshot |
| 109 | GetAttributeSnapshot |
| 110 | GetHitDescriptors |
| 111 | GetHitDirection |
| 112 | UndeployPlayerObject |
| 113 | IsDNALoot |
| 114 | IsPartLoot |
| 115 | GetTeleporterDestination |
| 116 | SetNavCollision |
| 117 | IsSameSpecies |
| 118 | CompareIDs |
| 119 | AddDependency |
| 120 | GetTriggerOwner |
| 121 | DNAPickup |
| 122 | MarkCantDropLoot |
| 123 | MarkNotWorthXP |
| 124 | GetSharedAbilityOffset |
| 125 | MakeElite |

Count matches known value (0x7d = 125). ✓

### nAttribute (9 fns) — @0x009ff090

| # | Name |
|---|------|
| 1 | AddAttributeModifier |
| 2 | RemoveAttributeModifier |
| 3 | GetAttributeValue |
| 4 | GetPrimaryAttributeValue |
| 5 | GetAttributeValue_FromSnapshot |
| 6 | CreateAttributeSnapshot_FromObject |
| 7 | CreateAttributeSnapshot_FromSnapshot |
| 8 | RemoveAttributeSnapshot |
| 9 | GetHealthMultiplierForDifficulty |

### nLocomotion (28 fns) — @0x00a07bc0, count 0x1c

| # | Name |
|---|------|
| 1 | TeleportAndFace |
| 2 | TeleportObject |
| 3 | JumpInDirection |
| 4 | SlideToPoint |
| 5 | MoveToPointExact |
| 6 | MoveToCircleEdge |
| 7 | MoveToPointWithinRange |
| 8 | MoveToObject |
| 9 | FollowAsPet |
| 10 | ApplyExternalVelocity |
| 11 | ClearExternalVelocity |
| 12 | ClearTargetObject |
| 13 | FaceObjectDuringMove |
| 14 | Stop |
| 15 | GetClosestPosition |
| 16 | GetClosestPositionFromPoint |
| 17 | GetClosestSpawnPosition |
| 18 | IsPositionReachable |
| 19 | IsPositionReachableExact |
| 20 | GetClosestReachablePosition |
| 21 | FindBallRollPosition |
| 22 | FindGoodMeleePosition |
| 23 | DistanceToObstacle |
| 24 | GetTerrainHeight |
| 25 | TurnToFace |
| 26 | TurnToFaceTargetObject |
| 27 | MoveToPointWhileFacingTarget |
| 28 | WalkingDistance |

### nObjectManager (24 fns) — @0x00a0bff0, count 0x18

| # | Name |
|---|------|
| 1 | IsValidObject |
| 2 | GetNumGameObjects |
| 3 | GetFirstObjectIndex |
| 4 | GetNextObjectIndex |
| 5 | GetObjectIdByIndex |
| 6 | CreateObject |
| 7 | CreateCreature |
| 8 | AttachTriggerVolume |
| 9 | DetachTriggerVolume |
| 10 | CreateTriggerVolume |
| 11 | CreateTriggerVolumeBox |
| 12 | DestroyTriggerVolume |
| 13 | RemoveObjectByIndex |
| 14 | RemoveObjectById |
| 15 | RemoveAllObjects |
| 16 | GetObjectsOfType |
| 17 | GetObjectsOfOwner |
| 18 | RemoveObjectsOfType |
| 19 | GetObjectsInRadius |
| 20 | GetObjectsInRadius_SortedByDistance |
| 21 | GetObjectsInRadius_SortedByFurthestDistance |
| 22 | GetObjectsAlongLine |
| 23 | GetNumInteractableObjects |
| 24 | GetInteractableObjects |

### nPlayer (39 fns) — @0x00a06890, count 0x27

| # | Name |
|---|------|
| 1 | DeployFirstCreature |
| 2 | DeployNextCreature |
| 3 | ClearQueuedCreature |
| 4 | GetQueuedCreatureType |
| 5 | IsPlayerControlledObject |
| 6 | GetPlayerControlledObjects |
| 7 | GetPlayerIdForObject |
| 8 | GetPlayerIds |
| 9 | GetTotalNumberOfPlayers |
| 10 | GetPlayerControlledObjectID |
| 11 | IsCrystalSlotAvailable |
| 12 | PickupCrystal |
| 13 | DropCrystals |
| 14 | AddLootToPlayer |
| 15 | PlayersRollForLoot |
| 16 | PickUpResurrectOrb |
| 17 | IsOverdriveActive |
| 18 | CanUseOverdrive |
| 19 | CanUseAbility |
| 20 | StartOverdrive |
| 21 | LockCamera |
| 22 | UnlockCamera |
| 23 | UnlockSecondCreature |
| 24 | UnlockNextAbility |
| 25 | UnlockOverdrive |
| 26 | UnlockCrystals |
| 27 | GetCurrentDeckIndex |
| 28 | GetHealthOfCreatureAtDeckIndex |
| 29 | GetMaxHealthOfCreatureAtDeckIndex |
| 30 | GetManaOfCreatureAtDeckIndex |
| 31 | GetMaxManaOfCreatureAtDeckIndex |
| 32 | HasBeatenThisLevel |
| 33 | GetSimDataAsInt |
| 34 | SetSimDataAsInt |
| 35 | GetSimDataAsFloat |
| 36 | SetSimDataAsFloat |
| 37 | GetSimDataAsObjectID |
| 38 | SetSimDataAsObjectID |
| 39 | GetSquadPassiveGUIDs |

### nEvent (4 fns) — @0x00a0c2e0

| # | Name |
|---|------|
| 1 | Notify |
| 2 | NotifyPlayer |
| 3 | NotifyObjects |
| 4 | NotifyOptionalInteractibleEvent |

### nAgent (4 fns) — @0x00a05a90

| # | Name |
|---|------|
| 1 | GetTargetsOnAggroList |
| 2 | HasTargetsOnAggroList |
| 3 | GetBestTarget |
| 4 | InPerceptionCircle |

### nDebug (1 fn) — inline in RegisterLuaFunctions

| # | Name |
|---|------|
| 1 | IsAbilityDebugEnabled |

### nMathUtil (6 fns) — @0x00a09740

| # | Name |
|---|------|
| 1 | RotateVectorByAxisAngle |
| 2 | TransformVector |
| 3 | CircleIntersectsArc |
| 4 | GetQuaternionFromFacingAndPosition |
| 5 | DistanceToLine |
| 6 | ClosestPointOnLine |

### nGameDirector (7 fns) — @0x00a00600

| # | Name |
|---|------|
| 1 | IsBossDead |
| 2 | SetBossId |
| 3 | GetKillPercent |
| 4 | ActivateHordeSpawn |
| 5 | ActivateMinionSpawn |
| 6 | ActivateLieutenantSpawn |
| 7 | IsHordeActive |

### nGameSimulator (10 fns) — @0x00a071f0

| # | Name |
|---|------|
| 1 | GetGameTime |
| 2 | GetGameObjectiveCompletionTime |
| 3 | GetDifficulty |
| 4 | GetMajorDifficulty |
| 5 | GetMinorDifficulty |
| 6 | SendActionCancelMessage |
| 7 | StartCinematic |
| 8 | IsGameSidekicking |
| 9 | IsChainGame |
| 10 | SetFirstLootPickedUp |

### nLevel (2 fns) — inline in RegisterLuaFunctions

| # | Name |
|---|------|
| 1 | DoorOpen |
| 2 | DoorClose |

### nObjective (20 fns) — @0x00a0c3fb

| # | Name |
|---|------|
| 1 | SetObjectiveIntData |
| 2 | SetObjectiveIntDataFromSPID |
| 3 | GetObjectiveIntData |
| 4 | SetObjectiveFloatData |
| 5 | SetObjectiveFloatDataFromSPID |
| 6 | GetObjectiveFloatData |
| 7 | SetObjectiveGUIDData |
| 8 | SetObjectiveGUIDDataFromSPID |
| 9 | GetObjectiveGUIDData |
| 10 | CreateObjectiveEvent |
| 11 | SendObjectiveEvent |
| 12 | DestroyObjectiveEvent |
| 13 | SetObjectiveEventIntData |
| 14 | GetObjectiveEventIntData |
| 15 | SetObjectiveEventFloatData |
| 16 | GetObjectiveEventFloatData |
| 17 | SetObjectiveEventGUIDData |
| 18 | GetObjectiveEventGUIDData |
| 19 | CallFunctionInContext |
| 20 | GetRegisteredDestructibles |

Note: Ghidra's decompile shows the stack starting at pcStack_1c; the first entry name
(`SetObjectiveIntData`) is inferred from count arithmetic — pcStack_18 = name ptr,
pcStack_1c = fn ptr. Count 20 confirmed by stack range 0x18-0xb4 (20 × 8B pairs).

### nAffix (3 fns) — inline in RegisterLuaFunctions

| # | Name |
|---|------|
| 1 | RegisterAffix |
| 2 | IsAffixPointValid |
| 3 | SetAffixPointInvalid |

### nJuggernaut (3 fns, all LuaStub) — inline in RegisterLuaFunctions

| # | Name |
|---|------|
| 1 | ApplyJuggernaut |
| 2 | RemoveJuggernaut |
| 3 | ScoreJuggernautKill |

### nTuning (5 fns) — @0x00a01bc0

| # | Name |
|---|------|
| 1 | CriticalRatingPerCritPercent |
| 2 | DefenseRatingPerPercent |
| 3 | DodgePercentCap |
| 4 | ResistPercentCap |
| 5 | CrystalBonus |

### nClient (3 fns, all LuaStub) — @0x00a01bd0 — registered BEFORE RegisterLuaFunctions

| # | Name |
|---|------|
| 1 | DrawReticle_Circle |
| 2 | DrawReticle_PointBlankCircle |
| 3 | DrawReticle_Cone |

---

## 4. ID Representation (CRITICAL — Step 3)

### nUtil.SPID — @0x009f9e00

```c
undefined4 nUtil::SPID(undefined4 param_1)
{
  int iVar1;
  undefined4 uVar2;

  iVar1 = lua_tolstring(param_1, 1, 0);   // reads Lua string arg
  uVar2 = 0;
  if (iVar1 != 0) {
    uVar2 = HashFunctionn(iVar1, 0x811c9dc5, 1);  // FNV multiply-then-xor
  }
  lua_pushnumber(param_1, uVar2);           // ← pushes as lua_Number (float)
  return 1;
}
```

### nUtil.ToGUID — @0x009f9e40

```c
undefined4 nUtil::ToGUID(undefined4 param_1)
{
  char *_Str;
  ulong uVar1;

  _Str = (char *)lua_tolstring(param_1, 1, 0);
  uVar1 = strtoul(_Str, (char **)0x0, 0x10);   // parses hex string
  lua_pushnumber(param_1, uVar1);               // ← pushes as lua_Number (float)
  return 1;
}
```

### Consumer — ObjectManager::GetObjectFromLuaArg @0x009f9740

```c
void ObjectManager::GetObjectFromLuaArg(undefined4 param_1, undefined4 param_2)
{
  int iVar1;
  float10 fVar2;
  undefined4 local_8;

  iVar1 = lua_type(param_1, param_2);
  if (iVar1 == 3) {                               // LUA_TNUMBER (3)
    fVar2 = (float10)Lua::GetNumberArg();
    local_8 = (int)(longlong)ROUND(fVar2);        // ← rounds float → integer
  } else {
    local_8 = Lua::GetObjectIdArg(param_1, param_2);
  }
  ...
}
```

### Verdict: **lua_pushnumber (float64 Lua number), round-tripped via ROUND()**

Both `SPID` and `ToGUID` call `lua_pushnumber`, which in Lua 5.1 is `lua_Number = double`
(64-bit IEEE 754). The consumer reads back with `lua_type == LUA_TNUMBER` (3) and
`ROUND(float10)` to recover the integer.

**Precision analysis:**
- Lua 5.1 `lua_Number` is `double` (64-bit). A double has 53 bits of mantissa.
- uint32 hashes fit exactly (max 0xFFFFFFFF < 2^32 < 2^53). **No precision loss for uint32.**
- `ToGUID` parses 64-bit hex via `strtoul` into `ulong`, then `lua_pushnumber`. A 64-bit
  integer with high bits set WILL lose precision in a double. GUIDs wider than 53 bits will
  be rounded — only the lower 53 bits are exact.

**C# binding implications:**
- `PushId(uint32 id)` → `lua_pushnumber(L, id)` — safe, exact.
- `ReadId()` → `(uint)(long)Math.Round(lua_tonumber(L, n))` — safe for uint32.
- For GUID (64-bit): use `lua_pushlstring` with a hex string + `lua_tolstring`, matching
  what `ToGUID` expects on input. Do NOT push 64-bit integers as lua_Number.

---

## 5. Summary

| Namespace | Count | Registrar address |
|-----------|-------|-------------------|
| math | 1 | inline |
| nThreadData | 8 | 0x009f9d60 |
| nTimeManager | 2 | inline |
| nThread | 23 | 0x00a0ab00 |
| nBehaviorTree | 2 | inline |
| nScenarioManager | 1 | inline |
| nAbility | 46 | 0x00a435c0 |
| nModifier | 32 | 0x00a435c0 (second call) |
| nCondition | 2 | 0x00a435c0 (third call) |
| nPhysics | 8 | 0x00a044b0 |
| nUtil | 7 | 0x00a02220 |
| nBit | 7 | 0x009fa3b0 |
| nGameObject | 125 | 0x00a08bc0 |
| nAttribute | 9 | 0x009ff090 |
| nLocomotion | 28 | 0x00a07bc0 |
| nObjectManager | 24 | 0x00a0bff0 |
| nPlayer | 39 | 0x00a06890 |
| nEvent | 4 | 0x00a0c2e0 |
| nAgent | 4 | 0x00a05a90 |
| nDebug | 1 | inline |
| nMathUtil | 6 | 0x00a09740 |
| nGameDirector | 7 | 0x00a00600 |
| nGameSimulator | 10 | 0x00a071f0 |
| nLevel | 2 | inline |
| nObjective | 20 | 0x00a0c3fb |
| nAffix | 3 | inline |
| nJuggernaut | 3 | inline |
| nTuning | 5 | 0x00a01bc0 |
| nClient | 3 | 0x00a01bd0 |
| **Total** | **436** | |
