using System.Numerics;
using System.Text;

namespace ReCap.Server.Adapters.RakNet.Packets;

// CombatEvent (wire 0xBA) — client ClientNet::OnGmsCombatEvent @0x0053ed50 parses it via the generic
// reflection parser into the CombatEvent struct (no leading object id; target/source are fields).
// Drives floating damage/heal numbers + the combat log. Layout (ids, order, types) comes from the
// AssetData.Parser CombatEvent schema (reflection_serializer<8>), not a hardcoded list:
//   0 flags(u16) 1 deltaHealth(f32) 2 absorbedAmount(f32) 3 targetID(u32) 4 sourceID(u32)
//   5 abilityID(u32) 6 damageDirection(Vector3) 7 integerHpChange(i32)
public class CombatEventPacket : IRakNetPacket
{
    public PacketType Type => PacketType.CombatEvent;

    public ushort Flags { get; set; }
    public float DeltaHealth { get; set; }
    public float AbsorbedAmount { get; set; }
    public uint TargetId { get; set; }
    public uint SourceId { get; set; }
    public uint AbilityId { get; set; }
    public Vector3 DamageDirection { get; set; }
    public int IntegerHpChange { get; set; }

    public void ReadFrom(Stream stream) { }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        AssetReflection.WriteReflection(writer, "CombatEvent", new Dictionary<string, object>
        {
            ["flags"] = Flags,
            ["deltaHealth"] = DeltaHealth,
            ["absorbedAmount"] = AbsorbedAmount,
            ["targetID"] = TargetId,
            ["sourceID"] = SourceId,
            ["abilityID"] = AbilityId,
            ["damageDirection"] = DamageDirection,
            ["integerHpChange"] = IntegerHpChange,
        });
    }
}
