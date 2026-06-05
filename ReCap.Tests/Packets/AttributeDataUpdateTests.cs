using ReCap.Server.Adapters.RakNet.Packets;

namespace ReCap.Tests.Packets;

// AttributeDataUpdate (0x96) hero spawn — wire-verified against cpp_loopback capture
// msg #612 (hero objId=2): 15 attributes, byte field-id + f32 LE, ascending ids,
// 0xFF terminator. Ids per the WORKING BINARY (Attributes.h enum order):
// 0 Strength, 1 Dexterity, 2 Mind, 4 MaxHealth, 5 MaxMana, 7 PhysicalDefense,
// 9 EnergyDefense, 10 CriticalRating, 11 NonCombatSpeed, 12 CombatSpeed,
// 23 AttackSpeedScale, 24 CooldownScale, 109 InvisibleToSecurityTeleporters,
// 111 MinWeaponDamage, 112 MaxWeaponDamage (binary ids; C++ source enum 101/102 drifted).
public class AttributeDataUpdateTests
{
    [Fact]
    public void HeroSpawn_Matches_Cpp_Capture_Msg612_Bytes()
    {
        var expected = Convert.FromHexString(
            "02000000" +
            "000000b841" + "0100004041" + "0200007041" +
            "0400004843" + "0500004843" +
            "0700004842" + "0900004842" + "0a00004842" +
            "0b00000441" + "0c0000f040" +
            "170000803f" + "180000803f" +
            "6d0000803f" + "6f0000803f" + "700000a040" +
            "ff");

        var attrs = new AttributeDataUpdatePacket { ObjectId = 2 };
        attrs.Set(AttributeDataUpdatePacket.Strength, 23f);
        attrs.Set(AttributeDataUpdatePacket.Dexterity, 12f);
        attrs.Set(AttributeDataUpdatePacket.Mind, 15f);
        attrs.Set(AttributeDataUpdatePacket.MaxHealth, 200f);
        attrs.Set(AttributeDataUpdatePacket.MaxMana, 200f);
        attrs.Set(AttributeDataUpdatePacket.PhysicalDefense, 50f);
        attrs.Set(AttributeDataUpdatePacket.EnergyDefense, 50f);
        attrs.Set(AttributeDataUpdatePacket.CriticalRating, 50f);
        attrs.Set(AttributeDataUpdatePacket.NonCombatSpeed, 8.25f);
        attrs.Set(AttributeDataUpdatePacket.CombatSpeed, 7.5f);
        attrs.Set(AttributeDataUpdatePacket.AttackSpeedScale, 1f);
        attrs.Set(AttributeDataUpdatePacket.CooldownScale, 1f);
        attrs.Set(AttributeDataUpdatePacket.InvisibleToSecurityTeleporters, 1f);
        attrs.Set(AttributeDataUpdatePacket.MinWeaponDamage, 1f);
        attrs.Set(AttributeDataUpdatePacket.MaxWeaponDamage, 5f);

        using var ms = new MemoryStream();
        attrs.WriteTo(ms);

        Assert.Equal(expected, ms.ToArray());
    }
}
