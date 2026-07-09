using ReCap.Server.Adapters.RakNet;
using ReCap.Server.Adapters.RakNet.Packets;

namespace ReCap.Tests.Packets;

// DirectorState (0x8B) — client ClientNet::OnGmsObjectDirectorState @0x0053dcd0 parses it as a
// reflection_serializer<7> over the cAIDirector schema (AssetData::cAIDirector @0x00f78480: 7 fields,
// registration order = wire id). Layout is driven by AssetData.Parser's cAIDirector struct schema, not
// a hardcoded field list. LE per the game protocol. Replaces the old wrong 0x4D0 raw zero-blob.
//   id 0 mbBossSpawned(bool) 1 mbBossHorde(bool) 2 mbCaptainSpawned(bool) 3 mbBossComplete(bool)
//   4 mbHordeSpawned(bool)   5 mBossId(u32)       6 mActiveHordeWaves(i32)
public class DirectorStatePacketTests
{
    private static byte[] Serialize(IRakNetPacket p)
    {
        using var ms = new MemoryStream();
        p.WriteTo(ms);
        return ms.ToArray();
    }

    [Fact]
    public void Idle_Writes_Bitmap_Plus_Zeroed_Fields()
    {
        // All 7 fields present (bitmap 0x7F), then 5 bool(0) + u32(0) + i32(0) = 1 + 5 + 4 + 4 = 14 bytes.
        var bytes = Serialize(new DirectorStatePacket());
        Assert.Equal(Convert.FromHexString("7f00000000000000000000000000"), bytes);
    }

    [Fact]
    public void Populated_Writes_Fields_In_Schema_Order_LE()
    {
        var bytes = Serialize(new DirectorStatePacket
        {
            BossSpawned = true,
            HordeSpawned = true,
            BossId = 0xDEADBEEFu,
            ActiveHordeWaves = 3,
        });
        // 7f | 01 00 00 00 01 | efbeadde (u32 LE) | 03000000 (i32 LE)
        Assert.Equal(Convert.FromHexString("7f0100000001efbeadde03000000"), bytes);
    }

    [Fact]
    public void Type_Is_DirectorState()
    {
        Assert.Equal(PacketType.DirectorState, new DirectorStatePacket().Type);
    }
}
