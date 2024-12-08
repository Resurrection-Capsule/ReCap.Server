using System.Diagnostics;

namespace ReCap.Server.Utils;

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