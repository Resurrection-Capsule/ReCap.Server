using System.Text;

namespace ReCap.Server.Adapters.RakNet.Packets;

public class GamePrepareForStartPacket : IRakNetPacket
{
    public PacketType Type => PacketType.GamePrepareForStart;

    public uint Slot { get; set; }
    public uint unk2; // 0x00000004 works
    public uint pLevelAsset; // Always gonna be SM_Map_v5.Level - 0x946D41FE
    public uint markerSet1; // SM_Map_v5_default.Markerset - 0x6B9636C0 works
    public uint markerSet2; // SM_Map_v5_default.Markerset - 0x6B9636C0 works
    public byte unk6; // 0x00 work

    public GamePrepareForStartPacket()
    {
    }

    public GamePrepareForStartPacket(uint slot)
    {
        Slot = slot;
    }

    public void ReadFrom(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);

        Slot = reader.ReadUInt32();
        unk2 = reader.ReadUInt32();
        pLevelAsset = reader.ReadUInt32();
        markerSet1 = reader.ReadUInt32();
        markerSet2 = reader.ReadUInt32();
        unk6 = reader.ReadByte();
    }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

        writer.Write(Slot);
        writer.Write(unk2);
        writer.Write(pLevelAsset);
        writer.Write(markerSet1);
        writer.Write(markerSet2);
        writer.Write(unk6);
    }
}
