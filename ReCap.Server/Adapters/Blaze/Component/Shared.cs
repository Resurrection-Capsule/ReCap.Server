using ReCap.Server.Util;

namespace ReCap.Server.Adapters.Blaze.Component;

public enum ClientType
{
    GameplayUser = 0,
    HTTPUser = 1,
    DedicatedServer = 2,
    Tools = 3,
    Invalid = 4
}

public enum ConnectionProfileType : int
{
    Native = 0xFFFF,
    Invalid = 0,
    Xb12 = 1,
    PS3 = 2,
    Wii = 3,
    PC = 4,
    Android = 5,
    iOS = 6,
    Qnx = 7,
    Common = 8,
    Mobile = 9,
    LegacyProfileId = 10,
    Verizon = 11,
    Facebook = 12,
    FacebookEacom = 13,
    Bebo = 14,
    Friendster = 15,
    Twitter = 16,
    WiiU = 17,
    Vita = 18,
    Cap = 19,
    Ket = 20
}

public class ClientData : Tdf
{
    [TdfField("IITO", false)]
    public bool IgnoreInactivityTimeout { get; set; }

    [TdfField("LANG", 0)]
    public uint Locale { get; set; }

    [TdfField("SVCN", "")]
    public string ServiceName { get; set; } = string.Empty;

    [TdfField("TYPE", ClientType.GameplayUser)]
    public ClientType ClientType { get; set; } = ClientType.GameplayUser;
}

public class ClientInfo : Tdf
{
    [TdfField("BSDK", "")]
    public string BlazeSDKVersion { get; set; } = string.Empty;

    [TdfField("BTIM", "")]
    public string BlazeSDKBuildDate { get; set; } = string.Empty;

    [TdfField("CLNT", "")]
    public string ClientName { get; set; } = string.Empty;

    [TdfField("CPFT", ConnectionProfileType.Native)]
    public ConnectionProfileType ConnectionProfileType { get; set; } = ConnectionProfileType.Native;

    [TdfField("CSKU", "")]
    public string ClientSkuId { get; set; } = string.Empty;

    [TdfField("CVER", "")]
    public string ClientVersion { get; set; } = string.Empty;

    [TdfField("DSDK", "")]
    public string DirtySDKVersion { get; set; } = string.Empty;

    [TdfField("ENV", "")]
    public string Environment { get; set; } = string.Empty;

    [TdfField("HWID", "")]
    public string HardwareId { get; set; } = string.Empty;

    [TdfField("LOC", 0)]
    public uint ClientLocale { get; set; } = 0;

    [TdfField("MAC", "")]
    public string MacAddress { get; set; } = string.Empty;

    [TdfField("PLAT", "")]
    public string PLAT { get; set; } = string.Empty;
}

public class FetchClientConfigRequest : Tdf
{
    [TdfField("CFID", "")]
    public string ConfigSection { get; set; } = string.Empty;
}

public class FetchConfigResponse : Tdf
{
    [TdfField("CONF")]
    public TdfPrimitiveMap<string, string> Config { get; } = [];
}

public class GetTelemetryServerResponse : Tdf
{
    [TdfField("ADRS", "")]
    public string Address { get; set; } = string.Empty;

    [TdfField("ANON", false)]
    public bool IsAnonymous { get; set; }

    [TdfField("DISA", "")]
    public string Disable { get; set; } = string.Empty;

    [TdfField("EDCT", false)]
    public bool EDCT { get; set; }

    [TdfField("FILT", "")]
    public string Filter { get; set; } = string.Empty;

    [TdfField("LOC", 0)]
    public uint Locale { get; set; }

    [TdfField("MINR", false)]
    public bool IsUnderage { get; set; }

    [TdfField("NOOK", "")]
    public string NoToggleOk { get; set; } = string.Empty;

    [TdfField("PORT", 0)]
    public uint Port { get; set; }

    [TdfField("SDLY", 0)]
    public uint SendDelay { get; set; }

    [TdfField("SESS", "")]
    public string SessionId { get; set; } = string.Empty;

    [TdfField("SKEY", "")]
    public string Key { get; set; } = string.Empty;

    [TdfField("SPCT", 0)]
    public uint SendPercentage { get; set; }

    [TdfField("STIM", "")]
    public string UseServerTime { get; set; } = string.Empty;

    [TdfField("SVNM", "")]
    public string ServerName { get; set; } = string.Empty;
}

public class GetTickerServerResponse : Tdf
{
    [TdfField("ADRS", "")]
    public string Address { get; set; } = string.Empty;

    [TdfField("PORT", 0)]
    public uint Port { get; set; }

    [TdfField("SKEY", "")]
    public string Key { get; set; } = string.Empty;
}

public class HostnameAddress : Tdf
{
    [TdfField("NAME", "")]
    public string HostName { get; set; } = string.Empty;

    [TdfField("PORT", 0)]
    public ushort Port { get; set; }
}

public class IpAddress : Tdf
{
    [TdfField("IP", 0)]
    public uint Ip { get; set; }

    [TdfField("PORT", 0)]
    public ushort Port { get; set; }
}

public class IpPairAddress : Tdf
{
    [TdfField("EXIP")]
    public IpAddress ExternalAddress { get; } = new();

    [TdfField("INIP")]
    public IpAddress InternalAddress { get; } = new();
}

public enum NetworkAddressMember : uint
{
    XboxClientAddress = 0,
    XboxServerAddress = 1,
    IpPairAddress = 2,
    IpAddress = 3,
    HostnameAddress = 4,
    Unset = 0x7F
}

public class NetworkAddress : TdfUnionT<NetworkAddressMember>
{
    [TdfUnionField(0)]
    public XboxClientAddress XboxClientAddress { get; } = new();

