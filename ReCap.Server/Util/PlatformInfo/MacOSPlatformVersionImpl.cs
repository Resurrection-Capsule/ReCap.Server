using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ReCap.Server.Util;

[SupportedOSPlatform(nameof(OSPlatform.OSX))]
internal class MacOSPlatformVersionImpl
    : PlatformVersionImplBase
{}