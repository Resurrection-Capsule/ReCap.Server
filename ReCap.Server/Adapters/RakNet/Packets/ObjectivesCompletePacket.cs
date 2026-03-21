namespace ReCap.Server.Adapters.RakNet.Packets;

/// <summary>
/// ObjectivesComplete (0xB9) — signals level completion with cashout data.
/// C++: SendObjectivesComplete writes CashOutData (0x2C8 bytes).
/// </summary>
public class ObjectivesCompletePacket : IRakNetPacket
{
    public PacketType Type => PacketType.ObjectivesComplete;

    /// <summary>
    /// Raw CashOutData (0x2C8 = 712 bytes).
    /// </summary>
    public byte[] CashOutData { get; set; } = new byte[0x2C8];

    public void ReadFrom(Stream stream) { }

    public void WriteTo(Stream stream)
    {
        stream.Write(CashOutData, 0, CashOutData.Length);
    }
}
