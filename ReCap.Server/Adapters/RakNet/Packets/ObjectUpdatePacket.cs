using System.Text;

namespace ReCap.Server.Adapters.RakNet.Packets;

public class ObjectUpdatePacket : IRakNetPacket
{
    public PacketType Type => PacketType.ObjectUpdate;
    public uint ObjectId { get; set; }

    public void ReadFrom(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);
        ObjectId = reader.ReadUInt32();
    }
    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(ObjectId);
    }
}