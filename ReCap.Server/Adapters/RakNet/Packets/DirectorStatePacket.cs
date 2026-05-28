using System.IO;
using System.Text;
using ReCap.Server.Util;

namespace ReCap.Server.Adapters.RakNet.Packets;

public class DirectorStatePacket : IRakNetPacket
{
    public PacketType Type => PacketType.DirectorState;

    public void ReadFrom(Stream stream) { }

    public void WriteTo(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        
        // C++ Server:
        // void Server::SendDirectorState(...)
        // outStream.Write(PacketID::DirectorState);
        // director.WriteTo(outStream);
        
        // Director.WriteTo(outStream) implementation in C++:
        // Write<uint32_t>(stream, mEnabled);
        // Write<uint32_t>(stream, mState);
        // Write<uint32_t>(stream, mIntensityState);
        // Write<float>(stream, mIntensity);
        
        writer.Write(1u); // Enabled
        writer.Write(0u); // State
        writer.Write(0u); // IntensityState
        writer.Write(0f); // Intensity
    }
}
