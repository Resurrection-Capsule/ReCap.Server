namespace ReCap.Server.Models;

public class AccountModel
{
    public required ulong Id { get; set; }

    public required string Email { get; set; }
    public required string Username { get; set; }
    public required string Password { get; set; }

    public required bool tutorialCompleted;
    public required bool grantAllAccess;
    public required bool? grantOnlineAccess;

    public required int chainProgression;
    public required int creatureRewards;

    public required int currentGameId;
    public required int currentPlaygroupId;

    public required int defaultDeckPveId;
    public required int defaultDeckPvpId;

    public required int level;
    public required int xp;
    public required int dna;
    public required int avatarId;

    public required int newPlayerInventory;
    public required int newPlayerProgress;

    public required int cashoutBonusTime;
    public required int starLevel;

    public required int unlockCatalysts;
    public required int unlockDiagonalCatalysts;
    public required int unlockInventory;
    public required int unlockFuelTanks;
    public required int unlockPveDecks;
    public required int unlockPvpDecks;
    public required int unlockStats;
    public required int unlockInventoryIdentify;
    public required int unlockEditorFlairSlots;

    public required int upsell;

    public required int capLevel;
    public required int capProgression;

    public Dictionary<string,string> settings;
}