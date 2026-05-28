# 0x94 — LocomotionDataUpdate

| Direction | Size | Phase | Status |
|---|---|---|---|
| S→C | variable | [10 Gameloop](../../flow/phases/10-gameloop.md) | ⚠️ |

Full locomotion-state broadcast (vs the trimmed [0x91 ObjectPlayerMove](0x91-objectplayermove.md)). Body wraps the entire `Locomotion::WriteTo` payload.

C++ comment hints `WriteReflection` was an option but commented out — the active path is the fixed `WriteTo`.

---

## Body layout

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `objectId` | u32 | **BE** | |
| `0x04` | `Locomotion::WriteTo` | raw | mixed | Fixed-layout 23-field block. See `Game::Locomotion` in C++ and `reflection_serializer<23>` instantiation at `Types.cpp:517` (reflection variant). |

---

## C++ writer

`recap_server_develop/darkspore_server/source/RakNet/Server.cpp:1749-1763`:

```cpp
void Server::SendLocomotionDataUpdate(const ClientPtr& client, const Game::ObjectPtr& object, const Game::Locomotion& locomotionData) {
    // 100%
    if (!object) return;

    BitStream outStream(8);
    outStream.Write(PacketID::LocomotionDataUpdate);

    Write<uint32_t>(outStream, object->GetId());
    // locomotionData.WriteReflection(outStream);
    locomotionData.WriteTo(outStream);
    Send(outStream, client);
    LogPacketSize("LocomotionDataUpdate", outStream);
}
```

The commented `WriteReflection` line suggests the author considered a delta-encoded variant. The shipped code uses the fixed `WriteTo`.

---

## C# packet class

`ReCap.Server/Adapters/RakNet/Packets/LocomotionDataUpdatePacket.cs` — file exists. Confirm it mirrors the C++ `Locomotion::WriteTo` field order byte-for-byte.

---

## Open audit items

1. **Verify `Locomotion::WriteTo` field schema** end-to-end. The reflection_serializer<23> path is well-documented; the raw `WriteTo` may diverge in subtle ways.
2. **High-frequency variant.** `LocomotionDataUnreliableUpdate (0x95)` is the no-ack high-frequency cousin. Not in scope here; covered in Tier-3 inventory.
3. **C# call sites.** Audit when (if ever) `Game.cs` actually emits this — distinct from `ObjectPlayerMove`.

---

## Related

- [0x91 ObjectPlayerMove](0x91-objectplayermove.md) — trimmed cousin
- `0x95 LocomotionDataUnreliableUpdate` (Tier-3, not sheeted)
- [Phase 10 Gameloop](../../flow/phases/10-gameloop.md)
- [REFLECTION_SERIALIZER.md](../REFLECTION_SERIALIZER.md) — `<23>` reflection schema
