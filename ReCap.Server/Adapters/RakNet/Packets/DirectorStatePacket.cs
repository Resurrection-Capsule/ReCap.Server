using System.Text;

namespace ReCap.Server.Adapters.RakNet.Packets;

// DirectorState (wire 0x8B) = the global cAIDirector, reflection-encoded (NOT a raw blob).
// Client parse: ClientNet::OnGmsDirectorState @0x0053dcd0 -> generic reflection parser; the field
// table is AssetData::cAIDirector @0x00f78480 (class 0x1fb04a19, struct 0x4d0, 7 fields). The
// previous 0x4D0 zero-blob "worked" only because the reflection parser read the leading 0x00 as an
// empty bitmap and ignored the 1231 trailing bytes — wrong-shaped and wasteful. This emits the real
// reflection_serializer<7>: 1-byte bitmap + the set fields, in registered id order.
//   0 mbBossSpawned(bool)  1 mbBossHorde(bool)  2 mbCaptainSpawned(bool)  3 mbBossComplete(bool)
//   4 mbHordeSpawned(bool)  5 mBossId(u32)  6 mActiveHordeWaves(i32)
// Defaults = idle director (Dungeon entry: no boss/horde). Populated once the AI director drives it.
public class DirectorStatePacket : IRakNetPacket
{
    public PacketType Type => PacketType.DirectorState;

    public bool BossSpawned { get; set; }
    public bool BossHorde { get; set; }
    public bool CaptainSpawned { get; set; }
    public bool BossComplete { get; set; }
    public bool HordeSpawned { get; set; }
    public uint BossId { get; set; }
    public int ActiveHordeWaves { get; set; }

    public void ReadFrom(Stream stream) { }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

        // Layout (field ids, order, types) comes from the AssetData.Parser cAIDirector schema, not a
        // hardcoded list — this packet only maps its typed properties to the schema's field names.
        AssetReflection.WriteReflection(writer, "cAIDirector", new Dictionary<string, object>
        {
            ["mbBossSpawned"] = BossSpawned,
            ["mbBossHorde"] = BossHorde,
            ["mbCaptainSpawned"] = CaptainSpawned,
            ["mbBossComplete"] = BossComplete,
            ["mbHordeSpawned"] = HordeSpawned,
            ["mBossId"] = BossId,
            ["mActiveHordeWaves"] = ActiveHordeWaves,
        });
    }
}
