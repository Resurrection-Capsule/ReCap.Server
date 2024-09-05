using System.Buffers.Binary;

namespace ReCap.Server.Adapters.Blaze.Component.UserSessions;

using BlazeServer;
using ReCap.Server.Adapters.Blaze.Component.Util;

public class UserSessionsComponent : IComponent
{
    public ushort Id { get; } = 0x7802;
    public Server? Server { get; set; }

    public bool HandlePacket(Client client, Packet packet)
    {
        switch (packet.Command)
        {
            case 0x05:
                return HandleUpdateExtendedDataAttribute(client, packet);

            case 0x14:
                return HandleUpdateNetworkInfo(client, packet);

            case 0x1A:
                return HandleSetUserInfoAttribute(client, packet);

            default:
                Log($"Unknown command: {packet.Command}");
                return false;
        }
    }

    private static bool HandleUpdateExtendedDataAttribute(Client client, Packet packet)
    {
        var request = packet.ReadContent<UpdateUserSessionAttributeRequest>();
        if (request is null)
        {
            Log("Unable to read content of updateExtendedDataAttribute request!");
            return false;
        }

        client.Notify(new UserStatus()
        {
            BlazeId = client.UserId,
            StatusFlags = 3
        }, 0x7802, 5);

        client.RespondTo(packet);
        return true;
    }

    private static bool HandleUpdateNetworkInfo(Client client, Packet packet)
    {
        var request = packet.ReadContent<NetworkInfo>();
        if (request is null)
        {
            Log("Unable to read content of updateNetworkInfo request!");
            return false;
        }

        client.RespondTo(packet);

        var addrBytes = client.EndPoint.Address.GetAddressBytes();

        var addr = addrBytes.Length == 4 ? BinaryPrimitives.ReadUInt32BigEndian(addrBytes) : 0;

        var update = new UserSessionExtendedDataUpdate
        {
            UserId = client.UserId
        };

        update.ExtendedData.Address.ActiveMember = NetworkAddressMember.IpPairAddress;
        update.ExtendedData.Address.IpPairAddress.ExternalAddress.Ip = addr;
        update.ExtendedData.Address.IpPairAddress.ExternalAddress.Port = (ushort)client.EndPoint.Port;
        update.ExtendedData.UserInfoAttribute = 0x4000000000000000; // disable popup about multiple locations

        client.Notify(update, 0x7802, 1);
        return true;
    }

    private static bool HandleSetUserInfoAttribute(Client client, Packet packet)
    {
        var request = packet.ReadContent<SetUserInfoAttributeRequest>();
        if (request is null)
        {
            Log("Unable to read content of setUserInfoAttribute request!");
            return false;
        }

        Log($"SetUserInfoAttribute: {request} (0x{request.AttributeBits:X}, 0x{request.MaskBits:X})");

        //client.RespondTo(packet);
        return true;
    }

    public string GetCommandName(ushort id)
    {
        return id switch
        {
            0x03 => "fetchExtendedData",
            0x05 => "updateExtendedDataAttribute",
            0x08 => "updateHardwareFlags",
            0x0C => "lookupUser",
            0x0D => "lookupUsers",
            0x0E => "lookupUsersByPrefix",
            0x14 => "updateNetworkInfo",
            0x17 => "lookupUserGeoIPData",
            0x18 => "overrideUserGeoIPData",
            0x19 => "updateUserSessionClientData",
            0x1A => "setUserInfoAttribute",
            0x1B => "resetUserGeoIPData",
            0x20 => "lookupUserSessionId",
            0x21 => "fetchLastLocaleUsedAndAuthError",
            0x22 => "fetchUserFirstLastAuthTime",
            0x23 => "resumeSession",
            0x25 => "setUserGeoOptIn",
            0x29 => "enableUserAuditLogging",
            0x2A => "disableUserAuditLogging",
            _ => "<unknown>"
        };
    }

    public string GetNotificationName(ushort id)
    {
        return id switch
        {
            1 => "UserSessionExtendedDataUpdate",
            2 => "UserAdded",
            3 => "UserRemoved",
            4 => "UserSessionDisconnected",
            5 => "UserUpdated",
            8 => "UserAuthenticated",
            9 => "UserUnauthenticated",
            _ => "<unknown>"
        };
    }

    private static void Log(string message) => Console.WriteLine($"[UserSessions component]: {message}");
}

public class NotifyUserAdded : Tdf
{
    [TdfField("DATA")]
    public UserSessionExtendedData ExtendedData { get; } = new();

    [TdfField("USER")]
    public UserIdentification UserInfo { get; } = new();
}

