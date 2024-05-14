namespace Darkspore.Server.Adapters.Blaze.Component.Entry;

using BlazeServer;

public class EntryComponent : IComponent
{
    public ushort Id { get; } = 0x825;
    public Server? Server { get; set; }

    public bool HandlePacket(Client client, Packet packet)
    {
        switch (packet.Command)
        {
            case 0x78:
                return HandleEnterService(client, packet);

            default:
                Log($"Unknown command: {packet.Command} {GetCommandName(packet.Command)}");
                return false;
        }
    }

    private static bool HandleEnterService(Client client, Packet packet)
    {
        var response = new EnterServiceResponse
        {
            Response = EntryResponse.Allowed
        };

        client.RespondTo(packet, response);
        return true;
    }

    public string GetCommandName(ushort id)
    {
        return id switch
        {
            0x64 => "getServiceStatus",
            0x6E => "setServiceStatus",
            0x78 => "enterService",
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

    private static void Log(string message) => Console.WriteLine($"[Entry component]: {message}");
}

public class EnterServiceRequest : Tdf
{
}

public enum EntryResponse
{
    Allowed = 0,
    Closed = 1,
    Queued = 2
}

public class EnterServiceResponse : Tdf
{
    [TdfField("CHEK", 0)]
    public uint Check { get; set; }

    [TdfField("QPOS", 0)]
    public uint QueuePosition { get; set; }

    [TdfField("RESP", EntryResponse.Allowed)]
    public EntryResponse Response { get; set; } = EntryResponse.Allowed;

    [TdfField("WAIT", 0)]
    public uint WaitTime { get; set; }
}