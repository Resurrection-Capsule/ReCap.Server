using System.Text;
using ReCap.Server.Util;

namespace ReCap.Server.Adapters.RakNet.Packets;

public class LabsPlayerUpdatePacket : IRakNetPacket
{
    public PacketType Type => PacketType.LabsPlayerUpdate;

    public byte PlayerId { get; set; }
    public ushort UpdateBits { get; set; }

    public LabsPlayerData? PlayerData { get; set; }

    public const ushort CharacterBits = 1 << 0;
    public const ushort CharacterMask = 0x07;
    public const ushort CrystalBits = 1 << 3;
    public const ushort CrystalMask = 0x0FF8;
    public const ushort PlayerBits = 1 << 12;

    public void ReadFrom(Stream stream) { }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

        writer.Write(PlayerId);
        writer.Write(UpdateBits);

        if ((UpdateBits & PlayerBits) != 0 && PlayerData != null)
        {
            PlayerData.WriteReflection(writer);
        }

        if ((UpdateBits & CharacterMask) != 0 && PlayerData != null)
        {
            for (int i = 0; i < 3; i++)
            {
                if ((UpdateBits & (CharacterBits << i)) != 0 && PlayerData.Characters[i] != null)
                {
                    PlayerData.Characters[i]!.WriteReflection(writer);
                }
            }
        }

        if ((UpdateBits & CrystalMask) != 0 && PlayerData != null)
        {
            for (int i = 0; i < 9; i++)
            {
                if ((UpdateBits & (CrystalBits << i)) != 0 && PlayerData.Catalysts[i] != null)
                {
                    PlayerData.Catalysts[i]!.WriteReflection(writer);
                }
            }
        }
    }
}

public class LabsPlayerData
{
    public bool DataSetup { get; set; }
    public int CurrentDeckIndex { get; set; }
    public int QueuedDeckIndex { get; set; }
    public byte PlayerIndex { get; set; }
    public byte Team { get; set; } = 1;
    public ulong PlayerOnlineId { get; set; }
    public uint Status { get; set; }
    public float StatusProgress { get; set; }
    public uint CurrentCreatureId { get; set; }
    public float EnergyPoints { get; set; }
    public bool IsCharged { get; set; }
    public int DNA { get; set; }
    public bool LockCamera { get; set; }
    public bool LockedOverdrive { get; set; }
    public bool LockedCrystals { get; set; }
    public uint LockedAbilityMin { get; set; } = 0xFF;
    public uint LockedDeckIndexMin { get; set; } = 0xFF;
    public uint DeckScore { get; set; }
    public uint AvatarLevel { get; set; }
    public float AvatarXP { get; set; }
    public uint ChainProgression { get; set; }

    public LabsCharacterData?[] Characters { get; set; } = new LabsCharacterData?[3];
    public LabsCatalystData?[] Catalysts { get; set; } = new LabsCatalystData?[9];
    public bool[] CatalystBonuses { get; set; } = new bool[8];

    internal HashSet<byte> _dataBits = new();
    internal bool _needsStatusUpdate;

    public void SetDataBit(byte field) => _dataBits.Add(field);
    public void ResetDataBits() => _dataBits.Clear();

    public void SetInitialDataBits()
    {
        byte[] initialBits = { 0, 4, 5, 6, 7, 8, 12, 15, 16, 18, 21, 22 };
        foreach (var b in initialBits) _dataBits.Add(b);
    }

    public void WriteReflection(BinaryWriter writer)
    {
        var reflector = new ReflectionSerializer(writer, 24);
        reflector.Begin();

        if (_dataBits.Contains(0)) reflector.Write(0, () => writer.Write(DataSetup));
        if (_dataBits.Contains(1)) reflector.Write(1, () => writer.Write(CurrentDeckIndex));
        if (_dataBits.Contains(2)) reflector.Write(2, () => writer.Write(QueuedDeckIndex));

        if (_dataBits.Contains(3))
        {
            reflector.Write(3, () =>
            {
                foreach (var ch in Characters)
                    (ch ?? new LabsCharacterData()).WriteTo(writer);
            });
        }

        if (_dataBits.Contains(4)) reflector.Write(4, () => writer.Write(PlayerIndex));
        if (_dataBits.Contains(5)) reflector.Write(5, () => writer.Write(Team));
        if (_dataBits.Contains(6)) reflector.Write(6, () => writer.Write(PlayerOnlineId));
        if (_dataBits.Contains(7)) reflector.Write(7, () => writer.Write(Status));
        if (_dataBits.Contains(8)) reflector.Write(8, () => writer.Write(StatusProgress));
        if (_dataBits.Contains(12)) reflector.Write(12, () => writer.Write(DNA));

        if (_dataBits.Contains(13))
        {
            reflector.Write(13, () =>
            {
                foreach (var cat in Catalysts)
                    (cat ?? new LabsCatalystData()).WriteTo(writer);
            });
        }

        if (_dataBits.Contains(14))
        {
            reflector.Write(14, () =>
            {
                foreach (var bonus in CatalystBonuses)
                    writer.Write(bonus);
            });
        }

        if (_dataBits.Contains(15)) reflector.Write(15, () => writer.Write(AvatarLevel));
        if (_dataBits.Contains(16)) reflector.Write(16, () => writer.Write(AvatarXP));
        if (_dataBits.Contains(17)) reflector.Write(17, () => writer.Write(ChainProgression));
        if (_dataBits.Contains(18)) reflector.Write(18, () => writer.Write(LockCamera));
        if (_dataBits.Contains(19)) reflector.Write(19, () => writer.Write(LockedOverdrive));
        if (_dataBits.Contains(20)) reflector.Write(20, () => writer.Write(LockedCrystals));
        if (_dataBits.Contains(21)) reflector.Write(21, () => writer.Write(LockedAbilityMin));
        if (_dataBits.Contains(22)) reflector.Write(22, () => writer.Write(LockedDeckIndexMin));
        if (_dataBits.Contains(23)) reflector.Write(23, () => writer.Write(DeckScore));

        reflector.End();
    }
}

