using ReCap.Server.Adapters.RakNet.Packets;
using ReCap.Server.Domain.Gameplay;

namespace ReCap.Tests.Gameplay;

public class PlayerUpdateBitsTests
{
    [Fact]
    public void SetUpdateBits_Accumulates_With_OR()
    {
        var player = new Player(1, 0);

        player.SetUpdateBits(LabsPlayerUpdatePacket.PlayerBits);
        player.SetUpdateBits(LabsPlayerUpdatePacket.CrystalMask);

        Assert.Equal(
            LabsPlayerUpdatePacket.PlayerBits | LabsPlayerUpdatePacket.CrystalMask,
            player.UpdateBits
        );
    }

    [Fact]
    public void ResetUpdateBits_Clears_All()
    {
        var player = new Player(1, 0);
        player.SetUpdateBits(0x1FFF);
        player.ResetUpdateBits();

        Assert.Equal(0, player.UpdateBits);
    }

    [Fact]
    public void AttachPlayer_Sets_PlayerBits_And_CrystalMask()
    {
        // Matches C++ OnHelloPlayerRequest: SetCatalyst x8 → CrystalBits<<0..7
        // + Player.Setup() → PlayerBits
        var player = new Player(1, 0);
        player.SetUpdateBits(LabsPlayerUpdatePacket.PlayerBits | LabsPlayerUpdatePacket.CrystalMask);

        Assert.True((player.UpdateBits & LabsPlayerUpdatePacket.PlayerBits) != 0);
        Assert.True((player.UpdateBits & LabsPlayerUpdatePacket.CrystalMask) != 0);
    }

    [Fact]
    public void PrepareGameStart_Sets_PlayerBits_And_CharacterMask()
    {
        // C++ SetSquad → SetCharacter(x3) → CharacterBits<<0..2
        var player = new Player(1, 0);
        player.SetUpdateBits(LabsPlayerUpdatePacket.PlayerBits | LabsPlayerUpdatePacket.CharacterMask);

        Assert.True((player.UpdateBits & LabsPlayerUpdatePacket.PlayerBits) != 0);
        Assert.True((player.UpdateBits & LabsPlayerUpdatePacket.CharacterMask) != 0);
    }

    [Fact]
    public void FullUpdateRequest_Sets_All_Bits()
    {
        // C++ LabsPlayerUpdate request handler: CharacterMask | CrystalMask | PlayerBits
        var player = new Player(1, 0);
        player.SetUpdateBits(
            LabsPlayerUpdatePacket.CharacterMask |
            LabsPlayerUpdatePacket.CrystalMask |
            LabsPlayerUpdatePacket.PlayerBits
        );

        Assert.Equal(0x1FFF, player.UpdateBits);
    }

    [Fact]
    public void LabsPlayerData_SetInitialDataBits_Matches_Cpp_Setup()
    {
        // C++ Player::Setup sets: DataSetup(0), Team(5), PlayerOnlineId(6),
        // LockCamera(18), LockAbilityMin(21), LockDeckIndexMin(22),
        // Level(15), DNA(12), Experience(16)
        // C++ Player constructor sets: PlayerIndex(4), ChainProgression(17),
        // CharacterData(3), CrystalData(13), CrystalBonuses(14)
        // SetStatus sets: Status(7), StatusProgress(8)
        // SetSquad sets: CurrentDeckIndex(1), QueuedDeckIndex(2), DeckScore(23)
        var pd = new LabsPlayerData();
        pd.SetInitialDataBits();

        byte[] expectedBits = { 0, 1, 2, 4, 5, 6, 7, 8, 12, 15, 16, 17, 18, 19, 20, 21, 22, 23 };
        foreach (var b in expectedBits)
        {
            Assert.True(pd._dataBits.Contains(b),
                $"Expected DataBit {b} to be set in SetInitialDataBits");
        }
    }

    [Fact]
    public void LabsPlayerData_ResetDataBits_Clears_All()
    {
        var pd = new LabsPlayerData();
        pd.SetInitialDataBits();
        pd.ResetDataBits();

        Assert.Empty(pd._dataBits);
    }

    [Fact]
    public void NeedsStatusUpdate_Sets_Bits_7_And_8()
    {
        var pd = new LabsPlayerData();
        pd._needsStatusUpdate = true;
        pd.SetDataBit(7);
        pd.SetDataBit(8);

        Assert.Contains((byte)7, pd._dataBits);
        Assert.Contains((byte)8, pd._dataBits);
    }
}
