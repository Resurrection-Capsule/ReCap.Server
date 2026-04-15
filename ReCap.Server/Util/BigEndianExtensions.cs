using System;
using System.Buffers.Binary;
using System.IO;
using System.Numerics;
using System.Text;

namespace ReCap.Server.Util;

public static class BigEndianExtensions
{
    public static uint ReadUInt32BE(this BinaryReader reader)
    {
        return BinaryPrimitives.ReverseEndianness(reader.ReadUInt32());
    }

    public static float ReadSingleBE(this BinaryReader reader)
    {
        uint val = reader.ReadUInt32();
        uint swapped = BinaryPrimitives.ReverseEndianness(val);
        return BitConverter.UInt32BitsToSingle(swapped);
    }

    public static ushort ReadUInt16BE(this BinaryReader reader)
    {
        return BinaryPrimitives.ReverseEndianness(reader.ReadUInt16());
    }

    public static ulong ReadUInt64BE(this BinaryReader reader)
    {
        return BinaryPrimitives.ReverseEndianness(reader.ReadUInt64());
    }

    public static void WriteBE(this BinaryWriter writer, uint value)
    {
        writer.Write(BinaryPrimitives.ReverseEndianness(value));
    }

    public static void WriteBE(this BinaryWriter writer, ushort value)
    {
        writer.Write(BinaryPrimitives.ReverseEndianness(value));
    }

    public static void WriteBE(this BinaryWriter writer, ulong value)
    {
        writer.Write(BinaryPrimitives.ReverseEndianness(value));
    }

    public static void WriteBE(this BinaryWriter writer, int value)
    {
        writer.Write(BinaryPrimitives.ReverseEndianness(value));
    }

    public static void WriteBE(this BinaryWriter writer, byte value)
    {
        writer.Write(value);
    }

    public static void WriteBE(this BinaryWriter writer, bool value)
    {
        writer.Write(value);
    }

    public static void WriteBE(this BinaryWriter writer, float value)
    {
        uint bits = BitConverter.SingleToUInt32Bits(value);
        writer.Write(BinaryPrimitives.ReverseEndianness(bits));
    }

    public static void WriteBE(this BinaryWriter writer, Vector2 value)
    {
        writer.WriteBE(value.X);
        writer.WriteBE(value.Y);
    }

    public static void WriteBE(this BinaryWriter writer, Vector3 value)
    {
        writer.WriteBE(value.X);
        writer.WriteBE(value.Y);
        writer.WriteBE(value.Z);
    }

    public static void WriteBE(this BinaryWriter writer, Quaternion value)
    {
        writer.WriteBE(value.X);
        writer.WriteBE(value.Y);
        writer.WriteBE(value.Z);
        writer.WriteBE(value.W);
    }
}
