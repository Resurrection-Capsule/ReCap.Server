using ReCap.Server.Adapters.RakNet;
using ReCap.Server.Adapters.RakNet.Packets;

namespace ReCap.Tests.Packets;

// Modifier lifecycle messages 0xA2/0xA3/0xA4 — raw fixed layouts read by ClientNet::ApplyModifier*
// (@0x0053c590 / 0x0053c280 / 0x0053c320). LE per the game protocol ([[endianness-wrapper-is-LE]]).
public class ModifierPacketTests
{
    private static byte[] Serialize(IRakNetPacket p)
    {
        using var ms = new MemoryStream();
        p.WriteTo(ms);
        return ms.ToArray();
    }

    [Fact]
    public void Created_Writes_37_Bytes_Fixed_LE()
    {
        var bytes = Serialize(new ModifierCreatedPacket
        {
            TargetId = 0x000000A2u,
            ModifierGuid = 0xDEADBEEFu,
            InstanceId = 0x00000007u,
            DurationMs = 0x0000EA60u, // 60000
            Overdrive = 1,
            StackCount = 1,
            StartTime = 0x1122334455667788ul,
            SourceId = 0,
            Bind = 0,
        });

        Assert.Equal(37, bytes.Length);
        Assert.Equal(Convert.FromHexString(
            "a2000000" +          // targetId
            "efbeadde" +          // modifierGuid
            "07000000" +          // instanceId
            "60ea0000" +          // durationMs
            "01000000" +          // overdrive
            "01000000" +          // stackCount
            "8877665544332211" +  // startTime u64
            "00000000" +          // sourceId
            "00"),                // bind
            bytes);
    }

    [Fact]
    public void Updated_Writes_21_Bytes_Fixed_LE()
    {
        var bytes = Serialize(new ModifierUpdatedPacket
        {
            TargetId = 0x000000A2u,
            InstanceId = 0x00000007u,
            StartTime = ModifierUpdatedPacket.KeepDuration,
            StackCount = 3,
            Bind = 1,
        });

        Assert.Equal(21, bytes.Length);
        Assert.Equal(Convert.FromHexString(
            "a2000000" +          // targetId
            "07000000" +          // instanceId
            "ffffffffffffffff" +  // startTime (keep-duration sentinel)
            "03000000" +          // stackCount
            "01"),                // bind
            bytes);
    }

    [Fact]
    public void Deleted_Writes_8_Bytes_LE()
    {
        var bytes = Serialize(new ModifierDeletedPacket { TargetId = 0x000000A2u, InstanceId = 0x00000007u });
        Assert.Equal(Convert.FromHexString("a200000007000000"), bytes);
    }

    [Fact]
    public void Types_Match()
    {
        Assert.Equal(PacketType.ModifierCreated, new ModifierCreatedPacket().Type);
        Assert.Equal(PacketType.ModifierUpdated, new ModifierUpdatedPacket().Type);
        Assert.Equal(PacketType.ModifierDeleted, new ModifierDeletedPacket().Type);
    }
}
