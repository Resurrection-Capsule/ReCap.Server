using System.Collections.Concurrent;
using ReCap.Server.Domain;

namespace ReCap.Server.Services;

// Process-wide registry of live PlayerSessions. Single source of truth for the REST auth-token ->
// account mapping (consolidates the former static AccountRepositoryAdapter.idByAuthToken) and the
// Blaze<->RakNet correlation used to reject unauthenticated RakNet HelloPlayer. Singleton to match
// the static store it replaces, without rewiring the ad-hoc `new AccountService(config)` call sites.
public sealed class SessionRegistry
{
    public static SessionRegistry Instance { get; } = new();

    private readonly ConcurrentDictionary<ulong, PlayerSession> _byAccount = new();
    private readonly ConcurrentDictionary<string, ulong> _accountByToken = new();

    public PlayerSession GetOrCreate(ulong accountId)
        => _byAccount.GetOrAdd(accountId, id => new PlayerSession { AccountId = id });

    public PlayerSession? GetByAccountId(ulong accountId)
        => _byAccount.TryGetValue(accountId, out var session) ? session : null;

    // RakNet HelloPlayer gate: only a Blaze-authenticated account may attach to gameplay.
    public bool IsAuthenticated(ulong accountId)
        => _byAccount.TryGetValue(accountId, out var session) && session.BlazeAuthenticated;

    // Blaze getAuthToken minted/associated a REST token for a logged-in client.
    public void SetToken(ulong accountId, string authToken)
    {
        var session = GetOrCreate(accountId);
        session.AuthToken = authToken;
        session.BlazeAuthenticated = true;
        session.LastSeenUtc = DateTime.UtcNow;
        _accountByToken[authToken] = accountId;
    }

    public ulong? AccountIdForToken(string authToken)
        => _accountByToken.TryGetValue(authToken, out var accountId) ? accountId : null;

    public void RemoveToken(string authToken)
        => _accountByToken.TryRemove(authToken, out _);

    // Blaze TCP session dropped: no longer authenticated, and its REST token is invalidated.
    public void OnBlazeDisconnect(ulong accountId)
    {
        if (accountId == 0 || !_byAccount.TryGetValue(accountId, out var session)) return;
        session.BlazeAuthenticated = false;
        if (session.AuthToken is { } token) _accountByToken.TryRemove(token, out _);
    }
}
