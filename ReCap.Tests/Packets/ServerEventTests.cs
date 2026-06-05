using System.Numerics;
using ReCap.Server.Adapters.RakNet.Packets;

namespace ReCap.Tests.Packets;

// ServerEvent (0x9B) — client-verified contract (Ghidra 2026-06-05): handler
// ClientNet::OnGmsServerEvent @0x0053ec80, registrar AssetData::ServerEvent @0x00f60ea0,
// 26-field reflection → sequence mode (field-ID byte + payload, 0xFF sentinel).
// FX-at-position = fields {6 asset, 10 position}; FX-attached = {6, 7 objectId};
// stop-attached = {7, 1 fxIndex, 2 bRemove}; UI event = {15 clientEventID} (mutually
// exclusive with the FX path at the client dispatch).
public class ServerEventTests
{
    private static byte[] Serialize(IRakNetPacket p)
    {
        using var ms = new MemoryStream();
        p.WriteTo(ms);
        return ms.ToArray();
    }

    [Fact]
    public void EffectAtPosition_Writes_Fields_6_And_10()
    {
        var bytes = Serialize(new ServerEventPacket
        {
            ServerEventDef = 0xAABBCCDD,
            Position = new Vector3(1f, 2f, 3f)
        });

        // 1 (id 6) + 4 + 1 (id 10) + 12 + 1 (0xFF) = 19
        Assert.Equal(19, bytes.Length);
        Assert.Equal(0x06, bytes[0]);
        Assert.Equal(0xAABBCCDDu, BitConverter.ToUInt32(bytes, 1));
        Assert.Equal(0x0A, bytes[5]);
        Assert.Equal(1f, BitConverter.ToSingle(bytes, 6));
        Assert.Equal(3f, BitConverter.ToSingle(bytes, 14));
        Assert.Equal(0xFF, bytes[18]);
    }

    [Fact]
    public void EffectAttachedToObject_Writes_Fields_6_And_7()
    {
        var bytes = Serialize(new ServerEventPacket { ServerEventDef = 0x11223344, ObjectId = 2 });

        Assert.Equal(11, bytes.Length);
        Assert.Equal(0x06, bytes[0]);
        Assert.Equal(0x11223344u, BitConverter.ToUInt32(bytes, 1));
        Assert.Equal(0x07, bytes[5]);
        Assert.Equal(2u, BitConverter.ToUInt32(bytes, 6));
        Assert.Equal(0xFF, bytes[10]);
    }

    [Fact]
    public void StopAttachedEffect_Writes_FxIndex_And_Remove()
    {
        var bytes = Serialize(new ServerEventPacket { ObjectId = 2, ObjectFxIndex = 3, Remove = true, HardStop = true });

        // {1 fxIndex u8, 2 remove, 3 hardStop, 7 objectId} in field-id order + 0xFF
        Assert.Equal(new byte[] { 0x01, 0x03, 0x02, 0x01, 0x03, 0x01, 0x07, 0x02, 0x00, 0x00, 0x00, 0xFF }, bytes);
    }

    [Fact]
    public void ClientEvent_Writes_Field_15_Only()
    {
        var bytes = Serialize(new ServerEventPacket { ClientEventId = 0x8D5AB239 });

        Assert.Equal(6, bytes.Length);
        Assert.Equal(0x0F, bytes[0]);
        Assert.Equal(0x8D5AB239u, BitConverter.ToUInt32(bytes, 1));
        Assert.Equal(0xFF, bytes[5]);
    }
}
