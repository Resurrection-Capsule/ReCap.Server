using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ReCap.Server.Util;

[SupportedOSPlatform(nameof(OSPlatform.Linux))]
internal class LinuxPlatformVersionImpl
    : PlatformVersionImplBase
{}