using System.Collections.Generic;
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
    public uint LastAnimationPlayTimeMs { get; set; }
    public uint OverrideMoveIdleAnimationState { get; set; }
    public uint GraphicsState { get; set; }
    public uint GraphicsStateStartTimeMs { get; set; }
    public uint NewGraphicsStateStartTimeMs { get; set; }
    public bool Visible { get; set; } = true;
    public bool HasCollision { get; set; }
    public uint OwnerID { get; set; }
    public byte MovementType { get; set; }
    public bool DisableRepulsion { get; set; }
    public uint InteractableState { get; set; }
    public uint SourceMarkerKeyMarkerId { get; set; }

    // C++ Object::mDataBits — Object::WriteReflection emits ONLY fields whose bit is set
    // (reflection_serializer<23>, field-ID + 0xFF terminator). Emitting all 23 unconditionally
    // produced a 193B ObjectCreate vs C++'s 93B and crashed the client on deploy.
    internal HashSet<byte> _dataBits = new();
    public void SetDataBit(byte field) => _dataBits.Add(field);
    public void ResetDataBits() => _dataBits.Clear();

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
        if (_dataBits.Contains(0)) reflector.Write(0, () => writer.Write(Team));
        if (_dataBits.Contains(1)) reflector.Write(1, () => writer.Write(PlayerControlled));
        if (_dataBits.Contains(2)) reflector.Write(2, () => writer.Write(InputSyncStamp));
        if (_dataBits.Contains(3)) reflector.Write(3, () => writer.Write(PlayerIdx));
        if (_dataBits.Contains(4)) reflector.Write(4, () => writer.Write(LinearVelocity));
        if (_dataBits.Contains(5)) reflector.Write(5, () => writer.Write(AngularVelocity));
        if (_dataBits.Contains(6)) reflector.Write(6, () => writer.Write(Position));
        if (_dataBits.Contains(7)) reflector.Write(7, () => writer.Write(Orientation));
        if (_dataBits.Contains(8)) reflector.Write(8, () => writer.Write(Scale));
        if (_dataBits.Contains(9)) reflector.Write(9, () => writer.Write(MarkerScale));
        if (_dataBits.Contains(10)) reflector.Write(10, () => writer.Write(LastAnimationState));
        if (_dataBits.Contains(11)) reflector.Write(11, () => writer.Write(LastAnimationPlayTimeMs));
        if (_dataBits.Contains(12)) reflector.Write(12, () => writer.Write(OverrideMoveIdleAnimationState));
        if (_dataBits.Contains(13)) reflector.Write(13, () => writer.Write(GraphicsState));
        if (_dataBits.Contains(14)) reflector.Write(14, () => writer.Write(GraphicsStateStartTimeMs));
        if (_dataBits.Contains(15)) reflector.Write(15, () => writer.Write(NewGraphicsStateStartTimeMs));
        if (_dataBits.Contains(16)) reflector.Write(16, () => writer.Write(Visible));
        if (_dataBits.Contains(17)) reflector.Write(17, () => writer.Write(HasCollision));
        if (_dataBits.Contains(18)) reflector.Write(18, () => writer.Write(OwnerID));
        if (_dataBits.Contains(19)) reflector.Write(19, () => writer.Write(MovementType));
        if (_dataBits.Contains(20)) reflector.Write(20, () => writer.Write(DisableRepulsion));
        if (_dataBits.Contains(21)) reflector.Write(21, () => writer.Write(InteractableState));
        if (_dataBits.Contains(22)) reflector.Write(22, () => writer.Write(SourceMarkerKeyMarkerId));
        reflector.End();
    }
}
