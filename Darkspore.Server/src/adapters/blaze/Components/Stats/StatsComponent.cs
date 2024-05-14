namespace Darkspore.Server.Adapters.Blaze.Component.Stats;

using BlazeServer;

public class StatsComponent : IComponent
{
    public ushort Id { get; } = 0x7;
    public Server? Server { get; set; }

    public bool HandlePacket(Client client, Packet packet)
    {
        switch (packet.Command)
        {
            case 0x4:
                return HandleGetStatGroup(client, packet);

            case 0xF:
                return HandleGetKeyScopesMap(client, packet);

            default:
                Log($"Unknown command: {packet.Command}");
                return false;
        }
    }

    private static bool HandleGetStatGroup(Client client, Packet packet)
    {
        client.RespondTo(packet);
        return true;
    }

    private static bool HandleGetKeyScopesMap(Client client, Packet packet)
    {
        client.RespondTo(packet);
        return true;
    }

    public string GetCommandName(ushort id)
    {
        return id switch
        {
            0x4 => "getStatGroup",
            0xF => "getKeyScopesMap",
            _ => "<unknown>"
        };
    }

    public string GetNotificationName(ushort id)
    {
        return id switch
        {
            0x32 => "GetStatsAsyncNotification",
            0x33 => "GetLeaderboardTreeNotification",
            _ => "<unknown>"
        };
    }

    private static void Log(string message) => Console.WriteLine($"[Stats component]: {message}");
}

