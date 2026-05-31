using ReCap.Server.Adapters.RakNet.Packets;

namespace ReCap.Tests.Packets;

// Wire-verified against the working C++ binary capture (cpp_loopback, DIVERGENCE_LEDGER D-009).
// ObjectivesInitForLevel (0xB7): u8 count + N×(u32 id + u24 value) = 7 bytes/objective.
//   5 objectives -> 36B body (+1 framing type byte = 37B on wire). ids = FNV-1 of names.
// ObjectiveUpdated (0xB8): 23B body (+1 = 24B wire).
public class ObjectivesTests
{
    private static byte[] Serialize(IRakNetPacket p)
    {
        using var ms = new MemoryStream();
        p.WriteTo(ms);
        return ms.ToArray();
    }

    [Fact]
    public void ObjectivesInit_Default_Is_36_Byte_Body_7_Bytes_Each()
    {
        var bytes = Serialize(ObjectivesInitForLevelPacket.CreateDefault());

        // 1 (count) + 5 × 7 = 36 bytes; +1 framing type = 37B on wire (matches C++ binary capture).
        Assert.Equal(36, bytes.Length);
        Assert.Equal(5, bytes[0]); // count
    }

    [Fact]
    public void ObjectivesInit_FirstEntry_Is_FinishLevelQuickly_Id_Plus_U24_Value()
    {
        var bytes = Serialize(ObjectivesInitForLevelPacket.CreateDefault());

        // First objective id = FNV-1("FinishLevelQuickly") = 0xFF9733EE (LE) at offset 1.
        Assert.Equal(new byte[] { 0xEE, 0x33, 0x97, 0xFF }, bytes[1..5]);
        // u24 value = 1 -> 01 00 00
        Assert.Equal(new byte[] { 0x01, 0x00, 0x00 }, bytes[5..8]);
    }

    [Fact]
    public void ObjectiveIds_Match_Fnv1_Of_Names()
    {
        Assert.Equal(0xFF9733EEu, ObjectivesInitForLevelPacket.ObjectiveIds[0]); // FinishLevelQuickly
        Assert.Equal(0xAC4273F3u, ObjectivesInitForLevelPacket.ObjectiveIds[1]); // DoDamageOften
        Assert.Equal(0x61C07561u, ObjectivesInitForLevelPacket.ObjectiveIds[2]); // TouchAllObelisks
        Assert.Equal(0xA28485CCu, ObjectivesInitForLevelPacket.ObjectiveIds[3]); // DefeatAllMonsters
        Assert.Equal(0x0478FACBu, ObjectivesInitForLevelPacket.ObjectiveIds[4]); // HugeDamage
    }

    [Fact]
    public void ObjectiveUpdated_Is_23_Byte_Body()
    {
        var bytes = Serialize(new ObjectiveUpdatedPacket
        {
            ObjectiveId = ObjectivesInitForLevelPacket.ObjectiveIds[0],
            ClientId = 0,
            Medal = 4,
            Voiceover = ObjectivesInitForLevelPacket.FnvHash("vo_ship_obelisk_accessed"),
            Value = 0
        });

        // 4 id + 1 clientId + 1 medal + 4 voiceover + 1 showNotif + 4 value + 4 + 4 = 23B (+1 type = 24B).
        Assert.Equal(23, bytes.Length);
        Assert.Equal(new byte[] { 0xEE, 0x33, 0x97, 0xFF }, bytes[0..4]); // id LE
        Assert.Equal(0x00, bytes[4]); // clientId
        Assert.Equal(0x04, bytes[5]); // medal = Gold
        Assert.Equal(new byte[] { 0xAF, 0x6E, 0x72, 0x5C }, bytes[6..10]); // voiceover hash LE (matches capture)
    }
}
