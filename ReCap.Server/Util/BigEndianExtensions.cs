using System;
using System.Buffers.Binary;
using System.IO;
using System.Numerics;
using System.Text;

namespace ReCap.Server.Util;

// Endianness note (verified 2026-05-28 via raw packet capture vs Darkspore source):
// the C++ darkspore `Write<T>(stream, value)` wrapper looks like it byte-swaps to network order,
// but it then calls `BitStream::Write<T>` from raknet 3.902 which itself reverses bytes on a
// little-endian host -- the two swaps cancel, so darkspore game packets (0x7F+) actually carry
// scalars in LITTLE-ENDIAN on the wire. BinaryWriter.Write(primitive) is already LE, so the
// neutral `Write(...)` extensions below cover the compound types (Vector*, Quaternion) the same
// way the C++ reflection_serializer does -- one little-endian float per component. The legacy
// WriteBE/ReadXxxBE helpers are kept for non-game-packet paths (Blaze TDF and RakNet handshake
// fields, which ARE big-endian).
public static class BigEndianExtensions
{
    public static void Write(this BinaryWriter writer, Vector2 value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
    }

    public static void Write(this BinaryWriter writer, Vector3 value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
    }

    public static void Write(this BinaryWriter writer, Quaternion value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
        writer.Write(value.W);
    }


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
