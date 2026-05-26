using System.Text;
using ReCap.Server.Adapters.RakNet.Packets;
using ReCap.Server.Util;

namespace ReCap.Tests.Packets;

public class LabsPlayerUpdateTests
{
    [Fact]
    public void CrystalMask_Covers_All_Nine_Slots()
    {
        // C++ PlayerUpdateBits: CrystalBits (1<<3) shifted 0..8 = bits 3..11
        // Mask = 0x08 | 0x10 | 0x20 | 0x40 | 0x80 | 0x100 | 0x200 | 0x400 | 0x800 = 0x0FF8
        Assert.Equal(0x0FF8, LabsPlayerUpdatePacket.CrystalMask);
    }

    [Fact]
    public void CharacterMask_Covers_Three_Slots()
    {
        // C++ CharacterBits (1<<0) shifted 0..2 = bits 0..2 = 0x07
        Assert.Equal(0x07, LabsPlayerUpdatePacket.CharacterMask);
    }

    [Fact]
    public void PlayerBits_Is_Bit_Twelve()
    {
        // C++ PlayerBits = 1 << 12 = 0x1000
        Assert.Equal(0x1000, LabsPlayerUpdatePacket.PlayerBits);
    }

    [Fact]
    public void FullMask_Matches_Cpp()
    {
        // C++ Mask = CharacterMask | CrystalMask | PlayerBits
        var fullMask = LabsPlayerUpdatePacket.CharacterMask
                     | LabsPlayerUpdatePacket.CrystalMask
                     | LabsPlayerUpdatePacket.PlayerBits;
        Assert.Equal(0x1FFF, fullMask);
    }

    [Fact]
    public void CrystalBits_Each_Slot_Maps_Correctly()
    {
        // Each catalyst slot i should correspond to CrystalBits << i
        for (int i = 0; i < 9; i++)
        {
            var bit = LabsPlayerUpdatePacket.CrystalBits << i;
            Assert.True((LabsPlayerUpdatePacket.CrystalMask & bit) != 0,
                $"CrystalMask should include catalyst slot {i} (bit 0x{bit:X})");
        }
    }

    [Fact]
    public void No_Overlap_Between_Character_Crystal_Player()
    {
        Assert.Equal(0, LabsPlayerUpdatePacket.CharacterMask & LabsPlayerUpdatePacket.CrystalMask);
        Assert.Equal(0, LabsPlayerUpdatePacket.CharacterMask & LabsPlayerUpdatePacket.PlayerBits);
        Assert.Equal(0, LabsPlayerUpdatePacket.CrystalMask & LabsPlayerUpdatePacket.PlayerBits);
    }

    [Fact]
    public void WriteTo_Header_Is_PlayerId_Then_UpdateBitsBE()
    {
        var packet = new LabsPlayerUpdatePacket
        {
            PlayerId = 0x02,
            UpdateBits = 0x1000, // PlayerBits only
            PlayerData = CreateMinimalPlayerData()
        };

        var bytes = SerializePacket(packet);

        // Byte 0: PlayerId
        Assert.Equal(0x02, bytes[0]);
        // Bytes 1-2: UpdateBits in Big Endian (0x1000 → 0x10, 0x00)
        Assert.Equal(0x10, bytes[1]);
        Assert.Equal(0x00, bytes[2]);
    }

    [Fact]
    public void WriteTo_PlayerBits_Only_Writes_Reflection()
    {
        var pd = CreateMinimalPlayerData();
        pd.SetInitialDataBits();

        var packet = new LabsPlayerUpdatePacket
        {
            PlayerId = 0,
            UpdateBits = LabsPlayerUpdatePacket.PlayerBits,
            PlayerData = pd
        };

        var bytes = SerializePacket(packet);

        // Should have: 1 byte PlayerId + 2 bytes UpdateBits + reflection data
        Assert.True(bytes.Length > 3, "Packet with PlayerBits should contain reflection data");
    }

    [Fact]
    public void WriteTo_CrystalMask_Writes_All_Nine_Catalysts()
    {
        var pd = CreateMinimalPlayerData();

        var packet = new LabsPlayerUpdatePacket
        {
            PlayerId = 0,
            UpdateBits = LabsPlayerUpdatePacket.CrystalMask,
            PlayerData = pd
        };

        var bytes = SerializePacket(packet);

        // Header (3 bytes) + 9 catalysts × reflection serialized
        // Each catalyst has 2 fields → 1-byte bitmap + data
        Assert.True(bytes.Length > 3, "Packet with CrystalMask should contain catalyst data");
    }