public class UserSessionExtendedDataUpdate : Tdf
{
    [TdfField("DATA")]
    public UserSessionExtendedData ExtendedData { get; } = new();

    [TdfField("SUBS", false)]
    public bool Subscribed { get; set; }

    [TdfField("USID", 0)]
    public ulong UserId { get; set; }
}

public class UpdateUserSessionAttributeRequest : Tdf
{
    [TdfField("ATID", 0)]
    public ulong ATID { get; set; }

    [TdfField("OPER", false)]
    public bool OPER { get; set; }

    [TdfField("UREA", 0)]
    public uint UREA { get; set; }

    [TdfField("VALU", 0)]
    public ulong VALU { get; set; }
}

public class UserIdentification : Tdf
{
    [TdfField("AID", 0)]
    public ulong AccountId { get; set; }

    [TdfField("ALOC", 0)]
    public uint AccountLocale { get; set; }

    [TdfField("EXBB")]
    public TdfBlob ExternalBlob { get; } = new();

    [TdfField("EXID", 0)]
    public ulong ExternalId { get; set; }

    [TdfField("ID", 0)]
    public ulong BlazeId { get; set; }

    [TdfField("NAME", "")]
    public string Name { get; set; } = string.Empty;

    [TdfField("ORIG", 0)]
    public ulong OriginId { get; set; }
}

public class UserSessionExtendedData : Tdf
{
    [TdfField("ADDR")]
    public NetworkAddress Address { get; } = new();

    [TdfField("BPS", "")]
    public string BestPingSiteAlias { get; set; } = string.Empty;

    [TdfField("CMAP")]
    public TdfPrimitiveMap<uint, int> ClientAttributes { get; } = [];

    [TdfField("CTY", "")]
    public string Country { get; set; } = string.Empty;

    [TdfField("CVAR")]
    public Tdf? ClientData { get; set; }

    [TdfField("DMAP")]
    public TdfPrimitiveMap<uint, long> DataMap { get; } = [];

    [TdfField("HWFG", 0)]
    public uint HardwareFlags { get; set; }

    [TdfField("PSLM")]
    public TdfPrimitiveVector<int> LatencyList { get; } = [];

    [TdfField("QDAT")]
    public NetworkQosData QosData { get; } = new();

    [TdfField("UATT", 0)]
    public ulong UserInfoAttribute { get; set; }

    [TdfField("ULST")]
    public TdfPrimitiveVector<BlazeObjectId> BlazeObjectIdList { get; } = [];
}

public class UserSessionLoginInfo : Tdf
{
    [TdfField("ALOC", 0)]
    public uint AccountLocale { get; set; }

    [TdfField("BUID", 0)]
    public ulong BlazeUserId { get; set; }

    [TdfField("DSNM", "")]
    public string DisplayName { get; set; } = string.Empty;

    [TdfField("FRST", false)]
    public bool FirstLogin { get; set; }

    [TdfField("KEY", "")]
    public string SessionKey { get; set; } = string.Empty;

    [TdfField("LAST", 0)]
    public uint LastLoginTime { get; set; }

    [TdfField("LLOG", 0)]
    public long LastLoginDateTime { get; set; }

    [TdfField("MAIL", "")]
    public string Email { get; set; } = string.Empty;

    [TdfField("PID", 0)]
    public ulong PersonaId { get; set; }

    [TdfField("PLAT", ConnectionProfileType.Invalid)]
    public ConnectionProfileType Platform { get; set; } = ConnectionProfileType.Invalid;

    [TdfField("UID")]
    public ulong UserId { get; set; }

    [TdfField("XREF")]
    public ulong ExternalId { get; set; }
}

public enum LogoutType
{
    Account = 0,
    Persona = 1
}

public class UserSessionLogoutInfo : Tdf
{
    [TdfField("BID", 0)]
    public ulong BlazeId { get; set; }

    [TdfField("LOTP", LogoutType.Account)]
    public LogoutType LogoutType { get; set; } = LogoutType.Account;

    public UserSessionLogoutInfo(ulong blazeId, LogoutType logoutType)
    {
        BlazeId = blazeId;
        LogoutType = logoutType;
    }
}

public class UserStatus : Tdf
{
    [TdfField("FLGS", 0)]
    public uint StatusFlags { get; set; }

    [TdfField("ID", 0)]
    public ulong BlazeId { get; set; }
}

public class SetUserInfoAttributeRequest : Tdf
{
    [TdfField("ATTV", 0)]
    public ulong AttributeBits { get; set; }

    [TdfField("MASK", 0)]
    public ulong MaskBits { get; set; }

    [TdfField("ULST")]
    public TdfPrimitiveVector<BlazeObjectId> BlazeObjectIdList { get; } = [];
}