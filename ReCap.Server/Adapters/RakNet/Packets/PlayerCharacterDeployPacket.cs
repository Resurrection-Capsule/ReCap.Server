using System.Text;

namespace ReCap.Server.Adapters.RakNet.Packets;

// C++ SendPlayerCharacterDeploy (0xA7), 9-byte body:
//   u8  player->GetId()
//   u32 creatureIndex (deck slot)
//   u32 characterObject->GetId()
public class PlayerCharacterDeployPacket : IRakNetPacket
{
    public PacketType Type => PacketType.PlayerCharacterDeploy;

    public PlayerCharacterDeployPacket(byte player, uint creatureIndex, uint localObjectId)
    {
        Player = player;
        CreatureIndex = creatureIndex;
        LocalObjectId = localObjectId;
    }

    public byte Player { get; set; }
    public uint CreatureIndex { get; set; }
    public uint LocalObjectId { get; set; }

    public void ReadFrom(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);
        Player = reader.ReadByte();
        CreatureIndex = reader.ReadUInt32();
        LocalObjectId = reader.ReadUInt32();
    }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(Player);
        writer.Write(CreatureIndex);
        writer.Write(LocalObjectId);
    }
}
