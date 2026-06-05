using System.IO;
using System.Text;

namespace ReCap.Server.Adapters.RakNet.Packets;

// CrystalMessage (0xC3) — server→client catalyst grid result, raw LE 29B body.
// Client handler ClientNet::OnGmsCrystalMessage @0x0053f700 reads exactly 0x1d bytes.
// Branched on MoveType (C++ Server.cpp:2302-2324):
//   0 = crystal reveal in world: u8 0 | u32 newSlot | u32 nounId | u32 rarity | u32 color | u32×3 zero
//   else (2=swap committed, 3=reject/snap-back, 0-as-default=error):
//       u8 type | u32×5 zero | u32 slot | u32 newSlot
// Reject (3) echoes the dragged slot so the client snaps the crystal back.
public class CrystalMessagePacket : IRakNetPacket
{
    public PacketType Type => PacketType.CrystalMessage;

    public byte MoveType { get; set; }
    public uint NounId { get; set; }
    public uint Rarity { get; set; }
    public uint Color { get; set; }
    public uint Slot { get; set; }
    public uint NewSlot { get; set; }

    public void ReadFrom(Stream stream) { }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(MoveType);
        if (MoveType == 0)
        {
            writer.Write(NewSlot);
            writer.Write(NounId);
            writer.Write(Rarity);
            writer.Write(Color);
            writer.Write((uint)0);
            writer.Write((uint)0);
            writer.Write((uint)0);
        }
        else
        {
            writer.Write((uint)0);
            writer.Write((uint)0);
            writer.Write((uint)0);
            writer.Write((uint)0);
            writer.Write((uint)0);
            writer.Write(Slot);
            writer.Write(NewSlot);
        }
    }
}
