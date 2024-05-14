using System.Buffers.Binary;

namespace Darkspore.Server.Adapters.Blaze.Extensions;

public static class BinaryWriterExtensions
{
    public static void WriteBigEndian(this BinaryWriter writer, short value)
    {
        Span<byte> buff = stackalloc byte[2];

        BinaryPrimitives.WriteInt16BigEndian(buff, value);

        writer.Write(buff);
    }

    public static void WriteBigEndian(this BinaryWriter writer, ushort value)
    {
        Span<byte> buff = stackalloc byte[2];

        BinaryPrimitives.WriteUInt16BigEndian(buff, value);

        writer.Write(buff);
    }

    public static void WriteBigEndian(this BinaryWriter writer, int value)
    {
        Span<byte> buff = stackalloc byte[4];

        BinaryPrimitives.WriteInt32BigEndian(buff, value);

        writer.Write(buff);
    }

    public static void WriteBigEndian(this BinaryWriter writer, uint value)
    {
        Span<byte> buff = stackalloc byte[4];

        BinaryPrimitives.WriteUInt32BigEndian(buff, value);

        writer.Write(buff);
    }

    public static void WriteBigEndian(this BinaryWriter writer, long value)
    {
        Span<byte> buff = stackalloc byte[8];

        BinaryPrimitives.WriteInt64BigEndian(buff, value);

        writer.Write(buff);
    }

    public static void WriteBigEndian(this BinaryWriter writer, ulong value)
    {
        Span<byte> buff = stackalloc byte[8];

        BinaryPrimitives.WriteUInt64BigEndian(buff, value);

        writer.Write(buff);
    }
}
