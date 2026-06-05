using System.Numerics;
using ReCap.Server.Adapters.RakNet.Packets;
using ReCap.Server.Domain.Gameplay.Objects;

namespace ReCap.Tests.Packets;

// World-object ObjectCreate shapes, wire-verified vs cpp_loopback (zelems_1, decoded
// 2026-06-05, tools/scratch/decode_creates.py):
//   enemy = 81B on wire (80B body): createData all-10 (pos real, ROT ZEROS, assetId 0,
//     scale 1, team 0, hasColl 1, playerCtrl 0) + object-reflection {6 pos, 7 orient} only;
//   marker object (SecurityTeleporter) = 93B on wire (92B body): same createData
//     (scale 0, hasColl 0) + object-reflection {6,7,8,17,22}.
// Capture rot-Y is -0.0 (sign bit only); we emit +0.0 — same value, not asserted byte-wise.
public class WorldObjectCreateTests
{
    private static byte[] Serialize(IRakNetPacket p)
    {
        using var ms = new MemoryStream();
        p.WriteTo(ms);
        return ms.ToArray();
    }

    [Fact]
    public void EnemyCreate_Body_Is_80_Bytes_Reflection_PosOrient_Only()
    {
        // Capture representative: objId=0x11A noun=0xB28B12F2 (ZelemSpecialHaster.Noun)
        var pos = new Vector3(-156.535f, -62.834f, 0.047f);
        var obj = new SporelabsObject { Position = pos, Orientation = Quaternion.Identity };
        obj.SetDataBit(6);
        obj.SetDataBit(7);

        var bytes = Serialize(new ObjectCreatePacket
        {
            ObjectId = 0x11A,
            CreateData = new GameObjectCreateData
            {
                Noun = 0xB28B12F2,
                Position = pos,
                Scale = 1f,
                Team = 0,
                HasCollision = true,
                PlayerControlled = false
            },
            ObjectData = obj
        });

        Assert.Equal(80, bytes.Length);
        Assert.Equal(0x03FF, BitConverter.ToUInt16(bytes, 4));            // createData bitmap, all 10
        Assert.Equal(0xB28B12F2u, BitConverter.ToUInt32(bytes, 6));       // noun
        Assert.Equal(0f, BitConverter.ToSingle(bytes, 22));               // rotX zero
        Assert.Equal(0f, BitConverter.ToSingle(bytes, 26));               // rotY zero
        Assert.Equal(0f, BitConverter.ToSingle(bytes, 30));               // rotZ zero
        Assert.Equal(0ul, BitConverter.ToUInt64(bytes, 34));              // assetId 0
        Assert.Equal(1f, BitConverter.ToSingle(bytes, 42));               // scale 1
        Assert.Equal(new byte[] { 0x00, 0x01, 0x00 }, bytes[46..49]);     // team, hasColl, playerCtrl
        Assert.Equal(0x06, bytes[49]);                                    // reflection: field 6
        Assert.Equal(0x07, bytes[62]);                                    // field 7 (after 12B pos)
        Assert.Equal(1f, BitConverter.ToSingle(bytes, 75));               // quat W = 1
        Assert.Equal(0xFF, bytes[79]);                                    // terminator
    }

    [Fact]
    public void MarkerObjectCreate_Body_Is_92_Bytes_With_MarkerId()
    {
        // Capture representative: objId=0x3E noun=0x2983C017 (SecurityTeleporter.Noun),
        // scale 0, no collision, markerId=0xAFFCC9D1.
        var pos = new Vector3(-132.087f, -126.314f, 5.816f);
        var obj = new SporelabsObject
        {
            Position = pos,
            Orientation = Quaternion.Identity,
            Scale = 0f,
            HasCollision = false,
            SourceMarkerKeyMarkerId = 0xAFFCC9D1
        };
        foreach (byte bit in new byte[] { 6, 7, 8, 17, 22 }) obj.SetDataBit(bit);

        var bytes = Serialize(new ObjectCreatePacket
        {
            ObjectId = 0x3E,
            CreateData = new GameObjectCreateData
            {
                Noun = 0x2983C017,
                Position = pos,
                Scale = 0f,
                Team = 0,
                HasCollision = false,
                PlayerControlled = false
            },
            ObjectData = obj
        });

        Assert.Equal(92, bytes.Length);
        Assert.Equal(0x2983C017u, BitConverter.ToUInt32(bytes, 6));       // noun
        Assert.Equal(0f, BitConverter.ToSingle(bytes, 42));               // createData scale 0
        Assert.Equal(0x06, bytes[49]);                                    // field 6
        Assert.Equal(0x07, bytes[62]);                                    // field 7
        Assert.Equal(0x08, bytes[79]);                                    // field 8 scale
        Assert.Equal(0f, BitConverter.ToSingle(bytes, 80));
        Assert.Equal(0x11, bytes[84]);                                    // field 17 hasCollision
        Assert.Equal(0x00, bytes[85]);
        Assert.Equal(0x16, bytes[86]);                                    // field 22 markerId
        Assert.Equal(0xAFFCC9D1u, BitConverter.ToUInt32(bytes, 87));
        Assert.Equal(0xFF, bytes[91]);                                    // terminator
    }
}
