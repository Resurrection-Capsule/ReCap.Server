using System.Numerics;
using ReCap.Server.Util;

namespace ReCap.Server.Adapters.RakNet.Packets;

/// <summary>
/// ActionCommandMsgs (0x9C) — client→server player gameplay input.
/// Verified vs Darkspore.exe + C++ OnActionCommandMsgs (Server.cpp:688).
///
/// ActionCommandCommonData (40 bytes, raw LE, #pragma pack(1)), client builder
/// FillCommandHeader @0x004e2150 + FUN_004e2970:
///   u8 type
///   u8 commandStamp — rolling counter DAT_011205e0, present on enqueue-path commands
///      (5/6/9/11/12/13, EnqueueActionCommand @0x004e2f40); 0 for movement (3/4).
///      Must be echoed as ActionCommandResponse byte +0 to clear the client command lock.
///   u8[2] pad
///   u32 locomotionFlags (hero+0x58; C++ names this inputSyncStamp)
///   u32 objectId (FillCommandHeader writes hero objectId; SendActionCommandMsgs @0x0053be60
///      overwrites from locomotion+0x2a8+8 — observed equal to the hero object id on wire)
///   vec3 position (f32×3), quat orientation (f32×4)
/// Then command-specific data depending on type.
///
/// ActionCommand enum (client emits; 1/2 exist in GetPayloadSize @0x00a1ca80 but no emit path):
///   Movement=3, StopMovement=4, SwitchCharacter=5, Overdrive=6, UseCharacterAbility=7,
///   UseSquadAbility=8, CatalystPickup=9, Cancel=10, UseInteractableObject=11,
///   Dance=12, Taunt=13
/// </summary>
public class ActionCommandMsgsPacket : IRakNetPacket
{
    public PacketType Type => PacketType.ActionCommandMsgs;

    // ── Common data (ActionCommandCommonData), in wire order ──
    public byte CommandType { get; set; }
    public byte CommandStamp { get; set; }
    public byte Pad2 { get; set; }
    public byte Pad3 { get; set; }
    public uint LocomotionFlags { get; set; }
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
        CommandStamp = reader.ReadByte();
        Pad2 = reader.ReadByte();
        Pad3 = reader.ReadByte();
        LocomotionFlags = reader.ReadUInt32();
        ObjectId = reader.ReadUInt32();
        PosX = reader.ReadSingle();
        PosY = reader.ReadSingle();
        PosZ = reader.ReadSingle();
        OriX = reader.ReadSingle();
        OriY = reader.ReadSingle();
        OriZ = reader.ReadSingle();
        OriW = reader.ReadSingle();

        var remaining = stream.Length - stream.Position;
        if (remaining > 0)
            ExtraData = reader.ReadBytes((int)remaining);
    }

    public void WriteTo(Stream stream) { /* server never sends this packet */ }

    // ── Helpers to read command-specific data from ExtraData ──

    /// <summary>
    /// ActionCommandMovementData (24 bytes): u32 unk, vec3 goalPosition, u32 goalFlags, u32 unk2.
    /// Used by Movement (3), StopMovement (4), Cancel (10).
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
    /// Used by UseInteractableObject (11)
    /// </summary>
    public uint ReadInteractableObjectId()
    {
        using var ms = new MemoryStream(ExtraData);
        using var r = new BinaryReader(ms);
        return r.ReadUInt32();
    }

    /// <summary>
    /// Overdrive (6) payload: [u8 heroSlot] — client emit @0x004ebb50 (0x004ebc52).
    /// </summary>
    public byte ReadOverdriveSlot() => ExtraData.Length > 0 ? ExtraData[0] : (byte)0;

    /// <summary>
    /// ActionCommandCatalystData (20 bytes): u32 objectId, vec3 position, u32 rank
    /// (client writes 0xFF in the low byte — emit @0x0044ece0/0x0044ed44).
    /// Used by CatalystPickup (9).
    /// </summary>
    public (uint objectId, Vector3 position) ReadCatalystData()
    {
        using var ms = new MemoryStream(ExtraData);
        using var r = new BinaryReader(ms);
        var objectId = r.ReadUInt32();
        var position = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        return (objectId, position);
    }

    /// <summary>
    /// ActionCommandAbilityData (44 bytes, C++ Types.h:855): u32 targetId, vec3 cursorPosition,
    /// vec3 targetPosition, u32 index (ability slot), i32 rank, u32 unk, u32 userData.
    /// Used by UseCharacterAbility (7), UseSquadAbility (8) — client emit @0x004d8a80.
    /// </summary>
    public (uint targetId, Vector3 cursorPos, Vector3 targetPos, uint index, int rank, uint userData) ReadAbilityData()
    {
        using var ms = new MemoryStream(ExtraData);
        using var r = new BinaryReader(ms);
        var targetId = r.ReadUInt32();
        var cursorPos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        var targetPos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        var index = r.ReadUInt32();
        var rank = r.ReadInt32();
        r.ReadUInt32();                 // unk
        var userData = r.ReadUInt32();
        return (targetId, cursorPos, targetPos, index, rank, userData);
    }
}
