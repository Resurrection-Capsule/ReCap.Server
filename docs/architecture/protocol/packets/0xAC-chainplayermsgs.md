# 0xAC — ChainPlayerMsgs

| Direction | Size | Phase | Status |
|---|---|---|---|
| C→S | 2 / 3 / 7 B (per `byteCount`) | [07 ChainVoting](../../flow/phases/07-chainvote.md), [08 PreDungeon](../../flow/phases/08-predungeon.md) | ✅ |

Client-driven chain-mode request. Dispatched by `byteCount` (total body length excluding opcode):

| `byteCount` | Meaning | Body shape |
|---|---|---|
| `1` | Single-byte action (vote setup / return-to-cashout) | `u8 value` |
| `2` | Two-byte unknown | `u8 value + u8 ready` |
| `6` | Mission vote | `u8 value + u8 unknown + u32 BE squadId` |

> 🔒 The `squadId` (byte 3..6) is **BE**. Reverse-engineered, confirmed in `feedback_chainplayermsgs_parse.md`.

---

## Body layout

### `byteCount = 1` (2 B total on wire)

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `value` | u8 | — | `0` ⇒ request ChainVoteMsgs(0)+(1). `2` ⇒ cashout. |

### `byteCount = 2` (3 B total)

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `value` | u8 | — | |
| `0x01` | `ready` | u8 | — | C# field name — unconfirmed semantic. |

### `byteCount = 6` (7 B total)

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `value` | u8 | — | |
| `0x01` | `unknown` | u8 | — | |
| `0x02..0x05` | `squadId` | u32 | **BE** 🔒 | Squad/deck selected for the mission. |

---

## C++ reader

`recap_server_develop/darkspore_server/source/RakNet/Server.cpp:986-1022`:

```cpp
void Server::OnChainPlayerMsgs(const ClientPtr& client) {
    auto bytesToRead = mInStream.GetNumberOfUnreadBits() / 8;

    uint8_t value;
    Read<uint8_t>(mInStream, value);

    switch (bytesToRead) {
        case 1: {
            if (value == 0) {
                SendChainVoteMessages(client, value);
                SendChainVoteMessages(client, 1);
            } else if (value == 2) {
                SendChainVoteMessages(client, value);    // cashout return
            }
            break;
        }
        case 2: { /* unknown */ break; }
        case 6: {
            uint8_t unknown;
            Read<uint8_t>(mInStream, unknown);
            uint32_t squadId;
            Read<uint32_t>(mInStream, squadId);                    // BE wrapper
            PrepareGameStart(client, unknown, squadId);
            break;
        }
    }
}
```

The `byteCount` is computed from the remaining unread bits — the packet itself carries no explicit length field.

---

## C# packet class

`ReCap.Server/Adapters/RakNet/Packets/ChainPlayerMsgsPacket.cs`:

```csharp
public byte Value { get; set; }
public byte Ready { get; set; }
public byte Unknown { get; set; }
public uint SquadId { get; set; }
public int ByteCount { get; set; }

public void ReadFrom(Stream stream)
{
    ByteCount = (int)(stream.Length - stream.Position);
    using var reader = new BinaryReader(stream, Encoding.UTF8, true);

    switch (ByteCount)
    {
        case 1: Value = reader.ReadByte(); break;
        case 2: Value = reader.ReadByte(); Ready = reader.ReadByte(); break;
        case 6:
            Value = reader.ReadByte();
            Unknown = reader.ReadByte();
            SquadId = reader.ReadUInt32BE();                       // BE — matches feedback
            break;
    }
}
```

Dispatched by `Game.HandleChainPlayerMsgs` (`Game.cs:289+`). `byteCount=2/value=0` triggers `ChainVoteMsgs` send; `byteCount=6` calls `PrepareGameStart`-equivalent (sets state to PreDungeon, fills squad characters, sends GamePrepareForStart).

---

## Open audit items

1. **`byteCount=2 ready` semantic.** C++ ignores; C# names it `Ready`. Capture would clarify.
2. **`byteCount=2 / value=0` flow.** C++ has nothing in `case 2` (no-op). C# dispatch in `Game.cs:289+` may interpret this differently — audit.
3. **`unknown` field meaning** in byteCount=6. Possibly the player slot id or a vote weight.

---

## Related

- [Phase 07 ChainVoting](../../flow/phases/07-chainvote.md) — byteCount=2 flow
- [Phase 08 PreDungeon](../../flow/phases/08-predungeon.md) — byteCount=6 flow
- [0xA9 ChainVoteMsgs](0xA9-chainvotemsgs.md) — server response
- [0xB0 GamePrepareForStart](0xB0-gameprepareforstart.md) — sent after byteCount=6
