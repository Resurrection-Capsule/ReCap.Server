using System.Buffers.Binary;
using System.Text;
using ReCap.Server.Config;
using ReCap.Server.Models;
using ReCap.Server.Services;
using ReCap.Server.Util;

namespace ReCap.Server.Adapters.Blaze.Component;

public class AuthenticationComponent : IComponent
{
    private AccountService accountService;

    public AuthenticationComponent(SqliteConfig newSqliteConfig) {
        accountService = new AccountService(newSqliteConfig);
    }

    public static uint CurrentUnixTime => (uint)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

    public ushort Id { get; } = 1;
    public BlazeServer? Server { get; set; }

    public bool HandlePacket(Client client, Packet packet)
    {
        switch (packet.Command)
        {
            case 0x24:
                return GetAuthToken(client, packet);

            case 0x28:
                return HandleLogin(client, packet);

            case 0x29:
                return HandleAcceptTOS(client, packet);

            case 0x2A:
                return HandleGetTOSInfo(client, packet);

            case 0x2E:
                return HandleGetTermsAndConditions(client, packet);

            case 0x2F:
                return HandleGetPrivacyPolicyContent(client, packet);

            case 0x32:
                return HandleSilentLogin(client, packet);

            case 0x3C:
                return HandleExpressLogin(client, packet);

            case 0x46:
                return HandleLogout(client, packet);

            case 0x6E:
                return HandleLoginPersona(client, packet);

            case 0xF1:
                return HandleAcceptTOS(client, packet);

            case 0xF2:
                return HandleGetEmailOptInSettings(client, packet);

            case 0xF6:
                return HandleGetLegalDocContent(client, packet);

            default:
                Log($"Unknown command: {packet.Command}");
                return false;
        }
    }

    private bool GetAuthToken(Client client, Packet packet)
    {
        var authToken = client.AuthToken ??= client.UserId.ToString();

        accountService.setAccountAuthToken(client.UserId, authToken);

        client.RespondTo(packet, new GetAuthTokenResponse { AuthToken = authToken });

        client.Notify(new UserStatus
        {
            BlazeId = client.UserId,
            StatusFlags = (uint)SessionState.Authenticated
        }, 0x7802, 5);

        return true;
    }

    private bool HandleLogin(Client client, Packet packet)
    {
        // NOTES:
        // error 0x320001 (AUTH_ERR_NEED_PCCDKEY) displays: Servers are down or you have not registered your beta key.
        // error 0x2A0001 (AUTH_ERR_PENDING) uses the error response to update the userid, but nothing else? strange

        var request = packet.ReadContent<LoginRequest>();
        if (request is null)
        {
            client.RespondTo(packet, null, error: 0x5E0001);
            return true;
        }

        ReCap.Server.Util.Logging.Log.Blaze.Info($"[Auth] Login attempt: Email='{request.Email}', Pass='{request.Password}'");

        AccountModel account;
        try
        {
            account = accountService.getAccountByEmailAndPassword(request.Email, request.Password);
        }
        catch (ForbiddenOperationException ex)
        {
            ReCap.Server.Util.Logging.Log.Blaze.Error($"[Auth] Login failed: {ex.Message}");
            client.RespondTo(packet, null, error: 0xB0001);
            return true;
        }

        client.UserId = account.Id;
        client.Username = account.Username;
        InitializeClientExtendedData(client);

        var response = new LoginResponse
        {
            IsOfLegalContactAge = false,
            UserId = account.Id,
            PCLoginToken = "unknown_data",
            SessionKey = "telemetry_key"
        };

        response.PersonaDetailsList.Add(new PersonaDetails()
        {
            DisplayName = account.Username,
            LastLoginTime = CurrentUnixTime,
            PersonaId = account.Id,
            Status = PersonaStatus.Active
        });

        client.RespondTo(packet, response);
        return true;
    }

