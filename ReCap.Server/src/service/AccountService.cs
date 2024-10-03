namespace ReCap.Server.Service.Account;

using ReCap.Server.Adapters.Persistence.SQLite.AccountRepository;
using ReCap.Server.Config.Sqlite;
using ReCap.Server.Domain.Account;
using ReCap.Server.Model.Account;

public class AccountService
{
    private AccountRepositoryAdapter accountRepository;

    public AccountService(SqliteConfig newSqliteConfig) {
        accountRepository = new AccountRepositoryAdapter(newSqliteConfig);
    }

    public AccountModel getAccountById(ulong id) {
        var account = accountRepository.getAccountById(id);
        if (account == null) {
            throw new ForbiddenOperationException("Account ID not found");
        }
        return account;
    }

    public AccountModel getAccountByEmailAndPassword(string email, string password) {
        var account = accountRepository.getAccountByEmail(email);
        if (account == null) {
            throw new ForbiddenOperationException("This e-mail does not belong to any account");
        }
        if (account.Password != password) {
            throw new ForbiddenOperationException("Invalid password");
        }
        return account;
    }

    public void deleteAuthToken(string authToken) {
        accountRepository.deleteAuthToken(authToken);
    }

    public void setAccountAuthToken(ulong accountId, string authToken) {
        accountRepository.setAccountAuthToken(accountId, authToken);
    }

    public AccountModel getAccountByAuthToken(string authToken) {
        var account = accountRepository.getAccountByAuthToken(authToken);
        if (account == null) {
            throw new ForbiddenOperationException("Unindentified account");
        }
        return account;
    }

    public AccountModel createAccount(string email, string name, string password, int avatarId, bool isTestAccount) {
        var oldAccount = accountRepository.getAccountByEmail(email);
        if (oldAccount != null) {
            throw new ForbiddenOperationException("This e-mail already belongs to a different account");
        }
        var account = new Account{
            Email = email,
            Username = name,
            Password = password
        };
        account.avatarId = avatarId;

        // TODO: Implement real default values

        if (isTestAccount)
        {
            account.tutorialCompleted = false;
            account.chainProgression = 24;
            account.creatureRewards = 100;
            account.currentGameId = 1;
            account.currentPlaygroupId = 1;
            account.defaultDeckPveId = 1;
            account.defaultDeckPvpId = 1;
            account.level = 100;
            account.dna = 10000000;
            account.newPlayerInventory = 1;
            account.newPlayerProgress = 9500;
            account.cashoutBonusTime = 1;
            account.starLevel = 10;
            account.unlockCatalysts = 1;
            account.unlockDiagonalCatalysts = 1;
            account.unlockFuelTanks = 1;
            account.unlockPveDecks = 2;
            account.unlockPvpDecks = 1;
            account.unlockStats = 1;
            account.unlockInventoryIdentify = 13;
            account.unlockInventory = 3000; // 570;
            account.unlockEditorFlairSlots = 1;
            account.upsell = 1;
            account.xp = 10000;
            account.grantAllAccess = true;
            account.grantOnlineAccess = true;
        }

        return accountRepository.insertAccount(account);
    }

    public void updateAccount(AccountModel accountModel) {
        accountRepository.updateAccount(accountModel);
    }
}