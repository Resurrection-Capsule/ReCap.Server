using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ReCap.Server.Util;

#nullable disable
[SupportedOSPlatform(nameof(OSPlatform.Linux))]
internal class LinuxProcessPermissionsImpl
    : UnixProcessPermissionsImplBase
{
    public override bool TryRerunElevated(string args, out Process elevatedProcess)
    {
        Debug.WriteLine($"{nameof(LinuxProcessPermissionsImpl)}.{nameof(TryRerunElevated)}('{args}', out...)");
        try
        {
            string sudoArgs = $"{CommandLineHelper.WrapArg(Environment.ProcessPath)} {args}";
            Debug.WriteLine($"{nameof(sudoArgs)}: '{sudoArgs}'");
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