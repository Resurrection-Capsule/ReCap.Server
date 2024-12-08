using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ReCap.Server.Utils;

[SupportedOSPlatform(nameof(OSPlatform.OSX))]
internal class MacOSPlatformVersionImpl
    : PlatformVersionImplBase
{}