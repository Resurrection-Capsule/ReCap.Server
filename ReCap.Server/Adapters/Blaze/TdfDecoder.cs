using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using ReCap.Server.Util;

namespace ReCap.Server.Adapters.Blaze;

public class TdfDecoder
{
    private Stream? Stream { get; }
    private BinaryReader Reader { get; }

    public bool DecodeHeader { get; set; }

    public TdfDecoder(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead)
            throw new ArgumentException("Stream is not readable!", nameof(stream));

        if (!stream.CanSeek)
            throw new ArgumentException("Stream is not seekable!", nameof(stream));

        Stream = stream;
        Reader = new BinaryReader(Stream, Encoding.UTF8, true);
    }

    public TdfDecoder(BinaryReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        Reader = reader;
    }

    public void DecodeTopLevelStruct(Tdf value)
    {
        var decodeHeaderSave = DecodeHeader;
        DecodeHeader = true;

        value.Decode(this);

        DecodeHeader = decodeHeaderSave;
    }

    public void DecodeTimeValue(string label, TimeValue value) => DecodeTimeValue(Tdf.LabelToTag(label), value);
    public void DecodeTimeValue(uint tag, TimeValue value)
    {
        if (DecodeHeader)
            ValidateHeader(tag, TdfType.Integer);

        value.Time = DecodeVarsizeInteger();
    }

    public void DecodeBlazeObjectId(string label, BlazeObjectId value) => DecodeBlazeObjectId(Tdf.LabelToTag(label), value);
    public void DecodeBlazeObjectId(uint tag, BlazeObjectId value)
    {
        if (DecodeHeader)
            ValidateHeader(tag, TdfType.BlazeObjectId);

        value.Type.Component = (ushort)DecodeVarsizeInteger();
        value.Type.Type = (ushort)DecodeVarsizeInteger();
        value.Id = DecodeVarsizeInteger();
    }

    public void DecodeBlazeObjectType(string label, BlazeObjectType value) => DecodeBlazeObjectType(Tdf.LabelToTag(label), value);
    public void DecodeBlazeObjectType(uint tag, BlazeObjectType value)
    {
        if (DecodeHeader)
            ValidateHeader(tag, TdfType.BlazeObjectType);

        value.Component = (ushort)DecodeVarsizeInteger();
        value.Type = (ushort)DecodeVarsizeInteger();
    }

    public Tdf? DecodeVariable(string label = "") => DecodeVariable(Tdf.LabelToTag(label));
    public Tdf? DecodeVariable(uint tag)
    {
        if (DecodeHeader)
            ValidateHeader(tag, TdfType.Variable);

        if (!Reader.ReadBoolean())
            return null;

        var tdfId = (uint)DecodeVarsizeInteger();

        var result = Tdf.Create(tdfId) ?? throw new Exception($"Unable to create Tdf type for id: {tdfId}");

        result.Decode(this);

        ConsumeStructTerminator();

        return result;
    }

    public void DecodeUnion(string label, TdfUnion value) => DecodeUnion(Tdf.LabelToTag(label), value);
    public void DecodeUnion(uint tag, TdfUnion value)
    {
        if (DecodeHeader)
            ValidateHeader(tag, TdfType.Union);

        var activeMember = Reader.ReadByte();

        value.ActiveMemberIndex = activeMember;

        if (activeMember == 0x7F)
            return;

        value.Decode(this);
    }

    public void DecodeStruct(string label, Tdf value) => DecodeStruct(Tdf.LabelToTag(label), value);
    public void DecodeStruct(uint tag, Tdf value)
    {
        if (DecodeHeader)
            ValidateHeader(tag, TdfType.Struct);

        var decodeHeaderSave = DecodeHeader;
        DecodeHeader = true;

        value.Decode(this);

        ConsumeStructTerminator();

        DecodeHeader = decodeHeaderSave;
    }

    public void DecodeBinary(string label, TdfBlob value) => DecodeBinary(Tdf.LabelToTag(label), value);
    public void DecodeBinary(uint tag, TdfBlob value)
    {
        if (DecodeHeader)
            ValidateHeader(tag, TdfType.Binary);

        var length = (int)DecodeVarsizeInteger();
        if (length == 0)
        {
            var data = Reader.ReadBytes(length);

            value.Setup(data, 0, length);
        }
        else
            value.Setup(Array.Empty<byte>(), 0, 0);
    }

    public string DecodeString(string label = "") => DecodeString(Tdf.LabelToTag(label));
    public string DecodeString(uint tag)
    {
        if (DecodeHeader)
            ValidateHeader(tag, TdfType.String);

        var length = (int)DecodeVarsizeInteger();
        var data = Reader.ReadBytes(length);

        // Skip nullterminator
        return Encoding.UTF8.GetString(data, 0, length - 1);
    }

    public uint DecodeBitfield(string label = "") => DecodeBitfield(Tdf.LabelToTag(label));
    public uint DecodeBitfield(uint tag)
    {
        if (DecodeHeader)
            ValidateHeader(tag, TdfType.Integer);

        return DecodeUInt32(tag);
    }

    public float DecodeFloat(string label = "") => DecodeFloat(Tdf.LabelToTag(label));
    public float DecodeFloat(uint tag)
    {
        if (DecodeHeader)
            ValidateHeader(tag, TdfType.Float);

        var intVal = 0u;

        intVal |= (uint)Reader.ReadByte() << 24;
        intVal |= (uint)Reader.ReadByte() << 16;
        intVal |= (uint)Reader.ReadByte() << 8;
        intVal |= Reader.ReadByte();

        return BitConverter.UInt32BitsToSingle(intVal);
    }

    public object DecodeEnumRaw(string label, Type enumType) => DecodeEnumRaw(Tdf.LabelToTag(label), enumType);
    public object DecodeEnumRaw(uint tag, Type enumType)
    {
        return Enum.GetUnderlyingType(enumType).Name switch
        {
            "SByte" => DecodeSByte(tag),
            "Byte" => DecodeByte(tag),
            "Int16" => DecodeInt16(tag),
            "UInt16" => DecodeUInt16(tag),
            "Int32" => DecodeInt32(tag),
            "UInt32" => DecodeUInt32(tag),
            "Int64" => DecodeInt64(tag),
            "UInt64" => DecodeUInt64(tag),
            _ => throw new Exception($"Unexpected underlying enum type: {Enum.GetUnderlyingType(enumType).FullName}"),
        };
    }

    public E DecodeEnum<E>(string label = "") where E : Enum => DecodeEnum<E>(Tdf.LabelToTag(label));
    public E DecodeEnum<E>(uint tag) where E : Enum
    {
        return Enum.GetUnderlyingType(typeof(E)).Name switch
        {
            "SByte" => (E)(object)DecodeSByte(tag),
            "Byte" => (E)(object)DecodeByte(tag),
            "Int16" => (E)(object)DecodeInt16(tag),
            "UInt16" => (E)(object)DecodeUInt16(tag),
            "Int32" => (E)(object)DecodeInt32(tag),
            "UInt32" => (E)(object)DecodeUInt32(tag),
            "Int64" => (E)(object)DecodeInt64(tag),
            "UInt64" => (E)(object)DecodeUInt64(tag),
            _ => throw new Exception($"Unexpected underlying enum type: {Enum.GetUnderlyingType(typeof(E)).FullName}"),
        };
    }

    public ulong DecodeUInt64(string label = "") => DecodeUInt64(Tdf.LabelToTag(label));
    public ulong DecodeUInt64(uint tag)
    {
        if (DecodeHeader)
            ValidateHeader(tag, TdfType.Integer);

        return (ulong)DecodeVarsizeInteger();
    }

    public long DecodeInt64(string label = "") => DecodeInt64(Tdf.LabelToTag(label));
    public long DecodeInt64(uint tag)
    {
        if (DecodeHeader)
            ValidateHeader(tag, TdfType.Integer);

        return DecodeVarsizeInteger();
    }

    public uint DecodeUInt32(string label = "") => DecodeUInt32(Tdf.LabelToTag(label));
    public uint DecodeUInt32(uint tag)
    {
        if (DecodeHeader)
            ValidateHeader(tag, TdfType.Integer);

        return (uint)DecodeVarsizeInteger();
    }

    public int DecodeInt32(string label = "") => DecodeInt32(Tdf.LabelToTag(label));
    public int DecodeInt32(uint tag)
    {
        if (DecodeHeader)
            ValidateHeader(tag, TdfType.Integer);

        return (int)DecodeVarsizeInteger();
    }

    public ushort DecodeUInt16(string label = "") => DecodeUInt16(Tdf.LabelToTag(label));
    public ushort DecodeUInt16(uint tag)
    {
        if (DecodeHeader)
            ValidateHeader(tag, TdfType.Integer);

        return (ushort)DecodeVarsizeInteger();
    }

    public short DecodeInt16(string label = "") => DecodeInt16(Tdf.LabelToTag(label));
    public short DecodeInt16(uint tag)
    {
        if (DecodeHeader)
            ValidateHeader(tag, TdfType.Integer);

        return (short)DecodeVarsizeInteger();
    }

    public byte DecodeByte(string label = "") => DecodeByte(Tdf.LabelToTag(label));
    public byte DecodeByte(uint tag)
    {
        if (DecodeHeader)
            ValidateHeader(tag, TdfType.Integer);

        return (byte)DecodeVarsizeInteger();
    }

    public sbyte DecodeSByte(string label = "") => DecodeSByte(Tdf.LabelToTag(label));
    public sbyte DecodeSByte(uint tag)
    {
        if (DecodeHeader)
            ValidateHeader(tag, TdfType.Integer);

        return (sbyte)DecodeVarsizeInteger();
    }

    public bool DecodeBool(string label = "") => DecodeBool(Tdf.LabelToTag(label));
    public bool DecodeBool(uint tag)
    {
        if (DecodeHeader)
            ValidateHeader(tag, TdfType.Integer);

        return DecodeVarsizeInteger() != 0;
    }

    public void DecodeMap(string label, TdfMapBase value) => DecodeMap(Tdf.LabelToTag(label), value);
    public void DecodeMap(uint tag, TdfMapBase value)
    {
        if (DecodeHeader)
            ValidateHeader(tag, TdfType.Map);

        var keyType = (TdfType)Reader.ReadByte();
        var valueType = (TdfType)Reader.ReadByte();

        var size = DecodeVarsizeInteger();

        if (keyType != value.KeyType || valueType != value.ValueType)
        {
            for (var i = 0; i < size; ++i)
            {
                SkipElement(keyType);
                SkipElement(valueType);
            }

            value.InitMap(0);
            return;
        }

        var decodeHeaderSave = DecodeHeader;
        DecodeHeader = false;

        value.InitMap((int)size);
        value.DecodeMembers(this, (int)size);

        DecodeHeader = decodeHeaderSave;
    }

    public void DecodeVector(string label, TdfVectorBase value) => DecodeVector(Tdf.LabelToTag(label), value);
    public void DecodeVector(uint tag, TdfVectorBase value)
    {
        if (DecodeHeader)
            ValidateHeader(tag, TdfType.List);

        var type = (TdfType)Reader.ReadByte();

        var size = DecodeVarsizeInteger();

        if (type != value.Type)
        {
            for (var i = 0; i < size; ++i)
                SkipElement(type);

            value.InitVector(0);
            return;
        }

        var decodeHeaderSave = DecodeHeader;
        DecodeHeader = false;

        value.DecodeMembers(this, (int)size);

        DecodeHeader = decodeHeaderSave;
    }

    public bool StructHasNextElement()
    {
        var start = Reader.BaseStream.Position;

        if (Reader.BaseStream.Position + 1 >= Reader.BaseStream.Length)
            return false;

        // Let's peek for the next byte, if it's a struct terminator 0 byte
        var hasElement = Reader.ReadByte() != 0;

        Reader.BaseStream.Position = start;

        return hasElement;
    }

    public bool PeekNextTag(out string label)
    {
        var start = Reader.BaseStream.Position;

        var res = GetNextElement(out label, out _);

        Reader.BaseStream.Position = start;

        return res;
    }

    public bool PeekNextTagAndType(out string label, out TdfType type)
    {
        var start = Reader.BaseStream.Position;

        var res = GetNextElement(out label, out type);

        Reader.BaseStream.Position = start;

        return res;
    }

    public bool GetNextElement(out string label, out TdfType type)
    {
        label = "";
        type = TdfType.Integer;

        if (Reader.BaseStream.Position + 4 >= Reader.BaseStream.Length)
            return false;

        var b0 = (uint)Reader.ReadByte() << 24;
        var b1 = (uint)Reader.ReadByte() << 16;
        var b2 = (uint)Reader.ReadByte() << 8;

        label = Tdf.TagToLabel(b0 | b1 | b2);
        type = (TdfType)Reader.ReadByte();

        return true;
    }

    public void SkipNextElement()
    {
        if (!GetNextElement(out var label, out var type))
            throw new Exception("Unable to skip next element!");

        Console.WriteLine($"TdfDecoder: Skipping ({label}, {type})...");

        SkipElement(type);
    }

    private void SkipElement(TdfType type)
    {
        switch (type)
        {
            case TdfType.Integer:
            case TdfType.TimeValue:
                _ = DecodeVarsizeInteger();
                break;

            case TdfType.String:
            case TdfType.Binary:
                var size = DecodeVarsizeInteger();

                Reader.BaseStream.Position += size;
                break;

            case TdfType.Struct:
                ConsumeStructTerminator();
                break;

            case TdfType.List:
                var listElemType = (TdfType)Reader.ReadByte();

                var listSize = DecodeVarsizeInteger();

                for (var i = 0; i < listSize; ++i)
                    SkipElement(listElemType);

                break;

            case TdfType.Map:
                var mapKeyType = (TdfType)Reader.ReadByte();
                var mapValueType = (TdfType)Reader.ReadByte();

                var mapSize = DecodeVarsizeInteger();

                for (var i = 0; i < mapSize; ++i)
                {
                    SkipElement(mapKeyType);
                    SkipElement(mapValueType);
                }
                break;

            case TdfType.Union:
                var activeMember = Reader.ReadByte();
                if (activeMember == 0x7F)
                    break;

                SkipNextElement();
                break;

            case TdfType.Variable:
                if (!Reader.ReadBoolean())
                    break;

                _ = DecodeVarsizeInteger();

                ConsumeStructTerminator();
                break;

            case TdfType.BlazeObjectType:
                _ = DecodeVarsizeInteger();
                _ = DecodeVarsizeInteger();
                break;

            case TdfType.BlazeObjectId:
                _ = DecodeVarsizeInteger();
                _ = DecodeVarsizeInteger();
                _ = DecodeVarsizeInteger();
                break;

            case TdfType.Float:
                Reader.BaseStream.Position += 4;
                break;

            default:
                Logger.info($"TdfDecoder: Skipping unknown type {type}, consuming remaining struct");
                ConsumeStructTerminator();
                break;
        }
    }

    private bool ValidateHeader(uint tag, TdfType type)
    {
        var b0 = (uint)Reader.ReadByte() << 24;
        var b1 = (uint)Reader.ReadByte() << 16;
        var b2 = (uint)Reader.ReadByte() << 8;

        var b3 = (TdfType)Reader.ReadByte();
        if (b3 > TdfType.TimeValue)
        {
            Logger.info($"TdfDecoder: Invalid type ({b3}) in header, skipping");
            return false;
        }

        var foundTag = b0 | b1 | b2;
        if (foundTag != tag)
            throw new Exception($"Invalid tag ({foundTag}) found in GetHeader!");

        return true;
    }

    private long DecodeVarsizeInteger()
    {
        var current = (long)Reader.ReadByte();

        var negative = (current & 0x40) == 0x40;
        var result = current & 0x3F;

        var shift = 6;
        
        while ((current & 0x80) == 0x80)
        {
            current = Reader.ReadByte();

            result |= (current & 0x7F) << shift;

            shift += 7;
        }

        if (negative)
        {
            if (result == 0)
                return long.MinValue;

            return -result;
        }

        return result;
    }

    private void ConsumeStructTerminator()
    {
        while (true)
        {
            var current = Reader.ReadByte();
            if (current == 0)
                return;

            Reader.BaseStream.Position -= 1;

            SkipNextElement();
        }
    }
}
