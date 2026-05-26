using System.Text;
using ReCap.Server.Adapters.RakNet;
using ReCap.Server.Util;

namespace ReCap.Tests.Packets;

public class ReflectionSerializerTests
{
    [Fact]
    public void SmallFieldCount_Uses_OneByte_Bitmap()
    {
        // ≤8 fields: 1-byte bitmap
        var bytes = SerializeWithFields(fieldCount: 8, writeFields: new byte[] { 0, 2, 5 });

        // First byte is bitmap: bit 0 | bit 2 | bit 5 = 0x25
        Assert.Equal(0x25, bytes[0]);
    }

    [Fact]
    public void MediumFieldCount_Uses_TwoByte_BitmapBE()
    {
        // 9-16 fields: 2-byte bitmap in Big Endian
        var bytes = SerializeWithFields(fieldCount: 16, writeFields: new byte[] { 0 });

        // First 2 bytes: bitmap BE with bit 0 set = 0x0001 → BE: 0x00, 0x01
        Assert.Equal(0x00, bytes[0]);
        Assert.Equal(0x01, bytes[1]);
    }

    [Fact]
    public void MediumFieldCount_HighBit_IsBE()
    {
        // Set bit 15 → 0x8000 in BE → 0x80, 0x00
        var bytes = SerializeWithFields(fieldCount: 16, writeFields: new byte[] { 15 });

        Assert.Equal(0x80, bytes[0]);
        Assert.Equal(0x00, bytes[1]);
    }

    [Fact]
    public void LargeFieldCount_Uses_FieldId_With_Terminator()
    {
        // >16 fields: byte per field ID + 0xFF terminator
        var bytes = SerializeWithFields(fieldCount: 24, writeFields: new byte[] { 3, 7 });

        // field 3, value (1 byte), field 7, value (1 byte), 0xFF terminator
        Assert.Equal(3, bytes[0]); // field ID
        // bytes[1] = value for field 3
        Assert.Equal(7, bytes[2]); // field ID
        // bytes[3] = value for field 7
        Assert.Equal(0xFF, bytes[4]); // terminator
    }

    [Fact]
    public void LargeFieldCount_Terminates_With_0xFF()
    {
        var bytes = SerializeWithFields(fieldCount: 24, writeFields: Array.Empty<byte>());

        // Only terminator
        Assert.Single(bytes);
        Assert.Equal(0xFF, bytes[0]);
    }

    [Fact]
    public void LabsPlayerData_Uses_24_Fields_FieldIdMode()
    {
        // LabsPlayerData has 24 fields → >16 → field-ID mode
        var pd = new Server.Adapters.RakNet.Packets.LabsPlayerData();
        pd.SetDataBit(0); // DataSetup
        pd.SetDataBit(7); // Status

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8, true);
        pd.WriteReflection(writer);

        var bytes = ms.ToArray();

        // Should start with field IDs (not bitmap)
        Assert.Equal(0, bytes[0]); // field 0 = DataSetup
        // field 0 value (bool = 1 byte), then field 7
        // Find terminator at end
        Assert.Equal(0xFF, bytes[^1]);
    }

    [Fact]
    public void LabsCharacterData_Uses_124_Fields_FieldIdMode()
    {
        // LabsCharacterData reflector has 124 fields → field-ID mode
        var ch = new Server.Adapters.RakNet.Packets.LabsCharacterData
        {
            Version = 1,
            NounId = 0x3039C538,
            AbilityRanks = new uint[] { 1, 1, 1, 1, 1, 1, 1, 1, 1 }
        };

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8, true);
        ch.WriteReflection(writer);

        var bytes = ms.ToArray();

        // Field-ID mode: first byte is field 0
        Assert.Equal(0, bytes[0]);
        // Last byte is 0xFF terminator
        Assert.Equal(0xFF, bytes[^1]);
    }

    [Fact]
    public void LabsCatalystData_Uses_2_Fields_OneByte_Bitmap()
    {
        // LabsCatalystData reflector has 2 fields → ≤8 → 1-byte bitmap
        var cat = new Server.Adapters.RakNet.Packets.LabsCatalystData
        {
            NounId = 0x02FB89EB,
            Rarity = 2
        };

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8, true);
        cat.WriteReflection(writer);

        var bytes = ms.ToArray();

        // 1-byte bitmap with bits 0 and 1 set = 0x03
        Assert.Equal(0x03, bytes[0]);
    }

    private static byte[] SerializeWithFields(int fieldCount, byte[] writeFields)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8, true);

        var reflector = new ReflectionSerializer(writer, fieldCount);
        reflector.Begin();

        foreach (var field in writeFields)
        {
            reflector.Write(field, () => writer.Write((byte)0x42));
        }

        reflector.End();
        return ms.ToArray();
    }
}
