namespace Darkspore.Server.Adapters.Blaze.Component.BGWeb;

using BlazeServer;

public class BGWebComponent : IComponent
{
    public ushort Id { get; } = 0x827;
    public Server? Server { get; set; }

    public bool HandlePacket(Client client, Packet packet)
    {
        switch (packet.Command)
        {
            case 0x6E:
                return HandleGetCurrentVersion(client, packet);

            default:
                Log($"Unknown command: {packet.Command}");
                return false;
        }
    }

    private static bool HandleGetCurrentVersion(Client client, Packet packet)
    {
        var response = new GetCurrentVersionResponse
        {
            DRC = "VersionString"
        };

        client.RespondTo(packet, response);
        return true;
    }

    public string GetCommandName(ushort id)
    {
        return id switch
        {
            0x6E => "getCurrentVersion",
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

    private static void Log(string message) => Console.WriteLine($"[BG Web component]: {message}");
}

public class GetCurrentVersionResponse : Tdf
{
    [TdfField("DRC", "")]
    public string DRC { get; set; } = string.Empty;
}