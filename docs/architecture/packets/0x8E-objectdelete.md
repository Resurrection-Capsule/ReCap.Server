# 0x8E — ObjectDelete

| Direction | Size | Phase | Status |
|---|---|---|---|
| S→C | 5 B (single) / 1 + 4×N B (batch) | [10 Gameloop](../phases/10-gameloop.md) | ❌ |

Tells the client to destroy one or more object instances. C++ has two overloads — single object (5 B body) and batch (1 + 4×N B). The batch form deletes multiple objects in a single packet.

---

## Body layout (single object)

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `objectId` | u32 | **BE** | Target object. |

Total: 1 byte opcode + 4 byte body = **5 bytes**.

## Body layout (batch)

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `objectId[0]` | u32 | **BE** | |
| `0x04` | `objectId[1]` | u32 | **BE** | |
| `…` | `…` | | | One u32 per object until end of packet. |

No length prefix — client reads to EOF. Total: 1 byte opcode + 4×N body.

---

## C++ writer (single)

`recap_server_develop/darkspore_server/source/RakNet/Server.cpp:1518-1530`:

```cpp
void Server::SendObjectDelete(const ClientPtr& client, const Game::ObjectPtr& object) {
    // 100%
    if (!object) return;

    BitStream outStream(8);
    outStream.Write(PacketID::ObjectDelete);
    Write<uint32_t>(outStream, object->GetId());

    Send(outStream, client);
}
```

## C++ writer (batch)

`Server.cpp:1532-1547`:

```cpp
void Server::SendObjectDelete(const ClientPtr& client, const std::vector<Game::ObjectPtr>& objects) {
    if (objects.empty()) return;

    BitStream outStream(8);
    outStream.Write(PacketID::ObjectDelete);
    for (auto object : objects) {
        if (object) Write<uint32_t>(outStream, object->GetId());
    }

    Send(outStream, client);
}
```

Both marked "100%" by the C++ author = "verified, don't touch."

---

## C# packet class

**Not implemented.** `Adapters/RakNet/Packets/PacketActivator.cs:67-68` is an empty `case PacketType.ObjectDelete: break;` stub. No `ObjectDeletePacket.cs` file.

Sketch:

```csharp
public class ObjectDeletePacket : IRakNetPacket
{
    public PacketType Type => PacketType.ObjectDelete;
    public List<uint> ObjectIds { get; } = new();

    public void ReadFrom(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);
        while (stream.Position < stream.Length)
            ObjectIds.Add(reader.ReadUInt32BE());
    }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        foreach (var id in ObjectIds)
            writer.WriteBE(id);
    }
}
```

---

## Open audit items

1. **Implement the packet class.** Trivial — 4 bytes per object id.
2. **Wire from `Instance::SendObjectDelete`** equivalent in C# (`Game.RemoveObject`?). Loot pickups + enemy deaths need this immediately.
3. **Batch threshold.** Decide whether the C# side ever needs the batch form, or single-object is fine (simpler).

---

## Related

- [0x8C ObjectCreate](0x8C-objectcreate.md) — symmetric spawn
- [Phase 10 Gameloop](../phases/10-gameloop.md) — loot/death lifecycle
