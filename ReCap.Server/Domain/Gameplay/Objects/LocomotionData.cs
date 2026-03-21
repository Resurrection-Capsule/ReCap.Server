using System.IO;
using System.Numerics;
using ReCap.Server.Adapters.RakNet;
using ReCap.Server.Util;

namespace ReCap.Server.Domain.Gameplay.Objects;

public class LobParams
{
    public float PlaneDirLinearParam { get; set; }
    public float UpLinearParam { get; set; }
    public float UpQuadraticParam { get; set; }
    public Vector3 LobUpDir { get; set; }
    public Vector3 PlaneDir { get; set; }
    public int BounceNum { get; set; }
    public float BounceRestitution { get; set; }
    public bool GroundCollisionOnly { get; set; }
    public bool StopBounceOnCreatures { get; set; }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true);
        var baseOffset = stream.Position;

        stream.Position = baseOffset + 0x18;
        writer.WriteBE(LobUpDir);

        stream.Position = baseOffset + 0x30;
        writer.WriteBE(BounceNum);
        writer.WriteBE(BounceRestitution);
        writer.Write(GroundCollisionOnly);
        writer.Write(StopBounceOnCreatures);

        stream.Position = baseOffset + 0x3C;
        writer.WriteBE(PlaneDir);

        stream.Position = baseOffset + 0x48;
        writer.WriteBE(PlaneDirLinearParam);
        writer.WriteBE(UpLinearParam);
        writer.WriteBE(UpQuadraticParam);

        stream.Position = baseOffset + 0x54;
    }

    public void WriteReflection(Stream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true);
        var reflector = new ReflectionSerializer(writer, 9);
        
        reflector.Begin();
        reflector.Write(0, () => writer.WriteBE(PlaneDirLinearParam));
        reflector.Write(1, () => writer.WriteBE(UpLinearParam));
        reflector.Write(2, () => writer.WriteBE(UpQuadraticParam));
        reflector.Write(3, () => writer.WriteBE(LobUpDir));
        reflector.Write(4, () => writer.WriteBE(PlaneDir));
        reflector.Write(5, () => writer.WriteBE(BounceNum));
        reflector.Write(6, () => writer.WriteBE(BounceRestitution));
        reflector.Write(7, () => writer.Write(GroundCollisionOnly));
        reflector.Write(8, () => writer.Write(StopBounceOnCreatures));
        reflector.End();
    }
}

public class ProjectileParams
{
    public float Speed { get; set; }
    public float Acceleration { get; set; }
    public uint JinkInfo { get; set; }
    public float Range { get; set; }
    public float SpinRate { get; set; }
    public Vector3 Direction { get; set; }
    public byte ProjectileFlags { get; set; }
    public float HomingDelay { get; set; }
    public float TurnRate { get; set; }
    public float TurnAcceleration { get; set; }
    public float Eccentricity { get; set; }
    public bool Piercing { get; set; }
    public bool IgnoreGroundCollide { get; set; }
    public bool IgnoreCreatureCollide { get; set; }
    public float CombatantSweepHeight { get; set; }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true);
        var baseOffset = stream.Position;

        stream.Position = baseOffset;
        writer.WriteBE(Speed);
        writer.WriteBE(Acceleration);
        writer.WriteBE(JinkInfo);
        writer.WriteBE(Range);
        writer.WriteBE(SpinRate);
        writer.WriteBE(Direction);
        writer.Write(ProjectileFlags);

        stream.Position = baseOffset + 0x24;
        writer.WriteBE(HomingDelay);
        writer.WriteBE(TurnRate);
        writer.WriteBE(TurnAcceleration);
        writer.Write(Piercing);
        writer.Write(IgnoreGroundCollide);
        writer.Write(IgnoreCreatureCollide);

        stream.Position = baseOffset + 0x34;
        writer.WriteBE(Eccentricity);
        writer.WriteBE(CombatantSweepHeight);

        stream.Position = baseOffset + 0x3C;
    }

    public void WriteReflection(Stream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true);
        var reflector = new ReflectionSerializer(writer, 15);
        
        reflector.Begin();
        reflector.Write(0, () => writer.WriteBE(Speed));
        reflector.Write(1, () => writer.WriteBE(Acceleration));
        reflector.Write(2, () => writer.WriteBE(JinkInfo));
        reflector.Write(3, () => writer.WriteBE(Range));
        reflector.Write(4, () => writer.WriteBE(SpinRate));
        reflector.Write(5, () => writer.WriteBE(Direction));
        reflector.Write(6, () => writer.Write(ProjectileFlags));
        reflector.Write(7, () => writer.WriteBE(HomingDelay));
        reflector.Write(8, () => writer.WriteBE(TurnRate));
        reflector.Write(9, () => writer.WriteBE(TurnAcceleration));
        reflector.Write(10, () => writer.WriteBE(Eccentricity));
        reflector.Write(11, () => writer.Write(Piercing));
        reflector.Write(12, () => writer.Write(IgnoreGroundCollide));
        reflector.Write(13, () => writer.Write(IgnoreCreatureCollide));
        reflector.Write(14, () => writer.WriteBE(CombatantSweepHeight));
        reflector.End();
    }
}

