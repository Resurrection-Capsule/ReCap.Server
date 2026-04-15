using System.ComponentModel.DataAnnotations.Schema;

namespace ReCap.Server.Models;

public class AccountModel
{
    public required ulong Id { get; set; }

    public required string Email { get; set; }
    public required string Username { get; set; }
    public required string Password { get; set; }

    public required bool tutorialCompleted { get; set; }
    public required bool grantAllAccess { get; set; }
    public bool? grantOnlineAccess { get; set; }

    public required int chainProgression { get; set; }
    public required int creatureRewards { get; set; }

    public required int currentGameId { get; set; }
    public required int currentPlaygroupId { get; set; }

    public required int defaultDeckPveId { get; set; }
    public required int defaultDeckPvpId { get; set; }

    public required int level { get; set; }
    public required int xp { get; set; }
    public required int dna { get; set; }
    public required int avatarId { get; set; }

    public required int newPlayerInventory { get; set; }
    public required int newPlayerProgress { get; set; }

    public required int cashoutBonusTime { get; set; }
    public required int starLevel { get; set; }

    public required int unlockCatalysts { get; set; }
    public required int unlockDiagonalCatalysts { get; set; }
    public required int unlockInventory { get; set; }
    public required int unlockFuelTanks { get; set; }
    public required int unlockPveDecks { get; set; }
    public required int unlockPvpDecks { get; set; }
    public required int unlockStats { get; set; }
    public required int unlockInventoryIdentify { get; set; }
    public required int unlockEditorFlairSlots { get; set; }

    public required int upsell { get; set; }

    public required int capLevel { get; set; }
    public required int capProgression { get; set; }

    [NotMapped]
    public Dictionary<string,string>? settings { get; set; }
}