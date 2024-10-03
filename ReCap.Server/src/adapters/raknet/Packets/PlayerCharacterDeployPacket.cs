using System.Text;

namespace ReCap.RakNet.Packets;

public class PlayerCharacterDeployPacket : IRakNetPacket
{
    public PacketType Type => PacketType.PlayerCharacterDeploy;
    public PlayerCharacterDeployPacket(byte player, uint localObjectId)
    {
        Player = player;
        LocalObjectId = localObjectId;
    }

    public byte Player { get; set; }
    public uint LocalObjectId { get; set; }

    public void ReadFrom(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);
        Player = reader.ReadByte();
        LocalObjectId = reader.ReadUInt32();
    }
    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(Player);
        writer.Write(LocalObjectId);
    }
}

