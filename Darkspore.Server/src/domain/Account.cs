using HttpServer;

namespace HttpServer;

public class Account
{
    public ulong Id { get; set; }

    public string Email { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }

    public bool tutorialCompleted = false;
    public bool grantAllAccess = false;
    public bool grantOnlineAccess = false;

    public int chainProgression = 0;
    public int creatureRewards = 0;

    public int currentGameId = 1;
    public int currentPlaygroupId = 1;

    public int defaultDeckPveId = 1;
    public int defaultDeckPvpId = 1;

    public int level = 1;
    public int xp = 0;
    public int dna = 0;
    public int avatarId = 0;

    public int newPlayerInventory = 0;
    public int newPlayerProgress = 0;

    public int cashoutBonusTime = 0;
    public int starLevel = 0;

    public int unlockCatalysts = 0;
    public int unlockDiagonalCatalysts = 0;
    public int unlockInventory = 0;
    public int unlockFuelTanks = 0;
    public int unlockPveDecks = 0;
    public int unlockPvpDecks = 0;
    public int unlockStats = 0;
    public int unlockInventoryIdentify = 0;
    public int unlockEditorFlairSlots = 0;

    public int upsell = 0;

    public int capLevel = 0;
    public int capProgression = 0;
}