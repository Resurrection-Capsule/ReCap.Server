using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using ReCap.Server.Domain.Gameplay;

namespace ReCap.Server.Adapters.RakNet.Packets;

public class ChainVoteMsgsPacket : IRakNetPacket
{
    public PacketType Type => PacketType.ChainVoteMsgs;

    public byte Value { get; set; }
    
    // For Value == 0
    public ChainData? ChainData { get; set; }
    
    // For Value == 1
    public float SecondsUntilDeployment { get; set; }
    
    // For Value == 2
    public bool StayInParty { get; set; }

    public void ReadFrom(Stream stream)
    {
        throw new NotImplementedException("Server only sends ChainVoteMsgs, does not receive them.");
    }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(Value);

        switch (Value)
        {
            case 0:
                if (ChainData != null)
                {
                    WriteChainData(writer, ChainData);
                }
                break;
            case 1:
                writer.Write(SecondsUntilDeployment);
                break;
            case 2:
                writer.Write(StayInParty);
                break;
        }
    }

    private void WriteChainData(BinaryWriter writer, ChainData data)
    {
        byte[] buffer = new byte[0x170]; // enlarged to safely write trailing properties

        Action<int, uint> writeUInt32 = (offset, val) => BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset), val);
        Action<int, float> writeFloat = (offset, val) => BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset), BitConverter.SingleToUInt32Bits(val));

        if (data.CompletedLevel)
        {
            writeUInt32(0x00, data.MinorDifficulty);
            writeUInt32(0x04, data.MajorDifficulty);
            writeFloat(0x08, 15 * 60 * 1000f);
            writeFloat(0x0C, 30 * 60 * 1000f);
        }
        else
        {
            Console.WriteLine($"[ChainVoteMsgsPacket] Writing Level: {data.Level:X8}, LevelIndex: {data.LevelIndex:X8}");
            writeUInt32(0x00, data.Level);
            writeUInt32(0x04, data.LevelIndex);
            writeUInt32(0x08, data.StarLevel);
            writeFloat(0x0C, 30 * 60 * 1000f);
        }

        buffer[0x10] = data.Progression;

        for (int i = 0; i < 6; i++)
        {
            writeUInt32(0x11 + (i * 4), data.EnemyNouns[i]);
        }

        for (int i = 0; i < 2; i++)
        {
            writeUInt32(0x29 + (i * 4), data.LevelNouns[i]);
        }

        if (data.CompletedLevel)
        {
            writeUInt32(0x31, 4);
            writeUInt32(0x35, 3);
        }

        writeUInt32(0x39, 0);
        writeUInt32(0x3D, ChainData.FnvHash("fmv_02_zelems.vp6"));
        writeUInt32(0x41, ChainData.FnvHash("fmv_02_zelems.vp6"));

        writeUInt32(0x45, ChainData.FnvHash("vo_ship_flow_reinfect_zelems"));
        writeUInt32(0x49, 0);

        if (data.CompletedLevel)
        {
            writeUInt32(0x4D, 0);
            writeUInt32(0x51, data.LevelIndex);

            for (int i = 0; i < 4; ++i)
            {
                writeUInt32(0x55 + (i * 4), (uint)(30 + i * 30));
            }

            for (int i = 0; i < 4; ++i)
            {
                writeUInt32(0x65 + (i * 4), (uint)(40 + i * 40));
            }

            for (int i = 0; i < 4; ++i)
            {
                int offset = (i * 0x19) + 0x76;
                writeUInt32(offset, (uint)(10 + i * 10)); // pve kills
                writeUInt32(offset + 4, 5); // unknown

                writeFloat(offset + 8, 15f); // damage dealt
                writeFloat(offset + 12, 10f); // damage taken

                writeFloat(offset + 16, 25f); // healing done
                writeFloat(offset + 20, 5f); // healing received

                buffer[offset + 24] = (byte)i;
            }
        }

        writeUInt32(0xD9, 0);
        writeUInt32(0xDD, ChainData.FnvHash("fmv_03_nocturna.vp6"));
        writeUInt32(0xE1, ChainData.FnvHash("fmv_03_nocturna.vp6"));
        writeUInt32(0xE5, 0);
        writeUInt32(0xE9, data.Level);

        if (data.CompletedLevel)
        {
            for (int i = 0; i < 4; ++i)
            {
                int offset = (i * 0x19) + 0xEE;
                writeUInt32(offset, (uint)(10 + i * 10)); // pve kills
                writeUInt32(offset + 4, 5); // unknown

                writeFloat(offset + 8, 125f); // damage dealt
                writeFloat(offset + 12, 1231f); // damage taken

                writeFloat(offset + 16, 515151f); // healing done
                writeFloat(offset + 20, 23123f); // healing received

                buffer[offset + 24] = (byte)i;
            }
        }

        int tailOffset = 0xEE + (data.CompletedLevel ? (4 * 0x19) : 0);
        writeUInt32(tailOffset, 10);
        writeUInt32(tailOffset + 4, 20);
        writeUInt32(tailOffset + 8, 30);
        writeUInt32(tailOffset + 12, 40);
        writeUInt32(tailOffset + 16, 50);
        writeUInt32(tailOffset + 20, 60);

        writer.Write(buffer, 0, 0x151); // Write exactly the 0x151 bytes to match C++ bounds
    }
}