    private static bool HandleLogout(Client client, Packet packet)
    {
        // TODO: move this to UserSessions component
        client.Notify(new UserSessionLogoutInfo(1, LogoutType.Account), 0x7802, 9);
        return true;
    }

    private bool HandleLoginPersona(Client client, Packet packet)
    {
        var request = packet.ReadContent<LoginPersonaRequest>();
        if (request is null)
        {
            client.RespondTo(packet, null, error: 0x5E0001);
            return true;
        }

        AccountModel account;
        try
        {
            account = accountService.getAccountById(client.UserId);
        }
        catch
        {
            client.RespondTo(packet, null, error: 0xB0001);
            return true;
        }

        client.RespondTo(packet, BuildSessionInfo(account, client));

        var addrBytes = client.EndPoint.Address.GetAddressBytes();
        var addr = addrBytes.Length == 4 ? BinaryPrimitives.ReadUInt32BigEndian(addrBytes) : 0;

        var userAdded = new NotifyUserAdded();
        CopyExtendedDataToNotification(client, userAdded.ExtendedData, addr);
        userAdded.UserInfo.AccountId = client.UserId;
        userAdded.UserInfo.AccountLocale = ReCap.Server.Config.LocaleSettings.Current.BlazeId;
        userAdded.UserInfo.BlazeId = client.UserId;
        userAdded.UserInfo.Name = account.Username;

        client.Notify(userAdded, 0x7802, 2);

        client.Notify(new UserStatus
        {
            BlazeId = account.Id,
            StatusFlags = (uint)SessionState.Connected
        }, 0x7802, 5);

        return true;
    }

    private static bool HandleGetEmailOptInSettings(Client client, Packet packet)
    {
        var request = packet.ReadContent<GetEmailOptInSettingsRequest>();
        if (request is null)
        {
            client.RespondTo(packet, null, error: 0x5E0001); // AUTH_ERR_NO_SUCH_AUTH_DATA
            return true;
        }

        client.RespondTo(packet, new GetEmailOptInSettingsResponse());
        return true;
    }

    private static bool HandleGetLegalDocContent(Client client, Packet packet)
    {
        var request = packet.ReadContent<GetLegalDocContentRequest>();
        if (request is null)
        {
            client.RespondTo(packet, null, error: 0x5E0001); // AUTH_ERR_NO_SUCH_AUTH_DATA
            return true;
        }

        client.RespondTo(packet, new GetLegalDocContentResponse());
        return true;
    }

    private static bool HandleGetPrivacyPolicyContent(Client client, Packet packet)
    {
        client.RespondTo(packet, new GetLegalDocContentResponse());
        return true;
    }

    private static bool HandleAcceptTOS(Client client, Packet packet)
    {
        client.RespondTo(packet);
        return true;
    }

    private static bool HandleGetTOSInfo(Client client, Packet packet)
    {
        client.RespondTo(packet, new GetEmailOptInSettingsResponse());
        return true;
    }

    private static bool HandleGetTermsAndConditions(Client client, Packet packet)
    {
        client.RespondTo(packet, new GetLegalDocContentResponse
        {
            LDVC = "Something",
            Length = 23,
            Text = "Hello this is something"
        });
        return true;
    }

    private static void CopyExtendedDataToNotification(Client client, UserSessionExtendedData target, uint addr)
    {
        target.Address.ActiveMember = NetworkAddressMember.IpPairAddress;
        target.Address.IpPairAddress.ExternalAddress.Ip = addr;
        target.Address.IpPairAddress.ExternalAddress.Port = (ushort)client.EndPoint.Port;
        target.Country = client.ExtendedData.Country;
        target.HardwareFlags = client.ExtendedData.HardwareFlags;
        target.UserInfoAttribute = client.ExtendedData.UserInfoAttribute;
        foreach (var obj in client.ExtendedData.BlazeObjectIdList)
            target.BlazeObjectIdList.Add(obj);
        foreach (var latency in client.ExtendedData.LatencyList)
            target.LatencyList.Add(latency);
        target.QosData.DownstreamBitsPerSecond = client.ExtendedData.QosData.DownstreamBitsPerSecond;
        target.QosData.NatType = client.ExtendedData.QosData.NatType;
        target.QosData.UpstreamBitsPerSecond = client.ExtendedData.QosData.UpstreamBitsPerSecond;
    }

