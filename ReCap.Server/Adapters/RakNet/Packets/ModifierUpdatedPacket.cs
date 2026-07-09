using System.Text;

namespace ReCap.Server.Adapters.RakNet.Packets;

// ModifierUpdated (wire 0xA3 / kGms 37) — client ClientNet::ApplyModifierUpdated @0x0053c280 reads a
// raw fixed 21-byte payload (ReadStreamBytes 0x15), finds the record in the target's modifier map by
// InstanceId, and patches timestamp + the two mutable fields. Verified 2026-07-09:
//   [+0x00] targetObjectId(u32)  [+0x04] instanceId(u32, key)
//   [+0x08] startTime(u64, 0xFFFFFFFFFFFFFFFF = do NOT update duration)
//   [+0x10] stackCount(u32 -> record+0x24)  [+0x14] bind(u8 -> record+0x2c)
public class ModifierUpdatedPacket : IRakNetPacket
{
    public const ulong KeepDuration = 0xFFFFFFFFFFFFFFFF;

    public PacketType Type => PacketType.ModifierUpdated;

    public uint TargetId { get; set; }
    public uint InstanceId { get; set; }
    public ulong StartTime { get; set; } = KeepDuration;
    public uint StackCount { get; set; }
    public byte Bind { get; set; }

    public void ReadFrom(Stream stream) { }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(TargetId);
        writer.Write(InstanceId);
        writer.Write(StartTime);
        writer.Write(StackCount);
        writer.Write(Bind);
    }
}
