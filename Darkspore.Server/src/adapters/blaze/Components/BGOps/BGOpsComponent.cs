namespace Darkspore.Server.Adapters.Blaze.Component.BGOps;

using BlazeServer;

public class BGOpsComponent : IComponent
{
    public ushort Id { get; } = 0x823;
    public Server? Server { get; set; }

    public bool HandlePacket(Client client, Packet packet)
    {
        switch (packet.Command)
        {
            case 0x09:
                return HandleSetGameId(client, packet);

            case 0x0A:
                return HandleGetGameId(client, packet);

            case 0x13:
                return HandleGetCurrentVersion(client, packet);

            case 0x20:
                return HandleIsUserMMLockedout(client, packet);

            default:
                Log($"Unknown command: {packet.Command} ({GetCommandName(packet.Command)})");
                return false;
        }
    }

    private static bool HandleSetGameId(Client client, Packet packet)
    {
        client.RespondTo(packet);
        return true;
    }

    private static bool HandleGetGameId(Client client, Packet packet)
    {
        var request = packet.ReadContent<GetPlayerGameIDRequest>();
        if (request is null)
        {
            Log("Unable to read content of getGameId request!");
            return false;
        }

        var response = new GetPlayerGameIDResponse();

        client.RespondTo(packet, response);
        return true;
    }

    private static bool HandleGetCurrentVersion(Client client, Packet packet)
    {
        var request = packet.ReadContent<GetCurrentVersionRequest>();
        if (request is null)
        {
            Log("Unable to read content of getCurrentVersion request!");
            return false;
        }

        var response = new GetCurrentVersionResponse()
        {
            NVSR = "2014-11-04_10-25-56"
        };

        client.RespondTo(packet, response);
        return true;
    }

    private static bool HandleIsUserMMLockedout(Client client, Packet packet)
    {
        var request = packet.ReadContent<IsUserMMLockedoutRequest>();
        if (request is null)
        {
            Log("Unable to read content of isUserMMLockedout request!");
            return false;
        }

        client.RespondTo(packet, new IsUserMMLockedoutResponse());
        return true;
    }

    public string GetCommandName(ushort id)
    {
        return id switch
        {
            0x01 => "testSlave",
            0x02 => "createGame",
            0x03 => "finishGame",
            0x04 => "addPlayer",
            0x05 => "disconnectPlayer",
            0x06 => "playerFinish",
            0x07 => "storeReplay",
            0x08 => "fetchReplay",
            0x09 => "setGameID",
            0x0A => "getGameID",
            0x0B => "setPlaygroupID",
            0x0C => "getPlaygroupID",
            0x0D => "reportGMSError",
            0x10 => "reportGMSUpdate",
            0x11 => "logoutGMS",
            0x12 => "updateVersionAndManifest",
            0x13 => "getCurrentVersion",
            0x14 => "setSystemStatus",
            0x15 => "getSystemStatus",
            0x16 => "setSystemProperty",
            0x17 => "getSystemProperty",
            0x18 => "getSystemStatusList",
            0x19 => "refreshSystemStatus",
            0x1A => "setLauncherFailsafe",
            0x1B => "getKillSwitch",
            0x1C => "setKillSwitch",
            0x1D => "refreshBroadcastMessages",
            0x1E => "clearMMLockout",
            0x1F => "setMMLockout",
            0x20 => "isUserMMLockedout",
            0x21 => "cancelScheduledUpdateVersion",
            0x22 => "getScheduledUpdateVersion",
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

    private static void Log(string message) => Console.WriteLine($"[BG Ops component]: {message}");
}

public class GetPlayerGameIDRequest : Tdf
{
    [TdfField("ACID", 0)]
    public ulong AccountId { get; set; }
}

public class GetPlayerGameIDResponse : Tdf
{
    [TdfField("GMID", "")]
    public string GameId { get; set; } = string.Empty;
}

public class SetPlayerGameIDRequest : Tdf
{
    [TdfField("ACID", 0)]
    public ulong AccountId { get; set; }

    [TdfField("GMID", "")]
    public string GameId { get; set; } = string.Empty;
}

public class BGOpsError : Tdf
{
    [TdfField("MSG", "")]
    public string Message { get; set; } = string.Empty;
}

public class GetCurrentVersionRequest : Tdf
{
    [TdfField("MENV", "")]
    public string MENV { get; set; } = string.Empty;
}

public class GetCurrentVersionResponse : Tdf
{
    [TdfField("BLTD", "")]
    public string BLTD { get; set; } = string.Empty;

    [TdfField("CRNU", "")]
    public string CRNU { get; set; } = string.Empty;

    [TdfField("CRTD", "")]
    public string CRTD { get; set; } = string.Empty;

    [TdfField("CUTO", 0)]
    public uint CUTO { get; set; }

    [TdfField("EMLR", "")]
    public string EMLR { get; set; } = string.Empty;

    [TdfField("INST", "")]
    public string INST { get; set; } = string.Empty;

    [TdfField("LACT", 0)]
    public uint LACT { get; set; }

    [TdfField("MANU", "")]
    public string MANU { get; set; } = string.Empty;

    [TdfField("MENV", "")]
    public string MENV { get; set; } = string.Empty;

    [TdfField("NVSR", "")]
    public string NVSR { get; set; } = string.Empty;

    [TdfField("TMLR", "")]
    public string TMLR { get; set; } = string.Empty;
}

public class IsUserMMLockedoutRequest : Tdf
{
    [TdfField("ACID", 0)]
    public ulong AccountId { get; set; }
}

public class IsUserMMLockedoutResponse : Tdf
{
    [TdfField("LOCK", 0)]
    public ulong LOCK { get; set; }
}
