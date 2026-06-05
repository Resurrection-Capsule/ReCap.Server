using System.IO;
using System.Text;

namespace ReCap.Server.Adapters.RakNet.Packets;

// C++ Server::SendAnimationState (Server.cpp:1989-2001), raw LE 25B body:
// u32 objectId | u32 state | u64 timestamp | u8 overlay | f32 scale | u32 state (repeated).
public class SetAnimationStatePacket : IRakNetPacket
{
    public PacketType Type => PacketType.SetAnimationState;

    public uint ObjectId { get; set; }
    public uint State { get; set; }
    public ulong Timestamp { get; set; }
    public bool Overlay { get; set; }
    public float Scale { get; set; } = 1f;

    public void ReadFrom(Stream stream) { }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(ObjectId);
        writer.Write(State);
        writer.Write(Timestamp);
        writer.Write(Overlay);
        writer.Write(Scale);
        writer.Write(State);
    }
}
