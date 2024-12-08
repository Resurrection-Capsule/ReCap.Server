using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ReCap.Server.Utils;

#nullable disable
[SupportedOSPlatform(nameof(OSPlatform.OSX))]
internal class MacOSProcessPermissionsImpl
    : UnixProcessPermissionsImplBase
{
    public override bool TryRerunElevated(string args, out Process elevatedProcess)
    {
        // [TODO: Implement]
        throw new NotImplementedException();
    }
}
#nullable restore