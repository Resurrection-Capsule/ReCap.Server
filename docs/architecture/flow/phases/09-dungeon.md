# Phase 09 — Dungeon (`state = 0x06`)

After the client finishes loading (status=8), the server flips state to `Dungeon`. The client then sends a `DebugPing` while in Dungeon state. That ping triggers the largest single burst of packets in the protocol: `DirectorState`, `QuickGameMsgs`, `ObjectivesInitForLevel`, multiple `ObjectiveUpdated`, an `ObjectCreate` per level marker (NPCs, obelisks, teleporters), the hero `ObjectCreate`, `PlayerCharacterDeploy`, and the first `LabsPlayerUpdate` with `SwapCharacter`.

This phase is currently stubbed in the C# port — most of the C++ behaviour (marker scanning, enemy spawning, ability preload, Lua hooks) is absent.

```mermaid
sequenceDiagram
    autonumber
    participant C as Client
    participant R as RakNet server
    participant G as Game (state machine)
    participant I as Instance / ObjectManager
    participant Lvl as Level (markerset cache)
    participant Lua as GlobalLua

    Note over C: status=8 received → server flipped state to Dungeon
    R-->>C: 0xB1 GameStart (5 B, u32 BE levelIndex)
    R-->>C: 0xCC DebugPing (echo)

    C->>R: 0xCC DebugPing (in Dungeon state, body=u64 LE time)
    R->>G: HandleDebugPing → Dungeon branch
    G->>I: SendDirectorState(empty cAIDirector)
    R-->>C: 0x8B DirectorState (~16 B: boss=0, bbBossSpawned=false, raw WriteTo)

    G->>I: SendQuickGame (QuickGameMsgs body)
    R-->>C: 0xAF QuickGameMsgs

    G->>I: Instance::OnPlayerStart(player)
    I->>Lua: PreloadAbilities()
    I->>Lvl: LoadLevel()
    Lvl-->>I: Markerset cache populated
    loop per obelisk / design / wanderer marker
        I->>I: ObjectManager.Create(marker)
        I-->>C: 0x8C ObjectCreate (variable size)
    end
    I-->>C: 0xB7 ObjectivesInitForLevel (variable)
    loop per objective
        I-->>C: 0xB8 ObjectiveUpdated
    end
    loop per existing object
        I-->>C: 0x8C ObjectCreate (active objects)
    end
    loop 3× character (visible only current; force send all so the client can swap)
        I->>I: SetPosition / SetAttribute
        I-->>C: 0x8D ObjectUpdate
    end

    G->>I: Instance::SwapCharacter(player, 1)
    I->>I: Player.SwapCharacter(1) → dataBit 1, PlayerBits
    Note over R,C: next 50 ms tick
    R-->>C: 0xA1 LabsPlayerUpdate (small; updateBits=PlayerBits, dataBits {1})
    R-->>C: 0xA7 PlayerCharacterDeploy (5 B: u8 slotId, u32 BE objectId)
```

---

## State transitions feeding this phase

`PlayerStatusUpdate(status=8)` in Phase 08 triggers:

| Action | C++ | C# |
|---|---|---|
| `gameStateData.state = Dungeon` | `Server.cpp:670` | `Game.cs:350` |
| Send `GameStart(levelIndex)` | `SendGameStart` (`Server.cpp:2071-2087`) — uses `chainData.GetLevelIndex()` | `new GameStartPacket(0)` (`Game.cs:351`) — **hardcoded `0`** |
| Send `DebugPing` echo | `SendDebugPing` (`Server.cpp:~2400`) | `new DebugPingPacket()` (`Game.cs:352`) |

> ⚠️ Latent: C# `GameStartPacket(0)` hardcodes `levelIndex=0`. Replace with `Chain.LevelIndex`. Currently inert because the bug in Phase 08 prevents `status=8` from ever firing.

---

## `GameStart` (0xB1) — 5 B

```
u8  PacketID = 0xB1
u32 BE levelIndex
```

C++ (`Server.cpp:2071-2087`):

```cpp
if (client->GetGameState() != GameState::PreDungeon || !client->SetGameState(GameState::Dungeon)) {
    return;
}
Write<uint32_t>(outStream, chainData.GetLevelIndex());
```

C# (`GameStartPacket.cs` — minimal: writes `u32 BE LevelIndex`).

