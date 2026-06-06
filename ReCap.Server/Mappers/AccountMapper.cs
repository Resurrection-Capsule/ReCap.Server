using ReCap.Server.Adapters.Rest.Contracts.Game.Models;
using ReCap.Server.Domain;
using ReCap.Server.Models;

namespace ReCap.Server.Mappers;

public class AccountMapper
{
    public AccountModel toModel(Account account) => new()
    {
        Id = account.Id,
        Email = account.Email,
        Username = account.Username,
        Password = account.Password,
        tutorialCompleted = account.tutorialCompleted,
        grantAllAccess = account.grantAllAccess,
        grantOnlineAccess = account.grantOnlineAccess,
        chainProgression = account.chainProgression,
        creatureRewards = account.creatureRewards,
        currentGameId = account.currentGameId,
        currentPlaygroupId = account.currentPlaygroupId,
        defaultDeckPveId = account.defaultDeckPveId,
        defaultDeckPvpId = account.defaultDeckPvpId,
        level = account.level,
        xp = account.xp,
        dna = account.dna,
        avatarId = account.avatarId,
        newPlayerInventory = account.newPlayerInventory,
        newPlayerProgress = account.newPlayerProgress,
        cashoutBonusTime = account.cashoutBonusTime,
        starLevel = account.starLevel,
        unlockCatalysts = account.unlockCatalysts,
        unlockDiagonalCatalysts = account.unlockDiagonalCatalysts,
        unlockInventory = account.unlockInventory,
        unlockFuelTanks = account.unlockFuelTanks,
        unlockPveDecks = account.unlockPveDecks,
        unlockPvpDecks = account.unlockPvpDecks,
        unlockStats = account.unlockStats,
        unlockInventoryIdentify = account.unlockInventoryIdentify,
        unlockEditorFlairSlots = account.unlockEditorFlairSlots,
        upsell = account.upsell,
        capLevel = account.capLevel,
        capProgression = account.capProgression,
        settings = account.settings,
    };

    public AccountContract toContract(AccountModel account) => new()
    {
        Id = (int)account.Id,
        tutorialCompleted = account.tutorialCompleted ? "Y" : "N",
        chainProgression = account.chainProgression,
        creatureRewards = account.creatureRewards,
        currentGameId = account.currentGameId,
        currentPlaygroupId = account.currentPlaygroupId,
        defaultDeckPveId = account.defaultDeckPveId,
        defaultDeckPvpId = account.defaultDeckPvpId,
        dna = account.dna,
        level = account.level,
        avatarId = account.avatarId,
        newPlayerInventory = account.newPlayerInventory,
        newPlayerProgress = account.newPlayerProgress,
        cashoutBonusTime = account.cashoutBonusTime,
        starLevel = account.starLevel,
        unlockCatalysts = account.unlockCatalysts,
        unlockDiagonalCatalysts = account.unlockDiagonalCatalysts,
        unlockFuelTanks = account.unlockFuelTanks,
        unlockInventory = account.unlockInventory,
        unlockPveDecks = account.unlockPveDecks,
        unlockPvpDecks = account.unlockPvpDecks,
        unlockStats = account.unlockStats,
        unlockInventoryIdentify = account.unlockInventoryIdentify,
        unlockEditorFlairSlots = account.unlockEditorFlairSlots,
        upsell = account.upsell,
        xp = account.xp,
        grantAllAccess = account.grantAllAccess ? 1 : 0,
        grantOnlineAccess = account.grantOnlineAccess is bool online ? (online ? 1 : 0) : null,
        capLevel = account.capLevel,
        capProgression = account.capProgression,
    };
}
