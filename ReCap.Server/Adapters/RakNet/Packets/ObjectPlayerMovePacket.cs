using System.IO;
using System.Text;

using ReCap.Server.Domain.Gameplay.Objects;
using ReCap.Server.Util;

namespace ReCap.Server.Adapters.RakNet.Packets;

public class ObjectPlayerMovePacket : IRakNetPacket
{
    public PacketType Type => PacketType.ObjectPlayerMove;
    public uint ObjectId { get; set; }
    public LocomotionData Locomotion { get; set; }

    public void ReadFrom(Stream stream)
    {
        // Typically not sent from client in this direction, but handled
    }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

        writer.WriteBE(ObjectId);
        writer.WriteBE(Locomotion.GoalFlags);
        writer.WriteBE(Locomotion.GoalPosition);
        writer.WriteBE(Locomotion.Facing);
        writer.WriteBE(Locomotion.ExternalLinearVelocity);
        writer.WriteBE(Locomotion.ExternalForce);
        writer.WriteBE(Locomotion.AllowedStopDistance);
        writer.WriteBE(Locomotion.DesiredStopDistance);
        writer.WriteBE(Locomotion.TargetPosition);
        writer.WriteBE(Locomotion.TargetObjectId);
    }
}