    private bool HandleSilentLogin(Client client, Packet packet)
    {
        var request = packet.ReadContent<SilentLoginRequest>();
        if (request is null)
        {
            client.RespondTo(packet, null, error: 0x5E0001);
            return true;
        }

        AccountModel account;
        try
        {
            account = accountService.getAccountByAuthToken(request.AuthToken);
        }
        catch
        {
            client.RespondTo(packet, null, error: 0xB0001);
            return true;
        }

        client.UserId = account.Id;
        client.Username = account.Username;
        client.AuthToken = request.AuthToken;
        InitializeClientExtendedData(client);

        client.RespondTo(packet, BuildSessionInfo(account, client));
        return true;
    }

    private bool HandleExpressLogin(Client client, Packet packet)
    {
        var request = packet.ReadContent<ExpressLoginRequest>();
        if (request is null)
        {
            client.RespondTo(packet, null, error: 0x5E0001);
            return true;
        }

        AccountModel account;
        try
        {
            account = accountService.getAccountByEmailAndPassword(request.Email, request.Password);
        }
        catch
        {
            client.RespondTo(packet, null, error: 0xB0001);
            return true;
        }

        client.UserId = account.Id;
        client.Username = account.Username;
        InitializeClientExtendedData(client);

        client.RespondTo(packet, BuildSessionInfo(account, client));
        return true;
    }

    private static void InitializeClientExtendedData(Client client)
    {
        if (client.ExtendedData.BlazeObjectIdList.Count > 0)
            return;

        client.ExtendedData.Country = "US";
        client.ExtendedData.HardwareFlags = 1;
        client.ExtendedData.UserInfoAttribute = 3;

        client.ExtendedData.BlazeObjectIdList.Add(new BlazeObjectId(0, new BlazeObjectType(4, 1)));
        client.ExtendedData.BlazeObjectIdList.Add(new BlazeObjectId(0, new BlazeObjectType(5, 1)));

        for (int i = 0; i < 5; i++)
            client.ExtendedData.LatencyList.Add(1161889797);

        client.ExtendedData.QosData.DownstreamBitsPerSecond = 128000;
        client.ExtendedData.QosData.NatType = NatType.Open;
        client.ExtendedData.QosData.UpstreamBitsPerSecond = 2;
    }

    private static SessionInfo BuildSessionInfo(AccountModel account, Client client)
    {
        var info = new SessionInfo
        {
            LastLoginDateTime = CurrentUnixTime,
            Email = account.Email,
            UserId = account.Id,
            BlazeUserId = account.Id
        };

        info.PersonaDetails.DisplayName = account.Username;
        info.PersonaDetails.LastLoginTime = CurrentUnixTime;
        info.PersonaDetails.PersonaId = client.UserId;
        info.PersonaDetails.Status = PersonaStatus.Active;

        return info;
    }

