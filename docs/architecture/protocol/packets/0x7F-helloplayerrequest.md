# 0x7F — HelloPlayerRequest

| Direction | Size | Phase | Status |
|---|---|---|---|
| C→S | 12 B (`u64 + u32`) — or 16 B w/ PlaygroupId | [06 Spaceship](../../flow/phases/06-spaceship.md) | ✅ |

First gameplay-wire packet from the client after RakNet handshake. Carries the Blaze user ID so the server can attach a `Player` to the session.

---

## Body layout

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `UserId` | u64 | **LE** | Blaze account ID. See [VERIFIED_FACTS.md](../../VERIFIED_FACTS.md). |
| `0x08` | `PlaygroupId` | u64 | LE | Optional. C# reads only if 8 more bytes available. |

> 🔒 **Frozen rule:** `UserId` is LE. C# uses `BinaryReader.ReadUInt64()` (LE default). Mirrored in `feedback_endianness.md` and CLAUDE.md.

> ⚠️ **C++ inconsistency:** `Server::OnHelloPlayerRequest` (`Server.cpp:596-601`) reads via `Read(mInStream, blazeId)` which is the BE wrapper (`Types.h:201-209`). If the client truly sends LE the C++ side ends up with a bswap'd value. Either the C++ side has been quietly wrong here, or `RakNet::BitStream::Write/Read<T>` already does network-order on x86 inside the upstream RakNet 3.92 — TBC by hex-diff against a capture.

---

## C++ reader

`ReCap.Cpp/darkspore_server/source/RakNet/Server.cpp:596-641`:

```cpp
void Server::OnHelloPlayerRequest(const ClientPtr& client) {
    uint64_t blazeId;
    Read(mInStream, blazeId);                                    // BE wrapper — see caveat above

    client->SetUser(SporeNet::Get().GetUserManager().GetUserById(blazeId));
    // ... catalyst init, SendHelloPlayer + SendPartyMergeComplete
}
```

`PlaygroupId` is not read on the C++ side — the field is ignored. C# parses it defensively if present.

---

## C# packet class

`ReCap.Server/Adapters/RakNet/Packets/HelloPlayerRequestPacket.cs`:

```csharp
public ulong UserId { get; set; }
public ulong PlaygroupId { get; set; }

public void ReadFrom(Stream stream)
{
    using var reader = new BinaryReader(stream, Encoding.UTF8, true);
    UserId = reader.ReadUInt64();                                // LE (BinaryReader default)
    if (stream.Position + 8 <= stream.Length)
        PlaygroupId = reader.ReadUInt64();                       // LE, optional
}
```

Dispatched by `RakNetServer.OnSessionReceiveRaw` (`RakNetServer.cs:86-100`): attaches the client to the matching `Game` via `accountService.getAccountById(client.UserId)`, then routes subsequent packets through `Game.HandlePacket`.

---

## Open audit items

1. **Settle the BE/LE inconsistency.** Run the official client, hex-dump the first packet's body, compare against `UserId` in the Blaze auth log. If bytes are LE in raw form, the C++ side is silently bug-mangling. If BE, the C# side is silently wrong and the `BinaryReader.ReadUInt64()` call must become `ReadUInt64BE`.
2. **Confirm `PlaygroupId` semantics.** C++ doesn't read it; C# parses it but never uses it. Is the client even sending it, or is the 8-byte tail a different field?

---

## Related

- [Phase 06 Spaceship](../../flow/phases/06-spaceship.md) — flow context
- [VERIFIED_FACTS.md](../../VERIFIED_FACTS.md)
