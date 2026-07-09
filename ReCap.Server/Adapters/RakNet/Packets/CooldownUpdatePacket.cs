using System.Text;

namespace ReCap.Server.Adapters.RakNet.Packets;

// CooldownUpdate (wire 0xC1 / kGms 66) — client ClientNet::ApplyCooldownUpdate @0x004d99f0 reads a
// raw fixed 36-byte payload (ReadStreamBytes 0x24), NOT a reflection bitmap. Verified 2026-07-09:
//   [+0] objectId(u32)  [+4] abilityId(u32)   -> 8-byte key of the per-ability cooldown entry
//   [+8] reserved(u32, ignored by the reader)
//   [+12] duration(u64) [+20] start(u64)      [+28] globalCooldown(u64)
// Relative form (start=0): the client stamps end = its own game-clock now + duration, so the server
// never needs the client's clock. globalCooldown=0 skips the client's global-cooldown flag path.
// UI-only (drives the ability-button cooldown swirl); the server-side spam gate is nAbility cooldown.
public class CooldownUpdatePacket : IRakNetPacket
{
    public PacketType Type => PacketType.CooldownUpdate;

    public uint ObjectId { get; set; }
    public uint AbilityId { get; set; }
    public ulong Duration { get; set; }
    public ulong Start { get; set; }
    public ulong GlobalCooldown { get; set; }

    public void ReadFrom(Stream stream) { }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(ObjectId);
        writer.Write(AbilityId);
        writer.Write(0u);
        writer.Write(Duration);
        writer.Write(Start);
        writer.Write(GlobalCooldown);
    }
}
