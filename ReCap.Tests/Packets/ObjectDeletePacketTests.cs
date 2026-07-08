using ReCap.Server.Adapters.RakNet;
using ReCap.Server.Adapters.RakNet.Packets;

namespace ReCap.Tests.Packets;

// ObjectDelete (0x8E) — client handler ClientNet::OnGmsObjectDelete @0x0053ddc0 (Ghidra 2026-06-28).
// Body = a flat array of u32 object ids (count = bodyLen/4, stride 4); no flags, no vaporize bool,
// no count prefix. The client iterates len>>2 ids and ObjectManager::RemoveObject each (immediate
// despawn, no death anim driven by this packet). LE per the game protocol ([[endianness-wrapper-is-LE]]).
public class ObjectDeletePacketTests
{
    private static byte[] Serialize(IRakNetPacket p)
    {
        using var ms = new MemoryStream();
        p.WriteTo(ms);
        return ms.ToArray();
    }

    [Fact]
    public void SingleId_Writes_Four_Bytes_LE()
    {
        var bytes = Serialize(new ObjectDeletePacket { ObjectIds = [0x000000A2u] });
        Assert.Equal(Convert.FromHexString("a2000000"), bytes);
    }

    [Fact]
    public void MultipleIds_Write_Contiguous_U32_LE()
    {
        var bytes = Serialize(new ObjectDeletePacket { ObjectIds = [2u, 16u, 0xDEADBEEFu] });
        Assert.Equal(Convert.FromHexString("0200000010000000efbeadde"), bytes);
    }

    [Fact]
    public void Type_Is_ObjectDelete()
    {
        Assert.Equal(PacketType.ObjectDelete, new ObjectDeletePacket().Type);
    }
}