public class LabsCharacterData
{
    public int Version { get; set; }
    public uint NounId { get; set; }
    public ulong AssetId { get; set; }
    public uint CreatureType { get; set; }
    public ulong DeployCooldown { get; set; }
    public uint AbilityPoints { get; set; } = 10;
    public uint[] AbilityRanks { get; set; } = new uint[9];
    public float Health { get; set; } = 200f;
    public float MaxHealth { get; set; } = 200f;
    public float Mana { get; set; } = 200f;
    public float MaxMana { get; set; } = 200f;
    public float GearScore { get; set; } = 300f;
    public float GearScoreFlattened { get; set; } = 300f;

    // Writes a 0x620-byte fixed-size block matching Character::WriteTo in C++
    public void WriteTo(BinaryWriter outerWriter)
    {
        var buffer = new byte[0x620];
        using var ms = new MemoryStream(buffer, writable: true);
        using var bw = new BinaryWriter(ms, Encoding.UTF8, true);

        ms.Position = 0x008;
        bw.Write(AssetId);   // uint64 BE at 0x008
        bw.Write(Version);   // int32 BE at 0x010

        ms.Position = 0x0B4;
        bw.Write(NounId);    // uint32 BE at 0x0B4

        // mPartAttributes (0x0B8..0x1E0): all zero â€” no attribute data for fake creatures

        ms.Position = 0x3B8;
        bw.Write(CreatureType); // uint32 BE at 0x3B8

        ms.Position = 0x3C0;
        bw.Write(DeployCooldown); // uint64 BE at 0x3C0
        bw.Write(AbilityPoints);  // uint32 BE at 0x3C8
        foreach (var rank in AbilityRanks)
            bw.Write(rank);       // uint32 BE each, 9 ranks starting at 0x3CC

        ms.Position = 0x3F0;
        bw.Write(Health);
        bw.Write(MaxHealth);
        bw.Write(Mana);
        bw.Write(MaxMana);
        bw.Write(GearScore);
        bw.Write(GearScoreFlattened);

        outerWriter.Write(buffer);
    }

    public void WriteReflection(BinaryWriter writer)
    {
        var reflector = new ReflectionSerializer(writer, 124);
        reflector.Begin();

        reflector.Write(0, () => writer.Write(Version));
        reflector.Write(1, () => writer.Write(NounId));
        reflector.Write(2, () => writer.Write(AssetId));
        reflector.Write(3, () => writer.Write(CreatureType));
        reflector.Write(4, () => writer.Write(DeployCooldown));
        reflector.Write(5, () => writer.Write(AbilityPoints));
        reflector.Write(6, () =>
        {
            foreach (var rank in AbilityRanks)
                writer.Write(rank);
        });
        reflector.Write(7, () => writer.Write(Health));
        reflector.Write(8, () => writer.Write(MaxHealth));
        reflector.Write(9, () => writer.Write(Mana));
        reflector.Write(10, () => writer.Write(MaxMana));
        reflector.Write(11, () => writer.Write(GearScore));
        reflector.Write(12, () => writer.Write(GearScoreFlattened));

        reflector.End();
    }
}

public class LabsCatalystData
{
    public uint NounId { get; set; }
    public ushort Rarity { get; set; }

    // Writes a 16-byte fixed-size block matching Catalyst::WriteTo in C++
    public void WriteTo(BinaryWriter writer)
    {
        writer.Write(NounId);           // uint32 BE (4 bytes)
        writer.Write(Rarity);           // uint16 BE (2 bytes)
        writer.Write(new byte[10]);       // 10 bytes padding (zeros)
    }

    public void WriteReflection(BinaryWriter writer)
    {
        var reflector = new ReflectionSerializer(writer, 2);
        reflector.Begin();

        reflector.Write(0, () => writer.Write(NounId));
        reflector.Write(1, () => writer.Write(Rarity));

        reflector.End();
    }
}
