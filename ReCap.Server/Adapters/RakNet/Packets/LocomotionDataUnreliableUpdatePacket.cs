using System.IO;
using System.Numerics;
using System.Text;

namespace ReCap.Server.Adapters.RakNet.Packets;

// LocomotionDataUnreliableUpdate (0x95). Client handler ClientNet::OnGmsLocomotionDataUnreliableUpdate
// @0x0053e600 (Ghidra): reads a 16-byte body — objId (u32) + vec3 goalPosition — and writes it into the
// object's locomotion component (goalPosition@+0x148, partialGoalPosition@+0x154), no local-hero gate.
// This is the smooth-movement channel for any object. LE per the game protocol.
public class LocomotionDataUnreliableUpdatePacket : IRakNetPacket
{
    public PacketType Type => PacketType.LocomotionDataUnreliableUpdate;
    public uint ObjectId { get; set; }
    public Vector3 GoalPosition { get; set; }

    public void ReadFrom(Stream stream) { }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(ObjectId);
        writer.Write(GoalPosition.X);
        writer.Write(GoalPosition.Y);
        writer.Write(GoalPosition.Z);
    }
}
