using System.IO;
using System.Text;

namespace ReCap.Server.Adapters.RakNet.Packets;

// ObjectDelete (0x8E). Client handler ClientNet::OnGmsObjectDelete @0x0053ddc0 (Ghidra 2026-06-28):
// the body is read as a flat array of u32 object ids (id count = bodyLen >> 2, stride 4) and each id
// is removed via ObjectManager::RemoveObject. No flags, no vaporize byte, no count prefix — the
// despawn is immediate (no death animation is driven by this message). LE per the game protocol.
public class ObjectDeletePacket : IRakNetPacket
{
    public PacketType Type => PacketType.ObjectDelete;
    public IReadOnlyList<uint> ObjectIds { get; set; } = [];

    public void ReadFrom(Stream stream) { }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        foreach (var id in ObjectIds)
            writer.Write(id);
    }
}
