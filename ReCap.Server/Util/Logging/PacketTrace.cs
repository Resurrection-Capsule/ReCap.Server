using System.Text;

namespace ReCap.Server.Util.Logging;

public static class PacketTrace
{
    public static string Sent(string name, byte[] data) => Render("→", name, data, null);

    public static string Received(string name, byte[] data, string? from) => Render("←", name, data, from);

    static string Render(string arrow, string name, byte[] data, string? from)
    {
        var sb = new StringBuilder();
        sb.Append(arrow).Append(' ').Append(name).Append(" (").Append(data.Length).Append("B)");
        if (from is not null)
            sb.Append(" from ").Append(from);

        if (data.Length == 0)
            return sb.ToString();

        if (data.Length <= 16)
            sb.Append("  ").Append(BitConverter.ToString(data).Replace('-', ' '));
        else
            sb.Append('\n').Append(HexDump(data));

        return sb.ToString();
    }

    public static string HexDump(byte[] data)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < data.Length; i += 16)
        {
            int len = Math.Min(16, data.Length - i);
            sb.Append("    ").Append(i.ToString("X4")).Append("  ");

            for (int j = 0; j < 16; j++)
            {
                if (j < len)
                    sb.Append(data[i + j].ToString("X2")).Append(' ');
                else
                    sb.Append("   ");
                if (j == 7)
                    sb.Append(' ');
            }

            sb.Append(' ');
            for (int j = 0; j < len; j++)
            {
                byte b = data[i + j];
                sb.Append(b is >= 0x20 and < 0x7F ? (char)b : '.');
            }

            if (i + 16 < data.Length)
                sb.Append('\n');
        }
        return sb.ToString();
    }
}
