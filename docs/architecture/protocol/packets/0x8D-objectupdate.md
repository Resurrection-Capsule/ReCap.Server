# 0x8D — ObjectUpdate

| Direction | Size | Phase | Status |
|---|---|---|---|
| S→C | variable | [09 Dungeon](../../flow/phases/09-dungeon.md), [10 Gameloop](../../flow/phases/10-gameloop.md) | ⚠️ |

Delta update of an existing object's state. Same `Object::WriteReflection` payload as [0x8C ObjectCreate](0x8C-objectcreate.md), but no `cGameObjectCreateData` prefix — the client already has the create record.

---

## Body layout

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `objectId` | u32 | **BE** | Target object. |
| `0x04` | `Object::WriteReflection` | reflection | mixed | Only fields with `mDataBits` set are emitted. |

Variable size depending on how many bits set since last reset.

---

## C++ writer

`recap_server_develop/darkspore_server/source/RakNet/Server.cpp:1501-1516`:

```cpp
void Server::SendObjectUpdate(const ClientPtr& client, const Game::ObjectPtr& object) {
    if (!object) return;

    BitStream outStream(8);
    outStream.Write(PacketID::ObjectUpdate);
    Write<uint32_t>(outStream, object->GetId());
    object->WriteReflection(outStream);

    // 0x15 data in loop (comment — implementation deferred)

    Send(outStream, client);
}
```

The trailing comment hints at a `0x15`-field-id loop the author intended to add (probably per-attribute data). Currently absent.

---

## C# packet class

`ReCap.Server/Adapters/RakNet/Packets/ObjectUpdatePacket.cs` — file exists. Partial implementation: matches C++ shape for the fields currently sent, but `SporelabsObject` reflection schema may diverge in field set.

---

## Open audit items

1. **Audit `Object::WriteReflection` field IDs** on both sides. Field ID 13 in C++ corresponds to ... ? Need a per-field table.
2. **Per-Character `_dataBits` clear.** C# `Player.ResetUpdateBits` doesn't reset the nested `LabsCharacterData._dataBits`. Same bug shape applies to `Object` if/when its dataBits are tracked. Today C# always emits full reflection — wasteful but not broken.
3. **The "0x15 data in loop" placeholder.** Decide whether to port the deferred logic or leave both sides without it.

---

## Related

- [0x8C ObjectCreate](0x8C-objectcreate.md) — same reflection payload, plus create prefix
- [0x8E ObjectDelete](0x8E-objectdelete.md)
- [Phase 10 Gameloop](../../flow/phases/10-gameloop.md) — when this fires
- [REFLECTION_SERIALIZER.md](../REFLECTION_SERIALIZER.md)
