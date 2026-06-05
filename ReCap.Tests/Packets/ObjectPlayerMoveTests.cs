using System.Numerics;
using ReCap.Server.Adapters.RakNet.Packets;
using ReCap.Server.Domain.Gameplay.Objects;

namespace ReCap.Tests.Packets;

// ObjectPlayerMove (0x91) — C++ Server::SendObjectPlayerMove (Server.cpp:1716, "100%").
// Movement contract: Locomotion::SetGoalPosition (Locomotion.cpp:477) resets target/facing/
// velocity and FORCES GoalFlags=0x001 before ORing the client's goalFlags (Server.cpp:726-728).
// Client log proved goalFlags=0x0 from the wire — without bit 0x001 the client never moves.
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
    public void Movement_Body_Is_80_Bytes_With_GoalFlag_Bit0_On_Wire()
    {
        var loco = new LocomotionData();
        loco.SetGoalPosition(new Vector3(-597.7f, 393.5f, 50.0f));

        var bytes = Serialize(new ObjectPlayerMovePacket { ObjectId = 2, Locomotion = loco });

        // 4 objId + 4 flags + 12 goal + 12 facing + 12 extLinVel + 12 extForce
        // + 4 allowedStop + 4 desiredStop + 12 targetPos + 4 targetId = 80 (C++ BitStream(81) = +1 type byte).
        Assert.Equal(80, bytes.Length);
        Assert.Equal(2u, BitConverter.ToUInt32(bytes, 0));
        Assert.Equal(0x001u, BitConverter.ToUInt32(bytes, 4));
        Assert.Equal(-597.7f, BitConverter.ToSingle(bytes, 8));
    }
}
