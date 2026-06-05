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
        writer.Write(LobUpDir);

        stream.Position = baseOffset + 0x30;
        writer.Write(BounceNum);
        writer.Write(BounceRestitution);
        writer.Write(GroundCollisionOnly);
        writer.Write(StopBounceOnCreatures);

        stream.Position = baseOffset + 0x3C;
        writer.Write(PlaneDir);

        stream.Position = baseOffset + 0x48;
        writer.Write(PlaneDirLinearParam);
        writer.Write(UpLinearParam);
        writer.Write(UpQuadraticParam);

        stream.Position = baseOffset + 0x54;
    }

    public void WriteReflection(Stream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true);
        var reflector = new ReflectionSerializer(writer, 9);
        
        reflector.Begin();
        reflector.Write(0, () => writer.Write(PlaneDirLinearParam));
        reflector.Write(1, () => writer.Write(UpLinearParam));
        reflector.Write(2, () => writer.Write(UpQuadraticParam));
        reflector.Write(3, () => writer.Write(LobUpDir));
        reflector.Write(4, () => writer.Write(PlaneDir));
        reflector.Write(5, () => writer.Write(BounceNum));
        reflector.Write(6, () => writer.Write(BounceRestitution));
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
        writer.Write(Speed);
        writer.Write(Acceleration);
        writer.Write(JinkInfo);
        writer.Write(Range);
        writer.Write(SpinRate);
        writer.Write(Direction);
        writer.Write(ProjectileFlags);

        stream.Position = baseOffset + 0x24;
        writer.Write(HomingDelay);
        writer.Write(TurnRate);
        writer.Write(TurnAcceleration);
        writer.Write(Piercing);
        writer.Write(IgnoreGroundCollide);
        writer.Write(IgnoreCreatureCollide);

        stream.Position = baseOffset + 0x34;
        writer.Write(Eccentricity);
        writer.Write(CombatantSweepHeight);

        stream.Position = baseOffset + 0x3C;
    }

    public void WriteReflection(Stream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true);
        var reflector = new ReflectionSerializer(writer, 15);
        
        reflector.Begin();
        reflector.Write(0, () => writer.Write(Speed));
        reflector.Write(1, () => writer.Write(Acceleration));
        reflector.Write(2, () => writer.Write(JinkInfo));
        reflector.Write(3, () => writer.Write(Range));
        reflector.Write(4, () => writer.Write(SpinRate));
        reflector.Write(5, () => writer.Write(Direction));
        reflector.Write(6, () => writer.Write(ProjectileFlags));
        reflector.Write(7, () => writer.Write(HomingDelay));
        reflector.Write(8, () => writer.Write(TurnRate));
        reflector.Write(9, () => writer.Write(TurnAcceleration));
        reflector.Write(10, () => writer.Write(Eccentricity));
        reflector.Write(11, () => writer.Write(Piercing));
        reflector.Write(12, () => writer.Write(IgnoreGroundCollide));
        reflector.Write(13, () => writer.Write(IgnoreCreatureCollide));
        reflector.Write(14, () => writer.Write(CombatantSweepHeight));
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

    // C++ Locomotion::SetGoalPosition (Locomotion.cpp:477): reset stale target state, force flag 0x001.
    public void SetGoalPosition(Vector3 position)
    {
        TargetObjectId = 0;
        Facing = Vector3.Zero;
        TargetPosition = Vector3.Zero;
        ExternalLinearVelocity = Vector3.Zero;

        GoalFlags = 0x001;
        GoalPosition = position;
    }

    // C++ Locomotion::Stop (Locomotion.cpp:564): GoalPosition preserved, flags become 0x020.
    public void Stop()
    {
        TargetObjectId = 0;
        ExternalLinearVelocity = Vector3.Zero;
        TargetPosition = Vector3.Zero;
        Facing = Vector3.Zero;

        GoalFlags = 0x020;
    }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true);
        var baseOffset = stream.Position;

        stream.Position = baseOffset + 0x08;
        writer.Write(ReflectedLastUpdate);

        stream.Position = baseOffset + 0x44;
        ProjectileParams.WriteTo(stream);

        stream.Position = baseOffset + 0x84;
        writer.Write(ExpectedGeoCollision);
        writer.Write(TargetObjectId);

        stream.Position = baseOffset + 0x9C;
        writer.Write(InitialDirection);

        stream.Position = baseOffset + 0xD8;
        writer.Write(LobStartTime);
        writer.Write(LobPrevSpeedModifier);
        LobParams.WriteTo(stream);

        stream.Position = baseOffset + 0x138;
        writer.Write(Offset);
        writer.Write(GoalFlags);
        writer.Write(GoalPosition);
        writer.Write(PartialGoalPosition);

        stream.Position = baseOffset + 0x178;
        writer.Write(Facing);
        writer.Write(ExternalLinearVelocity);
        writer.Write(ExternalForce);
        writer.Write(AllowedStopDistance);
        writer.Write(DesiredStopDistance);

        stream.Position = baseOffset + 0x1AC;
        writer.Write(TargetPosition);

        stream.Position = baseOffset + 0x290;
    }

    public void WriteReflection(Stream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true);
        var reflector = new ReflectionSerializer(writer, 18);
        
        reflector.Begin();
        reflector.Write(0, () => writer.Write(LobStartTime));
        reflector.Write(1, () => writer.Write(LobPrevSpeedModifier));
        reflector.Write(2, () => LobParams.WriteReflection(stream));
        reflector.Write(3, () => ProjectileParams.WriteReflection(stream));
        reflector.Write(4, () => writer.Write(GoalFlags));
        reflector.Write(5, () => writer.Write(GoalPosition));
        reflector.Write(6, () => writer.Write(PartialGoalPosition));
        reflector.Write(7, () => writer.Write(Facing));
        reflector.Write(8, () => writer.Write(ExternalLinearVelocity));
        reflector.Write(9, () => writer.Write(ExternalForce));
        reflector.Write(10, () => writer.Write(AllowedStopDistance));
        reflector.Write(11, () => writer.Write(DesiredStopDistance));
        reflector.Write(12, () => writer.Write(TargetObjectId));
        reflector.Write(13, () => writer.Write(TargetPosition));
        reflector.Write(14, () => writer.Write(ExpectedGeoCollision));
        reflector.Write(15, () => writer.Write(InitialDirection));
        reflector.Write(16, () => writer.Write(Offset));
        reflector.Write(17, () => writer.Write(ReflectedLastUpdate));
        reflector.End();
    }
}
