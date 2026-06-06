using System.IO;
using System.Text;

using ReCap.Server.Domain.Gameplay.Objects;
using ReCap.Server.Util;

namespace ReCap.Server.Adapters.RakNet.Packets;

public class ObjectPlayerMovePacket : IRakNetPacket
{
    public PacketType Type => PacketType.ObjectPlayerMove;
    public uint ObjectId { get; set; }
    public LocomotionData? Locomotion { get; set; }

    public void ReadFrom(Stream stream)
    {
        // Typically not sent from client in this direction, but handled
    }

    public void WriteTo(Stream stream)
    {
        var locomotion = Locomotion ?? throw new InvalidOperationException("ObjectPlayerMovePacket.Locomotion not set");
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

        writer.Write(ObjectId);
        writer.Write(locomotion.GoalFlags);
        writer.Write(locomotion.GoalPosition);
        writer.Write(locomotion.Facing);
        writer.Write(locomotion.ExternalLinearVelocity);
        writer.Write(locomotion.ExternalForce);
        writer.Write(locomotion.AllowedStopDistance);
        writer.Write(locomotion.DesiredStopDistance);
        writer.Write(locomotion.TargetPosition);
        writer.Write(locomotion.TargetObjectId);
    }
}
