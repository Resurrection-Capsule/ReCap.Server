namespace Darkspore.Server.Adapters.Blaze.Component.LivingLore;

using BlazeServer;

public class LivingLoreComponent : IComponent
{
    public ushort Id { get; } = 0x826;
    public Server? Server { get; set; }

    public bool HandlePacket(Client client, Packet packet)
    {
        switch (packet.Command)
        {
            case 0x6E:
                return HandleGetCurrentVote(client, packet);

            default:
                Log($"Unknown command: {packet.Command}");
                return false;
        }
    }

    private static bool HandleGetCurrentVote(Client client, Packet packet)
    {
        var response = new GetCurrentVoteResponse
        {
            CurrentVote = CurrentVote.None
        };

        client.RespondTo(packet, response);
        return true;
    }

    public string GetCommandName(ushort id)
    {
        return id switch
        {
            0x6E => "getCurrentVote",
            _ => "<unknown>"
        };
    }

    public string GetNotificationName(ushort id)
    {
        return id switch
        {
            _ => "<unknown>"
        };
    }

    private static void Log(string message) => Console.WriteLine($"[Living Lore component]: {message}");
}

public class GetCurrentVoteRequest : Tdf
{
}

public enum CurrentVote
{
    None = 0,
    A = 1,
    B = 2
}

public class GetCurrentVoteResponse : Tdf
{
    [TdfField("CUV", CurrentVote.None)]
    public CurrentVote CurrentVote { get; set; }

    [TdfField("CVT")]
    public RelativeVoteInfo CurrentVoteInfo { get; } = new();
}

public class RelativeVoteInfo : Tdf
{
    [TdfField("SPV", 0)]
    public int SPV { get; set; }

    [TdfField("STE", 0)]
    public int STE { get; set; }

    [TdfField("STS", 0)]
    public int STS { get; set; }

    [TdfField("VIF")]
    public VoteInfo VoteInfo { get; } = new();
}

public enum TrendingType
{
    AAA = 0,
    AA = 1,
    A = 2,
    Static = 3,
    B = 4,
    BB = 5,
    BBB = 6
}

public class VoteInfo : Tdf
{
    [TdfField("INV")]
    public TdfPrimitiveVector<int> INV { get; } = [];

    [TdfField("ISV", 0)]
    public uint ISV { get; set; }

    [TdfField("NAM", "")]
    public string NAM { get; set; } = string.Empty;

    [TdfField("NIV", 0)]
    public uint NIV { get; set; }

    [TdfField("TRD", TrendingType.AAA)]
    public TrendingType Trending { get; set; }

    [TdfField("VAP", 0.0f)]
    public float VAP { get; set; }
}