using System.Text;

namespace ReCap.Server.Adapters.RakNet.Packets;

// ModifierDeleted (wire 0xA4 / kGms 38) — client ClientNet::ApplyModifierDeleted @0x0053c320 reads a
// raw fixed 8-byte payload, unlinks+frees the record from the target's modifier map by InstanceId.
// Verified 2026-07-09: [+0x00] targetObjectId(u32)  [+0x04] instanceId(u32, key to remove).
public class ModifierDeletedPacket : IRakNetPacket
{
    public PacketType Type => PacketType.ModifierDeleted;

    public uint TargetId { get; set; }
    public uint InstanceId { get; set; }

    public void ReadFrom(Stream stream) { }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(TargetId);
        writer.Write(InstanceId);
    }
}
