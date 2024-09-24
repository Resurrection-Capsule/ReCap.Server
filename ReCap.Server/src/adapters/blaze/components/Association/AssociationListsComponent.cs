namespace ReCap.Server.Adapters.Blaze.Component.Association;

using BlazeServer;
using LoggerUtil;

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
        var request = packet.ReadContent<GetListsRequest>();
        if (request is null)
        {
            client.RespondTo(packet, null, error: 0x5E0001); // AUTH_ERR_NO_SUCH_AUTH_DATA
            return true;
        }

        var response = new GetListsResponse();

        foreach(var list in request.Lists)
        {
            var newList = new GetListsResponseList()
            {
                Info = list,
                OFRC = request.OFRC,
                TOCT = 0
            };
            
            var type = list.ID.Type;
            if (type == 4) {
                // Ignore list

                newList.Members.Add(new GetListsResponseListMember{
                    ID = new GetListsResponseListMemberId{
                        ID = 101,
                        Name = "Ignoredude"
                    },
                    TIME = 0
                });
            } else if (type == 5) {
                // Friend list

                newList.Members.Add(new GetListsResponseListMember{
                    ID = new GetListsResponseListMemberId{
                        ID = 100,
                        Name = "Dalkon"
                    },
                    TIME = 0
                });

                newList.Members.Add(new GetListsResponseListMember{
                    ID = new GetListsResponseListMemberId{
                        ID = 102,
                        Name = "test"
                    },
                    TIME = 0
                });
            }

            response.Lists.Add(newList);
        }

        client.RespondTo(packet, response);
        return true;

        // PlaygroupsComponent::NotifyJoinPlaygroup(client);
        // NotifyUpdateListMembership(client);
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

    private static void Log(string message) => Logger.debug($"[Association Lists component]: {message}");
}

public class ListIdentification : Tdf
{
    [TdfField("LNM", "")]
    public string Name { get; set; } = string.Empty;

    [TdfField("TYPE", 0)]
    public uint Type { get; set; }
}

public class GetListsRequest : Tdf
{
    [TdfField("MXRC", 0)]
    public uint MXRC { get; set; }

    [TdfField("OFRC", 0)]
    public uint OFRC { get; set; }

    [TdfField("ALST")]
    public TdfStructVector<BlazeList> Lists { get; } = [];
}

public class BlazeList : Tdf
{
    [TdfField("BOID")]
    public BlazeObjectId BlazeObjectId { get; set; } = new();

    [TdfField("FLGS", 0)]
    public uint Flags { get; set; }

    [TdfField("LID")]
    public ListIdentification ID { get; set; } = new();

    [TdfField("LMS", 0)]
    public ulong LMS { get; set; }

    [TdfField("PRID", 0)]
    public ulong PRID { get; set; }
}

public class GetListsResponse : Tdf
{
    [TdfField("LMAP")]
    public TdfStructVector<GetListsResponseList> Lists { get; } = [];

    [TdfField("GRP", 0xFF)]
    public ulong GRP { get; set; }

    [TdfField("LVL", 0xCC)]
    public ulong LVL { get; set; }

    [TdfField("STAT", 0x02)]
    public ulong STAT { get; set; }

    [TdfField("XTRA", 0xAA)]
    public ulong XTRA { get; set; }
}

public class GetListsResponseList : Tdf
{
    [TdfField("INFO")]
    public BlazeList Info { get; set; } = new();

    [TdfField("MEML")]
    public TdfStructVector<GetListsResponseListMember> Members { get; } = [];

    [TdfField("OFRC", 0)]
    public uint OFRC { get; set; }

    [TdfField("TOCT", 0)]
    public uint TOCT { get; set; }  // unknown so far, some sort of id?
}

public class GetListsResponseListMember : Tdf
{
    [TdfField("LMID")]
    public GetListsResponseListMemberId ID { get; set; } = new();

    [TdfField("TIME", 0)]
    public long TIME { get; set; }
}

public class GetListsResponseListMemberId : Tdf
{
    [TdfField("BLID", 0)]
    public uint ID { get; set; }

    [TdfField("PNAM", "")]
    public string Name { get; set; } = string.Empty;

    [TdfField("XREF", 0)]
    public uint ExternalReference { get; set; }

    [TdfField("XTYP", 0)]
    public uint ExternalReferenceType { get; set; }
}