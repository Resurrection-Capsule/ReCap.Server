# 0x80 — HelloPlayer

| Direction | Size | Phase | Status |
|---|---|---|---|
| S→C | ~12 B | [06 Spaceship](../../flow/phases/06-spaceship.md) | ⚠️ |

Server's response to `HelloPlayerRequest`. Tells the client its session index ("I AM PLAYER X") and re-advertises the RakNet endpoint.

---

## Body layout

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `type` | u8 | — | `0` makes the client log `"I AM PLAYER <mId>"`. Other values change semantics — TBC. |
| `0x01` | `mId` | u8 | — | Session-local player index (0-based). |
| `0x02` | `IP` | u32 | **raw 4 bytes** | `addr.binaryAddress`. Whatever order RakNet stored it as. **No bswap.** |
| `0x06` | `port` | u16 | **LE** (raw `stream.Write`) | RakNet endpoint port. |

Total: 10 bytes payload + 1 byte opcode = **11 bytes** on the wire (or 12 if accounting for an extra byte somewhere — capture would confirm). The C++ comment says "Packet size: 0x08" which doesn't match the literal field tally — the comment is stale.

---

## C++ writer

`recap_server_develop/darkspore_server/source/RakNet/Server.cpp:1235-1266`:

```cpp
void Server::SendHelloPlayer(const ClientPtr& client) {
    const auto& guid = mSelf->GetGuidFromSystemAddress(UNASSIGNED_SYSTEM_ADDRESS);
    auto addr = mSelf->GetSystemAddressFromGuid(guid);

    uint8_t type = 0;       // 0 = "I AM PLAYER mId"

    BitStream outStream(8);
    outStream.Write(PacketID::HelloPlayer);
    outStream.Write<uint8_t>(type);
    outStream.Write<uint8_t>(client->mId);
    outStream.WriteBits(reinterpret_cast<const uint8_t*>(&addr.binaryAddress),
                        sizeof(addr.binaryAddress) * 8, true);
    outStream.Write(addr.port);

    Send(outStream, client);
}
```

`addr.binaryAddress` is raw — RakNet's internal representation. On x86 with RakNet 3.92's typical config that's network-order (BE) inside a u32 host integer; `WriteBits` then copies the bytes as-is.

---

## C# packet class

`ReCap.Server/Adapters/RakNet/Packets/HelloPlayerPacket.cs`:

```csharp
public byte PlayerType { get; set; }
public byte GameplayIndex { get; set; }
public uint Address { get; set; }
public ushort Port { get; set; }

public void WriteTo(Stream stream)
{
    using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
    writer.Write(PlayerType);
    writer.Write(GameplayIndex);
    writer.WriteBE(Address);                                     // ⚠️ BE
    writer.Write(Port);                                          // LE (BinaryWriter default)
}
```

Diverges from C++:
- C# writes `Address` as **explicit BE**. C++ writes the raw `binaryAddress` bits.
- Match-or-mismatch depends on whether RakNet's `binaryAddress` is already BE on the host. If yes ⇒ both produce the same bytes; if no ⇒ the C# wire diverges.

---

## Open audit items

1. **Resolve the `binaryAddress` byte order.** Read `lib/RakNexus`'s equivalent of `SystemAddress::binaryAddress` to find what byte order it stores. Decide whether C# should match by `WriteBE(Address)` (current) or `Write(Address)` (default LE).
2. **`type` field non-zero semantics.** C++ uses `0`; the comment hints `type = static_cast<uint8_t>(Blaze::GameType::Tutorial)` was tried. Document the GameType enum values that the client accepts here.
3. **`Send(outStream, client)` 1-byte tail?** C++ comment "Packet size: 0x08" suggests an 8-byte body, but visible writes total ~9. Verify against capture.

---

## Related

- [Phase 06 Spaceship](../../flow/phases/06-spaceship.md) — call sequence
- [0x7F HelloPlayerRequest](0x7F-helloplayerrequest.md) — triggers this response
