using System.IO;
using System.Text;

using ReCap.Server.Domain.Gameplay.Objects;
using ReCap.Server.Util;

namespace ReCap.Server.Adapters.RakNet.Packets;

public class ObjectUpdatePacket : IRakNetPacket
{
    public PacketType Type => PacketType.ObjectUpdate;
    public uint ObjectId { get; set; }
    public SporelabsObject? ObjectData { get; set; }

    public void ReadFrom(Stream stream)
    {
        // Server typically does not read ObjectUpdate from Client
    }
    
    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(ObjectId);
        
        ObjectData?.WriteReflection(stream);
    }
}