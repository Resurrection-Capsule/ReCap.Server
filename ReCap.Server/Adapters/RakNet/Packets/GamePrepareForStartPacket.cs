using System.Text;

using ReCap.Server.Util;

namespace ReCap.Server.Adapters.RakNet.Packets;

public class GamePrepareForStartPacket : IRakNetPacket
{
    public PacketType Type => PacketType.GamePrepareForStart;

    public uint LevelHash { get; set; }
    public uint MarkerSetHash { get; set; }
    public uint PlayerBitmask { get; set; }
    public uint LevelIndex { get; set; }

    public GamePrepareForStartPacket()
    {
    }

    public GamePrepareForStartPacket(uint levelHash, uint markerSetHash, uint playerBitmask, uint levelIndex)
    {
        LevelHash = levelHash;
        MarkerSetHash = markerSetHash;
        PlayerBitmask = playerBitmask;
        LevelIndex = levelIndex;
    }

    public void ReadFrom(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);

        LevelHash = reader.ReadUInt32();
        MarkerSetHash = reader.ReadUInt32();
        PlayerBitmask = reader.ReadUInt32();
        LevelIndex = reader.ReadUInt32();
    }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

        writer.WriteBE(LevelHash);
        writer.WriteBE(MarkerSetHash);
        writer.WriteBE(PlayerBitmask);
        writer.WriteBE(LevelIndex);
    }
}
