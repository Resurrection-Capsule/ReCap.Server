using ReCap.Server.Util;

namespace ReCap.Server.Adapters.RakNet.Packets;

/// <summary>
/// ActionCommandMsgs (0x9C) — client→server: player gameplay input.
/// C++ reads: ActionCommandCommonData { u32 objectId, vec3 position, quat orientation, u8 type, u8[3] pad }
/// Then command-specific data depending on type.
///
/// ActionCommand enum:
///   Movement=3, StopMovement=4, SwitchCharacter=5, UseCharacterAbility=7,
///   UseSquadAbility=8, CatalystPickup=9, Cancel=10, UseInteractableObject=11,
///   Dance=12, Taunt=13
/// </summary>
public class ActionCommandMsgsPacket : IRakNetPacket
{
    public PacketType Type => PacketType.ActionCommandMsgs;

    // ── Common data (ActionCommandCommonData) ──
    public uint ObjectId { get; set; }
    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }
    public float OriX { get; set; }
    public float OriY { get; set; }
    public float OriZ { get; set; }
    public float OriW { get; set; }
    public byte CommandType { get; set; }
    public byte Pad1 { get; set; }
    public byte Pad2 { get; set; }
    public byte Pad3 { get; set; }

    // ── Remaining raw bytes for command-specific data ──
    public byte[] ExtraData { get; set; } = Array.Empty<byte>();

    public void ReadFrom(Stream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);

        ObjectId = reader.ReadUInt32BE();
        PosX = reader.ReadSingleBE();
        PosY = reader.ReadSingleBE();
        PosZ = reader.ReadSingleBE();
        OriX = reader.ReadSingleBE();
        OriY = reader.ReadSingleBE();
        OriZ = reader.ReadSingleBE();
        OriW = reader.ReadSingleBE();
        CommandType = reader.ReadByte();
        Pad1 = reader.ReadByte();
        Pad2 = reader.ReadByte();
        Pad3 = reader.ReadByte();

        // Read remaining bytes as ExtraData
        var remaining = stream.Length - stream.Position;
        if (remaining > 0)
            ExtraData = reader.ReadBytes((int)remaining);
    }

    public void WriteTo(Stream stream) { /* server never sends this packet */ }

    // ── Helpers to read command-specific data from ExtraData ──

    /// <summary>
    /// Read movement data: [u32 goalFlags] [vec3 goalPosition]
    /// Used by Movement (3), StopMovement (4), Cancel (10)
    /// </summary>
    public (uint goalFlags, float gx, float gy, float gz) ReadMovementData()
    {
        using var ms = new MemoryStream(ExtraData);
        using var r = new BinaryReader(ms);
        return (
            r.ReadUInt32BE(),
            r.ReadSingleBE(),
            r.ReadSingleBE(),
            r.ReadSingleBE()
        );
    }

    /// <summary>
    /// Read switch character data: [u32 creatureIndex]
    /// Used by SwitchCharacter (5)
    /// </summary>
    public uint ReadSwitchIndex()
    {
        using var ms = new MemoryStream(ExtraData);
        using var r = new BinaryReader(ms);
        return r.ReadUInt32BE();
    }

    /// <summary>
    /// Read interactable data: [u32 objectId]
    /// Used by UseInteractableObject (11), CatalystPickup (9)
    /// </summary>
    public uint ReadInteractableObjectId()
    {
        using var ms = new MemoryStream(ExtraData);
        using var r = new BinaryReader(ms);
        return r.ReadUInt32BE();
    }
}
