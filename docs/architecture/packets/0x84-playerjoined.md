# 0x84 — PlayerJoined

| Direction | Size | Phase | Status |
|---|---|---|---|
| S→C | 2 B | [05 RakNet Connect](../phases/05-raknet-connect.md) | ❓ |

Server announces a new player slot to existing clients. Single-byte body: the joining player's `mId`. Used for lobby roster updates — only meaningful in multi-player games (Darkspore's chain modes support 2–4 players).

In a single-player session the packet is rarely sent (the joining player is also the receiver, so it's mostly redundant).

---

## Body layout

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `mId` | u8 | — | Joining player's session index (0–3). |

Total: 1 byte opcode + 1 byte body = **2 bytes**.

---

## C++ writer

`recap_server_develop/darkspore_server/source/RakNet/Server.cpp:1288-1295`:

```cpp
void Server::SendPlayerJoined(const ClientPtr& client) {
    // Packet size: 0x01
    BitStream outStream(8);
    outStream.Write(PacketID::PlayerJoined);
    outStream.Write<uint8_t>(client->mId);

    Send(outStream, client);
}
```

Comment "Packet size: 0x01" refers to the body (excluding opcode).

---

## Call sites

**Zero call sites in the C++ source as published.** `grep "SendPlayerJoined" Server.cpp` returns only the definition. Like `SendPlayerDeparted` and `SendChainGame`, this helper is defined but unused.

In a real multi-player implementation it would fire from `Server::OnHelloPlayerRequest` immediately after `AddClient` — but only when broadcasting to *other* clients, not the joining one.

---

## C# packet class

`ReCap.Server/Adapters/RakNet/Packets/PlayerJoinedPacket.cs` — file exists per `PacketActivator.cs:34-35`:

```csharp
case PacketType.PlayerJoined:
    packet = new PlayerJoinedPacket();
    break;
```

Body audit pending — confirm a single `u8 PlayerId` field with `BinaryWriter.Write((byte)PlayerId)`.

No call sites in `Game.cs` either — same dead-code state as C++.

---

## Open audit items

1. **Multi-player handshake.** If/when two clients share a `Game`, `SendPlayerJoined` should fire to the existing roster on each new attach. Wire it from `Game.AttachPlayer` and broadcast to all *other* `Client`s.
2. **Verify single-byte body** on the C# side matches C++.
3. **No-op for the joining client.** Make sure `AttachPlayer` doesn't send `PlayerJoined` back to the joiner itself — they already know.

---

## Related

- [Phase 05 RakNet Connect](../phases/05-raknet-connect.md) — first-connect flow
- [0x86 PlayerDeparted](0x86-playerdeparted.md) — symmetric leave broadcast
