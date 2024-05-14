namespace Darkspore.Server.Adapters.Blaze.Component.BGAccounts;

using BlazeServer;

public class BGAccountsComponent : IComponent
{
    public ushort Id { get; } = 0x81F;
    public Server? Server { get; set; }

    public bool HandlePacket(Client client, Packet packet)
    {
        switch (packet.Command)
        {
            case 0x07:
                return HandleCreateAccountAsync(client, packet);

            case 0x73:
                return HandleGetBasicData(client, packet);

            case 0x76:
                return HandleGetCompleteData(client, packet);

            case 0x8A:
                return HandleGetUserSettings(client, packet);

            default:
                Log($"Unknown command: {packet.Command}");
                return false;
        }
    }

    private static bool HandleCreateAccountAsync(Client client, Packet packet)
    {
        var request = packet.ReadContent<BattlegroundsAccountsCreateAccountRequest>();
        if (request is null)
        {
            Log("Unable to read content of login request!");
            return false;
        }

        client.RespondTo(packet);
        return true;
    }

    private static bool HandleGetBasicData(Client client, Packet packet)
    {
        var request = packet.ReadContent<BGAMetagameGetBasicAccountDataRequest>();
        if (request is null)
        {
            Log("Unable to read content of login request!");
            return false;
        }

        var response = new BGAMetagameGetBasicAccountDataResponse();
        response.Data.Add(new BGABasicData
        {
            BlazeId = 1,
            ALEV = 15,
            ALXP = 100,
            Avatar = 10,
            GOOD = 1.0f,
            VOTE = 1.0f
        });

        client.RespondTo(packet, response);
        return true;
    }

    private static bool HandleGetCompleteData(Client client, Packet packet)
    {
        var request = packet.ReadContent<BGAMetagameGetCompleteAccountDataRequest>();
        if (request is null)
        {
            Log("Unable to read content of get complete data request!");
            return false;
        }

        var acc = new BGACompleteAccount();
        acc.BasicData.BlazeId = 1;
        acc.BasicData.ALEV = 15;
        acc.BasicData.ALXP = 100;
        acc.BasicData.Avatar = 10;
        acc.BasicData.GOOD = 1.0f;
        acc.BasicData.VOTE = 1.0f;

        var response = new BGAMetagameGetCompleteAccountDataResponse();
        response.Data.Add(acc);

        client.RespondTo(packet, response);
        return true;
    }

    private static bool HandleGetUserSettings(Client client, Packet packet)
    {
        var response = new GetUserSettingsResponse();

        client.RespondTo(packet, response);
        return true;
    }

    public string GetCommandName(ushort id)
    {
        return id switch
        {
            0x01 => "testSlave",
            0x03 => "readAccount",
            0x04 => "updateAccount",
            0x05 => "deleteAccount",
            0x07 => "createAccountAsync",
            0x08 => "submitBug",
            0x0A => "lookupFacebookFriends",
            0x0B => "getOriginFriendsList",
            0x73 => "getBasicData",
            0x76 => "getCompleteData",
            0x78 => "awardXP",
            0x7E => "deleteUserAccount",
            0x7F => "setAvatar",
            0x80 => "setLevel",
            0x81 => "updatePerkPage",
            0x82 => "validatePerkPage",
            0x83 => "resetPerkPage",
            0x84 => "setVotingRating",
            0x85 => "consumeFirstWin",
            0x86 => "setGoodBehavior",
            0x87 => "getPerkPageData",
            0x88 => "updateLivingLoreVote",
            0x89 => "getLivingLoreVote",
            0x8A => "getUserSettings",
            0x8B => "saveUserSettings",
            0x8C => "setUserFlags",
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

    private static void Log(string message) => Console.WriteLine($"[BG Accounts component]: {message}");
}

public class BattlegroundsAccountsCreateAccountRequest : Tdf
{
    [TdfField("ID", "")]
    public string AccountId { get; set; } = string.Empty;
}

public class BGAMetagameGetBasicAccountDataRequest : Tdf
{
    [TdfField("BZID")]
    public TdfPrimitiveVector<ulong> BlazeIds { get; } = [];
}

public class BGAMetagameGetBasicAccountDataResponse : Tdf
{
    [TdfField("BASD")]
    public TdfStructVector<BGABasicData> Data { get; } = [];
}

public class BGABasicData : Tdf
{
    [TdfField("ABAD", 0)]
    public uint ABAD { get; set; }

    [TdfField("ALEV", 0)]
    public uint ALEV { get; set; }

    [TdfField("ALXP", 0)]
    public uint ALXP { get; set; }

    [TdfField("ATIL", 0)]
    public uint ATIL { get; set; }

    [TdfField("AVTR", 0)]
    public uint Avatar { get; set; }

    [TdfField("BZID", 0)]
    public ulong BlazeId { get; set; }

    [TdfField("DEVF", 0)]
    public uint DeveloperFlags { get; set; }

    [TdfField("FIRS", 0)]
    public int FIRS { get; set; }

    [TdfField("FLAG", 0)]
    public ulong Flags { get; set; }

    [TdfField("GOOD", 0.0f)]
    public float GOOD { get; set; }

    [TdfField("VOTE", 0.0f)]
    public float VOTE { get; set; }
}

public class BGAMetagameGetCompleteAccountDataRequest : Tdf
{
    [TdfField("USRS")]
    public TdfPrimitiveVector<ulong> BlazeIds { get; } = [];
}

public class BGAMetagameGetCompleteAccountDataResponse : Tdf
{
    [TdfField("LIST")]
    public TdfStructVector<BGACompleteAccount> Data { get; } = [];
}

public class BGACompleteAccount : Tdf
{
    [TdfField("BASD")]
    public BGABasicData BasicData { get; } = new();

    [TdfField("ITEM")]
    public TdfStructVector<BGAInventoryItem> Items { get; } = [];

    [TdfField("PERK")]
    public TdfStructVector<BGACompletePerkPage> PerkPages { get; } = [];
}

public class BGAInventoryItem : Tdf
{
    [TdfField("CONT", 0)]
    public uint CONT { get; set; }

    [TdfField("ITID", 0)]
    public uint ITID { get; set; }

    [TdfField("TYPE", 0)]
    public uint TYPE { get; set; }
}

public class BGACompletePerkPage : Tdf
{
    [TdfField("PERK")]
    public TdfStructVector<BGAPerkPageShape> Perks { get; } = [];

    [TdfField("SUMM")]
    public BGASummaryPerkPage SummaryPerkPage { get; } = new();
}

public class BGASummaryPerkPage : Tdf
{
    [TdfField("NAME", "")]
    public string Name { get; set; } = string.Empty;

    [TdfField("PID", 0)]
    public uint PageId { get; set; }

    [TdfField("STAT", 0)]
    public byte Status { get; set; }

    [TdfField("VERS", 0)]
    public uint Version { get; set; }
}

public class BGAPerkPageShape : Tdf
{
    [TdfField("GEMS")]
    public TdfStructVector<BGAPerkPageGem> Gems { get; } = [];

    [TdfField("GIDX", 0)]
    public byte GIDX { get; set; }

    [TdfField("PID", 0)]
    public uint PageId { get; set; }

    [TdfField("ROT", 0)]
    public byte ROT { get; set; }
}

public class BGAPerkPageGem : Tdf
{
    [TdfField("GID", 0)]
    public uint GemId { get; set; }

    [TdfField("SIDX", 0)]
    public byte SIDX { get; set; }
}

public class GetUserSettingsResponse : Tdf
{
    [TdfField("USET")]
    public TdfPrimitiveMap<string, string> UserSettings { get; } = [];
}