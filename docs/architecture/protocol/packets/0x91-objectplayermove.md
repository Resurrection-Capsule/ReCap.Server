# 0x91 — ObjectPlayerMove

| Direction | Size | Phase | Status |
|---|---|---|---|
| S→C | ~81 B | [10 Gameloop](../../flow/phases/10-gameloop.md) | ✅ |

Authoritative movement broadcast. Echoes the player's intended motion back to all clients (including the originator). Body carries goal flags, goal/facing/velocity vectors, stop distances, and a target reference.

---

## Body layout

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `objectId` | u32 | **BE** | Moving object. |
| `0x04` | `goalFlags` | u32 | **BE** | Bitfield (teleport=`0x020`, etc.). |
| `0x08` | `goalPosition` | vec3 (3× f32) | BE | Target world coords. |
| `0x14` | `facing` | vec3 | BE | Heading vector. |
| `0x20` | `externalLinearVelocity` | vec3 | BE | |
| `0x2C` | `externalForce` | vec3 | BE | |
| `0x38` | `allowedStopDistance` | f32 | BE | |
| `0x3C` | `desiredStopDistance` | f32 | BE | |
| `0x40` | `targetPosition` | vec3 | BE | |
| `0x4C` | `targetId` | u32 | **BE** | Lock-on target (0 = none). |

Total: 1 byte opcode + 80 byte body = **81 bytes**. C++ pre-sizes `BitStream(81)` for this exact value.

---

## C++ writer

`ReCap.Cpp/darkspore_server/source/RakNet/Server.cpp:1698-1715`:

```cpp
void Server::SendObjectPlayerMove(const ClientPtr& client, const Game::ObjectPtr& object, const Game::Locomotion& locomotionData) {
    // 100%
    BitStream outStream(81);
    outStream.Write(PacketID::ObjectPlayerMove);

    Write<uint32_t>(outStream, object->GetId());
    Write<uint32_t>(outStream, locomotionData.GetGoalFlags());
    Write(outStream, locomotionData.GetGoalPosition());
    Write(outStream, locomotionData.GetFacing());
    Write(outStream, locomotionData.GetExternalLinearVelocity());
    Write(outStream, locomotionData.GetExternalForce());
    Write<float>(outStream, locomotionData.GetAllowedStopDistance());
    Write<float>(outStream, locomotionData.GetDesiredStopDistance());
    Write(outStream, locomotionData.GetTargetPosition());
    Write<uint32_t>(outStream, locomotionData.GetTargetId());

    Send(outStream, client);
}
```

"100%" comment = verified.

---

## C# packet class

`ReCap.Server/Adapters/RakNet/Packets/ObjectPlayerMovePacket.cs` — file exists. Sent from `Game.HandleActionCommand` case 3 (Movement) and case 4 (StopMovement, with `goalFlags=0x020`).

C# matches the byte layout; both `Vector3` and `WriteBE(float)` map onto C++ `Write(vec3)` (3× f32 BE) and `Write<float>` (f32 BE) respectively.

---

## Open audit items

1. **Server-side world state.** C# echoes the client's intent back but doesn't track it on the server. Other clients in the same `Game` thus never see authoritative positions. Required for multi-player.
2. **`Stop()` semantics.** C++ calls `locomotionData->Stop()` for the stop case; C# inlines `goalFlags=0x020`. Functional but bypasses the C++ helper's clear-other-fields behaviour. Verify there are no residual `externalForce` values from a previous move.
3. **`vec3` field order.** `glm::vec3` is `(x, y, z)`; `System.Numerics.Vector3` is also `(X, Y, Z)`. `BigEndianExtensions.WriteBE(Vector3)` writes `(X, Y, Z)`. Match confirmed.

---

## Related

- [0x94 LocomotionDataUpdate](0x94-locomotiondataupdate.md) — fuller locomotion broadcast
- [0x9C ActionCommandMsgs](0x9C-actioncommandmsgs.md) — client-side trigger
- [Phase 10 Gameloop](../../flow/phases/10-gameloop.md) — movement handler
