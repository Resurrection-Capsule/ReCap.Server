using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ReCap.Server.Utils;

[SupportedOSPlatform(nameof(OSPlatform.Linux))]
internal class LinuxPlatformVersionImpl
    : PlatformVersionImplBase
{}