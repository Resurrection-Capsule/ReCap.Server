using System.IO;
using System.Numerics;
using ReCap.Server.Adapters.RakNet;
using ReCap.Server.Util;

namespace ReCap.Server.Domain.Gameplay.Objects;

public class SporelabsObject
{
    public byte Team { get; set; } = 1;
    public bool PlayerControlled { get; set; }
    public uint InputSyncStamp { get; set; }
    public byte PlayerIdx { get; set; }
    public Vector3 LinearVelocity { get; set; }
    public Vector3 AngularVelocity { get; set; }
    public Vector3 Position { get; set; }
    public Quaternion Orientation { get; set; } = Quaternion.Identity;
    public float Scale { get; set; } = 1f;
    public float MarkerScale { get; set; } = 1f;
    public uint LastAnimationState { get; set; }
    public ulong LastAnimationPlayTimeMs { get; set; }
    public uint OverrideMoveIdleAnimationState { get; set; }
    public uint GraphicsState { get; set; }
    public ulong GraphicsStateStartTimeMs { get; set; }
    public ulong NewGraphicsStateStartTimeMs { get; set; }
    public bool Visible { get; set; } = true;
    public bool HasCollision { get; set; }
    public uint OwnerID { get; set; }
    public byte MovementType { get; set; }
    public bool DisableRepulsion { get; set; }
    public uint InteractableState { get; set; }
    public uint SourceMarkerKeyMarkerId { get; set; }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true);
        var baseOffset = stream.Position;

        stream.Position = baseOffset + 0x010;
        writer.Write(Scale);
        writer.Write(MarkerScale);

        stream.Position = baseOffset + 0x018;
        writer.Write(Position);
        writer.Write(Orientation);
        writer.Write(LinearVelocity);
        writer.Write(AngularVelocity);

        stream.Position = baseOffset + 0x050;
        writer.Write(OwnerID);
        writer.Write(Team);
        writer.Write(PlayerIdx);

        stream.Position = baseOffset + 0x058;
        writer.Write(InputSyncStamp);
        writer.Write(PlayerControlled);

        stream.Position = baseOffset + 0x05F;
        writer.Write(Visible);
        writer.Write(HasCollision);
        writer.Write(MovementType);

        stream.Position = baseOffset + 0x088;
        writer.Write(SourceMarkerKeyMarkerId);

        stream.Position = baseOffset + 0x0AC;
        writer.Write(LastAnimationState);

        stream.Position = baseOffset + 0x0B8;
        writer.Write(LastAnimationPlayTimeMs);
        writer.Write(OverrideMoveIdleAnimationState);

        stream.Position = baseOffset + 0x258;
        writer.Write(GraphicsState);

        stream.Position = baseOffset + 0x260;
        writer.Write(GraphicsStateStartTimeMs);
        writer.Write(NewGraphicsStateStartTimeMs);

        stream.Position = baseOffset + 0x284;
        writer.Write(DisableRepulsion);

        stream.Position = baseOffset + 0x288;
        writer.Write(InteractableState);

        stream.Position = baseOffset + 0x308;
    }

    public void WriteReflection(Stream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true);
        var reflector = new ReflectionSerializer(writer, 23);
        
        reflector.Begin();
        reflector.Write(0, () => writer.Write(Team));
        reflector.Write(1, () => writer.Write(PlayerControlled));
        reflector.Write(2, () => writer.Write(InputSyncStamp));
        reflector.Write(3, () => writer.Write(PlayerIdx));
        reflector.Write(4, () => writer.Write(LinearVelocity));
        reflector.Write(5, () => writer.Write(AngularVelocity));
        reflector.Write(6, () => writer.Write(Position));
        reflector.Write(7, () => writer.Write(Orientation));
        reflector.Write(8, () => writer.Write(Scale));
        reflector.Write(9, () => writer.Write(MarkerScale));
        reflector.Write(10, () => writer.Write(LastAnimationState));
        reflector.Write(11, () => writer.Write(LastAnimationPlayTimeMs));
        reflector.Write(12, () => writer.Write(OverrideMoveIdleAnimationState));
        reflector.Write(13, () => writer.Write(GraphicsState));
        reflector.Write(14, () => writer.Write(GraphicsStateStartTimeMs));
        reflector.Write(15, () => writer.Write(NewGraphicsStateStartTimeMs));
        reflector.Write(16, () => writer.Write(Visible));
        reflector.Write(17, () => writer.Write(HasCollision));
        reflector.Write(18, () => writer.Write(OwnerID));
        reflector.Write(19, () => writer.Write(MovementType));
        reflector.Write(20, () => writer.Write(DisableRepulsion));
        reflector.Write(21, () => writer.Write(InteractableState));
        reflector.Write(22, () => writer.Write(SourceMarkerKeyMarkerId));
        reflector.End();
    }
}
