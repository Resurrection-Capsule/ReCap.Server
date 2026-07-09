using System.Text;

namespace ReCap.Server.Adapters.RakNet.Packets;

// ModifierCreated (wire 0xA2 / kGms 36) — client ClientNet::ApplyModifierCreated @0x0053c590 reads a
// raw fixed 37-byte payload (ReadStreamBytes 0x25) and inserts a record into the target's modifier
// map (object+0x2b4) keyed by InstanceId. NOT a reflection bitmap. Layout verified 2026-07-09 by
// Ghidra offset decode + C++ SendModifierCreated field order (Server.cpp:1932):
//   [+0x00] targetObjectId(u32)  [+0x04] modifierGuid(u32, the def hash -> icon)  [+0x08] instanceId(u32, map key)
//   [+0x0C] durationMs(u32, 0xFFFFFFFF=infinite)  [+0x10] overdrive(u32)  [+0x14] stackCount(u32, mutable via 0xA3)
//   [+0x18] startTime(u64)  [+0x20] sourceObjectId(u32, 0 is safe — parser guards ==0)  [+0x24] bind(u8)
// The parser stores every field; only target lookup (+0x00) and the local-hero combat-text path can
// fault, so unknown fields left 0 are safe. overdrive/stackCount exact meaning unconfirmed (C++ sends 1).
public class ModifierCreatedPacket : IRakNetPacket
{
    public PacketType Type => PacketType.ModifierCreated;

    public uint TargetId { get; set; }
    public uint ModifierGuid { get; set; }
    public uint InstanceId { get; set; }
    public uint DurationMs { get; set; } = 0xFFFFFFFF;
    public uint Overdrive { get; set; }
    public uint StackCount { get; set; } = 1;
    public ulong StartTime { get; set; }
    public uint SourceId { get; set; }
    public byte Bind { get; set; }

    public void ReadFrom(Stream stream) { }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(TargetId);
        writer.Write(ModifierGuid);
        writer.Write(InstanceId);
        writer.Write(DurationMs);
        writer.Write(Overdrive);
        writer.Write(StackCount);
        writer.Write(StartTime);
        writer.Write(SourceId);
        writer.Write(Bind);
    }
}
