using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ReCap.Server.Util;

#nullable disable
[SupportedOSPlatform(nameof(OSPlatform.OSX))]
internal class MacOSProcessPermissionsImpl
    : UnixProcessPermissionsImplBase
{
    public override bool TryRerunElevated(string args, out Process elevatedProcess)
    {
        try
        {
            string sudoArgs = $"{CommandLineHelper.WrapArg(Environment.ProcessPath)} {args}";
            ProcessStartInfo startInfo = new("sudo", sudoArgs);
            elevatedProcess = Process.Start(startInfo);
            return true;
        }
        catch
        {
            elevatedProcess = default;
            return false;
        }
    }
}
#nullable restore