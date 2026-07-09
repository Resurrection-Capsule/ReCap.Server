using System.Numerics;
using ReCap.Server.Adapters.RakNet;
using ReCap.Server.Adapters.RakNet.Packets;

namespace ReCap.Tests.Packets;

// CombatEvent (0xBA) — client ClientNet::OnGmsCombatEvent @0x0053ed50, reflection_serializer<8> over
// the AssetData.Parser CombatEvent schema (no leading id). Drives floating damage numbers/combat log.
//   0 flags(u16) 1 deltaHealth(f32) 2 absorbedAmount(f32) 3 targetID(u32) 4 sourceID(u32)
//   5 abilityID(u32) 6 damageDirection(Vector3) 7 integerHpChange(i32) — all LE.
public class CombatEventPacketTests
{
    private static byte[] Serialize(IRakNetPacket p)
    {
        using var ms = new MemoryStream();
        p.WriteTo(ms);
        return ms.ToArray();
    }

    [Fact]
    public void Damage_Writes_All_Eight_Fields_LE()
    {
        var bytes = Serialize(new CombatEventPacket
        {
            Flags = 0,
            DeltaHealth = -50f,
            AbsorbedAmount = 0f,
            TargetId = 0x11,
            SourceId = 0x22,
            AbilityId = 0x33,
            DamageDirection = Vector3.Zero,
            IntegerHpChange = -50,
        });

        // ff | 0000 | -50f(000048c2) | 0f | 11.. | 22.. | 33.. | vec3(0) | -50i(ceffffff) = 39 bytes
        const string expected =
            "ff" + "0000" + "000048c2" + "00000000" + "11000000" + "22000000" + "33000000" +
            "000000000000000000000000" + "ceffffff";
        Assert.Equal(Convert.FromHexString(expected), bytes);
    }

    [Fact]
    public void Type_Is_CombatEvent()
    {
        Assert.Equal(PacketType.CombatEvent, new CombatEventPacket().Type);
    }
}
