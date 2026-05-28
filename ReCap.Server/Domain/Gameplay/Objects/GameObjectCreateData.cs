using System.IO;
using System.Numerics;
using ReCap.Server.Adapters.RakNet;
using ReCap.Server.Util;

namespace ReCap.Server.Domain.Gameplay.Objects;

public class GameObjectCreateData
{
    public uint Noun { get; set; }
    public Vector3 Position { get; set; }
    public float RotXDegrees { get; set; }
    public float RotYDegrees { get; set; }
    public float RotZDegrees { get; set; }
    public ulong AssetId { get; set; }
    public float Scale { get; set; }
    public byte Team { get; set; }
    public bool HasCollision { get; set; }
    public bool PlayerControlled { get; set; }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true);
        var baseOffset = stream.Position;

        stream.Position = baseOffset;
        writer.Write(Noun);
        writer.Write(Position);
        writer.Write(RotXDegrees);
        writer.Write(RotYDegrees);
        writer.Write(RotZDegrees);

        stream.Position = baseOffset + 0x20;
        writer.Write(AssetId);
        writer.Write(Scale);
        writer.Write(Team);
        writer.Write(HasCollision);
        writer.Write(PlayerControlled);

        stream.Position = baseOffset + 0x70;
    }

    public void WriteReflection(Stream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true);
        var reflector = new ReflectionSerializer(writer, 10);
        
        reflector.Begin();
        reflector.Write(0, () => writer.Write(Noun));
        reflector.Write(1, () => writer.Write(Position));
        reflector.Write(2, () => writer.Write(RotXDegrees));
        reflector.Write(3, () => writer.Write(RotYDegrees));
        reflector.Write(4, () => writer.Write(RotZDegrees));
        reflector.Write(5, () => writer.Write(AssetId));
        reflector.Write(6, () => writer.Write(Scale));
        reflector.Write(7, () => writer.Write(Team));
        reflector.Write(8, () => writer.Write(HasCollision));
        reflector.Write(9, () => writer.Write(PlayerControlled));
        reflector.End();
    }
}
