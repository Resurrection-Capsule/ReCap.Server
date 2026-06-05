using System.IO;
using System.Text;

namespace ReCap.Server.Adapters.RakNet.Packets;

// CrystalDragMessage (0xC2) — client→server catalyst HUD drag, raw LE 24B (6×u32).
// C++ OnCrystalDragMessage (Server.cpp:1026-1091); client sender nPlayer::PickupCrystal
// @0x009ff6d0 → FUN_00a25f50. MoveType: 0=drop to world, 1=unknown, 2=grid-to-grid move.
// PickupTime/DropTime = client sim timestamps; Unk read-but-ignored by C++.
public class CrystalDragMessagePacket : IRakNetPacket
{
    public PacketType Type => PacketType.CrystalDragMessage;

    public uint CrystalSlot { get; set; }
    public uint MoveType { get; set; }
    public uint PickupTime { get; set; }
    public uint DropTime { get; set; }
    public uint Unk { get; set; }
    public uint NewSlot { get; set; }

    public void ReadFrom(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        CrystalSlot = reader.ReadUInt32();
        MoveType = reader.ReadUInt32();
        PickupTime = reader.ReadUInt32();
        DropTime = reader.ReadUInt32();
        Unk = reader.ReadUInt32();
        NewSlot = reader.ReadUInt32();
    }

    public void WriteTo(Stream stream) { /* server never sends this packet */ }
}
