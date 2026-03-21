using System.Text;
using ReCap.Server.Util;

namespace ReCap.Server.Adapters.RakNet.Packets;

public class LabsPlayerUpdatePacket : IRakNetPacket
{
    public PacketType Type => PacketType.LabsPlayerUpdate;

    public byte PlayerId { get; set; }
    public ushort UpdateBits { get; set; }

    public LabsPlayerData? PlayerData { get; set; }
    public LabsCharacterData?[] Characters { get; set; } = new LabsCharacterData?[3];
    public LabsCatalystData?[] Catalysts { get; set; } = new LabsCatalystData?[9];

    public const ushort CharacterBits = 1 << 0;
    public const ushort CharacterMask = 0x07;
    public const ushort CrystalBits = 1 << 3;
    public const ushort CrystalMask = 0x07F8;
    public const ushort PlayerBits = 1 << 12;

    public void ReadFrom(Stream stream) { }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

        writer.Write(PlayerId);
        writer.WriteBE(UpdateBits);

        if ((UpdateBits & PlayerBits) != 0 && PlayerData != null)
        {
            PlayerData.WriteReflection(writer);
        }

        if ((UpdateBits & CharacterMask) != 0)
        {
            for (int i = 0; i < 3; i++)
            {
                if ((UpdateBits & (CharacterBits << i)) != 0 && Characters[i] != null)
                {
                    Characters[i]!.WriteReflection(writer);
                }
            }
        }

        if ((UpdateBits & CrystalMask) != 0)
        {
            for (int i = 0; i < 9; i++)
            {
                if ((UpdateBits & (CrystalBits << i)) != 0 && Catalysts[i] != null)
                {
                    Catalysts[i]!.WriteReflection(writer);
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

    public void WriteReflection(BinaryWriter writer)
    {
        var reflector = new ReflectionSerializer(writer, 24);
        reflector.Begin();

        reflector.Write(0, () => writer.WriteBE(DataSetup));
        reflector.Write(1, () => writer.WriteBE(CurrentDeckIndex));
        reflector.Write(2, () => writer.WriteBE(QueuedDeckIndex));
        reflector.Write(4, () => writer.Write(PlayerIndex));
        reflector.Write(5, () => writer.Write(Team));
        reflector.Write(6, () => writer.WriteBE(PlayerOnlineId));
        reflector.Write(7, () => writer.WriteBE(Status));
        reflector.Write(8, () => writer.WriteBE(StatusProgress));
        reflector.Write(15, () => writer.WriteBE(AvatarLevel));
        reflector.Write(16, () => writer.WriteBE(AvatarXP));
        reflector.Write(17, () => writer.WriteBE(ChainProgression));
        reflector.Write(18, () => writer.WriteBE(LockCamera));
        reflector.Write(19, () => writer.WriteBE(LockedOverdrive));
        reflector.Write(20, () => writer.WriteBE(LockedCrystals));
        reflector.Write(21, () => writer.WriteBE(LockedAbilityMin));
        reflector.Write(22, () => writer.WriteBE(LockedDeckIndexMin));
        reflector.Write(23, () => writer.WriteBE(DeckScore));
        reflector.Write(12, () => writer.WriteBE(DNA));

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

    public void WriteReflection(BinaryWriter writer)
    {
        var reflector = new ReflectionSerializer(writer, 124);
        reflector.Begin();

        reflector.Write(0, () => writer.WriteBE(Version));
        reflector.Write(1, () => writer.WriteBE(NounId));
        reflector.Write(2, () => writer.WriteBE(AssetId));
        reflector.Write(3, () => writer.WriteBE(CreatureType));
        reflector.Write(4, () => writer.WriteBE(DeployCooldown));
        reflector.Write(5, () => writer.WriteBE(AbilityPoints));
        reflector.Write(6, () =>
        {
            foreach (var rank in AbilityRanks)
                writer.WriteBE(rank);
        });
        reflector.Write(7, () => writer.WriteBE(Health));
        reflector.Write(8, () => writer.WriteBE(MaxHealth));
        reflector.Write(9, () => writer.WriteBE(Mana));
        reflector.Write(10, () => writer.WriteBE(MaxMana));
        reflector.Write(11, () => writer.WriteBE(GearScore));
        reflector.Write(12, () => writer.WriteBE(GearScoreFlattened));

        reflector.End();
    }
}

public class LabsCatalystData
{
    public uint NounId { get; set; }
    public ushort Rarity { get; set; }

    public void WriteReflection(BinaryWriter writer)
    {
        var reflector = new ReflectionSerializer(writer, 2);
        reflector.Begin();

        reflector.Write(0, () => writer.WriteBE(NounId));
        reflector.Write(1, () => writer.WriteBE(Rarity));

        reflector.End();
    }
}
