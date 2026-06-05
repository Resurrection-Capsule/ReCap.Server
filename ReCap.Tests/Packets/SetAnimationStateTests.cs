using ReCap.Server.Adapters.RakNet.Packets;

namespace ReCap.Tests.Packets;

// SetAnimationState (0xA5) — C++ Server::SendAnimationState (Server.cpp:1989-2001):
// u32 objId | u32 state | u64 timestamp | u8 overlay | f32 scale | u32 state (again) = 25B body.
// Dance/Taunt use state = FNV("emote_dance_all"/"emote_taunt_all"), overlay=false, scale=1
// (Server.cpp:966-977, Instance.cpp:990 + Instance.h:202 default).
public class SetAnimationStateTests
{
    [Fact]
    public void Body_Is_25_Bytes_With_State_Repeated()
    {
        var pkt = new SetAnimationStatePacket
        {
            ObjectId = 2,
            State = 0x11223344,
            Timestamp = 0x0102030405060708UL,
            Overlay = false,
            Scale = 1f
        };

        using var ms = new MemoryStream();
        pkt.WriteTo(ms);
        var bytes = ms.ToArray();

        Assert.Equal(25, bytes.Length);
        Assert.Equal(2u, BitConverter.ToUInt32(bytes, 0));
        Assert.Equal(0x11223344u, BitConverter.ToUInt32(bytes, 4));
        Assert.Equal(0x0102030405060708UL, BitConverter.ToUInt64(bytes, 8));
        Assert.Equal(0, bytes[16]);
        Assert.Equal(1f, BitConverter.ToSingle(bytes, 17));
        Assert.Equal(0x11223344u, BitConverter.ToUInt32(bytes, 21));
    }
}
