using System.IO;
using System.Numerics;
using System.Text;
using ReCap.Server.Util;

namespace ReCap.Server.Adapters.RakNet.Packets;

// C++ RakNet/Server.cpp:1702-1713. Raw 33 bytes: u32 objectId | vec3 position | quat orientation (xyzw LE).
// No reflection bitmap. Guard DBG_SEND_TELEPORTS=true by default (Server.cpp:80).
public class ObjectTeleportPacket : IRakNetPacket
{
    public PacketType Type => PacketType.ObjectTeleport;
    public uint ObjectId { get; set; }
    public Vector3 Position { get; set; }
    public Quaternion Orientation { get; set; } = Quaternion.Identity;

    public void ReadFrom(Stream stream) { }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(ObjectId);
        writer.Write(Position);
        writer.Write(Orientation);
    }
}