> The C++ helper enforces a state-machine guard (`PreDungeon → Dungeon`) and refuses to send if the state is wrong. C# has no equivalent guard.

---

## `DebugPing` (0xCC) — Dungeon branch

### C++ (`Server.cpp:1186-1204`)

```cpp
case GameState::Dungeon: {
    cAIDirector director;
    director.mBossId = 0;
    director.mbBossSpawned = false;

    SendDirectorState(client, director);
    SendQuickGame(client);
    mGame.OnPlayerStart(player);
    mGame.SwapCharacter(player, 1);
    break;
}
```

### C# (`Game.cs:281-286`)

```csharp
case GameState.Dungeon:
    sender.SendPacket(new DirectorStatePacket());
    sender.SendPacket(new QuickGameMsgsPacket());
    OnPlayerStart(sender);
    break;
```

> C# misses `SwapCharacter(player, 1)` entirely. C++ calls it to set the active deck index (`Player::SwapCharacter(1)`) which fires dataBit 1 + `PlayerBits` → next LPU tells the client which creature is deployed.

---

## `DirectorState` (0x8B)

C++ writes a raw `cAIDirector::WriteTo` (`Server.cpp:1411-1419`). Fields used: `mBossId = 0`, `mbBossSpawned = false`. Total body is small (the struct is mostly zero on a fresh director).

C# `DirectorStatePacket.cs` exists in the activator table (`PacketActivator.cs:142-143`) — to confirm the body layout matches C++ byte-for-byte, audit the C# implementation when reaching this point in the bug bisect.

---

## `QuickGameMsgs` (0xAF)

C++ `SendQuickGame` writes `QuickGameMsgs`. C# `QuickGameMsgsPacket` exists. Body bytes need a wire diff.

---

## `Instance::OnPlayerStart` — the big work item

C++ (`Instance.cpp:308-435`) does:

1. **`mLua->PreloadAbilities()`** — preloads Lua ability scripts for the current deck.
2. **`mGameStarted = LoadLevel()`** — loads the level's marker / config XML if not already cached.
3. **Obelisks** (`Instance.cpp:319-329`): pull `levelName + "_obelisk_1.Markerset"` from the level cache, for each marker matching `prefab_health_obelisk.Noun` or `prefab_boss_obelisk.Noun` call `mObjectManager->Create(marker)`.
4. **Design / spawners** (`Instance.cpp:331-343`): for `_design` and `_design_spawners` markersets, register markers; for any with `TeleporterData`, create them as objects.
5. **Wanderers / enemy spawn points** (`Instance.cpp:345-389`): for `_AI_Wander*` markersets, spawn one random enemy per `SpawnPoint_*` marker, position/rotation from the marker. Uses `mChainData.GetEnemyNoun(random(0..5))`.
6. **Objectives** (`Instance.cpp:396-401`): `SendObjectivesInitForLevel(client)` then `SendObjectiveUpdate` per objective.
7. **Object create flood** (`Instance.cpp:403-410`): for each active object in the object manager, `SendObjectCreate`.
8. **Character objects** (`Instance.cpp:413-434`): for each of 3 player characters, set spawnpoint, visibility, `InvisibleToSecurityTeleporters / AttackSpeedScale / CooldownScale` attributes; send `ObjectUpdate`.

### C# `Game.OnPlayerStart` (`Game.cs:358-436`)

1. Iterates `Assets.GetLevelMarkers($"{Chain.LevelName}.level")` (if `--assetdata-path` provided).
2. For each marker: builds an `ObjectCreatePacket` with the marker's `nounDef`, position, scale; `Team=2`, `HasCollision=true`, `PlayerControlled=false`.
3. Spawns the player hero at hardcoded `Vector3(44.0f, 0.47f, 17.5f)` with `creatureNoun = 1667741389u` (= `0x636B7CCD`), `Team=1`, `PlayerControlled=true`.
4. Sends `PlayerCharacterDeployPacket(player.Slot, objectId)`.

### Divergences

