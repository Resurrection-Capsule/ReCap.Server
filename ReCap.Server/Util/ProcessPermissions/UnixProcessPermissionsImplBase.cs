using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ReCap.Server.Util;

#nullable disable
[SupportedOSPlatform(nameof(OSPlatform.Linux))]
internal abstract class UnixProcessPermissionsImplBase
    : IProcessPermissionsImpl
{
    [DllImport ("libc", SetLastError = true)]
    static extern uint geteuid();

    public virtual bool IsCurrentProcessElevated
    {
        get => geteuid() == 0;
    }


    public abstract bool TryRerunElevated(string args, out Process elevatedProcess);
}
#nullable restore