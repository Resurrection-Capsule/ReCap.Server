using System.Buffers.Binary;

namespace ReCap.Server.Adapters.Blaze.Extensions;

public static class BinaryReaderExtensions
{
    public static ushort ReadBigEndianUInt16(this BinaryReader reader)
    {
        Span<byte> buff = stackalloc byte[2];

        if (reader.Read(buff) < 2)
            throw new Exception($"Unable to read required bytes!");

        return BinaryPrimitives.ReadUInt16BigEndian(buff);
    }

    public static int ReadBigEndianInt32(this BinaryReader reader)
    {
        Span<byte> buff = stackalloc byte[4];

        if (reader.Read(buff) < 4)
            throw new Exception($"Unable to read required bytes!");

        return BinaryPrimitives.ReadInt32BigEndian(buff);
    }
}
