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

        writer.Write(ObjectId);
        writer.Write(Locomotion.GoalFlags);
        writer.Write(Locomotion.GoalPosition);
        writer.Write(Locomotion.Facing);
        writer.Write(Locomotion.ExternalLinearVelocity);
        writer.Write(Locomotion.ExternalForce);
        writer.Write(Locomotion.AllowedStopDistance);
        writer.Write(Locomotion.DesiredStopDistance);
        writer.Write(Locomotion.TargetPosition);
        writer.Write(Locomotion.TargetObjectId);
    }
}