    [TdfUnionField(1)]
    public XboxServerAddress XboxServerAddress { get; } = new();

    [TdfUnionField(2)]
    public IpPairAddress IpPairAddress { get; } = new();

    [TdfUnionField(3)]
    public IpAddress IpAddress { get; } = new();

    [TdfUnionField(4)]
    public HostnameAddress HostnameAddress { get; } = new();
}

public class NetworkInfo : Tdf
{
    [TdfField("ADDR")]
    public NetworkAddress Address { get; } = new();

    [TdfField("NLMP")]
    public TdfPrimitiveMap<string, int> PingSiteLatencyByAlias { get; } = [];

    [TdfField("NQOS")]
    public NetworkQosData QosData { get; } = new();
}

public enum NatType
{
    Open = 0,
    Moderate = 1,
    StrictSequential = 2,
    Strict = 3,
    Unknown = 4,
    None = 5,
    Pending = 6
}

public class NetworkQosData : Tdf
{
    [TdfField("DBPS", 0)]
    public uint DownstreamBitsPerSecond { get; set; }

    [TdfField("NATT", NatType.Open)]
    public NatType NatType { get; set; }

    [TdfField("UBPS", 0)]
    public uint UpstreamBitsPerSecond { get; set; }
}

public class PingResponse : Tdf
{
    [TdfField("STIM", 0)]
    public uint ServerTime { get; set; }
}

public class PostAuthResponse : Tdf
{
    [TdfField("PSS")]
    public PssConfig PssConfig { get; } = new();

    [TdfField("TELE")]
    public GetTelemetryServerResponse Telemetry { get; } = new();

    [TdfField("TICK")]
    public GetTickerServerResponse Ticker { get; } = new();

    [TdfField("UROP")]
    public UserOptions Options { get; } = new();
}

public class PreAuthRequest : Tdf
{
    [TdfField("CINF")]
    public ClientInfo ClientInfo { get; } = new();

    [TdfField("CDAT")]
    public ClientData ClientData { get; } = new();

    [TdfField("FCCR")]
    public FetchClientConfigRequest FetchClientConfig { get; } = new();
}

public class PreAuthResponse : Tdf
{
    [TdfField("ASRC", "")]
    public string AuthenticationSource { get; set; } = string.Empty;

    [TdfField("CIDS")]
    public TdfPrimitiveVector<ushort> ComponentIds { get; set; } = [];

    [TdfField("CONF")]
    public FetchConfigResponse Config { get; set; } = new();

    [TdfField("EEFA", true)]
    public bool AnonymousChildAccountsEnabled { get; set; } = true;

    [TdfField("INST", "")]
    public string InstanceName { get; set; } = string.Empty;

    [TdfField("MINR", false)]
    public bool UnderageSupported { get; set; } = false;

    [TdfField("NASP", "")]
    public string PersonaNamespace { get; set; } = string.Empty;

    [TdfField("PILD", "")]
    public string PILD { get; set; } = string.Empty;

    [TdfField("PLAT", "")]
    public string Platform { get; set; } = string.Empty;

    [TdfField("QOSS")]
    public QosConfigInfo QosSettings { get; set; } = new();

    [TdfField("RSRC", "")]
    public string RegistrationSource { get; set; } = string.Empty;

    [TdfField("SVER", "")]
    public string ServerVersion { get; set; } = string.Empty;
}

public class PssConfig : Tdf
{
    [TdfField("ADRS", "")]
    public string Address { get; set; } = string.Empty;

    [TdfField("CSIG")]
    public TdfBlob NpCommSignature { get; } = new();

    [TdfField("OIDS")]
    public TdfPrimitiveVector<string> OfferIds { get; } = [];

    [TdfField("PJID", "")]
    public string ProjectId { get; set; } = string.Empty;

    [TdfField("PORT", 0)]
    public uint Port { get; set; }

    [TdfField("RPRT", 0)]
    public uint InitialReportTypes { get; set; }

    [TdfField("TIID", 0)]
    public uint TitleId { get; set; }
}

public class QosConfigInfo : Tdf
{
    [TdfField("BWPS")]
    public QosPingSiteInfo BandwithPingSiteInfo { get; set; } = new();

    [TdfField("LNP", 0)]
    public ushort NumLatencyProbes { get; set; } = 10;

    [TdfField("LTPS")]
    public TdfStructMap<string, QosPingSiteInfo> PingSiteInfoByAliasMap { get; } = [];

    [TdfField("SVID", 0x45410805u)]
    public uint ServiceId { get; set; } = 0x45410805u;
}

public class QosPingSiteInfo : Tdf
{
    [TdfField("PSA", "")]
    public string Address { get; set; } = string.Empty;

    [TdfField("PSP", 0)]
    public ushort Port { get; set; } = 0;

    [TdfField("SNA", "")]
    public string SiteName { get; set; } = string.Empty;
}

public enum TelemetryOpt
{
    OptOut = 0,
    OptIn = 1
}

public class UserOptions : Tdf
{
    [TdfField("TMOP", TelemetryOpt.OptOut)]
    public TelemetryOpt TelemetryOpt { get; set; } = TelemetryOpt.OptOut;

    [TdfField("UID", 0)]
    public ulong UserId { get; set; }
}

public class XboxClientAddress : Tdf
{
    [TdfField("XDDR")]
    public TdfBlob XnAddr { get; set; } = new();

    [TdfField("XUID", 0)]
    public ulong Xuid { get; set; }
}

public class XboxServerAddress : Tdf
{
    [TdfField("PORT", 0)]
    public ushort Port { get; set; }

    [TdfField("SITE", "")]
    public string SiteName { get; set; } = string.Empty;

    [TdfField("SVID", 0)]
    public uint Ip { get; set; }
}