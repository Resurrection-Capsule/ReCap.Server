using System.Diagnostics;
using System.Text;

namespace BlazeServer;

public class TdfEncoder
{
    private Stream? Stream { get; }
    private BinaryWriter Writer { get; }

    public bool EncodeHeader { get; set; }

    public TdfEncoder()
        : this(new MemoryStream())
    {
    }

    public TdfEncoder(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanWrite)
            throw new ArgumentException("Stream is not writable!", nameof(stream));

        Stream = stream;
        Writer = new BinaryWriter(Stream, Encoding.UTF8, true);
    }

    public TdfEncoder(BinaryWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        Writer = writer;
    }

    public void EncodeTopLevelStruct(Tdf value)
    {
        var encodeHeaderSave = EncodeHeader;
        EncodeHeader = true;

        value.Encode(this);

        EncodeHeader = encodeHeaderSave;
    }

    public void EncodeTimeValue(string label, TimeValue value) => EncodeTimeValue(Tdf.LabelToTag(label), value);
    public void EncodeTimeValue(uint tag, TimeValue value)
    {
        if (EncodeHeader)
            PutHeader(tag, TdfType.Integer);

        EncodeVarsizeInteger(value.Time);
    }

    public void EncodeBlazeObjectId(string label, BlazeObjectId value) => EncodeBlazeObjectId(Tdf.LabelToTag(label), value);
    public void EncodeBlazeObjectId(uint tag, BlazeObjectId value)
    {
        if (EncodeHeader)
            PutHeader(tag, TdfType.BlazeObjectId);

        EncodeVarsizeInteger(value.Type.Component);
        EncodeVarsizeInteger(value.Type.Type);
        EncodeVarsizeInteger(value.Id);
    }

    public void EncodeBlazeObjectType(string label, BlazeObjectType value) => EncodeBlazeObjectType(Tdf.LabelToTag(label), value);
    public void EncodeBlazeObjectType(uint tag, BlazeObjectType value)
    {
        if (EncodeHeader)
            PutHeader(tag, TdfType.BlazeObjectType);

        EncodeVarsizeInteger(value.Component);
        EncodeVarsizeInteger(value.Type);
    }

    public void EncodeVariable(string label, Tdf? value) => EncodeVariable(Tdf.LabelToTag(label), value);
    public void EncodeVariable(uint tag, Tdf? value)
    {
        if (value is not null && value.Id == 0)
            throw new ArgumentException("Variable contains a non-registered Tdf!", nameof(value));

        if (EncodeHeader)
            PutHeader(tag, TdfType.Variable);

        Writer.Write(value is not null);

        if (value is not null)
        {
            EncodeVarsizeInteger(value.Id);

            value.Encode(this);

            Writer.Write((byte)0);
        }
    }

    public void EncodeUnion(string label, TdfUnion value) => EncodeUnion(Tdf.LabelToTag(label), value);
    public void EncodeUnion(uint tag, TdfUnion value)
    {
        if (EncodeHeader)
            PutHeader(tag, TdfType.Union);

        Writer.Write((byte)(value.ActiveMemberIndex & 0xFF));

        value.Encode(this);
    }

    public void EncodeStruct(string label, Tdf value) => EncodeStruct(Tdf.LabelToTag(label), value);
    public void EncodeStruct(uint tag, Tdf value)
    {
        if (EncodeHeader)
            PutHeader(tag, TdfType.Struct);

        var encodeHeaderSave = EncodeHeader;
        EncodeHeader = true;

        value.Encode(this);

        Writer.Write((byte)0);

        EncodeHeader = encodeHeaderSave;
    }

    public void EncodeBinary(string label, TdfBlob value) => EncodeBinary(Tdf.LabelToTag(label), value);
    public void EncodeBinary(uint tag, TdfBlob value)
    {
        if (EncodeHeader)
            PutHeader(tag, TdfType.Binary);

        EncodeVarsizeInteger(value.Length);

        Writer.Write(value.Data, value.Offset, value.Length);
    }

    public void EncodeString(string label, string value, string? defaultValue = null) => EncodeString(Tdf.LabelToTag(label), value, defaultValue);
    public void EncodeString(uint tag, string value, string? defaultValue = null)
    {
        if (EncodeHeader && value == defaultValue)
            return;

        if (EncodeHeader)
            PutHeader(tag, TdfType.String);

        var bytes = Encoding.UTF8.GetBytes(value);

        EncodeVarsizeInteger(bytes.Length + 1);

        Writer.Write(bytes);
        Writer.Write((byte)0);
    }

    public void EncodeBitfield(string label, uint value) => EncodeBitfield(Tdf.LabelToTag(label), value);
    public void EncodeBitfield(uint tag, uint bits)
    {
        if (EncodeHeader)
            PutHeader(tag, TdfType.Integer);

        EncodeVarsizeInteger(bits);
    }

    public void EncodeFloat(string label, float value, float? defaultValue = null) => EncodeFloat(Tdf.LabelToTag(label), value, defaultValue);
    public void EncodeFloat(uint tag, float value, float? defaultValue = null)
    {
        if (EncodeHeader && defaultValue.HasValue && value == defaultValue)
            return;

        if (EncodeHeader)
            PutHeader(tag, TdfType.Float);

        var intVal = BitConverter.SingleToUInt32Bits(value);

        Writer.Write((byte)(((intVal & 0xFF000000) >> 24) & 0xFF));
        Writer.Write((byte)(((intVal & 0x00FF0000) >> 16) & 0xFF));
        Writer.Write((byte)(((intVal & 0x0000FF00) >> 8) & 0xFF));
        Writer.Write((byte)(intVal & 0x000000FF));
    }

    public void EncodeEnumRaw(string label, object value, object? defaultValue) => EncodeEnumRaw(Tdf.LabelToTag(label), value, defaultValue);
    public void EncodeEnumRaw(uint tag, object value, object? defaultValue)
    {
        if (EncodeHeader && value == defaultValue)
            return;

        switch (Enum.GetUnderlyingType(value.GetType()).Name)
        {
            case "SByte":
                EncodeSByte(tag, (sbyte)value);
                return;

            case "Byte":
                EncodeByte(tag, (byte)value);
                return;

            case "Int16":
                EncodeInt16(tag, (short)value);
                return;

            case "UInt16":
                EncodeUInt16(tag, (ushort)value);
                return;

            case "Int32":
                EncodeInt32(tag, (int)value);
                return;

            case "UInt32":
                EncodeUInt32(tag, (uint)value);
                return;

            case "Int64":
                EncodeInt64(tag, (long)value);
                return;

            case "UInt64":
                EncodeUInt64(tag, (ulong)value);
                return;

            default:
                throw new Exception($"Unexpected underlying enum type: {Enum.GetUnderlyingType(value.GetType()).FullName}");
        }
    }

    public void EncodeEnum<E>(string label, E value, object? defaultValue = null) where E : Enum => EncodeEnum(Tdf.LabelToTag(label), value, defaultValue);
    public void EncodeEnum<E>(uint tag, E value, object? defaultValue = null) where E : Enum
    {
        var objVal = (object)value;

        if (EncodeHeader && objVal == defaultValue)
            return;

        switch (Enum.GetUnderlyingType(typeof(E)).Name)
        {
            case "SByte":
                EncodeSByte(tag, (sbyte)objVal);
                return;

            case "Byte":
                EncodeByte(tag, (byte)objVal);
                return;

            case "Int16":
                EncodeInt16(tag, (short)objVal);
                return;

            case "UInt16":
                EncodeUInt16(tag, (ushort)objVal);
                return;

            case "Int32":
                EncodeInt32(tag, (int)objVal);
                return;

            case "UInt32":
                EncodeUInt32(tag, (uint)objVal);
                return;

            case "Int64":
                EncodeInt64(tag, (long)objVal);
                return;

            case "UInt64":
                EncodeUInt64(tag, (ulong)objVal);
                return;

            default:
                throw new Exception($"Unexpected underlying enum type: {Enum.GetUnderlyingType(typeof(E)).FullName}");
        }
    }

    public void EncodeUInt64(string label, ulong value, ulong? defaultValue = null) => EncodeUInt64(Tdf.LabelToTag(label), value, defaultValue);
    public void EncodeUInt64(uint tag, ulong value, ulong? defaultValue = null)
    {
        if (EncodeHeader && defaultValue.HasValue && value == defaultValue)
            return;

        if (EncodeHeader)
            PutHeader(tag, TdfType.Integer);

        EncodeVarsizeInteger((long)value);
    }

    public void EncodeInt64(string label, long value, long? defaultValue = null) => EncodeInt64(Tdf.LabelToTag(label), value, defaultValue);
    public void EncodeInt64(uint tag, long value, long? defaultValue = null)
    {
        if (EncodeHeader && defaultValue.HasValue && value == defaultValue)
            return;

        if (EncodeHeader)
            PutHeader(tag, TdfType.Integer);

        EncodeVarsizeInteger(value);
    }

    public void EncodeUInt32(string label, uint value, uint? defaultValue = null) => EncodeUInt32(Tdf.LabelToTag(label), value, defaultValue);
    public void EncodeUInt32(uint tag, uint value, uint? defaultValue = null)
    {
        if (EncodeHeader && defaultValue.HasValue && value == defaultValue)
            return;

        if (EncodeHeader)
            PutHeader(tag, TdfType.Integer);

        EncodeVarsizeInteger(value);
    }

    public void EncodeInt32(string label, int value, int? defaultValue = null) => EncodeInt32(Tdf.LabelToTag(label), value, defaultValue);
    public void EncodeInt32(uint tag, int value, int? defaultValue = null)
    {
        if (EncodeHeader && defaultValue.HasValue && value == defaultValue)
            return;

        if (EncodeHeader)
            PutHeader(tag, TdfType.Integer);

        EncodeVarsizeInteger(value);
    }

    public void EncodeUInt16(string label, ushort value, ushort? defaultValue = null) => EncodeUInt16(Tdf.LabelToTag(label), value, defaultValue);
    public void EncodeUInt16(uint tag, ushort value, ushort? defaultValue = null)
    {
        if (EncodeHeader && defaultValue.HasValue && value == defaultValue)
            return;

        if (EncodeHeader)
            PutHeader(tag, TdfType.Integer);

        EncodeVarsizeInteger(value);
    }

    public void EncodeInt16(string label, short value, short? defaultValue = null) => EncodeInt16(Tdf.LabelToTag(label), value, defaultValue);
    public void EncodeInt16(uint tag, short value, short? defaultValue = null)
    {
        if (EncodeHeader && defaultValue.HasValue && value == defaultValue)
            return;

        if (EncodeHeader)
            PutHeader(tag, TdfType.Integer);

        EncodeVarsizeInteger(value);
    }

    public void EncodeByte(string label, byte value, byte? defaultValue = null) => EncodeByte(Tdf.LabelToTag(label), value, defaultValue);
    public void EncodeByte(uint tag, byte value, byte? defaultValue = null)
    {
        if (EncodeHeader && defaultValue.HasValue && value == defaultValue)
            return;

        if (EncodeHeader)
            PutHeader(tag, TdfType.Integer);

        EncodeVarsizeInteger(value);
    }

    public void EncodeSByte(string label, sbyte value, sbyte? defaultValue = null) => EncodeSByte(Tdf.LabelToTag(label), value, defaultValue);
    public void EncodeSByte(uint tag, sbyte value, sbyte? defaultValue = null)
    {
        if (EncodeHeader && defaultValue.HasValue && value == defaultValue)
            return;

        if (EncodeHeader)
            PutHeader(tag, TdfType.Integer);

        EncodeVarsizeInteger(value);
    }

    public void EncodeBool(string label, bool value, bool? defaultValue = null) => EncodeBool(Tdf.LabelToTag(label), value, defaultValue);
    public void EncodeBool(uint tag, bool value, bool? defaultValue = null)
    {
        if (EncodeHeader && defaultValue.HasValue && value == defaultValue)
            return;

        if (EncodeHeader)
            PutHeader(tag, TdfType.Integer);

        EncodeVarsizeInteger(value ? 1 : 0);
    }

    public void EncodeMap(string label, TdfMapBase value) => EncodeMap(Tdf.LabelToTag(label), value);
    public void EncodeMap(uint tag, TdfMapBase value)
    {
        if (EncodeHeader && value.Size == 0)
            return;

        if (EncodeHeader)
            PutHeader(tag, TdfType.Map);

        Writer.Write((byte)((byte)value.KeyType & 0xFF));
        Writer.Write((byte)((byte)value.ValueType & 0xFF));

        EncodeVarsizeInteger(value.Size);

        var encodeHeaderSave = EncodeHeader;
        EncodeHeader = false;

        value.EncodeMembers(this);

        EncodeHeader = encodeHeaderSave;
    }

    public void EncodeVector(string label, TdfVectorBase value) => EncodeVector(Tdf.LabelToTag(label), value);
    public void EncodeVector(uint tag, TdfVectorBase value)
    {
        if (EncodeHeader && value.Size == 0)
            return;

        if (EncodeHeader)
            PutHeader(tag, TdfType.List);

        Writer.Write((byte)((byte)value.Type & 0xFF));

        EncodeVarsizeInteger(value.Size);

        var encodeHeaderSave = EncodeHeader;
        EncodeHeader = false;

        value.EncodeMembers(this);

        EncodeHeader = encodeHeaderSave;
    }

    private void PutHeader(uint tag, TdfType type)
    {
        Writer.Write((byte)(((tag & 0xFF000000) >> 24) & 0xFF));
        Writer.Write((byte)(((tag & 0x00FF0000) >> 16) & 0xFF));
        Writer.Write((byte)(((tag & 0x0000FF00) >>  8) & 0xFF));
        Writer.Write((byte)((byte)type & 0x1F));
    }

    private void EncodeVarsizeInteger(long value)
    {
        if (value == 0)
        {
            Writer.Write((byte)0);
            return;
        }

        byte negativeBit = 0;
        if (value < 0)
        {
            negativeBit = 0x40;

            value = -value;
        }

        Writer.Write((byte)((value & 0x3F) | negativeBit | (byte)(value >= 0x40 ? 0x80 : 0x00)));

        value >>= 6;

        while (value > 0)
        {
            Writer.Write((byte)((value & 0x7F) | (byte)(value >= 0x80 ? 0x80 : 0x00)));

            value >>= 7;
        }
    }
}
