using ReCap.Server.Adapters.RakNet.Packets;

namespace ReCap.Tests.Packets;

// ActionCommandResponse (0xA8) — client handler ClientNet::OnGmsActionCommandResponse
// @0x0053cb10 reads EXACTLY 0x38=56B body, routes on byte +1 (FUN_004d9ba0).
// Type 8 = movement GO: client executes its click-stashed goal via
// Locomotion::SetGoalPositionWithDistance — the retail smooth-movement protocol.
// C++ ref ability sender (Server.cpp:1659) writes byte0=0x00; remaining fields unused
// by the type-8 path. CLIENT_MOVEMENT_CONTRACT.md.
public class ActionCommandResponseTests
{
    [Fact]
    public void MovementGo_Body_Is_56_Bytes_Type8()
    {
        using var ms = new MemoryStream();
        new ActionCommandResponsePacket { ActionType = 8 }.WriteTo(ms);
        var bytes = ms.ToArray();

        Assert.Equal(56, bytes.Length);
        Assert.Equal(0x00, bytes[0]);
        Assert.Equal(0x08, bytes[1]);
        Assert.All(bytes[2..], b => Assert.Equal(0x00, b));
    }
}
