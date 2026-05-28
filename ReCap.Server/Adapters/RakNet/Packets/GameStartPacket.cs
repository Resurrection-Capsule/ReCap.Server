using System.Text;

using ReCap.Server.Util;

namespace ReCap.Server.Adapters.RakNet.Packets;

public class GameStartPacket : IRakNetPacket
{
    public PacketType Type => PacketType.GameStart;

    public uint LevelIndex { get; set; }

    public GameStartPacket(uint levelIndex) => LevelIndex = levelIndex;
    public GameStartPacket() { }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

        writer.Write(LevelIndex);
    }

    public void ReadFrom(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);

        LevelIndex = reader.ReadUInt32();
    }
}
