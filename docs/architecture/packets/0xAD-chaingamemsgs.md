# 0xAD — ChainGameMsgs

| Direction | Size | Phase | Status |
|---|---|---|---|
| S→C | 2 B | [12 GameOver](../phases/12-gameover.md) | ❌ |

Single-byte state hint to the client. Defined in C++ but **all four call sites are commented out** — dead code in the reference, never invoked.

| `state` | Meaning (per C++ comment) |
|---|---|
| `0` | "unk (fade to black) — [set state ChainVote]" |
| `1` | mission failed — [set state GameOver] |
| `2` | "observer? (seems to do nothing)" |

---

## Body layout

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `state` | u8 | — | See table above. |

Total: 1 byte opcode + 1 byte body = **2 bytes**.

---

## C++ writer

`recap_server_develop/darkspore_server/source/RakNet/Server.cpp:2334-2348`:

```cpp
void Server::SendChainGame(const ClientPtr& client, uint8_t state) {
    BitStream outStream(8);
    outStream.Write(PacketID::ChainGameMsgs);

    // 9500
    /*
        state:
            0 = unk (fade to black) // [set state ChainVote]
            1 = mission failed // [set state GameOver]
            2 = observer? (seems to do nothing)
    */
    Write<uint8_t>(outStream, state);

    Send(outStream, client);
}
```

### Commented-out call sites

| File:line | Original intent |
|---|---|
| `Server.cpp:1164` | `// SendChainGame(client, 0);` — Spaceship→ChainVoting burst |
| `Server.cpp:1188` | `// SendChainGame(client, 2);` — Dungeon-entry burst |
| `Server.cpp:1199` | `// SendChainGame(client, 2);` — Dungeon-entry burst |
| `Server.cpp:2105` | comment: "same function as SendChainGame with value 2, nothing" |

**Zero active call sites.** The failure lane this packet would drive is purely spec.

---

## C# packet class

**Not implemented.** `Adapters/RakNet/PacketType.cs:50` declares `ChainGameMsgs = 0xAD`. `Adapters/RakNet/Packets/PacketActivator.cs:166-167` is an empty stub.

Sketch:

```csharp
public class ChainGameMsgsPacket : IRakNetPacket
{
    public PacketType Type => PacketType.ChainGameMsgs;
    public byte State { get; set; }

    public void ReadFrom(Stream stream) => State = (byte)stream.ReadByte();
    public void WriteTo(Stream stream) => stream.WriteByte(State);
}
```

---

## Open audit items

1. **Capture-required for `state` semantics.** The C++ comments are folklore — no one has confirmed what the client actually does for each value.
2. **Wipe-detection trigger.** Neither side detects party wipe. Without that, `state=1` has nowhere to fire from. See [Phase 12](../phases/12-gameover.md) for the trigger discussion.
3. **`state=0` fade-to-black** — could be useful for cinematic transitions (boss intros). Hold off until a real use case appears.
4. **`state=2` observer no-op** — if it truly does nothing, drop the value from any future implementation rather than carrying dead semantics.

---

## Related

- [Phase 12 GameOver](../phases/12-gameover.md) — failure lane
- [0xAE ChainGameOverMsgs](0xAE-chaingameovermsgs.md) — declared sibling, also unimplemented
- [STATE_MACHINE.md](../STATE_MACHINE.md) — `GameOver = 0x0D` rules
