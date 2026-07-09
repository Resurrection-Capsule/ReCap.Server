using ReCap.Server.Util;

namespace ReCap.Server.Adapters.Blaze.Component;

public class UtilComponent : IComponent
{
    // public static uint CurrentUnixTime => (uint)(new DateTimeOffset(DateTime.UtcNow)).ToUnixTimeMilliseconds();
    public static uint CurrentUnixTime => (uint)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

    public ushort Id => 9;
    public BlazeServer? Server { get; set; }

    public bool HandlePacket(Client client, Packet packet)
    {
        return packet.Command switch
        {
            2 => HandlePing(client, packet),
            7 => HandlePreAuth(client, packet),
            8 => HandlePostAuth(client, packet),
            _ => false,
        };
    }

    private static bool HandlePing(Client client, Packet packet)
    {
        client.RespondTo(packet, new PingResponse
        {
            ServerTime = CurrentUnixTime
        });
        return true;
    }

    private static bool HandlePreAuth(Client client, Packet packet)
    {
        var request = packet.ReadContent<PreAuthRequest>();
        if (request is null)
        {
            Log($"Received empty PreAuth from client {client.EndPoint}! Disconnecting...");

            client.Disconnect();
            return false;
        }

        var response = new PreAuthResponse
        {
            AuthenticationSource = "321915",
            InstanceName = request.ClientData.ServiceName,
            PersonaNamespace = "cem_ea_id",
            Platform = "pc",
            RegistrationSource = "321915",
            ServerVersion = "Blaze 3.9.3.1"
        };

        response.ComponentIds.Add(1);
        response.ComponentIds.Add(25);
        response.ComponentIds.Add(4);
        response.ComponentIds.Add(27);
        response.ComponentIds.Add(28);
        response.ComponentIds.Add(6);
        response.ComponentIds.Add(7);
        response.ComponentIds.Add(9);
        response.ComponentIds.Add(10);
        response.ComponentIds.Add(11);
        response.ComponentIds.Add(30720);
        response.ComponentIds.Add(30721);
        response.ComponentIds.Add(30722);
        response.ComponentIds.Add(30723);
        response.ComponentIds.Add(20);
        response.ComponentIds.Add(30725);
        response.ComponentIds.Add(30726);
        response.ComponentIds.Add(2000);

        response.Config.Config.Add("connIdleTimeout", "90s");
        response.Config.Config.Add("defaultRequestTimeout", "80s");
        response.Config.Config.Add("pingPeriod", "20s");
        response.Config.Config.Add("voipHeadsetUpdateRate", "1000");
        response.Config.Config.Add("xlspConnectionIdleTimeout", "300");

        // Point the client's QoS probes at the REST listener, which now serves /qos/* (was a dead
        // 127.0.0.1:17502 nothing listened on). QoS HTTP shares the REST Api port.
        const int qosPort = ReCap.Server.Adapters.Rest.Api.Api.DEFAULT_PORT;
        response.QosSettings.BandwithPingSiteInfo.Address = "127.0.0.1";
        response.QosSettings.BandwithPingSiteInfo.Port = qosPort;
        response.QosSettings.BandwithPingSiteInfo.SiteName = "ams";

        response.QosSettings.PingSiteInfoByAliasMap["ams"] = new();
        response.QosSettings.PingSiteInfoByAliasMap["ams"].Address = "127.0.0.1";
        response.QosSettings.PingSiteInfoByAliasMap["ams"].Port = qosPort;
        response.QosSettings.PingSiteInfoByAliasMap["ams"].SiteName = "ams";

        client.RespondTo(packet, response);
        return true;
    }

    private static bool HandlePostAuth(Client client, Packet packet)
    {
        var response = new PostAuthResponse();

        response.PssConfig.Address = "127.0.0.1";
        response.PssConfig.ProjectId = "123071";
        response.PssConfig.Port = 42125;
        response.PssConfig.InitialReportTypes = 9;

        response.Telemetry.Address = "127.0.0.1";
        response.Telemetry.Port = 42125;
        response.Telemetry.SendDelay = 15000;
        response.Telemetry.Locale = ReCap.Server.Config.LocaleSettings.Current.BlazeId;
        response.Telemetry.Disable = "AD,AF,AG,AI,AL,AM,AN,AO,AQ,AR,AS,AW,AX,AZ,BA,BB,BD,BF,BH,BI,BJ,BM,BN,BO,BR,BS,BT,BV,BW,BY,BZ,CC,CD,CF,CG,CI,CK,CL,CM,CN,CO,CR,CU,CV,CX,DJ,DM,DO,DZ,EC,EG,EH,ER,ET,FJ,FK,FM,FO,GA,GD,GE,GF,GG,GH,GI,GL,GM,GN,GP,GQ,GS,GT,GU,GW,GY,HM,HN,HT,ID,IL,IM,IN,IO,IQ,IR,IS,JE,JM,JO,KE,KG,KH,KI,KM,KN,KP,KR,KW,KY,KZ,LA,LB,LC,LI,LK,LR,LS,LY,MA,MC,MD,ME,MG,MH,ML,MM,MN,MO,MP,MQ,MR,MS,MU,MV,MW,MY,MZ,NA,NC,NE,NF,NG,NI,NP,NR,NU,OM,PA,PE,PF,PG,PH,PK,PM,PN,PS,PW,PY,QA,RE,RS,RW,SA,SB,SC,SD,SG,SH,SJ,SL,SM,SN,SO,SR,ST,SV,SY,SZ,TC,TD,TF,TG,TH,TJ,TK,TL,TM,TN,TO,TT,TV,TZ,UA,UG,UM,UY,UZ,VA,VC,VE,VG,VN,VU,WF,WS,YE,YT,ZM,ZW,ZZ";
        response.Telemetry.NoToggleOk = "US,CA,MX";
        response.Telemetry.SessionId = "telemetry_session";
        response.Telemetry.Key = "telemetry_key";
        response.Telemetry.SendPercentage = 75;
        response.Telemetry.UseServerTime = CurrentUnixTime.ToString();
        response.Telemetry.ServerName = "BGServ";

        response.Ticker.Address = "127.0.0.1";
        response.Ticker.Port = 42125;
        response.Ticker.Key = "0,127.0.0.1:8999,darkspore-pc,10,50,50,50,50,0,0";

        response.Options.UserId = client.UserId;
        response.Options.TelemetryOpt = TelemetryOpt.OptOut;

        client.RespondTo(packet, response);
        return true;
    }

    public string GetCommandName(ushort id)
    {
        return id switch
        {
            1 => "fetchClientConfig",
            2 => "ping",
            3 => "setClientData",
            4 => "localizeStrings",
            5 => "getTelemetryServer",
            6 => "getTickerServer",
            7 => "preAuth",
            8 => "postAuth",
            10 => "userSettingsLoad",
            11 => "userSettingsSave",
            12 => "userSettingsLoadAll",
            14 => "deleteUserSettings",
            20 => "filterForProfanity",
            21 => "fetchQosConfig",
            22 => "setClientMetrics",
            23 => "setConnectionState",
            24 => "getPssConfig",
            25 => "getUserOptions",
            26 => "setUserOptions",
            27 => "suspendUserPing",
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

    private static void Log(string message) => ReCap.Server.Util.Logging.Log.Blaze.Debug($"[Util component]: {message}");


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

    
}