public class LocomotionData
{
    public ulong LobStartTime { get; set; }
    public float LobPrevSpeedModifier { get; set; }
    public LobParams LobParams { get; set; } = new();
    public ProjectileParams ProjectileParams { get; set; } = new();
    public uint GoalFlags { get; set; }
    public Vector3 GoalPosition { get; set; }
    public Vector3 PartialGoalPosition { get; set; }
    public Vector3 Facing { get; set; }
    public Vector3 ExternalLinearVelocity { get; set; }
    public Vector3 ExternalForce { get; set; }
    public float AllowedStopDistance { get; set; }
    public float DesiredStopDistance { get; set; }
    public uint TargetObjectId { get; set; }
    public Vector3 TargetPosition { get; set; }
    public Vector3 ExpectedGeoCollision { get; set; }
    public Vector3 InitialDirection { get; set; }
    public Vector3 Offset { get; set; }
    public int ReflectedLastUpdate { get; set; }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true);
        var baseOffset = stream.Position;

        stream.Position = baseOffset + 0x08;
        writer.WriteBE(ReflectedLastUpdate);

        stream.Position = baseOffset + 0x44;
        ProjectileParams.WriteTo(stream);

        stream.Position = baseOffset + 0x84;
        writer.WriteBE(ExpectedGeoCollision);
        writer.WriteBE(TargetObjectId);

        stream.Position = baseOffset + 0x9C;
        writer.WriteBE(InitialDirection);

        stream.Position = baseOffset + 0xD8;
        writer.WriteBE(LobStartTime);
        writer.WriteBE(LobPrevSpeedModifier);
        LobParams.WriteTo(stream);

        stream.Position = baseOffset + 0x138;
        writer.WriteBE(Offset);
        writer.WriteBE(GoalFlags);
        writer.WriteBE(GoalPosition);
        writer.WriteBE(PartialGoalPosition);

        stream.Position = baseOffset + 0x178;
        writer.WriteBE(Facing);
        writer.WriteBE(ExternalLinearVelocity);
        writer.WriteBE(ExternalForce);
        writer.WriteBE(AllowedStopDistance);
        writer.WriteBE(DesiredStopDistance);

        stream.Position = baseOffset + 0x1AC;
        writer.WriteBE(TargetPosition);

        stream.Position = baseOffset + 0x290;
    }

    public void WriteReflection(Stream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true);
        var reflector = new ReflectionSerializer(writer, 18);
        
        reflector.Begin();
        reflector.Write(0, () => writer.WriteBE(LobStartTime));
        reflector.Write(1, () => writer.WriteBE(LobPrevSpeedModifier));
        reflector.Write(2, () => LobParams.WriteReflection(stream));
        reflector.Write(3, () => ProjectileParams.WriteReflection(stream));
        reflector.Write(4, () => writer.WriteBE(GoalFlags));
        reflector.Write(5, () => writer.WriteBE(GoalPosition));
        reflector.Write(6, () => writer.WriteBE(PartialGoalPosition));
        reflector.Write(7, () => writer.WriteBE(Facing));
        reflector.Write(8, () => writer.WriteBE(ExternalLinearVelocity));
        reflector.Write(9, () => writer.WriteBE(ExternalForce));
        reflector.Write(10, () => writer.WriteBE(AllowedStopDistance));
        reflector.Write(11, () => writer.WriteBE(DesiredStopDistance));
        reflector.Write(12, () => writer.WriteBE(TargetObjectId));
        reflector.Write(13, () => writer.WriteBE(TargetPosition));
        reflector.Write(14, () => writer.WriteBE(ExpectedGeoCollision));
        reflector.Write(15, () => writer.WriteBE(InitialDirection));
        reflector.Write(16, () => writer.WriteBE(Offset));
        reflector.Write(17, () => writer.WriteBE(ReflectedLastUpdate));
        reflector.End();
    }
}
