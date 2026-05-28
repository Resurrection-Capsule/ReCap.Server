using ReCap.Server.Util;

namespace ReCap.Server.Adapters.RakNet.Packets;

/// <summary>
/// ObjectivesInitForLevel (0xB7) â€” sends the list of level objectives to the client.
/// C++: SendObjectivesInitForLevel â†’ [u8 count] [for each: Objective::WriteTo]
///
/// Objective::WriteTo format:
///   [u32 id] [u32 value] [0x40 bytes padding/debug data]
///   Total per objective: 8 + 64 = 72 bytes
/// </summary>
public class ObjectivesInitForLevelPacket : IRakNetPacket
{
    public PacketType Type => PacketType.ObjectivesInitForLevel;

    public List<ObjectiveData> Objectives { get; } = new();

    public void ReadFrom(Stream stream) { }

    public void WriteTo(Stream stream)
    {
        stream.WriteByte((byte)Objectives.Count);
        foreach (var obj in Objectives)
            obj.WriteTo(stream);
    }

    /// <summary>
    /// Create the default 5-objective set matching C++ Instance constructor.
    /// </summary>
    public static ObjectivesInitForLevelPacket CreateDefault()
    {
        var packet = new ObjectivesInitForLevelPacket();

        uint[] ids =
        {
            FnvHash("FinishLevelQuickly"),
            FnvHash("DoDamageOften"),
            FnvHash("TouchAllObelisks"),
            FnvHash("DefeatAllMonsters"),
            FnvHash("HugeDamage"),
        };

        foreach (var id in ids)
            packet.Objectives.Add(new ObjectiveData { Id = id, Value = 1 });

        return packet;
    }

    // FNV-1 matching C++ utils::hash_id (multiply THEN xor, lowercase)
    private static uint FnvHash(string s)
    {
        uint h = 0x811C9DC5;
        foreach (var c in s) { h *= 0x01000193; h ^= (byte)char.ToLower(c); }
        return h;
    }
}

/// <summary>
/// Per-objective data. Mirrors C++ Objective::WriteTo.
/// </summary>
public class ObjectiveData
{
    public uint Id    { get; set; }
    public uint Value { get; set; }

    // C++ Objective::WriteTo (Types.cpp): [u32 id][u32 value][char[0x30] description, null-padded].
    // Total = 0x38 (56) bytes per objective. Description left empty (zeroed). The previous
    // 0x40 garbage-pattern padding mis-sized every objective and desynced the client read,
    // crashing on Dungeon entry.
    private const int DescriptionSize = 0x30;

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(Id);
        writer.Write(Value);
        stream.Write(new byte[DescriptionSize], 0, DescriptionSize);
    }
}
