# 0x85 — PartyMergeComplete

| Direction | Size | Phase | Status |
|---|---|---|---|
| S→C | 9 B | [06 Spaceship](../../flow/phases/06-spaceship.md) | ✅ |

Sent immediately after `HelloPlayer`. Tells the client "your party is assembled, you can show the lobby UI." Body carries a server timestamp.

C++ comment claims it also implicitly triggers a `DebugPing` on the client side — i.e. the next `DebugPing` we receive is a direct consequence of this packet.

---

## Body layout

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `Timestamp` | u64 | **BE** | Unix seconds. C++ `utils::get_unix_time()`; C# `DateTimeOffset.UtcNow.ToUnixTimeSeconds()`. |

Total: 1 byte opcode + 8 byte body = **9 bytes**.

---

## C++ writer

`ReCap.Cpp/darkspore_server/source/RakNet/Server.cpp:2264-2272`:

```cpp
void Server::SendPartyMergeComplete(const ClientPtr& client) {
    // This packet also "sends" debug ping automatically
    BitStream outStream(8);
    outStream.Write(PacketID::PartyMergeComplete);

    Write<uint64_t>(outStream, utils::get_unix_time());          // BE wrapper

    Send(outStream, client);
}
```

Called from `Server::OnHelloPlayerRequest` at `Server.cpp:639`.

---

## C# packet class

`ReCap.Server/Adapters/RakNet/Packets/PartyMergeCompletePacket.cs`:

```csharp
public ulong Timestamp { get; set; }

public PartyMergeCompletePacket()
{
    Timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}

public void WriteTo(Stream stream)
{
    using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
    writer.WriteBE(Timestamp);
}
```

Sent from `Game.HandleHelloPlayerRequest` flow. `WriteBE(Timestamp)` matches C++ `Write<u64>` wrapper output exactly.

---

## Open audit items

1. **"Implicit DebugPing" claim.** Verify with a wire capture — does the client send `DebugPing` immediately after receiving `PartyMergeComplete`, with no other server-side trigger? Phase 06 relies on this.
2. **Timestamp semantics.** Unix seconds vs ms? `utils::get_unix_time()` returns seconds. `DateTimeOffset.UtcNow.ToUnixTimeSeconds()` returns seconds. Match. Confirm the client uses it for anything (display? sync?) — it may be cosmetic.

---

## Related

- [Phase 06 Spaceship](../../flow/phases/06-spaceship.md) — `HelloPlayer` → `PartyMergeComplete` → first LPU
- [0xCC DebugPing](0xCC-debugping.md) — implicitly triggered by this packet
