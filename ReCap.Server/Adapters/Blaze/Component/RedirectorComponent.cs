using ReCap.Server.Util;

namespace ReCap.Server.Adapters.Blaze.Component;

public class RedirectorComponent : IComponent
{
    public ushort Id { get; } = 5;
    public BlazeServer? Server { get; set; }

    public string HostName { get; set; } = string.Empty;
    public uint Ip { get; set; }
    public ushort Port { get; set; }
    public bool IsSecure { get; set; }

    public bool HandlePacket(Client client, Packet packet)
    {
        Log($"Handling {GetCommandName(packet.Command)} from {client.EndPoint}!");

        switch (packet.Command)
        {
            case 1:
                return HandleGetServerInstance(client, packet);

            default:
                Log($"Unknown command: {packet.Command} -> {GetCommandName(packet.Command)}");
                return false;
        }
    }

    private bool HandleGetServerInstance(Client client, Packet packet)
    {
        var request = packet.ReadContent<ServerInstanceRequest>();
        if (request is null)
        {
            Log("Unable to read content of getServerInstance request! Sending error...");

            client.RespondTo(packet, response: new ServerInstanceError(), error: 0x10005);
            return true;
        }

        if (string.IsNullOrEmpty(HostName) && Ip == 0)
        {
            Log("No target server is specified! Sending error...");

            client.RespondTo(packet, response: new ServerInstanceError(), error: 0x10005);
            return true;
        }

        var serverInfo = new ServerInstanceInfo
        {
            Secure = IsSecure
        };

        serverInfo.Address.ActiveMember = ServerAddressMember.IpAddress;
        serverInfo.Address.IpAddress.Hostname = HostName;
        serverInfo.Address.IpAddress.Ip = Ip;
        serverInfo.Address.IpAddress.Port = Port;

        client.RespondTo(packet, serverInfo);
        //client.OnDisconnect += () => client.Server.Stop();
        return true;
    }

    public string GetCommandName(ushort id)
    {
        return id switch
        {
            1 => "getServerInstance",
            8 => "getSunsetList",
            9 => "getCACertificates",
            10 => "findCACertificates",
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

    private static void Log(string message) => ReCap.Server.Util.Logging.Log.Blaze.Debug($"[Redirector component]: {message}");


    public class AddressRemapEntry : Tdf
    {
        [TdfField("DPRT", 0)]
        public ushort DstPort { get; set; }

        [TdfField("MASK", 0)]
        public uint NetMask { get; set; }

        [TdfField("SID", 0)]
        public uint ServiceId { get; set; }

        [TdfField("SIP", 0)]
        public uint SrcIp { get; set; }

        [TdfField("SPRT", 0)]
        public ushort SrcPort { get; set; }
    }

    public enum FirstPartyIdMember : uint
    {
        PS3Ticket = 0,
        XboxId = 1,
        Unset = 0x7F
    }

    public class FirstPartyId : TdfUnionT<FirstPartyIdMember>
    {
        [TdfUnionField(0)]
        public TdfBlob PS3Ticket { get; set; } = new();

        [TdfUnionField(1)]
        public XboxId XboxId { get; set; } = new();
    }

    public class RedirectorIpAddress : Tdf
    {
        [TdfField("HOST", "")]
        public string Hostname { get; set; } = string.Empty;

        [TdfField("IP", 0)]
        public uint Ip { get; set; }

        [TdfField("PORT", 0)]
        public ushort Port { get; set; }
    }

    public class NameRemapEntry : Tdf
    {
        [TdfField("DPRT", 0)]
        public ushort DstPort { get; set; }

        [TdfField("SID", 0)]
        public uint ServiceId { get; set; }

        [TdfField("SIP", "")]
        public string Hostname { get; set; } = string.Empty;

        [TdfField("SITE", "")]
        public string SiteName { get; set; } = string.Empty;

        [TdfField("SPRT", 0)]
        public ushort SrcPort { get; set; }
    }

    public class ServerInstanceRequest : Tdf
    {
        [TdfField("BSDK", "")]
        public string BlazeSDKVersion { get; set; } = string.Empty;

        [TdfField("BTIM", "")]
        public string BlazeSDKBuildDate { get; set; } = string.Empty;

        [TdfField("CLNT", "")]
        public string ClientName { get; set; } = string.Empty;

        [TdfField("CLTP", ClientType.Invalid)]
        public ClientType ClientType { get; set; } = ClientType.Invalid;

        [TdfField("CPLT", ConnectionProfileType.Invalid)]
        public ConnectionProfileType ConnectionProfileType { get; set; } = ConnectionProfileType.Invalid;

        [TdfField("CSKU", "")]
        public string ClientSkuId { get; set; } = string.Empty;

        [TdfField("CVER", "")]
        public string ClientVersion { get; set; } = string.Empty;

        [TdfField("DSDK", "")]
        public string DirtySDKVersion { get; set; } = string.Empty;

        [TdfField("ENV", "")]
        public string Environment { get; set; } = string.Empty;

        [TdfField("FPID")]
        public FirstPartyId FirstPartyId { get; set; } = new();

        [TdfField("LOC", 0)]
        public uint ClientLocale { get; set; } = 0;

        [TdfField("NAME", "")]
        public string Name { get; set; } = string.Empty;

        [TdfField("PLAT", "")]
        public string Platform { get; set; } = string.Empty;

        [TdfField("PROF", "")]
        public string ConnectionProfile { get; set; } = string.Empty;
    }

    public class ServerInstanceError : Tdf
    {
        [TdfField("MSGS")]
        public TdfPrimitiveVector<string> Messages { get; } = [];
    }

    public class ServerInstanceInfo : Tdf
    {
        [TdfField("ADDR")]
        public ServerAddress Address { get; } = new();

        [TdfField("AMAP")]
        public TdfStructVector<AddressRemapEntry> AddressRemaps { get; } = [];

        [TdfField("CERT")]
        public TdfPrimitiveVector<TdfBlob> CertificateList { get; } = [];

        [TdfField("MSGS")]
        public TdfPrimitiveVector<string> Messages { get; } = [];

        [TdfField("NMAP")]
        public TdfStructVector<NameRemapEntry> NameRemaps { get; } = [];

        [TdfField("SECU", false)]
        public bool Secure { get; set; }

        [TdfField("XDNS", 0)]
        public uint DefaultDNSAddress { get; set; }
    }

    public enum ServerAddressMember : uint
    {
        IpAddress = 0,
        XboxServerAddress = 1,
        Unset = 0x7F
    }

    public class ServerAddress : TdfUnionT<ServerAddressMember>
    {
        [TdfUnionField(0)]
        public RedirectorIpAddress IpAddress { get; } = new();

        [TdfUnionField(1)]
        public RedirectorXboxServerAddress XboxServerAddress { get; } = new();
    }

    public class XboxId : Tdf
    {
        [TdfField("GTAG", "")]
        public string Gamertag { get; set; } = string.Empty;

        [TdfField("XUID", 0)]
        public ulong Xuid { get; set; }
    }

    public class RedirectorXboxServerAddress : Tdf
    {
        [TdfField("PORT", 0)]
        public ushort Port { get; set; }

        [TdfField("SID", 0)]
        public uint ServiceId { get; set; }

        [TdfField("SITE", "")]
        public string SiteName { get; set; } = string.Empty;
    }
}