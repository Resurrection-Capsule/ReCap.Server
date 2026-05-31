using ReCap.Server.Util;

namespace ReCap.Server.Adapters.Blaze.Component;

public class RoomsComponent : IComponent
{
    public ushort Id { get; } = 0x15;
    public BlazeServer? Server { get; set; }

    private readonly RoomManager _roomManager = new();

    public bool HandlePacket(Client client, Packet packet)
    {
        switch (packet.Command)
        {
            case 0xA:
                return HandleSelectViewUpdates(client, packet);

            case 0xB:
                return HandleSelectCategoryUpdates(client, packet);

            case 0x14:
                return HandleJoinRoom(client, packet);

            default:
                Log($"Unknown command: {packet.Command}");
                return false;
        }
    }

    private bool HandleSelectViewUpdates(Client client, Packet packet)
    {
        var request = packet.ReadContent<SelectViewUpdatesRequest>();
        if (request is null)
        {
            Log("Unable to read content of selectViewUpdates request!");
            return false;
        }

        var update = request.Updates != 0;
        var viewId = update ? 1u : 0u;

        client.RespondTo(packet, new SelectViewUpdatesResponse
        {
            StreamId = 1,
            UpdateReason = (uint)RoomViewUpdate.UserRoomCreated,
            UserSessionId = client.UserId,
            ViewId = viewId
        });

        if (update)
        {
            NotifyRoomView(client, viewId, 0x0B); // NotifyRoomViewAdded
            NotifyRoomView(client, viewId, 0x0A); // NotifyRoomViewUpdated
        }

        return true;
    }

    private bool HandleSelectCategoryUpdates(Client client, Packet packet)
    {
        var request = packet.ReadContent<SelectCategoryUpdatesRequest>();
        if (request is null)
        {
            Log("Unable to read content of selectCategoryUpdates request!");
            return false;
        }

        if (request.ViewId != 0)
        {
            foreach (var category in _roomManager.Categories)
            {
                NotifyRoomCategory(client, category.Id, 0x15); // NotifyRoomCategoryAdded
                NotifyRoomCategory(client, category.Id, 0x14); // NotifyRoomCategoryUpdated
            }
        }

        client.RespondTo(packet, new SelectCategoryUpdatesResponse { ViewId = request.ViewId });
        return true;
    }

    private bool HandleJoinRoom(Client client, Packet packet)
    {
        var request = packet.ReadContent<JoinRoomRequest>();
        if (request is null)
        {
            Log("Unable to read content of joinRoom request!");
            return false;
        }

        var newRoom = false;

        Room? room;
        if (request.RoomId == 0)
        {
            room = _roomManager.CreateRoom();
            newRoom = true;
        }
        else
        {
            room = _roomManager.GetRoom(request.RoomId);
        }

        if (room is null)
        {
            client.RespondTo(packet, error: 0xB0015); // ROOMS_ERR_NOT_FOUND
            return true;
        }

        var category = request.CategoryId == 0
            ? _roomManager.CreateRoomCategory()
            : _roomManager.GetRoomCategory(request.CategoryId);

        if (category is null)
        {
            client.RespondTo(packet, error: 0x190015); // ROOMS_ERR_CREATE_UNKNOWN_CATEGORY
            return true;
        }

        category.View ??= _roomManager.CreateRoomView();

        room.Category = category;
        room.AddUser(client.UserId, client.Username);

        if (newRoom)
        {
            NotifyRoom(client, room.Id, 0x1F); // NotifyRoomAdded
            NotifyRoom(client, room.Id, 0x1E); // NotifyRoomUpdated
        }

        client.RespondTo(packet, BuildJoinRoom(room, client.UserId));

        NotifyRoomMemberJoined(client, room.Id, client.UserId);
        return true;
    }

    private static JoinRoomResponse BuildJoinRoom(Room room, ulong userId)
    {
        var category = room.Category!;
        var view = category.View!;

        return new JoinRoomResponse
        {
            Category = category.ToTdf(),
            Room = room.ToTdf(),
            View = view.ToTdf(),
            Member = new RoomMemberData { MemberId = userId, RoomId = room.Id }
        };
    }

    private void NotifyRoomView(Client client, uint viewId, ushort command)
    {
        var view = _roomManager.GetRoomView(viewId);
        if (view is not null)
            client.Notify(view.ToTdf(), Id, command);
    }

    private void NotifyRoomCategory(Client client, uint categoryId, ushort command)
    {
        var category = _roomManager.GetRoomCategory(categoryId);
        if (category is not null)
            client.Notify(category.ToTdf(), Id, command);
    }

    private void NotifyRoom(Client client, uint roomId, ushort command)
    {
        var room = _roomManager.GetRoom(roomId);
        if (room is not null)
            client.Notify(room.ToTdf(), Id, command);
    }

    private void NotifyRoomMemberJoined(Client client, uint roomId, ulong memberId)
    {
        client.Notify(new NotifyRoomMemberJoined
        {
            MemberId = memberId,
            RoomId = roomId
        }, Id, 0x32);
    }

