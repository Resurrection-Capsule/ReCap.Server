using System.Text;

namespace ReCap.Server.Adapters.Persistence;

// EA QoS NAT-negotiation endpoints (C++ API.cpp:591/633/681). The client hits these (at the
// host:port advertised in Blaze Util preAuth QOSS) before matchmaking to probe its NAT type; we
// answer the best-case shape so it concludes "open NAT" and proceeds. Path-matched, XML body.
//   /qos/qos      — probe config (echoes client port + request type)
//   /qos/firewall — interface/IP enumeration
//   /qos/firetype — final verdict: always NatType::Open (1)
public sealed class QosStorageAdapter
{
    private const int ReqSecret = 0x1337; // 4919

    private readonly int _qosPort;

    public QosStorageAdapter(int qosPort) => _qosPort = qosPort;

    public bool Handles(string uri) =>
        uri.Equals("/qos/qos", StringComparison.OrdinalIgnoreCase) ||
        uri.Equals("/qos/firewall", StringComparison.OrdinalIgnoreCase) ||
        uri.Equals("/qos/firetype", StringComparison.OrdinalIgnoreCase);

    public byte[] GetFile(string uri, Dictionary<string, string> parameters)
    {
        if (uri.Equals("/qos/firetype", StringComparison.OrdinalIgnoreCase))
            return Xml("<firetype>\n  <firetype>1</firetype>\n</firetype>");

        if (uri.Equals("/qos/firewall", StringComparison.OrdinalIgnoreCase))
        {
            uint nint = ParseUInt(parameters, "nint", 1);
            var sb = new StringBuilder();
            sb.Append("<firewall>\n  <numinterfaces>").Append(nint).Append("</numinterfaces>\n  <ips>\n");
            for (uint i = 0; i < nint; i++) sb.Append("    <ips>127.0.0.1</ips>\n");
            sb.Append("  </ips>\n  <ports>\n");
            for (uint i = 0; i < nint; i++) sb.Append("    <ports>").Append(_qosPort).Append("</ports>\n");
            sb.Append("  </ports>\n  <requestid>1</requestid>\n  <reqsecret>").Append(ReqSecret).Append("</reqsecret>\n</firewall>");
            return Xml(sb.ToString());
        }

        // /qos/qos — only qtyp 1|2 get a body; anything else is an empty plain-text response.
        int qtyp = (int)ParseUInt(parameters, "qtyp", 0);
        if (qtyp != 1 && qtyp != 2) return Array.Empty<byte>();
        uint prpt = ParseUInt(parameters, "prpt", 0);
        return Xml($"<qos>\n  <numprobes>2</numprobes>\n  <probesize>8</probesize>\n  <qosport>{prpt}</qosport>\n  <requestid>{qtyp}</requestid>\n  <reqsecret>{ReqSecret}</reqsecret>\n</qos>");
    }

    private static byte[] Xml(string body) =>
        Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" + body);

    private static uint ParseUInt(Dictionary<string, string> parameters, string key, uint fallback) =>
        parameters.TryGetValue(key, out var v) && uint.TryParse(v, out var parsed) ? parsed : fallback;
}
