using ReCap.Server.Adapters.RakNet.Packets;

namespace ReCap.Tests.Packets;

// Crystal drag pair — client ground-truth (Ghidra 2026-06-05):
// 0xC2 inbound = 24B (6×u32: crystalSlot, moveType, pickupTime, dropTime, unk, newSlot),
//   C++ OnCrystalDragMessage Server.cpp:1032-1048.
// 0xC3 outbound = fixed 29B, ClientNet::OnGmsCrystalMessage @0x0053f700 reads exactly 0x1d;
//   moveType==0 → newSlot/nounId/rarity/color + 3 zeros; else 5 zeros + slot + newSlot
//   (C++ SendCrystalMessage Server.cpp:2302-2324). Reject=3 echoes the dragged slot.
public class CrystalMessageTests
{
    [Fact]
    public void Reject_Body_Is_29_Bytes_Type3_With_Slot_Echo()
    {
        using var ms = new MemoryStream();
        new CrystalMessagePacket { MoveType = 3, Slot = 5, NewSlot = 0 }.WriteTo(ms);
        var bytes = ms.ToArray();

        Assert.Equal(29, bytes.Length);
        Assert.Equal(0x03, bytes[0]);
        Assert.All(bytes[1..21], b => Assert.Equal(0x00, b));
        Assert.Equal(5u, BitConverter.ToUInt32(bytes, 21));
        Assert.Equal(0u, BitConverter.ToUInt32(bytes, 25));
    }

    [Fact]
    public void Swap_Body_Carries_Slot_And_NewSlot()
    {
        using var ms = new MemoryStream();
        new CrystalMessagePacket { MoveType = 2, Slot = 1, NewSlot = 7 }.WriteTo(ms);
        var bytes = ms.ToArray();

        Assert.Equal(29, bytes.Length);
        Assert.Equal(0x02, bytes[0]);
        Assert.Equal(1u, BitConverter.ToUInt32(bytes, 21));
        Assert.Equal(7u, BitConverter.ToUInt32(bytes, 25));
    }

    [Fact]
    public void Reveal_Type0_Carries_Noun_Rarity_Color()
    {
        using var ms = new MemoryStream();
        new CrystalMessagePacket
        {
            MoveType = 0,
            NewSlot = 0xFFFFFFFF,
            NounId = 0xAABB0011,
            Rarity = 1,
            Color = 4
        }.WriteTo(ms);
        var bytes = ms.ToArray();

        Assert.Equal(29, bytes.Length);
        Assert.Equal(0x00, bytes[0]);
        Assert.Equal(0xFFFFFFFFu, BitConverter.ToUInt32(bytes, 1));
        Assert.Equal(0xAABB0011u, BitConverter.ToUInt32(bytes, 5));
        Assert.Equal(1u, BitConverter.ToUInt32(bytes, 9));
        Assert.Equal(4u, BitConverter.ToUInt32(bytes, 13));
        Assert.All(bytes[17..], b => Assert.Equal(0x00, b));
    }

    [Fact]
    public void Drag_ReadFrom_Parses_Six_U32()
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true))
        {
            w.Write(5u);            // crystalSlot
            w.Write(2u);            // moveType
            w.Write(1111u);         // pickupTime
            w.Write(2222u);         // dropTime
            w.Write(0xDEADu);       // unk
            w.Write(7u);            // newSlot
        }
        ms.Position = 0;

        var packet = new CrystalDragMessagePacket();
        packet.ReadFrom(ms);

        Assert.Equal(5u, packet.CrystalSlot);
        Assert.Equal(2u, packet.MoveType);
        Assert.Equal(1111u, packet.PickupTime);
        Assert.Equal(2222u, packet.DropTime);
        Assert.Equal(7u, packet.NewSlot);
    }
}
