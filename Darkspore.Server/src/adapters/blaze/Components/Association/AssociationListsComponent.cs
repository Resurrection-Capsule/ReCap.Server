namespace Darkspore.Server.Adapters.Blaze.Component.Association;

using BlazeServer;

public class AssociationListsComponent : IComponent
{
    public ushort Id { get; } = 0x19;
    public Server? Server { get; set; }

    public bool HandlePacket(Client client, Packet packet)
    {
        switch (packet.Command)
        {
            case 0x6:
                return HandleGetLists(client, packet);

            default:
                Log($"Unknown command: {packet.Command}");
                return false;
        }
    }

    private static bool HandleGetLists(Client client, Packet packet)
    {
        /*var response = new TdfBuilder()
            .AddStructList("LMAP", b =>
            {

            })
            .Build();*/

        client.RespondTo(packet, null);
        return true;
    }

    public string GetCommandName(ushort id)
    {
        return id switch
        {
            0x1 => "addUsersToList",
            0x2 => "removeUsersFromList",
            0x3 => "clearLists",
            0x4 => "setUsersToList",
            0x5 => "getListForUser",
            0x6 => "getLists",
            0x7 => "subscribeToLists",
            0x8 => "unsubscribeFromLists",
            0x9 => "getConfigListsInfo",
            _ => "<unknown>"
        };
    }

    public string GetNotificationName(ushort id)
    {
        return id switch
        {
            1 => "NotifyUpdateListMembership",
            _ => "<unknown>"
        };
    }

    private static void Log(string message) => Console.WriteLine($"[Association Lists component]: {message}");
}

public class ListIdentification : Tdf
{
    [TdfField("LNM", "")]
    public string Name { get; set; } = string.Empty;

    [TdfField("TYPE", 0)]
    public uint Type { get; set; }
}
