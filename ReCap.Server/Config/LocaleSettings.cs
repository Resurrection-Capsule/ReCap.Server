namespace ReCap.Server.Config;

public sealed class LocaleSettings
{
    public static readonly string[] KnownCodes = ["en-us", "pt-br", "de-de", "fr-fr", "pl-pl", "ru-ru"];
    public const string DefaultCode = "en-us";

    public string Code { get; }
    public uint BlazeId { get; }
    public string TextPackageRelativePath { get; }

    private LocaleSettings(string code)
    {
        Code = code;
        BlazeId = DeriveBlazeId(code);
        TextPackageRelativePath = Path.Combine("Locale", code, "Text.package");
    }

    public static LocaleSettings Current { get; private set; } = new(DefaultCode);

    public static void SetCurrent(LocaleSettings settings) => Current = settings;

    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var code = raw.Trim().ToLowerInvariant().Replace('_', '-');
        return Array.IndexOf(KnownCodes, code) >= 0 ? code : null;
    }

    public static LocaleSettings Resolve(string? cliCode, ILocaleSource registry)
    {
        var code = Normalize(cliCode) ?? Normalize(registry.Read()) ?? DefaultCode;
        return new LocaleSettings(code);
    }

    // BlazeId mirrors the legacy literal 0x656E5553 = ASCII "enUS" big-endian (lang lowercase +
    // region UPPERCASE). Cosmetic: the client stores but ignores it (Ghidra 2026-06-07).
    internal static uint DeriveBlazeId(string code)
    {
        var parts = code.Split('-');
        var lang = parts[0];
        var region = parts[1].ToUpperInvariant();
        return ((uint)lang[0] << 24) | ((uint)lang[1] << 16) | ((uint)region[0] << 8) | region[1];
    }
}
