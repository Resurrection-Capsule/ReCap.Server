using System.Diagnostics;

namespace ReCap.Server.Util;

internal class PlatformVersionImplBase
    : IPlatformVersionImpl
{
    public virtual Version GetOSVersion()
    {
        try
        {
            return Environment.OSVersion.Version;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return new Version();
        }
    }
}