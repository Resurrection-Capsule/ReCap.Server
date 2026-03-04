using System.Numerics;
using System.Text;

namespace ReCap.Server.Adapters.RakNet.Packets;

public struct ObjectCreationData
{
    public uint noun;
    public Vector3 position;
    public float rotXDegrees;
    public float rotYDegrees;
    public float rotZDegrees;
    public float scale;
    public byte team;
    public uint ownerId;
    public byte flags;
}

public class ObjectCreatePacket : IRakNetPacket
{
    public PacketType Type => PacketType.ObjectCreate;
    public uint ObjectId { get; set; }
    public ObjectCreationData objectCreationData { get; set; }

    public void ReadFrom(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);

        ObjectId = reader.ReadUInt32();
        
        /* todo reflection read*/
    }
    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

        writer.Write(ObjectId);

        // This should be handled by the reflection code eventually
        writer.Write((ushort)0x1F2F);
        writer.Write(objectCreationData.noun);
        writer.Write(objectCreationData.position.X);
        writer.Write(objectCreationData.position.Y);
        writer.Write(objectCreationData.position.Z);
        writer.Write(objectCreationData.rotXDegrees);
        writer.Write(objectCreationData.rotYDegrees);
        writer.Write(objectCreationData.rotZDegrees);
        writer.Write(objectCreationData.scale);
        writer.Write(objectCreationData.team);
        writer.Write(objectCreationData.ownerId);
        writer.Write(objectCreationData.flags);
    }
}