    public string GetCommandName(ushort id)
    {
        return id switch
        {
            0xA => "selectViewUpdates",
            0xB => "selectCategoryUpdates",
            0x14 => "joinRoom",
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

public enum RoomViewUpdate : uint
{
    ConfigReloaded = 0,
    UserRoomCreated = 1,
    UserRoomDestroyed = 2
}

public class SelectViewUpdatesRequest : Tdf
{
    [TdfField("UPDT", 0)]
    public uint Updates { get; set; }
}

public class SelectViewUpdatesResponse : Tdf
{
    [TdfField("SEID", 0)]
    public uint StreamId { get; set; }

    [TdfField("UPRE", 0)]
    public uint UpdateReason { get; set; }

    [TdfField("USID", 0)]
    public ulong UserSessionId { get; set; }

    [TdfField("VWID", 0)]
    public ulong ViewId { get; set; }
}

public class SelectCategoryUpdatesRequest : Tdf
{
    [TdfField("VWID", 0)]
    public uint ViewId { get; set; }
}

public class SelectCategoryUpdatesResponse : Tdf
{
    [TdfField("VWID", 0)]
    public uint ViewId { get; set; }
}

public class JoinRoomRequest : Tdf
{
    [TdfField("CTID", 0)]
    public uint CategoryId { get; set; }

    [TdfField("INID", 0)]
    public ulong InviterId { get; set; }

    [TdfField("INVT", false)]
    public bool Invited { get; set; }

    [TdfField("PASS", "")]
    public string Password { get; set; } = string.Empty;

    [TdfField("PVAL", "")]
    public string PasswordValue { get; set; } = string.Empty;

    [TdfField("RMID", 0)]
    public uint RoomId { get; set; }
}

public class JoinRoomResponse : Tdf
{
    [TdfField("CDAT")]
    public RoomCategoryData Category { get; set; } = new();

    [TdfField("CRIT")]
    public string Criteria { get; set; } = string.Empty;

    [TdfField("MDAT")]
    public RoomMemberData Member { get; set; } = new();

    [TdfField("RDAT")]
    public RoomData Room { get; set; } = new();

    [TdfField("VDAT")]
    public RoomViewData View { get; set; } = new();

    [TdfField("VERS", 0)]
    public uint Version { get; set; } = 1;
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

public class RoomCategoryData : Tdf
{
    [TdfField("CAPA", 0)]
    public uint Capacity { get; set; }

    [TdfField("CMET")]
    public TdfPrimitiveMap<string, string> CategoryMetaData { get; } = [];

    [TdfField("CRIT")]
    public TdfPrimitiveMap<string, string> Criteria { get; } = [];

    [TdfField("CTID", 0)]
    public uint CategoryId { get; set; }

    [TdfField("DESC", "")]
    public string Description { get; set; } = string.Empty;

    [TdfField("DISP", "")]
    public string DisplayName { get; set; } = string.Empty;

    [TdfField("DISR", "")]
    public string DisplayRegion { get; set; } = string.Empty;

    [TdfField("EMAX", 0)]
    public uint MaxEntries { get; set; }

    [TdfField("EPCT", 0)]
    public uint ExpansionPercent { get; set; }

    [TdfField("FLAG", 0)]
    public uint Flags { get; set; }

    [TdfField("GMET")]
    public TdfPrimitiveMap<string, string> GameMetaData { get; } = [];

    [TdfField("LOCL", "")]
    public string Locale { get; set; } = string.Empty;

    [TdfField("NAME", "")]
    public string Name { get; set; } = string.Empty;

    [TdfField("NEXP", 0)]
    public uint NextExpansion { get; set; }

    [TdfField("PASS", "")]
    public string Password { get; set; } = string.Empty;

    [TdfField("UCRT", 0)]
    public uint UserCreated { get; set; }

    [TdfField("VWID", 0)]
    public uint ViewId { get; set; }
}

public class RoomData : Tdf
{
    [TdfField("AREM", 0)]
    public uint AdminRoomEnumeration { get; set; }

    [TdfField("ATTR")]
    public TdfPrimitiveMap<string, string> Attributes { get; } = [];

    [TdfField("BLST")]
    public TdfPrimitiveVector<long> MemberList { get; } = [];

    [TdfField("CAP", 0)]
    public uint Capacity { get; set; }

    [TdfField("CNAM", "")]
    public string CategoryName { get; set; } = string.Empty;

    [TdfField("CRET", 0)]
    public uint CreatorId { get; set; }

    [TdfField("CRIT")]
    public TdfPrimitiveMap<string, string> Criteria { get; } = [];

    [TdfField("CRTM", 0)]
    public uint CreationTime { get; set; }

    [TdfField("CTID", 0)]
    public uint CategoryId { get; set; }

    [TdfField("ENUM", 0)]
    public uint Enumeration { get; set; }

    [TdfField("HNAM", "")]
    public string HostName { get; set; } = string.Empty;

    [TdfField("HOST", 0)]
    public uint Host { get; set; }

    [TdfField("NAME", "")]
    public string Name { get; set; } = string.Empty;

    [TdfField("POPU", 0)]
    public uint Population { get; set; }

    [TdfField("PSWD", "")]
    public string Password { get; set; } = string.Empty;

    [TdfField("PVAL", "")]
    public string PasswordValue { get; set; } = string.Empty;

    [TdfField("RMID", 0)]
    public uint RoomId { get; set; }

    [TdfField("UCRT", 0)]
    public uint UserCreated { get; set; }
}

public class RoomMemberData : Tdf
{
    [TdfField("BZID", 0)]
    public ulong MemberId { get; set; }

    [TdfField("RMID", 0)]
    public uint RoomId { get; set; }
}

public class NotifyRoomMemberJoined : Tdf
{
    [TdfField("BZID", 0)]
    public ulong MemberId { get; set; }

    [TdfField("RMID", 0)]
    public uint RoomId { get; set; }
}
