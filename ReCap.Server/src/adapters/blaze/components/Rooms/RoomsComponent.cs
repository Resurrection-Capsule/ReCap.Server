namespace ReCap.Server.Adapters.Blaze.Component.Rooms;

using BlazeServer;

public class RoomsComponent : IComponent
{
    public ushort Id { get; } = 0x15;
    public Server? Server { get; set; }

    public bool HandlePacket(Client client, Packet packet)
    {
        switch (packet.Command)
        {
            case 0xA:
                return HandleSelectViewUpdates(client, packet);

            default:
                Log($"Unknown command: {packet.Command}");
                return false;
        }
    }

    private static bool HandleSelectViewUpdates(Client client, Packet packet)
    {
        var request = packet.ReadContent<SelectViewUpdatesRequest>();
        if (request is null)
        {
            Log("Unable to read content of selectViewUpdates request!");
            return false;
        }

        if (request.Updates == 1)
        {
            // client.Notify(new RoomViewData
            // {
            //     DisplayName = "",
            //     MaxUserRooms = 255,
            //     Name = "HelloDarkspore's Room",
            //     NumUserRooms = 1,
            //     ViewId = 1
            // }, Id, 0xA);
        }

        return true;
    }

    public string GetCommandName(ushort id)
    {
        return id switch
        {
            0xA => "selectViewUpdates",
            _ => "<unknown>"
        };
    }

    public string GetNotificationName(ushort id)
    {
        return id switch
        {
            0x0A => "RoomViewUpdatedNotification",
            0x0B => "RoomViewAddedNotification",
            0x0C => "RoomViewRemovedNotification",
            0x14 => "RoomCategoryUpdatedNotification",
            0x15 => "RoomCategoryAddedNotification",
            0x16 => "RoomCategoryRemovedNotification",
            0x1E => "RoomUpdatedNotification",
            0x1F => "RoomAddedNotification",
            0x20 => "RoomRemovedNotification",
            0x28 => "RoomPopulationUpdated",
            0x32 => "RoomMemberJoined",
            0x33 => "RoomMemberLeft",
            0x34 => "RoomMemberUpdated",
            0x3C => "RoomKick",
            0x46 => "RoomHostTransfer",
            0x50 => "RoomAttributesSet",
            0x5A => "MemberAttributesSet",
            _ => "<unknown>"
        };
    }

    private static void Log(string message) => Console.WriteLine($"[Rooms component]: {message}");
}

public class SelectViewUpdatesRequest : Tdf
{
    [TdfField("UPDT", 0)]
    public uint Updates { get; set; }
}

public class RoomViewData : Tdf
{
    [TdfField("DISP", "")]
    public string DisplayName { get; set; } = string.Empty;

    [TdfField("GMET")]
    public TdfPrimitiveMap<string, string> GameMetaData { get; } = [];

    [TdfField("META")]
    public TdfPrimitiveMap<string, string> ClientMetaData { get; } = [];

    [TdfField("MXRM", 0)]
    public uint MaxUserRooms { get; set; }

    [TdfField("NAME", "")]
    public string Name { get; set; } = string.Empty;

    [TdfField("USRM", 0)]
    public uint NumUserRooms { get; set; }

    [TdfField("VWID", 0)]
    public ulong ViewId { get; set; }
}