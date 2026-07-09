# 0xB9 — ObjectivesComplete

| Direction | Size | Phase | Status |
|---|---|---|---|
| S→C | variable (1 + N×72 + 4 B) | [11 ChainCashOut](../../flow/phases/11-chaincashout.md) | ⚠️ |

Final summary of all objectives at the end of a successful chain. Body = `count + per-objective WriteTo + u32 medals` (packed medal byte per objective).

> ⚠️ **C# `ObjectivesCompletePacket.cs` is MIS-ENCODED.** It writes a raw 712-byte CashOutData blob — wrong opcode. The 712-B blob belongs to [0xAB ChainCashOutMsgs](0xAB-chaincashoutmsgs.md). See audit item #1.

---

## Body layout

| Offset | Field | Type | Endian | Notes |
|---|---|---|---|---|
| `0x00` | `count` | u8 | — | Number of objectives. |
| `0x01..` | `Objective::WriteTo` × `count` | 72 B each | mixed | Same per-objective block as [0xB7](0xB7-objectivesinitforlevel.md). |
| (tail) | `medals` | u32 | **BE** | Packed: bits `(8*i)..(8*i+7)` = objective `i`'s medal byte. |

Typical payload: 5 objectives ⇒ 1 + 5×72 + 4 = 365 B body.

---

## C++ writer

`ReCap.Cpp/darkspore_server/source/RakNet/Server.cpp:2233-2253`:

```cpp
void Server::SendObjectivesComplete(const ClientPtr& client) {
    BitStream outStream(8);
    outStream.Write(PacketID::ObjectivesComplete);

    const auto& objectives = mGame.GetObjectives();

    uint8_t count = static_cast<uint8_t>(objectives.size());
    uint32_t medals = 0;

    Write<uint8_t>(outStream, count);
    for (uint8_t i = 0; i < count; ++i) {
        const auto& objective = objectives[i];
        objective.WriteTo(outStream);

        medals |= (static_cast<uint32_t>(objective.medal) << (8 * i));
    }

    Write<uint32_t>(outStream, medals);

    Send(outStream, client);
}
```

`medals` packing rule: byte `i` of the u32 carries objective `i`'s medal value. With 5 objectives the high byte of the u32 is unused.

---

## C# packet class

`ReCap.Server/Adapters/RakNet/Packets/ObjectivesCompletePacket.cs` — **WRONG**:

```csharp
public class ObjectivesCompletePacket : IRakNetPacket
{
    public PacketType Type => PacketType.ObjectivesComplete;
    public byte[] CashOutData { get; set; } = new byte[0x2C8];   // ⚠️ wrong payload type
    public void WriteTo(Stream stream) => stream.Write(CashOutData, 0, CashOutData.Length);
}
```

Sends a raw 712-byte buffer under opcode `0xB9`. The real `ObjectivesComplete` body shape (count + per-objective + medals) is absent. Whoever wired this confused the 712-B CashOutData (which belongs in `ChainCashOutMsgs`) with the 0xB9 payload.

---

## Open audit items

1. **Fix the body type.** Replace the raw 712-B blob with:
   ```csharp
   public List<ObjectiveData> Objectives { get; } = new();

   public void WriteTo(Stream stream)
   {
       stream.WriteByte((byte)Objectives.Count);
       uint medals = 0;
       for (int i = 0; i < Objectives.Count; i++)
       {
           Objectives[i].WriteTo(stream);
           medals |= (uint)(Objectives[i].Medal << (8 * i));
       }
       using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
       writer.WriteBE(medals);
   }
   ```
2. **Move the 712-B CashOutData** to the new `ChainCashOutMsgsPacket` class (Phase 11 audit item).
3. **`ObjectiveData.Medal`** field — add to `ObjectiveData` (currently only `Id`, `Value`).
4. **Call site.** Not invoked anywhere in C# `Game.cs` today. Wire from a future `BeamOut` / level-clear flow.

---

## Related

- [Phase 11 ChainCashOut](../../flow/phases/11-chaincashout.md) — distinguishes this from the cashout blob
- [0xB7 ObjectivesInitForLevel](0xB7-objectivesinitforlevel.md) — reused per-objective block
- [0xB8 ObjectiveUpdated](0xB8-objectiveupdated.md) — delta variant
- [0xAB ChainCashOutMsgs](0xAB-chaincashoutmsgs.md) — owner of the 712-B blob
