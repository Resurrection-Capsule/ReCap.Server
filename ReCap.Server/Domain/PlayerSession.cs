namespace ReCap.Server.Domain;

// One live player correlated across all three transports (Blaze lobby / REST / RakNet gameplay),
// keyed by account id. Additive index owned by SessionRegistry — NOT a replacement for
// AccountModel (persistence), Blaze Client, RakNetClient, or Game.Player; it just binds them so
// there is one place to answer "who is this token / is this account authenticated / where is it".
public sealed class PlayerSession
{
    public required ulong AccountId { get; init; }
    public string? AuthToken { get; set; }
    public bool BlazeAuthenticated { get; set; }
    public ulong? CurrentGameId { get; set; }
    public uint? CurrentRoomId { get; set; }
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
}
