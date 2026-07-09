using System;
using ReCap.Server.Util;

namespace ReCap.Server.Adapters.RakNet.Packets;

/// <summary>
/// ObjectivesInitForLevel (0xB7) — sends the list of level objectives to the client.
/// WIRE-VERIFIED against the working C++ binary capture (cpp_loopback, DIVERGENCE_LEDGER D-009):
///   [u8 count] [for each: u32 id + u24 value]  -> 7 bytes/objective, 37B for the 5 objectives.
/// The 5 ids are the FNV-1 hashes of the objective names (confirmed exact). The client looks up
/// the display text locally by id hash, so NO description is sent on the wire. The ReCap.Cpp SOURCE
/// tree drifted to a 56-byte (id+value+0x30 desc) layout, but the binary that actually drives the
/// client uses the 7-byte form — the capture is the ground truth. See OBJECTS_OBJECTIVES_SYSTEM.md.
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

    // The 5 hardcoded Dungeon objectives (C++ Instance ctor). Ids are FNV-1 of the names.
    public static readonly string[] ObjectiveNames =
    {
        "FinishLevelQuickly", "DoDamageOften", "TouchAllObelisks", "DefeatAllMonsters", "HugeDamage"
    };

    public static readonly uint[] ObjectiveIds = Array.ConvertAll(ObjectiveNames, FnvHash);

    /// <summary>Create the default 5-objective set matching the C++ Instance constructor.</summary>
    public static ObjectivesInitForLevelPacket CreateDefault()
    {
        var packet = new ObjectivesInitForLevelPacket();
        foreach (var id in ObjectiveIds)
            packet.Objectives.Add(new ObjectiveData { Id = id, Value = 1 });
        return packet;
    }

    // FNV-1 matching C++ utils::hash_id (multiply THEN xor, lowercase)
    public static uint FnvHash(string s)
    {
        uint h = 0x811C9DC5;
        foreach (var c in s) { h *= 0x01000193; h ^= (byte)char.ToLower(c); }
        return h;
    }
}

/// <summary>Per-objective entry on the wire: u32 id + u24 value (7 bytes). No description.</summary>
public class ObjectiveData
{
    public uint Id    { get; set; }
    public uint Value { get; set; }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(Id);
        // u24 LE value (the working wire's 3-byte value field; 3rd byte is value, not medal)
        stream.WriteByte((byte)(Value & 0xFF));
        stream.WriteByte((byte)((Value >> 8) & 0xFF));
        stream.WriteByte((byte)((Value >> 16) & 0xFF));
    }
}
