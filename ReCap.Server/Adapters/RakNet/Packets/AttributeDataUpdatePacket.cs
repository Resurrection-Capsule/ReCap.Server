using System.Text;

namespace ReCap.Server.Adapters.RakNet.Packets;

// C++ Server::SendAttributeDataUpdate (0x96): u32 objectId + Attributes::WriteReflection.
// reflection_serializer<113> => >16 fields => byte field-ID + f32 value per entry, 0xFF terminator.
// MinWeaponDamage (111) and MaxWeaponDamage (112) are always written by C++.
public class AttributeDataUpdatePacket : IRakNetPacket
{
    public PacketType Type => PacketType.AttributeDataUpdate;

    public uint ObjectId { get; set; }

    // AttributeType field IDs (Attributes.h)
    public const byte MaxHealth = 4;
    public const byte MaxMana = 5;
    public const byte AttackSpeedScale = 23;
    public const byte CooldownScale = 24;
    public const byte InvisibleToSecurityTeleporters = 109;
    public const byte MinWeaponDamage = 111;
    public const byte MaxWeaponDamage = 112;

    // Field IDs must be written in ascending order, as C++ iterates the attribute table.
    private readonly SortedDictionary<byte, float> _values = new();

    public void Set(byte fieldId, float value) => _values[fieldId] = value;

    public void ReadFrom(Stream stream) { }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(ObjectId);

        foreach (var (fieldId, value) in _values)
        {
            writer.Write(fieldId);
            writer.Write(value);
        }
        writer.Write((byte)0xFF);
    }
}
