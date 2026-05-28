namespace ReCap.Server.Util.Logging;

public static class BitField
{
    public static string Format(IEnumerable<int> bits)
        => "{" + string.Join(",", bits.OrderBy(b => b)) + "}";

    public static string Format(IEnumerable<int> bits, IReadOnlyDictionary<int, string> names)
        => "{" + string.Join(", ", bits.OrderBy(b => b)
            .Select(b => names.TryGetValue(b, out var n) ? $"{b}={n}" : b.ToString())) + "}";

    public static string FromMask(uint mask)
    {
        var bits = new List<int>();
        for (int i = 0; i < 32; i++)
            if ((mask & (1u << i)) != 0)
                bits.Add(i);
        return Format(bits) + $" (0x{mask:X})";
    }
}
