using System.Text;

namespace ReCap.Server.Adapters.RakNet.Packets;

// C++ Server::SendCombatantDataUpdate (0x97): u32 objectId + CombatantData::WriteReflection<2>.
// reflection_serializer<2> => 1-byte bitmap + f32 mHitPoints + f32 mManaPoints.
// Required for creature objects (player heroes): without it the client has no
// health/mana for the deployed hero and crashes on deploy.
public class CombatantDataUpdatePacket : IRakNetPacket
{
    public PacketType Type => PacketType.CombatantDataUpdate;

    public uint ObjectId { get; set; }
    public float HitPoints { get; set; }
    public float ManaPoints { get; set; }

    public void ReadFrom(Stream stream) { }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(ObjectId);

        var reflection = new ReflectionSerializer(writer, 2);
        reflection.Begin();
        reflection.Write(0, () => writer.Write(HitPoints));
        reflection.Write(1, () => writer.Write(ManaPoints));
        reflection.End();
    }
}