    public string GetCommandName(ushort id)
    {
        return id switch
        {
            0x0A => "createAccount",
            0x14 => "updateAccount",
            0x15 => "upgradeAccount",
            0x1D => "listUserEntitlements2",
            0x1E => "getAccount",
            0x1F => "grantEntitlement",
            0x20 => "listEntitlements",
            0x21 => "hasEntitlement",
            0x22 => "getUseCount",
            0x23 => "decrementUseCount",
            0x24 => "getAuthToken",
            0x26 => "getPasswordRules",
            0x27 => "grantEntitlement2",
            0x28 => "login",
            0x29 => "acceptTos",
            0x2B => "modifyEntitlement2",
            0x2C => "consumecode",
            0x2D => "passwordForgot",
            0x2F => "getPrivacyPolicyContent",
            0x30 => "listPersonaEntitlements2",
            0x32 => "silentLogin",
            0x33 => "checkAgeReq",
            0x34 => "getOptIn",
            0x35 => "enableOptIn",
            0x36 => "disableOptIn",
            0x3C => "expressLogin",
            0x46 => "logout",
            0x50 => "createPersona",
            0x5A => "getPersona",
            0x64 => "listPersonas",
            0x6E => "loginPersona",
            0x78 => "logoutPersona",
            0x8C => "deletePersona",
            0x8D => "disablePersona",
            0x96 => "xboxCreateAccount",
            0x98 => "originLogin",
            0xA0 => "xboxAssociateAccount",
            0xAA => "xboxLogin",
            0xB4 => "ps3CreateAccount",
            0xBE => "ps3AssociateAccount",
            0xC8 => "ps3Login",
            0xC9 => "wiiUCreateAccount",
            0xCA => "wiiUAssociateAccount",
            0xCB => "wiiULogin",
            0xD2 => "validateSessionKey",
            0xE6 => "createWalUserSession",
            0xF1 => "acceptLegalDocs",
            0xF2 => "getEmailOptInSettings",
            0xF6 => "getTermsOfServiceContent",
            0x104 => "getOriginPersona",
            0x10E => "checkEmail",
            0x118 => "getPersonaNameSuggestions",
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

    private static void Log(string message) => ReCap.Server.Util.Logging.Log.Blaze.Debug($"[Authentication component]: {message}");
}

public class CreateAccountResponse : Tdf
{
    [TdfField("PNAM", "")]
    public string PersonaName { get; set; } = string.Empty;

    [TdfField("UID", 0)]
    public long UserId { get; set; }
}

public class GetEmailOptInSettingsRequest : Tdf
{
    [TdfField("CTRY", "")]
    public string Country { get; set; } = string.Empty;

    [TdfField("PTFM", ConnectionProfileType.Native)]
    public ConnectionProfileType Platform { get; set; } = ConnectionProfileType.Native;
}

public class GetEmailOptInSettingsResponse : Tdf
{
    [TdfField("EAMC", 0)]
    public uint EAMC { get; set; }

    [TdfField("PMC", 0)]
    public uint PMC { get; set; }
}

public enum ContentType
{
    Plain = 0,
    HTML = 1,
}

public class GetLegalDocContentRequest : Tdf
{
    [TdfField("CPFT", ConnectionProfileType.Native)]
    public ConnectionProfileType Platform { get; set; } = ConnectionProfileType.Native;

    [TdfField("CTRY", "")]
    public string Country { get; set; } = string.Empty;

    [TdfField("FTCH", false)]
    public bool Fetch { get; set; } = true;

    [TdfField("LANG", "")]
    public string Lang { get; set; } = string.Empty;

    [TdfField("TEXT", ContentType.Plain)]
    public ContentType ContentType { get; set; } = ContentType.Plain;
}

public class GetLegalDocContentResponse : Tdf
{
    [TdfField("LDVC", "")]
    public string LDVC { get; set; } = string.Empty;

    [TdfField("TCOL", 0)]
    public uint Length { get; set; }

    [TdfField("TCOT", "")]
    public string Text { get; set; } = string.Empty;
}

public class LoginPersonaRequest : Tdf
{
    [TdfField("PNAM", "")]
    public string PersonaName { get; set; } = string.Empty;
}

public enum AuthenticationTokenType
{
    Unknown = 0,
    AuthToken = 1,
    PCLoginToken = 2,
}

public class GetAuthTokenResponse : Tdf
{
    [TdfField("AUTH", "")]
    public string AuthToken { get; set; } = string.Empty;
}

public class LoginRequest : Tdf
{
    [TdfField("DVID", 0)]
    public ulong DeviceID { get; set; } // ?

    [TdfField("MAIL", "")]
    public string Email { get; set; } = string.Empty;

    [TdfField("PASS", "")]
    public string Password { get; set; } = string.Empty;

    [TdfField("TOKN", "")]
    public string Token { get; set; } = string.Empty;

    [TdfField("TYPE", AuthenticationTokenType.AuthToken)]
    public AuthenticationTokenType TokenType { get; set; } = AuthenticationTokenType.AuthToken;
}

public class LoginResponse : Tdf
{
    [TdfField("ANON", false)]
    public bool IsAnonymousLogin { get; set; }

    [TdfField("NTOS", false)]
    public bool NeedsLegalDoc { get; set; }

    [TdfField("PCTK", "")]
    public string PCLoginToken { get; set; } = string.Empty;

    [TdfField("PLST")]
    public TdfStructVector<PersonaDetails> PersonaDetailsList { get; } = [];

    [TdfField("SKEY", "")]
    public string SessionKey { get; set; } = string.Empty;

    [TdfField("SPAM", false)]
    public bool IsOfLegalContactAge { get; set; }

    [TdfField("UID", 0)]
    public ulong UserId { get; set; }

    [TdfField("UNDR", false)]
    public bool IsUnderage { get; set; }

    [TdfField("PRIV", "")]
    public string PRIV { get; set; } = string.Empty;

    [TdfField("THST", "")]
    public string THST { get; set; } = string.Empty;

    [TdfField("TURI", "")]
    public string TURI { get; set; } = string.Empty;
}

public enum PersonaStatus
{
    Unknown = 0,
    Pending = 1,
    Active = 2,
    Deactivated = 3,
    Disabled = 4,
    Deleted = 5,
    Banned = 6
}

public class PersonaDetails : Tdf
{
    [TdfField("DSNM", "")]
    public string DisplayName { get; set; } = string.Empty;

    [TdfField("LAST", 0)]
    public uint LastLoginTime { get; set; }

    [TdfField("PID", 0)]
    public ulong PersonaId { get; set; }

    [TdfField("XTYP", ConnectionProfileType.Invalid)]
    public ConnectionProfileType Platform { get; set; } = ConnectionProfileType.Invalid;

    [TdfField("STAS", PersonaStatus.Unknown)]
    public PersonaStatus Status { get; set; } = PersonaStatus.Unknown;

    [TdfField("XREF", 0)]
    public ulong ExternalId { get; set; }
}

public class SessionInfo : Tdf
{
    [TdfField("BUID", 0)]
    public ulong BlazeUserId { get; set; }

    [TdfField("FRST", false)]
    public bool FirstLogin { get; set; }

    [TdfField("KEY", "")]
    public string SessionKey { get; set; } = string.Empty;

    [TdfField("LLOG", 0)]
    public long LastLoginDateTime { get; set; }

    [TdfField("MAIL", "")]
    public string Email { get; set; } = string.Empty;

    [TdfField("PDTL")]
    public PersonaDetails PersonaDetails { get; } = new();

    [TdfField("UID", 0)]
    public ulong UserId { get; set; }
}

public class SilentLoginRequest : Tdf
{
    [TdfField("AUTH", "")]
    public string AuthToken { get; set; } = string.Empty;

    [TdfField("PID", 0)]
    public long PersonaId { get; set; }

    [TdfField("TYPE", AuthenticationTokenType.Unknown)]
    public AuthenticationTokenType TokenType { get; set; } = AuthenticationTokenType.Unknown;
}

public class ExpressLoginRequest : Tdf
{
    [TdfField("MAIL", "")]
    public string Email { get; set; } = string.Empty;

    [TdfField("PASS", "")]
    public string Password { get; set; } = string.Empty;

    [TdfField("PNAM", "")]
    public string PersonaName { get; set; } = string.Empty;
}
