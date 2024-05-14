using System.Buffers;
using System.Text;

namespace BlazeServer;

using Darkspore.Server.Adapters.Blaze.Extensions;

public enum PacketType : byte
{
    Message      = 0,
    Reply        = 1,
    Notification = 2,
    ErrorReply   = 3
}

[Flags]
public enum PacketOptions : byte
{
    None       = 0x0,
    Jumbo      = 0x1,
    HasContext = 0x2,
    Immediate  = 0x4,
    Unk8       = 0x8
}

public class Packet
{
    public const int SmallestValidHeaderSize = 12;

    public int Id { get; private set; }
    public ushort Component { get; private set; }
    public ushort Command { get; private set; }
    public uint Error { get; private set; }
    public byte UserIndex { get; private set; }
    public PacketType Type { get; private set; }
    public PacketOptions Options { get; private set; }
    public int ContentLength { get; private set; }
    public byte[]? Content { get; private set; }
    public int Context { get; private set; }
    public int Unk8 { get; private set; }

    public int HeaderLength => SmallestValidHeaderSize +
        (Options.HasFlag(PacketOptions.Jumbo) ? 2 : 0) +
        (Options.HasFlag(PacketOptions.HasContext) ? (Options.HasFlag(PacketOptions.Unk8) ? 8 : 4) : 0);

    public Packet(int id, ushort component, ushort command, uint error, byte userIndex, PacketType type, PacketOptions options, int context = 0, int unk8 = 0)
    {
        Id = id;
        Component = component;
        Command = command;
        Error = error;
        UserIndex = userIndex;
        Type = type;
        Options = options;
        Context = context;
        Unk8 = unk8;
    }

    ~Packet()
    {
        if (Content is not null)
            ArrayPool<byte>.Shared.Return(Content);
    }

    public void SetContent(Tdf value)
    {
        using var ms = new MemoryStream();

        var encoder = new TdfEncoder(ms);

        encoder.EncodeTopLevelStruct(value);

        var contentLength = (int)ms.Length;
        var content = ArrayPool<byte>.Shared.Rent(contentLength);

        ms.Position = 0;
        ms.Read(content, 0, contentLength);
        
        SetContent(contentLength, content);
    }

    private void SetContent(int length, byte[] data)
    {
        ContentLength = length;
        Content = data;
    }

    public T ReadContent<T>() where T : Tdf, new()
    {
        var instance = (Activator.CreateInstance(typeof(T)) as T)!;

        if (ContentLength == 0 || Content is null)
            return instance;

        using var ms = new MemoryStream(Content, 0, ContentLength, false);

        var decoder = new TdfDecoder(ms);

        decoder.DecodeTopLevelStruct(instance);

        return instance;
    }

    public void WriteTo(Stream target)
    {
        ArgumentNullException.ThrowIfNull(target);

        using var writer = new BinaryWriter(target, Encoding.UTF8, true);

        if (ContentLength >= 0x10000)
            Options |= PacketOptions.Jumbo;

        var lengthFirstPart = (ushort)(ContentLength & 0xFFFF);
        var lengthSecondPart = (ushort)((ContentLength >> 16) & 0xFFFF);

        writer.WriteBigEndian(lengthFirstPart);
        writer.WriteBigEndian(Component);
        writer.WriteBigEndian(Command);
        writer.WriteBigEndian((ushort)(Error >> 16));
        writer.Write((byte)((((byte)Type) << 4) | (UserIndex & 0xF)));
        writer.Write((byte)((((byte)Options) << 4) | ((Id >> 16) & 0xF)));
        writer.WriteBigEndian((ushort)(Id & 0xFFFF));

        if (Options.HasFlag(PacketOptions.Jumbo))
            writer.WriteBigEndian(lengthSecondPart);

        if (Options.HasFlag(PacketOptions.HasContext))
        {
            writer.WriteBigEndian(Context);

            if (Options.HasFlag(PacketOptions.Unk8))
                writer.WriteBigEndian(Unk8);
        }

        if (Content is not null)
            writer.Write(Content, 0, ContentLength);
    }

    public static Packet? Parse(MemoryStream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);

        var contentLength = (int)reader.ReadBigEndianUInt16();
        var component = reader.ReadBigEndianUInt16();
        var command = reader.ReadBigEndianUInt16();

        var error = (uint)reader.ReadBigEndianUInt16() << 16;
        if (error != 0 && (error & 0x40000000) == 0)
            error |= component;

        var byte8 = reader.ReadByte();
        var msgType = (PacketType)(byte8 >> 4);
        var userIndex = (byte)(byte8 & 0xF);
        
        var byte9 = reader.ReadByte();
        var options = (PacketOptions)(byte9 >> 4);
        var msgId = reader.ReadBigEndianUInt16() | ((byte9 & 0xF) << 16);

        var extraBytes = 0;
        var context = 0;
        var unk8 = 0;

        if (options.HasFlag(PacketOptions.Jumbo))
        {
            extraBytes += 2;

            if (stream.Length < SmallestValidHeaderSize + extraBytes)
                return null;

            contentLength |= reader.ReadBigEndianUInt16() << 16;
        }

        if (options.HasFlag(PacketOptions.HasContext))
        {
            extraBytes += 4 + (options.HasFlag(PacketOptions.Unk8) ? 4 : 0);

            if (stream.Length < SmallestValidHeaderSize + extraBytes)
                return null;

            context = reader.ReadBigEndianInt32();

            if (options.HasFlag(PacketOptions.Unk8))
                unk8 = reader.ReadBigEndianInt32();
        }

        if (stream.Length < SmallestValidHeaderSize + extraBytes + contentLength)
            return null;

        var packet = new Packet(msgId, component, command, error, userIndex, msgType, options, context: context, unk8: unk8);

        if (contentLength > 0)
        {
            var content = ArrayPool<byte>.Shared.Rent(contentLength);

            if (reader.Read(content, 0, contentLength) != contentLength)
                return null;

            packet.SetContent(contentLength, content);
        }

        return packet;
    }

    public string ToString(string componentAndCommand) => $"Packet(#{Id}, {componentAndCommand}, Error: 0x{Error:X}, {Type}, {Options}, Length: {ContentLength}, Context: {Context}, Unk8: {Unk8})";
    public override string ToString() => ToString($"(0x{Component:X} -> 0x{Command:X})");
}
