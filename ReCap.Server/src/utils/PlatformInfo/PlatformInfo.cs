using System.Runtime.InteropServices;

namespace ReCap.Server.Utils;

public static class PlatformInfo
{
    public static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public static readonly bool IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    public static readonly bool IsMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);


    // MUST BE DEFINED AFTER THE THREE ABOVE
    static readonly IPlatformVersionImpl _impl = PlatformUtils.GetForPlatform<IPlatformVersionImpl>();




    /*
    Gets a <see cref="Version"/> object that identifies the operating system.
    
    Returns:
        A System.Version object that describes the major version, minor version, build,
        and revision numbers for the operating system.


    A <see cref="Version"/> object that describes the major version, minor version, build, and revision numbers for the operating system.
    */
    /// <summary>
    /// Gets a <see cref="Version"/> object that <em>accurately</em> identifies the operating system.
    /// </summary>
    /// <returns>
    /// A <see cref="Version"/> object that describes the major version, minor version, and build numbers for the operating system:
    ///     <list type="bullet">
    /// 	    <item>
    /// 		    <term><see cref="Version.Major"/></term>
    /// 		    <description>describes the major OS version.</description>
    /// 	    </item>
    /// 	    <item>
    /// 		    <term><see cref="Version.Minor"/></term>
    /// 		    <description>describes the minor OS version.</description>
    /// 	    </item>
    /// 	    <item>
    /// 		    <term><see cref="Version.Build"/></term>
    /// 		    <description>describes the OS build.</description>
    /// 	    </item>
    ///     </list>
    /// 	<br/>
    /// 	<item>
    /// 	    <term>NOTE</term>
    /// 	    <description><see cref="Version.Revision"/> is usually inaccurate on Windows, and should be disregarded.</description>
    /// 	</item>
    /// </returns>
    /// <remarks>
    /// This is literally only needed because Windows sometimes lies and reports a <see cref="Version"/>
    /// for Windows 8.0 (6.2.9200), which sometimes fools <see cref="Environment.OSVersion.Version" />.
    /// </remarks>
    public static readonly Version OSVersion = _impl.GetOSVersion();
}



