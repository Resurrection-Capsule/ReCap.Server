namespace ReCap.Server.Adapters.Blaze.Component;

public class RoomView
{
    public uint Id { get; internal set; }
    public string Name { get; internal set; } = string.Empty;

    public RoomViewData ToTdf() => new()
    {
        DisplayName = "hello",
        MaxUserRooms = 1,
        Name = Name,
        NumUserRooms = 0,
        ViewId = Id
    };
}

public class RoomCategory
{
    public uint Id { get; internal set; }
    public string Name { get; internal set; } = string.Empty;
    public string Description { get; internal set; } = string.Empty;
    public string Password { get; internal set; } = string.Empty;
    public RoomView? View { get; set; }

    public RoomCategoryData ToTdf() => new()
    {
        Capacity = 10,
        CategoryId = Id,
        Description = Description,
        MaxEntries = 10,
        Name = Name,
        NextExpansion = 1,
        Password = Password,
        UserCreated = 1,
        ViewId = View?.Id ?? 0
    };
}

public class Room
{
    private readonly Dictionary<ulong, string> _users = new();

    public uint Id { get; internal set; }
    public string Name { get; internal set; } = string.Empty;
    public string Password { get; internal set; } = string.Empty;
    public uint Capacity { get; internal set; } = 1;
    public RoomCategory? Category { get; set; }

    public void AddUser(ulong id, string name) => _users[id] = name;
    public void RemoveUser(ulong id) => _users.Remove(id);

    public RoomData ToTdf()
    {
        var data = new RoomData
        {
            AdminRoomEnumeration = 1,
            Capacity = Capacity,
            CategoryName = Category?.Name ?? "Unknown category",
            CategoryId = Category?.Id ?? 0,
            Enumeration = 1,
            HostName = "Lobby",
            Host = 1,
            Name = Name,
            Population = (uint)_users.Count,
            Password = Password,
            RoomId = Id,
            UserCreated = 1
        };

        foreach (var id in _users.Keys)
            data.MemberList.Add((long)id);

        return data;
    }
}

public class RoomManager
{
    private readonly SortedDictionary<uint, Room> _rooms = new();
    private readonly SortedDictionary<uint, RoomCategory> _categories = new();
    private readonly SortedDictionary<uint, RoomView> _views = new();

    public RoomManager()
    {
        var view = CreateRoomView(1);

        for (uint i = 0; i < 4; i++)
            CreateRoomCategory(i + 1).View = view;

        for (uint i = 0; i < 4; i++)
            CreateRoom(i + 1).Category = GetRoomCategory(i + 1);
    }

    public IEnumerable<RoomCategory> Categories => _categories.Values;

    public Room? GetRoom(uint id) => _rooms.GetValueOrDefault(id);

    public Room CreateRoom(uint id = 0)
    {
        if (id == 0)
            id = _rooms.Count == 0 ? 1 : _rooms.Keys.Max() + 1;

        if (!_rooms.TryGetValue(id, out var room))
        {
            room = new Room { Id = id, Name = $"Lobby #{id}" };
            _rooms[id] = room;
        }

        return room;
    }

    public RoomCategory? GetRoomCategory(uint id) => _categories.GetValueOrDefault(id);

    public RoomCategory CreateRoomCategory(uint id = 0)
    {
        if (id == 0)
            id = _categories.Count == 0 ? 1 : _categories.Keys.Max() + 1;

        if (!_categories.TryGetValue(id, out var category))
        {
            category = new RoomCategory { Id = id, Name = $"Lobby Category #{id}" };
            _categories[id] = category;
        }

        return category;
    }

    public RoomView? GetRoomView(uint id) => _views.GetValueOrDefault(id);

    public RoomView CreateRoomView(uint id = 0)
    {
        if (id == 0)
            id = _views.Count == 0 ? 1 : _views.Keys.Max() + 1;

        if (!_views.TryGetValue(id, out var view))
        {
            view = new RoomView { Id = id, Name = $"Lobby View #{id}" };
            _views[id] = view;
        }

        return view;
    }
}
