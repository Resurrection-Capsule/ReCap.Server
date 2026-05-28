using ReCap.Server.Util;

namespace ReCap.Server.Adapters.RakNet.Packets;

/// <summary>
/// ActionCommandMsgs (0x9C) — client→server player gameplay input.
/// Verified vs Darkspore.exe + C++ OnActionCommandMsgs (Server.cpp:688).
///
/// ActionCommandCommonData (40 bytes, raw LE, #pragma pack(1)):
///   u8 type, u8[3] unk, u32 inputSyncStamp, u32 objectId,
///   vec3 position (f32×3), quat orientation (f32×4)
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

    // ── Common data (ActionCommandCommonData), in wire order ──
    public byte CommandType { get; set; }
    public byte Pad1 { get; set; }
    public byte Pad2 { get; set; }
    public byte Pad3 { get; set; }
    public uint InputSyncStamp { get; set; }
    public uint ObjectId { get; set; }
    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }
    public float OriX { get; set; }
    public float OriY { get; set; }
    public float OriZ { get; set; }
    public float OriW { get; set; }

    // ── Remaining raw bytes for command-specific data ──
    public byte[] ExtraData { get; set; } = Array.Empty<byte>();

    public void ReadFrom(Stream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);

        CommandType = reader.ReadByte();
        Pad1 = reader.ReadByte();
        Pad2 = reader.ReadByte();
        Pad3 = reader.ReadByte();
        InputSyncStamp = reader.ReadUInt32();
        ObjectId = reader.ReadUInt32();
        PosX = reader.ReadSingle();
        PosY = reader.ReadSingle();
        PosZ = reader.ReadSingle();
        OriX = reader.ReadSingle();
        OriY = reader.ReadSingle();
        OriZ = reader.ReadSingle();
        OriW = reader.ReadSingle();

        // Read remaining bytes as ExtraData (command-specific)
        var remaining = stream.Length - stream.Position;
        if (remaining > 0)
            ExtraData = reader.ReadBytes((int)remaining);
    }

    public void WriteTo(Stream stream) { /* server never sends this packet */ }

    // â”€â”€ Helpers to read command-specific data from ExtraData â”€â”€

    /// <summary>
    /// ActionCommandMovementData (24 bytes): u32 unk, vec3 goalPosition, u32 goalFlags, u32 unk2.
    /// Used by Movement (3), StopMovement (4).
    /// </summary>
    public (uint goalFlags, float gx, float gy, float gz) ReadMovementData()
    {
        using var ms = new MemoryStream(ExtraData);
        using var r = new BinaryReader(ms);
        r.ReadUInt32();                 // unk
        float gx = r.ReadSingle();
        float gy = r.ReadSingle();
        float gz = r.ReadSingle();
        uint goalFlags = r.ReadUInt32();
        return (goalFlags, gx, gy, gz);
    }

    /// <summary>
    /// Read switch character data: [u32 creatureIndex]
    /// Used by SwitchCharacter (5)
    /// </summary>
    public uint ReadSwitchIndex()
    {
        using var ms = new MemoryStream(ExtraData);
        using var r = new BinaryReader(ms);
        return r.ReadUInt32();
    }

    /// <summary>
    /// Read interactable data: [u32 objectId]
    /// Used by UseInteractableObject (11), CatalystPickup (9)
    /// </summary>
    public uint ReadInteractableObjectId()
    {
        using var ms = new MemoryStream(ExtraData);
        using var r = new BinaryReader(ms);
        return r.ReadUInt32();
    }
}