| Item | C++ | C# | Status |
|---|---|---|---|
| `LoadLevel` (XML / markerset cache populate) | yes (`Instance.cpp:312`) | indirect via `AssetData.Parser` lookups, no cache | ⚠️ |
| Lua ability preload | `mLua->PreloadAbilities` | absent | ❌ |
| Obelisk markers (`_obelisk_1`) | yes (filtered by noun) | not filtered — all markers in level go through | ⚠️ |
| Design markers (`_design`, `_design_spawners`) + teleporter creation | yes | not split out | ⚠️ |
| Enemy spawn from wanderer markersets | yes (random enemy per spawn point) | absent | ❌ |
| `ObjectivesInitForLevel` | yes (`Server.cpp:2198-2212`) | not sent in `OnPlayerStart` | ⚠️ |
| `ObjectiveUpdated` per objective | yes (`Server.cpp:2214-2231`) | absent | ❌ |
| `ObjectCreate` per active object | yes — flood | only per `Assets.GetLevelMarkers` result | ⚠️ |
| Hero `ObjectCreate` | from `CharacterObject[]` cached on `SetCharacter` | constructed inline with hardcoded creature noun + spawnpoint | ⚠️ |
| 3-character `ObjectUpdate` (visibility/attributes) | yes — all 3 characters even though only current is visible (so client can swap fast) | absent — only the deployed one is created | ❌ |
| `PlayerCharacterDeploy` | sent by Instance (`SendPlayerCharacterDeploy`) inside `SwapCharacter` flow | sent inline after hero `ObjectCreate` (`Game.cs:435`) | ✅ structurally same packet |
| `SwapCharacter(player, 1)` after `OnPlayerStart` | yes | **missing** | ⚠️ |

---

## `PlayerCharacterDeploy` (0xA7) — 5 B

```
u8  PacketID = 0xA7
u8  playerSlotId
u32 BE objectId
```

C++ (`Server.cpp:1421-1440`). C# (`PlayerCharacterDeployPacket.cs`). Layout is identical.

---

## `ObjectCreate` (0x8C)

Variable size. Body wraps a `GameObjectCreateData` (noun, position, scale, team, collision, player-controlled) and a `SporelabsObject` (position, team, visibility, scale, etc.).

C++ `SendObjectCreate` (`Server.cpp:1442-1499`) emits the full structure.
C# `ObjectCreatePacket` builds it in `Adapters/RakNet/Packets/ObjectCreatePacket.cs`.

### Packet body (M4-3 audit 2026-05-24)

```
u8  PacketID = 0x8C
u32 BE ObjectId
{ cGameObjectCreateData::WriteReflection — 10 fields, bm2 (2-byte BE bitmap) }
{ sporelabsObject::WriteReflection      — 23 fields, bmID (byte field-ID + 0xFF terminator) }
```

Both sides use **`WriteReflection`**, never the dense `WriteTo` form. The `WriteTo` methods on both `cGameObjectCreateData` and `sporelabsObject` are unused on the wire (kept for symmetry with the C++ source). The dense-buffer offsets (`0x70` for `cGameObjectCreateData`, `0x308` for `sporelabsObject`) are mirrored in C# but do not feed any send path.

### `cGameObjectCreateData::WriteReflection` (10 fields, bm2)

| Field ID | Name | Type | C++ writer | C# writer | Match? |
|---|---|---|---|---|---|
| 0 | `noun` | u32 BE | `reflector.write<0>(noun)` | `reflector.Write(0, () => writer.WriteBE(Noun))` | ✅ |
| 1 | `position` | vec3 BE (3 × f32 BE = 12 B) | `write<1>(position)` | `WriteBE(Position)` | ✅ |
| 2 | `rotXDegrees` | f32 BE | `write<2>(rotXDegrees)` | `WriteBE(RotXDegrees)` | ✅ |
| 3 | `rotYDegrees` | f32 BE | — | — | ✅ |
| 4 | `rotZDegrees` | f32 BE | — | — | ✅ |
| 5 | `assetId` | u64 BE | `write<5>(assetId)` | `WriteBE(AssetId)` | ✅ |
| 6 | `scale` | f32 BE | `write<6>(scale)` | `WriteBE(Scale)` | ✅ |
| 7 | `team` | u8 | `write<7>(team)` | `writer.Write(Team)` | ✅ |
| 8 | `hasCollision` | bool (u8) | `write<8>(hasCollision)` | `writer.Write(HasCollision)` | ✅ |
| 9 | `playerControlled` | bool (u8) | `write<9>(playerControlled)` | `writer.Write(PlayerControlled)` | ✅ |

