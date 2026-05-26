# 0x8C — ObjectCreate

| Direction | Size | Phase | Status |
|---|---|---|---|
| S→C | variable (typically 50-200 B) | [09 Dungeon](../phases/09-dungeon.md), [10 Gameloop](../phases/10-gameloop.md) | ⚠️ |

Spawns a new game object on the client side — markers, NPCs, hero creatures, loot, obelisks. Body is dense: `objectId` + a reflection-encoded `cGameObjectCreateData` + the object's own `WriteReflection`.

---

## Body layout

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `objectId` | u32 | **BE** | Server-assigned. |
| `0x04` | `cGameObjectCreateData` reflection | reflection | mixed | Contains: `noun`, `position` (vec3 BE), `rotXDegrees/rotYDegrees/rotZDegrees` (3× f32), `assetId`, `scale` (f32), `team` (u8), `hasCollision` (bool), `playerControlled` (bool). |
| `0x??` | object body reflection | reflection | mixed | `Object::WriteReflection` → SporelabsObject fields. |

Variable size; both `WriteReflection` calls emit bitmap + present fields per [REFLECTION_SERIALIZER.md](../REFLECTION_SERIALIZER.md).

---

## C++ writer

`recap_server_develop/darkspore_server/source/RakNet/Server.cpp:1442-1499`:

```cpp
void Server::SendObjectCreate(const ClientPtr& client, const Game::ObjectPtr& object) {
    if (!DBG_SEND_OBJECT_SPAWNS) return;
    if (!object) return;

    const auto& euler = glm::eulerAngles(object->GetOrientation());

    BitStream outStream(8);
    outStream.Write(PacketID::ObjectCreate);
    Write<uint32_t>(outStream, object->GetId());

    cGameObjectCreateData createData;
    createData.noun = object->GetNounId();
    createData.position = object->GetPosition();
    createData.rotXDegrees = euler.x;
    createData.rotYDegrees = euler.y;
    createData.rotZDegrees = euler.z;
    createData.assetId = object->GetAssetId();
    createData.scale = object->GetScale();
    createData.team = object->GetTeam();
    createData.hasCollision = object->HasCollision();
    createData.playerControlled = object->IsPlayerControlled();

    createData.WriteReflection(outStream);
    object->WriteReflection(outStream);

    Send(outStream, client);
    LogPacketSize("ObjectCreate", outStream);
}
```

Gated by `DBG_SEND_OBJECT_SPAWNS` — a compile-time flag. When disabled, `SendObjectCreate` no-ops entirely.

---

## C# packet class

`ReCap.Server/Adapters/RakNet/Packets/ObjectCreatePacket.cs` — file exists, body mirrors C++ via inline `GameObjectCreateData` + `SporelabsObject` field blocks built in `Game.OnPlayerStart` (`Game.cs:374-398`).

C# emits a curated subset — only `markers` from the level + a hardcoded hero spawn. Loot and dynamic spawns are absent (Phase 09 audit).

---

## Open audit items

1. **Byte-level diff vs C++** for `cGameObjectCreateData` reflection layout. Verify bitmap regime (likely `<10>` ⇒ bm2) and field order.
2. **`SporelabsObject` reflection schema.** C++ `Object::WriteReflection` enumerates `mDataBits`; C# version unconfirmed.
3. **Rotation encoding.** C++ writes euler angles as 3× f32 BE. Confirm C# does the same and doesn't accidentally send a quaternion.
4. **`DBG_SEND_OBJECT_SPAWNS` parity.** C# has no equivalent compile-time flag — always sends. Decide if that's the right default.
5. **Active-object flood on Phase 09 entry.** C++ broadcasts every active object after `OnPlayerStart`; C# only sends markers + hero. Implement the flood once the object manager exists.

---

## Related

- [Phase 09 Dungeon](../phases/09-dungeon.md) — marker + hero spawn flow
- [Phase 10 Gameloop](../phases/10-gameloop.md) — dynamic spawns (loot, enemies)
- [0x8D ObjectUpdate](0x8D-objectupdate.md) — same `Object::WriteReflection` body
- [0x8E ObjectDelete](0x8E-objectdelete.md) — symmetric destroy
- [REFLECTION_SERIALIZER.md](../REFLECTION_SERIALIZER.md)
