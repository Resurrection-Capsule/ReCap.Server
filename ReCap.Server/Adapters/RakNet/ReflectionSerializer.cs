using System;
using System.IO;

using ReCap.Server.Util;

namespace ReCap.Server.Adapters.RakNet;

public class ReflectionSerializer
{
    private readonly BinaryWriter _writer;
    private readonly int _fieldCount;
    private long _startOffset;
    private ushort _writeBits;

    public ReflectionSerializer(BinaryWriter writer, int fieldCount)
    {
        if (fieldCount is <= 0 or > 255)
            throw new ArgumentOutOfRangeException(nameof(fieldCount));
            
        _writer = writer;
        _fieldCount = fieldCount;
        _startOffset = -1;
        _writeBits = 0;
    }

    public void Begin()
    {
        if (_startOffset != -1) return;

        _startOffset = _writer.BaseStream.Position;
        _writeBits = 0;

        if (_fieldCount <= 8)
            _writer.Write((byte)0);
        else if (_fieldCount <= 16)
            _writer.Write((ushort)0);
    }

    public void End()
    {
        if (_fieldCount > 16)
        {
            _writer.Write((byte)0xFF);
        }
        else
        {
            var endOffset = _writer.BaseStream.Position;
            _writer.BaseStream.Position = _startOffset;

            if (_fieldCount <= 8)
                _writer.Write((byte)_writeBits);
            else
                _writer.Write(_writeBits);

            _writer.BaseStream.Position = endOffset;
        }

        _startOffset = -1;
    }

    public void Write(byte field, Action writeAction)
    {
        if (field >= _fieldCount)
            throw new ArgumentOutOfRangeException(nameof(field));

        if (_fieldCount > 16)
            _writer.Write(field);
        else
            _writeBits |= (ushort)(1 << field);

        writeAction();
    }
}
