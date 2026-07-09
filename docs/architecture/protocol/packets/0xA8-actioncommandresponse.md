# 0xA8 — ActionCommandResponse

| Direction | Size | Phase | Status |
|---|---|---|---|
| S→C | variable (≥ 5 B) | [10 Gameloop](../../flow/phases/10-gameloop.md) | ⚠️ |

Server's response to an `ActionCommandMsgs` request. Confirms acceptance, returns ability animation data, or signals cancel. C++ has multiple overloads — short forms (4-byte tail) and full-form with `AbilityCommandResponse`.

---

## Body layout (short form — `SendActionCommandResponse(client, uint8_t type)`)

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `value` | u8 | — | Status flag — `0xFF` in the typical "ack" case. |
| `0x01` | `type` | u8 | — | Original `ActionCommand` subtype. |
| `0x02` | `0xAA` | u8 | — | Magic / debug byte. |
| `0x03` | `0xBB` | u8 | — | Magic / debug byte. |
| `0x04+` | (extra) | varies | BE | More bytes for specific subtypes. |

## Body layout (cancel form — `SendActionCancel`)

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `value` | u8 | — | |
| `0x01` | `0x02` | u8 | — | Hardcoded "Cancel" action type. |
| `0x34` | `otherValue` | u32 | BE | At absolute offset 0x34 (intermediate bytes zero-padded). |

Total: 1 opcode + 0x38 body = **57 bytes**.

---

## C++ writers (two overloads + cancel)

`ReCap.Cpp/darkspore_server/source/RakNet/Server.cpp:1562-1571` (short form):

```cpp
void Server::SendActionCommandResponse(const ClientPtr& client, uint8_t type) {
    BitStream outStream(8);
    outStream.Write(PacketID::ActionCommandResponse);

    // seems like bitflags but only one is accepted at a time.
    Write<uint8_t>(outStream, 0xFF);
    Write<uint8_t>(outStream, type);
    Write<uint8_t>(outStream, 0xAA);
    Write<uint8_t>(outStream, 0xBB);
    // ... continues with subtype-specific bytes
```

`Server.cpp:1549-1560` (cancel form):

```cpp
void Server::SendActionCancel(const ClientPtr& client, uint8_t value, uint32_t otherValue) {
    BitStream outStream(0x38 + 1);
    outStream.Write(PacketID::ActionCommandResponse);

    Write<uint8_t>(outStream, value);
    Write<uint8_t>(outStream, 0x02); // Action type: Cancel

    outStream.SetWriteOffset(bits_to_bytes(0x34 + 1));
    Write<uint32_t>(outStream, otherValue);

    Send(outStream, client);
}
```

Note the `SetWriteOffset` trick — leaves bytes `0x03..0x33` as zero/uninitialized garbage.

A third overload at `Server.cpp:1641+` (`SendActionCommandResponse(client, AbilityCommandResponse&)`) handles ability use responses.

---

## C# packet class

`ReCap.Server/Adapters/RakNet/Packets/ActionCommandResponsePacket.cs` — file exists. Used inline from `Game.HandleActionCommand` for the cases C# handles (Movement / StopMovement).

Verify which overload C# mirrors — the short form is likely, but `SetWriteOffset(0x34)` and the cancel layout are unique enough that they need a dedicated path.

---

## Open audit items

1. **Audit C# coverage of all three overloads.** Currently unclear which subtype paths emit this and which don't.
2. **`0xAA` / `0xBB` magic bytes.** Why? Capture confirms they survive on the wire?
3. **Cancel form `SetWriteOffset(0x34)` byte layout.** Document exactly what fills 0x03..0x33 (zero? garbage from the `BitStream` constructor's pre-allocation?).
4. **`AbilityCommandResponse` overload** body — required for ability tier work.

---

## Related

- [0x9C ActionCommandMsgs](0x9C-actioncommandmsgs.md) — triggers this
- [Phase 10 Gameloop](../../flow/phases/10-gameloop.md) — action handler tier
