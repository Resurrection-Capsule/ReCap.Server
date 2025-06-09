using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ReCap.Server.Utils;

[SupportedOSPlatform(nameof(OSPlatform.Windows))]
internal class WindowsPlatformVersionImpl
    : PlatformVersionImplBase
{
    const Int32 STATUS_SUCCESS = 0x00000000;




    public override Version GetOSVersion()
    {
        RTL_OSVERSIONINFOEXW versionInfo = new();

        if (RtlGetVersion(ref versionInfo) != STATUS_SUCCESS)
            return base.GetOSVersion();

        int major = (int)versionInfo.dwMajorVersion;
        int minor = (int)versionInfo.dwMinorVersion;
        int build = (int)versionInfo.dwBuildNumber;
        return new(major, minor, build);
    }




    [SupportedOSPlatform(nameof(OSPlatform.Windows))]
    [StructLayout(LayoutKind.Sequential)]
    struct RTL_OSVERSIONINFOEXW
    {
        public UInt32 dwOSVersionInfoSize;
        public UInt32 dwMajorVersion;
        public UInt32 dwMinorVersion;
        public UInt32 dwBuildNumber;
        public UInt32 dwPlatformId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x80)]
        public string szCSDVersion;
        public UInt16 wServicePackMajor;
        public UInt16 wServicePackMinor;
        public UInt16 wSuiteMask;
        public byte bProductType;
        public byte bReserved;
    }

    [SupportedOSPlatform(nameof(OSPlatform.Windows))]
    [DllImport("ntdll.dll", ExactSpelling = true)]
    static extern Int32 RtlGetVersion([In, Out] ref RTL_OSVERSIONINFOEXW PRTL_OSVERSIONINFOW);
}