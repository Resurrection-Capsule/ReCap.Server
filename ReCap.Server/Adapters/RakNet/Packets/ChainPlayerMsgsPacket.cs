using System.IO;
using System.Text;

using ReCap.Server.Util;

namespace ReCap.Server.Adapters.RakNet.Packets;

public class ChainPlayerMsgsPacket : IRakNetPacket
{
    public PacketType Type => PacketType.ChainPlayerMsgs;

    public byte Value { get; set; }
    public byte Ready { get; set; }
    public byte Unknown { get; set; }
    public uint SquadId { get; set; }

    public int ByteCount { get; set; }

    public void ReadFrom(Stream stream)
    {
        ByteCount = (int)(stream.Length - stream.Position);
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);

        switch (ByteCount)
        {
            case 1:
                Value = reader.ReadByte();
                break;
            case 2:
                Value = reader.ReadByte();
                Ready = reader.ReadByte();
                break;
            case 6:
                Value = reader.ReadByte();
                Unknown = reader.ReadByte();
                SquadId = reader.ReadUInt32();
                break;
        }
    }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

        if (ByteCount == 1)
        {
            writer.Write(Value);
        }
        else if (ByteCount == 2)
        {
            writer.Write(Value);
            writer.Write(Ready);
        }
        else if (ByteCount == 6)
        {
            writer.Write(Value);
            writer.Write(Unknown);
            writer.Write(SquadId);
        }
    }
}
