using System.IO;
using System.Text;

using ReCap.Server.Domain.Gameplay.Objects;
using ReCap.Server.Util;

namespace ReCap.Server.Adapters.RakNet.Packets;

public class ObjectCreatePacket : IRakNetPacket
{
    public PacketType Type => PacketType.ObjectCreate;
    public uint ObjectId { get; set; }
    public GameObjectCreateData CreateData { get; set; } = new();
    public SporelabsObject ObjectData { get; set; } = new();

    public void ReadFrom(Stream stream)
    {
        // Server does not receive this packet
    }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

        writer.WriteBE(ObjectId);
        
        CreateData.WriteReflection(stream);
        ObjectData.WriteReflection(stream);
    }
}

