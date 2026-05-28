# 0xAB — ChainCashOutMsgs

| Direction | Size | Phase | Status |
|---|---|---|---|
| S→C | 2 B (value=0) / 714 B (value=1) | [11 ChainCashOut](../../flow/phases/11-chaincashout.md) | ❌ |

The reward / cashout payload sent at the end of a successful chain run. Body shape mirrors [ChainVoteMsgs](0xA9-chainvotemsgs.md): `u8 value + value-dependent payload`.

> ⚠️ **C++ wire-ID quirk.** The C++ helper `SendChainCashOutMessages` is *named* after this opcode but writes `PacketID::ChainVoteMsgs (0xA9)` on the wire — not `0xAB`. Either copy-paste bug or the client demultiplexes both opcodes through the same parser. The "true" wire ID is undecided until a capture settles it.

---

## Body layout by `value`

### `value = 0` — empty signal (2 B total)

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `value` | u8 | — | =0 |

No payload — `if (value != 0)` guards the CashOutData write.

### `value = 1` — cashout payload (714 B total)

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `value` | u8 | — | =1 |
| `0x01..0x2C8` | `CashOutData::WriteTo` 0x2C8 buffer | absolute-offset writes | BE (default) — **unverified** | See [Phase 11](../../flow/phases/11-chaincashout.md#cashoutdatawriteto-instancecpp30-52) for full offset table. |

CashOutData field offsets (from `Game/Instance.cpp:30-52`):

| Offset | Field | Type |
|---|---|---|
| `0x000` | `mPlanetsCompleted` | u32 |
| `0x004` | `mDna` | f32 |
| `0x034..0x043` | `mGoldMedals[4]` | u32[4] |
| `0x044..0x053` | `mSilverMedals[4]` | u32[4] |
| `0x054..0x063` | `mBronzeMedals[4]` | u32[4] |
| `0x064..0x073` | `mUniqueChances[4]` | u32[4] |
| `0x074..0x083` | `mRareChances[4]` | u32[4] |
| `0x084..0x2C7` | (zero pad) | — |

---

## C++ writer

`recap_server_develop/darkspore_server/source/RakNet/Server.cpp:2184-2196`:

```cpp
void Server::SendChainCashOutMessages(const ClientPtr& client, uint8_t value) {
    // Packet size: 0x04
    BitStream outStream(8);
    outStream.Write(PacketID::ChainVoteMsgs);   // ⚠️ NOT ChainCashOutMsgs

    Write<uint8_t>(outStream, value);
    if (value != 0) {
        mGame.GetCashOutData().WriteTo(outStream);
    }

    Send(outStream, client);
}
```

Called from `OnDebugPing` ChainCashOut branch (`Server.cpp:1173-1176`) after the client transitions into `ChainCashOut` (which itself is triggered by `BeamOut` → `ReconnectPlayer(ChainVoting)` + `chainData.completed=true`).

---

## C# packet class

**Not implemented.** `Adapters/RakNet/PacketType.cs:48` declares `ChainCashOutMsgs = 0xAB`. `Adapters/RakNet/Packets/PacketActivator.cs:159-160` is an empty stub.

> ⚠️ **`ObjectivesCompletePacket.cs` is mis-encoded** — it writes a raw 712-B `CashOutData` buffer under opcode `0xB9`. The 712-B blob belongs *here*, not in [0xB9 ObjectivesComplete](0xB9-objectivescomplete.md).

Sketch:

```csharp
public class ChainCashOutMsgsPacket : IRakNetPacket
{
    public PacketType Type => PacketType.ChainCashOutMsgs;        // or PacketType.ChainVoteMsgs per quirk
    public byte Value { get; set; }
    public CashOutData? Data { get; set; }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(Value);
        if (Value != 0 && Data != null)
            Data.WriteTo(stream);
    }
}
```

Plus a `Domain/Gameplay/CashOutData.cs` with the offset-based layout.

---

## Open audit items

1. **Settle the wire opcode** — capture an official cashout and confirm whether the byte is `0xA9` or `0xAB`. Mirror in C# to match capture, not enum name.
2. **CashOutData endianness.** C++ uses the `Write<T>` wrapper (BE) but the per-offset `SetWriteOffset` semantics may interact with `bswap` differently. Verify each field's actual byte order by capture.
3. **Fix `ObjectivesCompletePacket.cs`** — strip the misplaced CashOutData payload (Phase 11 audit item #1, [0xB9 sheet](0xB9-objectivescomplete.md)).
4. **Implement `CashOutData` class** in C#.

---

## Related

- [Phase 11 ChainCashOut](../../flow/phases/11-chaincashout.md) — full flow + 0x2C8 layout
- [0xA9 ChainVoteMsgs](0xA9-chainvotemsgs.md) — same opcode per the C++ bug
- [0xB9 ObjectivesComplete](0xB9-objectivescomplete.md) — mis-encoded in C# today
- [0x81 ReconnectPlayer](0x81-reconnectplayer.md) — drives the state into ChainCashOut
