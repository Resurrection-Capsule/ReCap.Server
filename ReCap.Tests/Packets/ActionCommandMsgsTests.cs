using ReCap.Server.Adapters.RakNet.Packets;

namespace ReCap.Tests.Packets;

// ActionCommandMsgs (0x9C) inbound parse — 40B common header (client FillCommandHeader
// @0x004e2150 + FUN_004e2970: u8 type, u8 commandStamp(rolling counter DAT_011205e0,
// enqueue-path commands only), u16 pad, u32 locomotionFlags, u32 objectId, vec3 pos,
// quat orient) + per-type payload (C++ Types.h:826-875, GetPayloadSize @0x00a1ca80).
public class ActionCommandMsgsTests
{
    private static ActionCommandMsgsPacket Parse(byte type, byte stamp, byte[] payload)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true))
        {
            w.Write(type);
            w.Write(stamp);
            w.Write((ushort)0);
            w.Write(0xAABBCCDDu);            // locomotionFlags
            w.Write(0x2u);                   // objectId
            w.Write(1f); w.Write(2f); w.Write(3f);
            w.Write(0f); w.Write(0f); w.Write(0f); w.Write(1f);
            w.Write(payload);
        }
        ms.Position = 0;

        var packet = new ActionCommandMsgsPacket();
        packet.ReadFrom(ms);
        return packet;
    }

    [Fact]
    public void Header_Reads_Type_Stamp_Flags_ObjectId()
    {
        var packet = Parse(12, 0x2A, Array.Empty<byte>());

        Assert.Equal(12, packet.CommandType);
        Assert.Equal(0x2A, packet.CommandStamp);
        Assert.Equal(0xAABBCCDDu, packet.LocomotionFlags);
        Assert.Equal(0x2u, packet.ObjectId);
        Assert.Equal(1f, packet.PosX);
        Assert.Equal(3f, packet.PosZ);
        Assert.Empty(packet.ExtraData);
    }

    [Fact]
    public void Overdrive_Payload_Is_Single_Slot_Byte()
    {
        var packet = Parse(6, 0x05, new byte[] { 0x01 });
        Assert.Equal(0x01, packet.ReadOverdriveSlot());
    }

    [Fact]
    public void Catalyst_Payload_Reads_ObjectId_And_Position()
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms))
        {
            w.Write(0xDEAD0001u);
            w.Write(10f); w.Write(20f); w.Write(30f);
            w.Write(0x000000FFu);            // rank (client writes 0xFF low byte)
        }
        var packet = Parse(9, 0x07, ms.ToArray());

        var (objectId, position) = packet.ReadCatalystData();
        Assert.Equal(0xDEAD0001u, objectId);
        Assert.Equal(10f, position.X);
        Assert.Equal(30f, position.Z);
    }

    [Fact]
    public void Ability_Payload_Reads_All_44_Bytes()
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms))
        {
            w.Write(0xBEEF0002u);                      // targetId
            w.Write(1f); w.Write(2f); w.Write(3f);     // cursorPosition
            w.Write(4f); w.Write(5f); w.Write(6f);     // targetPosition
            w.Write(3u);                               // index (ability slot)
            w.Write(-1);                               // rank
            w.Write(0u);                               // unk
            w.Write(0x123456u);                        // userData
        }
        var payload = ms.ToArray();
        Assert.Equal(44, payload.Length);
        var packet = Parse(7, 0x09, payload);

        var (targetId, cursorPos, targetPos, index, rank, userData) = packet.ReadAbilityData();
        Assert.Equal(0xBEEF0002u, targetId);
        Assert.Equal(2f, cursorPos.Y);
        Assert.Equal(6f, targetPos.Z);
        Assert.Equal(3u, index);
        Assert.Equal(-1, rank);
        Assert.Equal(0x123456u, userData);
    }
}