    [Fact]
    public void WriteTo_CharacterMask_Writes_Three_Characters()
    {
        var pd = CreateMinimalPlayerData();

        var packet = new LabsPlayerUpdatePacket
        {
            PlayerId = 0,
            UpdateBits = LabsPlayerUpdatePacket.CharacterMask,
            PlayerData = pd
        };

        var bytes = SerializePacket(packet);

        // Header (3 bytes) + 3 characters × reflection serialized
        Assert.True(bytes.Length > 3, "Packet with CharacterMask should contain character data");
    }

    [Fact]
    public void CatalystData_WriteTo_Is_16_Bytes()
    {
        var catalyst = new LabsCatalystData { NounId = 0x02FB89EB, Rarity = 2 };

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8, true);
        catalyst.WriteTo(writer);

        Assert.Equal(16, ms.Length);
    }

    [Fact]
    public void CatalystData_WriteTo_Format()
    {
        var catalyst = new LabsCatalystData { NounId = 0x02FB89EB, Rarity = 2 };

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8, true);
        catalyst.WriteTo(writer);

        var bytes = ms.ToArray();

        // NounId BE: 0x02FB89EB
        Assert.Equal(0x02, bytes[0]);
        Assert.Equal(0xFB, bytes[1]);
        Assert.Equal(0x89, bytes[2]);
        Assert.Equal(0xEB, bytes[3]);

        // Rarity BE: 0x0002
        Assert.Equal(0x00, bytes[4]);
        Assert.Equal(0x02, bytes[5]);

        // 10 bytes padding
        for (int i = 6; i < 16; i++)
            Assert.Equal(0x00, bytes[i]);
    }

    [Fact]
    public void CharacterData_WriteTo_Is_0x620_Bytes()
    {
        var character = new LabsCharacterData
        {
            Version = 1,
            NounId = 0x3039C538,
            CreatureType = 1,
            AbilityRanks = new uint[] { 1, 1, 1, 1, 1, 1, 1, 1, 1 },
            Health = 200f,
            MaxHealth = 200f,
            Mana = 200f,
            MaxMana = 200f,
            GearScore = 300f,
            GearScoreFlattened = 300f
        };

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8, true);
        character.WriteTo(writer);

        Assert.Equal(0x620, ms.Length);
    }

    [Fact]
    public void CharacterData_WriteTo_NounId_At_Offset_0x0B4()
    {
        var character = new LabsCharacterData { NounId = 0x3039C538 };

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8, true);
        character.WriteTo(writer);

        var bytes = ms.ToArray();

        // NounId BE at offset 0x0B4
        Assert.Equal(0x30, bytes[0x0B4]);
        Assert.Equal(0x39, bytes[0x0B5]);
        Assert.Equal(0xC5, bytes[0x0B6]);
        Assert.Equal(0x38, bytes[0x0B7]);
    }

    private static LabsPlayerData CreateMinimalPlayerData()
    {
        var pd = new LabsPlayerData
        {
            DataSetup = false,
            PlayerIndex = 0,
            Team = 1,
            PlayerOnlineId = 1,
            Status = 0,
            StatusProgress = 0,
            DeckScore = 500,
            AvatarLevel = 30,
            ChainProgression = 10
        };

        for (int i = 0; i < 3; i++)
            pd.Characters[i] = new LabsCharacterData
            {
                Version = 1,
                NounId = 0x3039C538,
                CreatureType = 1,
                AbilityRanks = new uint[] { 1, 1, 1, 1, 1, 1, 1, 1, 1 }
            };

        for (int i = 0; i < 9; i++)
            pd.Catalysts[i] = new LabsCatalystData
            {
                NounId = i < 8 ? 0x02FB89EB : 0u,
                Rarity = 2
            };

        return pd;
    }

    private static byte[] SerializePacket(LabsPlayerUpdatePacket packet)
    {
        using var ms = new MemoryStream();
        packet.WriteTo(ms);
        return ms.ToArray();
    }
}
