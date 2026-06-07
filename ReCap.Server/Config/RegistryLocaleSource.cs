namespace ReCap.Server.Config;

public sealed class RegistryLocaleSource : ILocaleSource
{
    public string? Read()
    {
        if (!OperatingSystem.IsWindows()) return null;
        return ReadValue(@"SOFTWARE\WOW6432Node\Electronic Arts\Darkspore")
            ?? ReadValue(@"SOFTWARE\Electronic Arts\Darkspore");
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static string? ReadValue(string subKey)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(subKey);
            return key?.GetValue("Locale") as string;
        }
        catch
        {
            return null;
        }
    }
}