Bitmap: 10 fields = 9-16 range → 2-byte BE bitmap (`reflection_serializer<10>` Begin writes `Write<uint16_t>` = BE, End rewrites at start; C# `ReflectionSerializer(_, 10)` `Begin` reserves `ushort`, `End` `WriteBE(_writeBits)` — match).

If all 10 fields set: bitmap = `0x03FF`, on wire `03 FF`.

### `sporelabsObject::WriteReflection` (23 fields, bmID)

23 > 16, so the bmID regime applies: each field emits its own byte-ID prefix, terminated by `0xFF`.

| Field ID | Name | Type | C++ writer (`Types.cpp:516-543`) | C# writer (`SporelabsObject.cs:89-119`) | Match? |
|---|---|---|---|---|---|
| 0 | `mTeam` | u8 | `write<0>(mTeam)` | `writer.Write(Team)` | ✅ |
| 1 | `mbPlayerControlled` | bool (u8) | `write<1>(mbPlayerControlled)` | `writer.Write(PlayerControlled)` | ✅ |
| 2 | `mInputSyncStamp` | u32 BE | `write<2>(mInputSyncStamp)` | `WriteBE(InputSyncStamp)` | ✅ |
| 3 | `mPlayerIdx` | u8 | `write<3>(mPlayerIdx)` | `writer.Write(PlayerIdx)` | ✅ |
| 4 | `mLinearVelocity` | vec3 BE | `write<4>(mLinearVelocity)` | `WriteBE(LinearVelocity)` | ✅ |
| 5 | `mAngularVelocity` | vec3 BE | `write<5>(mAngularVelocity)` | `WriteBE(AngularVelocity)` | ✅ |
| 6 | `mPosition` | vec3 BE | `write<6>(mPosition)` | `WriteBE(Position)` | ✅ |
| 7 | `mOrientation` | quat BE (4 × f32 BE = 16 B; order `x, y, z, w`) | `write<7>(mOrientation)` | `WriteBE(Orientation)` — C# `Quaternion.X, Y, Z, W` | ✅ |
| 8 | `mScale` | f32 BE | `write<8>(mScale)` | `WriteBE(Scale)` | ✅ |
| 9 | `mMarkerScale` | f32 BE | `write<9>(mMarkerScale)` | `WriteBE(MarkerScale)` | ✅ |
| 10 | `mLastAnimationState` | u32 BE | `write<10>(mLastAnimationState)` | `WriteBE(LastAnimationState)` | ✅ |
| 11 | `mLastAnimationPlayTimeMs` | u64 BE | `write<11>(mLastAnimationPlayTimeMs)` | `WriteBE(LastAnimationPlayTimeMs)` | ✅ |
| 12 | `mOverrideMoveIdleAnimationState` | u32 BE | `write<12>(...)` | `WriteBE(OverrideMoveIdleAnimationState)` | ✅ |
| 13 | `mGraphicsState` | u32 BE | `write<13>(mGraphicsState)` | `WriteBE(GraphicsState)` | ✅ |
| 14 | `mGraphicsStateStartTimeMs` | u64 BE | `write<14>(...)` | `WriteBE(GraphicsStateStartTimeMs)` | ✅ |
| 15 | `mNewGraphicsStateStartTimeMs` | u64 BE | `write<15>(...)` | `WriteBE(NewGraphicsStateStartTimeMs)` | ✅ |
| 16 | `mVisible` | bool (u8) | `write<16>(mVisible)` | `writer.Write(Visible)` | ✅ |
| 17 | `mbHasCollision` | bool (u8) | `write<17>(mbHasCollision)` | `writer.Write(HasCollision)` | ✅ |
| 18 | `mOwnerID` | u32 BE (tObjID = uint32_t per `Types.h:20`) | `write<18>(mOwnerID)` | `WriteBE(OwnerID)` | ✅ |
| 19 | `mMovementType` | u8 | `write<19>(mMovementType)` | `writer.Write(MovementType)` | ✅ |
| 20 | `mDisableRepulsion` | bool (u8) | `write<20>(mDisableRepulsion)` | `writer.Write(DisableRepulsion)` | ✅ |
| 21 | `mInteractableState` | u32 BE | `write<21>(mInteractableState)` | `WriteBE(InteractableState)` | ✅ |
| 22 | `sourceMarkerKey_markerId` | u32 BE | `write<22>(sourceMarkerKey_markerId)` | `WriteBE(SourceMarkerKeyMarkerId)` | ✅ |

`reflection_serializer<23>::end()` writes the `0xFF` terminator byte; C# `ReflectionSerializer.End()` (`ReflectionSerializer.cs:39-44`) writes `(byte)0xFF` when `_fieldCount > 16`. Match.

### Dense `WriteTo` offsets (parallel reference, unused on the wire)

For completeness — if a future send path switches from `WriteReflection` to `WriteTo`:

**`cGameObjectCreateData::WriteTo`** (size = `0x70` B). C++ `Types.cpp:393-413`, C# `GameObjectCreateData.cs:21-41`. Both write at identical offsets:

| Offset | Field | Type |
|---|---|---|
| `0x00` | `noun` | u32 BE |
| `0x04` | `position` | vec3 BE |
| `0x10` | `rotXDegrees` | f32 BE |
| `0x14` | `rotYDegrees` | f32 BE |
| `0x18` | `rotZDegrees` | f32 BE |
| `0x20` | `assetId` | u64 BE |
| `0x28` | `scale` | f32 BE |
| `0x2C` | `team` | u8 |
| `0x2D` | `hasCollision` | bool (u8) |
| `0x2E` | `playerControlled` | bool (u8) |
| `0x2F..0x6F` | gap (zero) | — |

**`sporelabsObject::WriteTo`** (size = `0x308` B). C++ `Types.cpp:461-514`, C# `SporelabsObject.cs:34-87`. All offsets match:

| Offset | Field | Type |
|---|---|---|
| `0x010` | `mScale` | f32 BE |
| `0x014` | `mMarkerScale` | f32 BE |
| `0x018` | `mPosition` | vec3 BE |
| `0x024` | `mOrientation` | quat BE |
| `0x034` | `mLinearVelocity` | vec3 BE |
| `0x040` | `mAngularVelocity` | vec3 BE |
| `0x050` | `mOwnerID` | u32 BE |
| `0x054` | `mTeam` | u8 |
| `0x055` | `mPlayerIdx` | u8 |
| `0x058` | `mInputSyncStamp` | u32 BE |
| `0x05C` | `mbPlayerControlled` | bool (u8) |
| `0x05F` | `mVisible` | bool (u8) |
| `0x060` | `mbHasCollision` | bool (u8) |
| `0x061` | `mMovementType` | u8 |
| `0x088` | `sourceMarkerKey_markerId` | u32 BE |
| `0x0AC` | `mLastAnimationState` | u32 BE |
| `0x0B8` | `mLastAnimationPlayTimeMs` | u64 BE |
| `0x0C0` | `mOverrideMoveIdleAnimationState` | u32 BE |
| `0x258` | `mGraphicsState` | u32 BE |
| `0x260` | `mGraphicsStateStartTimeMs` | u64 BE |
| `0x268` | `mNewGraphicsStateStartTimeMs` | u64 BE |
| `0x284` | `mDisableRepulsion` | bool (u8) |
| `0x288` | `mInteractableState` | u32 BE |
| `0x289..0x307` | gap (zero) | — |

### M4-3 verdict

**ObjectCreate is wire-identical between C++ and C#.** No divergences in `WriteReflection` field order, bm size selection, endianness, type widths, or vec/quat element order. Dense `WriteTo` paths also byte-aligned but currently unused on the wire (both sides ship `WriteReflection`).

---

## Parity table (Phase 09)

| Item | C++ | C# | Status |
|---|---|---|---|
| `GameStart` levelIndex | from `chainData.GetLevelIndex()` | hardcoded `0` | ⚠️ |
| State guard PreDungeon→Dungeon | enforced (`SendGameStart`) | none | ⚠️ |
| `DebugPing` echo after `GameStart` | yes | yes | ✅ |
| `DirectorState` body | empty `cAIDirector` raw `WriteTo` | `DirectorStatePacket` — verify body | ❓ |
| `QuickGameMsgs` body | server-side helper | `QuickGameMsgsPacket` — verify body | ❓ |
| `ObjectCreate` body (`cGameObjectCreateData::WriteReflection` + `sporelabsObject::WriteReflection`) | bm2 BE (10 fields) + bmID (23 fields, `0xFF` terminator), all multi-byte BE, quat `x,y,z,w` | identical (M4-3 audit 2026-05-24) | ✅ |
| `OnPlayerStart` workload | ~10 distinct side-effects | ~2 (marker loop + hero spawn) | ⚠️ |
| Lua ability preload | yes | absent | ❌ |
| `LoadLevel` + markerset cache | yes | inline `AssetData` lookup, no cache | ⚠️ |
| Spawn enemies from wanderer markers | yes | absent | ❌ |
| Objectives init + updates | yes (init + per-objective update) | absent | ❌ |
| Object flood (active objects) | yes | partial (only markers) | ⚠️ |
| Hero `ObjectCreate` + `ObjectUpdate × 3` | yes (force-create all 3 chars) | only deployed one | ⚠️ |
| `SwapCharacter(player, 1)` | yes — final step | **missing** | ⚠️ |
| `PlayerCharacterDeploy` | from `SwapCharacter` path | inline in `OnPlayerStart` | ⚠️ Different driver, same packet. |

---

## Open audit items

1. **Add `SwapCharacter(player, 1)` to C#.** Currently the player is deployed but the LPU never confirms the `CurrentDeckIndex = 1` change.
2. **`GameStart.levelIndex` hardcoded `0`.** Replace with `Chain.LevelIndex`.
3. **Implement `ObjectivesInitForLevel` + `ObjectiveUpdated` per objective.** Absent today.
4. **Object flood — create the 3 character objects on the wire** so the client can swap to any creature without a missing-object error.
5. **`DirectorState` byte-level audit.** Verify the C# `DirectorStatePacket` matches the C++ `cAIDirector::WriteTo` layout.
6. **`QuickGameMsgs` byte-level audit.** Same.
7. ~~**`ObjectCreate` parity.**~~ **Closed M4-3 (2026-05-24).** WriteReflection paths wire-identical: field order, bm size (10 → bm2 BE; 23 → bmID + `0xFF`), endianness (BE for multi-byte, raw u8 for bool/byte), quaternion element order (`x, y, z, w`). Dense `WriteTo` offsets also match on both sides but are unused on the wire. Full per-field diff in the section above.
8. **Lua / ability preload.** Out-of-scope for the immediate stall; required for combat to work.
9. **State guard.** Optional but worthwhile — refuse to send `GameStart` if the player is not in `PreDungeon`, matching C++ semantics.

---

## Files referenced

C++:

- `recap_server_develop/darkspore_server/source/RakNet/Server.cpp` (`OnDebugPing`, `SendGameStart`, `SendDirectorState`, `SendQuickGame`, `SendObjectivesInitForLevel`, `SendObjectiveUpdate`, `SendObjectCreate`, `SendPlayerCharacterDeploy`)
- `recap_server_develop/darkspore_server/source/Game/Instance.cpp` (`OnPlayerStart`, `SwapCharacter`)
- `recap_server_develop/darkspore_server/source/Game/Level.cpp` (`Markerset`, `LoadLevel`)
- `recap_server_develop/darkspore_server/source/Game/Lua.cpp` (`PreloadAbilities`)
- `recap_server_develop/darkspore_server/source/Game/Object.cpp` (`Object` lifecycle)

C#:

- `ReCap.Server/Domain/Gameplay/Game.cs` (`HandleDebugPing`, `OnPlayerStart`)
- `ReCap.Server/Adapters/RakNet/Packets/GameStartPacket.cs`
- `ReCap.Server/Adapters/RakNet/Packets/DirectorStatePacket.cs`
- `ReCap.Server/Adapters/RakNet/Packets/QuickGameMsgsPacket.cs`
- `ReCap.Server/Adapters/RakNet/Packets/ObjectCreatePacket.cs`
- `ReCap.Server/Adapters/RakNet/Packets/PlayerCharacterDeployPacket.cs`
- `ReCap.Server/Adapters/RakNet/Packets/ObjectivesInitForLevelPacket.cs`
- `ReCap.Server/Adapters/RakNet/Packets/ObjectiveUpdatedPacket.cs`
- `ReCap.Server/Services/AssetDatabase.cs`
