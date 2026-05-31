# 0xA9 — ChainVoteMsgs

| Direction | Size | Phase | Status |
|---|---|---|---|
| S→C | 2 / 339 / 6 / 3 B (per `value`) | [07 ChainVoting](../../flow/phases/07-chainvote.md), [11 ChainCashOut](../../flow/phases/11-chaincashout.md) | ✅ |

Dispatched on the wire ID `0xA9` regardless of which logical content follows. Drives the planet-vote UI, the deployment countdown, and (per the C++ quirk in Phase 11) the cashout payload.

---

## Body layout by `value`

### `value = 0` — full vote payload (339 B total)

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `value` | u8 | — | =0 |
| `0x01..0x151` | `ChainData::WriteTo` 0x151 buffer | raw | **LE** | Levels, enemies, cinematics, voiceovers, completionFlag. See [Phase 07](../../flow/phases/07-chainvote.md#chaindata-buffer-0x151-bytes) for the per-offset breakdown. |

### `value = 1` — deployment countdown (6 B total)

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `value` | u8 | — | =1 |
| `0x01` | `secondsUntilDeployment` | f32 | **BE** | C++ hardcodes `30.0f`. |

### `value = 2` — return-to-cashout (3 B total)

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `value` | u8 | — | =2 |
| `0x01` | `stayInParty` | bool (u8) | — | C++ hardcodes `false`. |

> **Confirmed:** the 0x151 buffer is LE. Flipping to BE breaks levels/enemies UI display. Documented in [VERIFIED_FACTS.md](../../VERIFIED_FACTS.md).

---

## C++ writer

`recap_server_develop/darkspore_server/source/RakNet/Server.cpp:2140-2182`:

```cpp
void Server::SendChainVoteMessages(const ClientPtr& client, uint8_t value) {
    // Packet size: 0x04
    BitStream outStream(8);
    outStream.Write(PacketID::ChainVoteMsgs);

    /*
        0 = 0x151 bytes of data
        1 = 0x04 bytes of data (show planet info + countdown)
        2 = 0x01 bytes of data (StayInParty + go to CashOut)
    */

    Write<uint8_t>(outStream, value);
    switch (value) {
        case 0: {
            auto& data = mGame.GetChainData();
            data.WriteTo(outStream);
            break;
        }
        case 1: {
            float secondsUntilDeployment = 30.0f;
            Write<float>(outStream, secondsUntilDeployment);
            break;
        }
        case 2: {
            bool stayInParty = false;
            Write<bool>(outStream, stayInParty);
            break;
        }
    }

    Send(outStream, client);
}
```

> ⚠️ **Helper-name overlap.** The Phase 11 cashout helper `SendChainCashOutMessages` also writes `PacketID::ChainVoteMsgs (0xA9)` — see [0xAB sheet](0xAB-chaincashoutmsgs.md) for the quirk.

---

## C# packet class

`ReCap.Server/Adapters/RakNet/Packets/ChainVoteMsgsPacket.cs`:

```csharp
public byte Value { get; set; }
public ChainData? ChainData { get; set; }
public float SecondsUntilDeployment { get; set; }
public bool StayInParty { get; set; }

public void WriteTo(Stream stream)
{
    using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
    writer.Write(Value);

    switch (Value)
    {
        case 0:
            if (ChainData != null) WriteChainData(writer, ChainData);   // LE buffer, 0x151 B
            break;
        case 1:
            writer.WriteBE(SecondsUntilDeployment);                     // f32 BE
            break;
        case 2:
            writer.Write(StayInParty);                                  // bool
            break;
    }
}
```

`WriteChainData` (lines 52-164) writes a 0x170 pre-zero buffer then truncates to `0x151` bytes. All field writes use `BinaryPrimitives.WriteUInt32LittleEndian` (and friends) — matches the 🔒 frozen LE rule.

---

## Open audit items

1. **ChainData per-offset diff** vs C++ `Game::ChainData::WriteTo`. Specific offsets (FNV hashes for `fmv_02_zelems.vp6`, voiceover `vo_ship_flow_reinfect_zelems`) are C#-only invented placeholders. Confirm against capture.
2. **`CompletedLevel` branch.** C# adds an entirely different layout when the level was completed (offsets `0x55`, `0x65`, `0x76`+, etc.). Not present in C++ source as published — possibly C# extension or reverse-engineered from a different build.
3. **0x170 over-allocation.** `WriteChainData` writes to a 0x170 buffer then truncates to 0x151. Risk: a future offset bump past 0x151 silently drops bytes.
4. **`value = 2` invocation.** C++ never calls `SendChainVoteMessages(client, 2)` outside of comments. Confirm whether the C# side ever needs the stayInParty form.

---

## Related

- [Phase 07 ChainVoting](../../flow/phases/07-chainvote.md) — main flow
- [Phase 11 ChainCashOut](../../flow/phases/11-chaincashout.md) — cashout reuse of this opcode
- [0xAB ChainCashOutMsgs](0xAB-chaincashoutmsgs.md) — wire-ID quirk
- [0xAC ChainPlayerMsgs](0xAC-chainplayermsgs.md) — client-side vote trigger
- [VERIFIED_FACTS.md](../../VERIFIED_FACTS.md)
