using System.IO;
using System.Numerics;
using ReCap.Server.Adapters.RakNet;
using ReCap.Server.Adapters.RakNet.Packets;

namespace ReCap.Tests.Packets;

public class LocomotionUnreliablePacketTests
{
    [Fact]
    public void WritesObjectIdThenGoalPositionLE16Bytes()
    {
        var packet = new LocomotionDataUnreliableUpdatePacket
        {
            ObjectId = 0x11223344,
            GoalPosition = new Vector3(1.0f, 2.0f, 3.0f),
        };
        using var ms = new MemoryStream();
        packet.WriteTo(ms);
        var bytes = ms.ToArray();

        // 0x95 body (client OnGmsLocomotionDataUnreliableUpdate @0x0053e600): objId u32 LE + vec3 LE.
        Assert.Equal(16, bytes.Length);
        Assert.Equal(new byte[] { 0x44, 0x33, 0x22, 0x11 }, bytes[0..4]);
        Assert.Equal(BitConverter.GetBytes(1.0f), bytes[4..8]);
        Assert.Equal(BitConverter.GetBytes(2.0f), bytes[8..12]);
        Assert.Equal(BitConverter.GetBytes(3.0f), bytes[12..16]);
        Assert.Equal(PacketType.LocomotionDataUnreliableUpdate, packet.Type);
    }
}
