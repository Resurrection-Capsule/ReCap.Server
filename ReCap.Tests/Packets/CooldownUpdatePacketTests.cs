using ReCap.Server.Adapters.RakNet;
using ReCap.Server.Adapters.RakNet.Packets;

namespace ReCap.Tests.Packets;

// CooldownUpdate (0xC1) — client ClientNet::ApplyCooldownUpdate @0x004d99f0 reads a raw fixed 36-byte
// payload (ReadStreamBytes 0x24), NOT a reflection bitmap. Layout verified 2026-07-09:
//   [+0] objectId(u32) [+4] abilityId(u32) [+8] reserved(u32=0)
//   [+12] duration(u64) [+20] start(u64) [+28] globalCooldown(u64). LE ([[endianness-wrapper-is-LE]]).
public class CooldownUpdatePacketTests
{
    private static byte[] Serialize(IRakNetPacket p)
    {
        using var ms = new MemoryStream();
        p.WriteTo(ms);
        return ms.ToArray();
    }

    [Fact]
    public void Writes_36_Bytes_Fixed_LE()
    {
        var bytes = Serialize(new CooldownUpdatePacket
        {
            ObjectId = 0x000000A2u,
            AbilityId = 0xDEADBEEFu,
            Duration = 0x1122334455667788ul,
            Start = 0,
            GlobalCooldown = 0,
        });

        Assert.Equal(36, bytes.Length);
        Assert.Equal(Convert.FromHexString(
            "a2000000" +          // objectId
            "efbeadde" +          // abilityId
            "00000000" +          // reserved
            "8877665544332211" +  // duration u64
            "0000000000000000" +  // start u64
            "0000000000000000"),  // globalCooldown u64
            bytes);
    }

    [Fact]
    public void Type_Is_CooldownUpdate()
    {
        Assert.Equal(PacketType.CooldownUpdate, new CooldownUpdatePacket().Type);
    }
}
