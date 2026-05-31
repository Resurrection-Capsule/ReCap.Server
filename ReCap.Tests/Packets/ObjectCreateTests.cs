using System.Numerics;
using ReCap.Server.Adapters.RakNet.Packets;
using ReCap.Server.Domain.Gameplay.Objects;

namespace ReCap.Tests.Packets;

// Wire-verified against C++ cpp_loopback hero objId=1 (DIVERGENCE_LEDGER D-009).
// C++ hero ObjectCreate = 59B on wire (1 type byte + 58B body); object-reflection
// field set = {0 Team, 1 PlayerControlled, 3 PlayerIdx, 17 HasCollision}.
// C++ hero ObjectUpdate = 21B on wire (1 type byte + 20B body); field set = {6 Position, 16 Visible}.
// WriteTo emits the body only (the framing layer prepends the 1-byte PacketType).
public class ObjectCreateTests
{
    private static SporelabsObject HeroCreateObj()
    {
        var o = new SporelabsObject { Team = 1, PlayerControlled = true, PlayerIdx = 0, HasCollision = true, Scale = 1f, MarkerScale = 1f };
        foreach (byte b in new byte[] { 0, 1, 3, 17 }) o.SetDataBit(b);
        return o;
    }

    private static byte[] Serialize(IRakNetPacket p)
    {
        using var ms = new MemoryStream();
        p.WriteTo(ms);
        return ms.ToArray();
    }

    [Fact]
    public void HeroObjectCreate_Body_Is_58_Bytes()
    {
        var pkt = new ObjectCreatePacket
        {
            ObjectId = 1,
            CreateData = new GameObjectCreateData { Noun = 0x6367B6CD, Position = new Vector3(44f, 0.47f, 17.5f), Scale = 1f, Team = 1, HasCollision = true, PlayerControlled = true },
            ObjectData = HeroCreateObj()
        };

        var bytes = Serialize(pkt);

        // 4 (objId) + 45 (createData: 2-byte bitmap + 10 fields) + 9 (object reflection {0,1,3,17}) = 58.
        // Plus the framing type byte = 59B on wire, matching C++.
        Assert.Equal(58, bytes.Length);
    }

    [Fact]
    public void HeroObjectCreate_ObjectReflection_FieldSet_Matches_Cpp()
    {
        var pkt = new ObjectCreatePacket
        {
            ObjectId = 1,
            CreateData = new GameObjectCreateData { Noun = 0x6367B6CD, Scale = 1f, Team = 1, HasCollision = true, PlayerControlled = true },
            ObjectData = HeroCreateObj()
        };

        var bytes = Serialize(pkt);

        // Object reflection is the last 9 bytes: field-ID mode, terminated by 0xFF.
        var refl = bytes[^9..];
        // 00 <Team=01> 01 <PC=01> 03 <PlayerIdx=00> 11 <HasCollision=01> FF
        Assert.Equal(new byte[] { 0x00, 0x01, 0x01, 0x01, 0x03, 0x00, 0x11, 0x01, 0xFF }, refl);
    }

    [Fact]
    public void HeroObjectUpdate_Body_Is_20_Bytes_PositionVisible()
    {
        var move = new SporelabsObject { Position = new Vector3(44f, 0.47f, 17.5f), Visible = true };
        foreach (byte b in new byte[] { 6, 16 }) move.SetDataBit(b);

        var bytes = Serialize(new ObjectUpdatePacket { ObjectId = 1, ObjectData = move });

        // 4 (objId) + 1 (field 6) + 12 (Vector3) + 1 (field 16) + 1 (Visible) + 1 (0xFF) = 20.
        // Plus framing type byte = 21B on wire, matching C++ hero ObjectUpdate.
        Assert.Equal(20, bytes.Length);
        // last 3 bytes: field 16 (0x10), Visible=01, terminator 0xFF
        Assert.Equal(new byte[] { 0x10, 0x01, 0xFF }, bytes[^3..]);
    }
}
