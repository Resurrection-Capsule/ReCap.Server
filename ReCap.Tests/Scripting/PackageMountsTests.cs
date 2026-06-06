using System.IO;
using ReCap.Server.Adapters.Persistence;

namespace ReCap.Tests.Scripting;

public class PackageMountsTests
{
    [Fact]
    public void UnknownDataDirYieldsNullReader()
    {
        var mounts = new PackageMounts(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        Assert.Null(mounts.Get(WellKnownPackage.ServerData));
    }

    [Fact]
    public void MissingPackageIsNegativeCachedAcrossCalls()
    {
        var mounts = new PackageMounts(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        Assert.Null(mounts.Get(WellKnownPackage.ServerData));
        Assert.Null(mounts.Get(WellKnownPackage.ServerData));
    }

    [Fact]
    public void PackagePathsAreDataRelative()
    {
        Assert.Equal("ServerData.package", WellKnownPackage.ServerData.RelativePath);
        Assert.Equal(Path.Combine("Locale", "en-us", "Text.package"), WellKnownPackage.LocaleTextEnUs.RelativePath);
    }
}
