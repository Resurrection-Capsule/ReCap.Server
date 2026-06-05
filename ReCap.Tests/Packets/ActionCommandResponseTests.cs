using ReCap.Server.Adapters.RakNet.Packets;

namespace ReCap.Tests.Packets;

// ActionCommandResponse (0xA8) — client handler ClientNet::OnGmsActionCommandResponse
// @0x0053cb10 reads EXACTLY 0x38=56B body, routes on byte +1 (FUN_004d9ba0).
// Case 2 = complete: clears the pending-command lock (block+4, set by FUN_004d9150 with a
// 3000ms deadline) when byte +0 echoes the rolling command stamp the client sent in
// ActionCommandMsgs byte +0x01; UserData (+0x34) = 0xFFFFFFFF (<0) skips the client
// DAT_0143fe3c write in FUN_004e2030. Type 8 = movement GO (click-stashed goal via
// Locomotion::SetGoalPositionWithDistance). C++ ref shape: Server.cpp:1659.
public class ActionCommandResponseTests
{
    [Fact]
    public void MovementGo_Body_Is_56_Bytes_Type8()
    {
        using var ms = new MemoryStream();
        new ActionCommandResponsePacket { ResponseType = 8 }.WriteTo(ms);
        var bytes = ms.ToArray();

        Assert.Equal(56, bytes.Length);
        Assert.Equal(0x00, bytes[0]);
        Assert.Equal(0x08, bytes[1]);
        Assert.All(bytes[2..], b => Assert.Equal(0x00, b));
    }

    [Fact]
    public void EmoteUnlock_Type2_Echoes_Stamp_And_Skips_UserData()
    {
        using var ms = new MemoryStream();
        new ActionCommandResponsePacket
        {
            SyncStamp = 0x2A,
            ResponseType = 2,
            UserData = 0xFFFFFFFF
        }.WriteTo(ms);
        var bytes = ms.ToArray();

        Assert.Equal(56, bytes.Length);
        Assert.Equal(0x2A, bytes[0]);                          // stamp echo → case-2 gate (block+8)
        Assert.Equal(0x02, bytes[1]);                          // responseType=2 (complete)
        Assert.All(bytes[2..0x34], b => Assert.Equal(0x00, b));
        Assert.Equal(0xFFFFFFFFu, BitConverter.ToUInt32(bytes, 0x34)); // userData=-1 → no DAT_0143fe3c write
    }

    [Fact]
    public void Full_Field_Offsets_Match_Client_Layout()
    {
        using var ms = new MemoryStream();
        new ActionCommandResponsePacket
        {
            SyncStamp = 0x01,
            ResponseType = 1,
            ObjectId = 0x11223344,
            Flags08 = 0x55667788,
            Time10 = 0x0102030405060708,
            TimeImmobilizedUntil = 0x1112131415161718,
            TimeCooldownUntil = 0x2122232425262728,
            Time28 = 0x3132333435363738,
            UserData = 0x4321
        }.WriteTo(ms);
        var bytes = ms.ToArray();

        Assert.Equal(56, bytes.Length);
        Assert.Equal(0x11223344u, BitConverter.ToUInt32(bytes, 0x04));
        Assert.Equal(0x55667788u, BitConverter.ToUInt32(bytes, 0x08));
        Assert.Equal(0u, BitConverter.ToUInt32(bytes, 0x0C));
        Assert.Equal(0x0102030405060708ul, BitConverter.ToUInt64(bytes, 0x10));
        Assert.Equal(0x1112131415161718ul, BitConverter.ToUInt64(bytes, 0x18));
        Assert.Equal(0x2122232425262728ul, BitConverter.ToUInt64(bytes, 0x20));
        Assert.Equal(0x3132333435363738ul, BitConverter.ToUInt64(bytes, 0x28));
        Assert.Equal(0u, BitConverter.ToUInt32(bytes, 0x30));
        Assert.Equal(0x4321u, BitConverter.ToUInt32(bytes, 0x34));
    }
}
