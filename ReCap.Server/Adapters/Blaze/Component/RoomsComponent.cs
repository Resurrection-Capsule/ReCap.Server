using ReCap.Server.Util;

namespace ReCap.Server.Adapters.Blaze.Component;

public class RoomsComponent : IComponent
{
    public ushort Id { get; } = 0x15;
    public BlazeServer? Server { get; set; }

    public bool HandlePacket(Client client, Packet packet)
    {
        switch (packet.Command)
        {
            case 0xA:
                return HandleSelectViewUpdates(client, packet);

            case 0xB:
                return HandleSelectCategoryUpdates(client, packet);

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

        client.Notify(new RoomViewNotification
        {
            ViewId = 0,
            Name = "default"
        }, 0x15, 0x0B); // NotifyRoomViewAdded

        client.Notify(new RoomViewNotification
        {
            ViewId = 0,
            Name = "default"
        }, 0x15, 0x0A); // NotifyRoomViewUpdated

        client.RespondTo(packet);
        return true;
    }

    private static bool HandleSelectCategoryUpdates(Client client, Packet packet)
    {
        var request = packet.ReadContent<SelectCategoryUpdatesRequest>();
        if (request is null)
        {
            Log("Unable to read content of selectCategoryUpdates request!");
            return false;
        }

        string[] categoryNames = { "General", "Help", "Trading", "LFG" };

        for (uint i = 0; i < categoryNames.Length; i++)
        {
            client.Notify(new RoomCategoryNotification
            {
                CategoryId = i,
                Name = categoryNames[i],
                ViewId = request.ViewId
            }, 0x15, 0x15); // NotifyRoomCategoryAdded

            client.Notify(new RoomCategoryNotification
            {
                CategoryId = i,
                Name = categoryNames[i],
                ViewId = request.ViewId
            }, 0x15, 0x14); // NotifyRoomCategoryUpdated
        }

        client.RespondTo(packet);
        return true;
    }

    public string GetCommandName(ushort id)
    {
        return id switch
        {
            0xA => "selectViewUpdates",
            0xB => "selectCategoryUpdates",
            _ => "<unknown>"
        };
    }

    public string GetNotificationName(ushort id)
    {
        return id switch
        {
            0x0A => "NotifyRoomViewUpdated",
            0x0B => "NotifyRoomViewAdded",
            0x0C => "NotifyRoomViewRemoved",
            0x14 => "NotifyRoomCategoryUpdated",
            0x15 => "NotifyRoomCategoryAdded",
            0x16 => "NotifyRoomCategoryRemoved",
            0x1E => "NotifyRoomUpdated",
            0x1F => "NotifyRoomAdded",
            0x20 => "NotifyRoomRemoved",
            0x28 => "RoomPopulationUpdated",
            0x32 => "NotifyRoomMemberJoined",
            0x33 => "NotifyRoomMemberLeft",
            0x34 => "NotifyRoomMemberUpdated",
            0x3C => "RoomKick",
            0x46 => "RoomHostTransfer",
            0x50 => "RoomAttributesSet",
            0x5A => "MemberAttributesSet",
            _ => "<unknown>"
        };
    }

    private static void Log(string message) => ReCap.Server.Util.Logging.Log.Blaze.Debug($"[Rooms component]: {message}");
}

public class SelectViewUpdatesRequest : Tdf
{
    [TdfField("UPDT", 0)]
    public uint Updates { get; set; }
}

public class SelectCategoryUpdatesRequest : Tdf
{
    [TdfField("VWID", 0)]
    public uint ViewId { get; set; }
}

public class RoomViewNotification : Tdf
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

public class RoomCategoryNotification : Tdf
{
    [TdfField("CTID", 0)]
    public uint CategoryId { get; set; }

    [TdfField("NAME", "")]
    public string Name { get; set; } = string.Empty;

    [TdfField("VWID", 0)]
    public uint ViewId { get; set; }
}