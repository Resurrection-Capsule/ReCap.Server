using System.Numerics;
using ReCap.Server.Adapters.RakNet.Packets;
using ReCap.Server.Domain.Gameplay.Objects;

namespace ReCap.Tests.Packets;

// ObjectPlayerMove (0x91) — C++ Server::SendObjectPlayerMove (Server.cpp:1716, "100%").
// Movement contract: Locomotion::SetGoalPosition (Locomotion.cpp:477) resets target/facing/
// velocity and FORCES GoalFlags=0x001 before ORing the client's goalFlags (Server.cpp:726-728).
// WIRE GROUND TRUTH (cpp_loopback.pcapng, working binary): every click produces
// 0x90 ObjectTeleport(pos=goal, quat=0) THEN 0x91 with flags=0x021 — the binary runs
// teleportMovement=true (Server.cpp:76/722-724, goalFlags |= 0x020). flags=0x001 alone was
// client-tested 2026-06-05: the client does NOT move. 0x021 is the verified moving contract.
public class ObjectPlayerMoveTests
{
    private static byte[] Serialize(IRakNetPacket p)
    {
        using var ms = new MemoryStream();
        p.WriteTo(ms);
        return ms.ToArray();
    }

    [Fact]
    public void SetGoalPosition_Forces_GoalFlag_0x001_And_Resets_Stale_State()
    {
        var loco = new LocomotionData
        {
            TargetObjectId = 9,
            Facing = new Vector3(1, 2, 3),
            TargetPosition = new Vector3(4, 5, 6),
            ExternalLinearVelocity = new Vector3(7, 8, 9)
        };

        loco.SetGoalPosition(new Vector3(-597.7f, 393.5f, 50.0f));

        Assert.Equal(0x001u, loco.GoalFlags);
        Assert.Equal(new Vector3(-597.7f, 393.5f, 50.0f), loco.GoalPosition);
        Assert.Equal(0u, loco.TargetObjectId);
        Assert.Equal(Vector3.Zero, loco.Facing);
        Assert.Equal(Vector3.Zero, loco.TargetPosition);
        Assert.Equal(Vector3.Zero, loco.ExternalLinearVelocity);
    }

    [Fact]
    public void Movement_0x91_Matches_Cpp_Capture_Bytes()
    {
        // Capture msg #928: objId=2 flags=0x21 goal=(-129.75,-134.66,8.04), all else zero.
        var expected = Convert.FromHexString(
            "020000002100000020c001c3cea706c301b20041" + new string('0', 120));

        var goal = new Vector3(
            BitConverter.ToSingle(expected, 8),
            BitConverter.ToSingle(expected, 12),
            BitConverter.ToSingle(expected, 16));
        var loco = new LocomotionData();
        loco.SetGoalPosition(goal);
        loco.GoalFlags |= 0x020;

        var bytes = Serialize(new ObjectPlayerMovePacket { ObjectId = 2, Locomotion = loco });

        // 4 objId + 4 flags + 12 goal + 12 facing + 12 extLinVel + 12 extForce
        // + 4 allowedStop + 4 desiredStop + 12 targetPos + 4 targetId = 80 (C++ BitStream(81) = +1 type byte).
        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void Movement_0x90_Teleport_Matches_Cpp_Capture_Bytes()
    {
        // Capture msg #926: objId=2 pos=goal, orientation = ZERO quat (not identity) on movement clicks.
        var expected = Convert.FromHexString(
            "0200000020c001c3cea706c301b20041" + new string('0', 32));

        var bytes = Serialize(new ObjectTeleportPacket
        {
            ObjectId = 2,
            Position = new Vector3(
                BitConverter.ToSingle(expected, 4),
                BitConverter.ToSingle(expected, 8),
                BitConverter.ToSingle(expected, 12)),
            Orientation = default
        });

        Assert.Equal(expected, bytes);
    }
}
