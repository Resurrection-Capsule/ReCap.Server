using System.IO;
using System.Text;

namespace ReCap.Server.Adapters.RakNet.Packets;

// C++ Server::SendInteractableDataUpdate (0x98): u32 objectId + cInteractableData::WriteTo (0x34 blob).
// The C++ blob is mostly uninitialized heap (pointer garbage on the wire); the client reads only the
// meaningful fields, so a zeroed blob with those fields set is a clean equivalent (VERIFIED_FACTS gap rule).
// Verified offsets within the 0x34 block: @0x08 s32 timesUsed, @0x0C s32 usesAllowed, @0x14 u32 ability.
// C++ sends one of these per created object (wire: 0x98 ×N paired with ObjectCreate ×N).
public class InteractableDataUpdatePacket : IRakNetPacket
{
    public PacketType Type => PacketType.InteractableDataUpdate;

    public uint ObjectId { get; set; }
    public int TimesUsed { get; set; }
    public int UsesAllowed { get; set; }
    public uint Ability { get; set; }

    public void ReadFrom(Stream stream) { }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(ObjectId);

        var buffer = new byte[0x34];
        using var ms = new MemoryStream(buffer, writable: true);
        using var bw = new BinaryWriter(ms, Encoding.UTF8, true);
        ms.Position = 0x08; bw.Write(TimesUsed);
        ms.Position = 0x0C; bw.Write(UsesAllowed);
        ms.Position = 0x14; bw.Write(Ability);

        writer.Write(buffer);
    }
}
