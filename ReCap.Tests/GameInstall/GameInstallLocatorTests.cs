using ReCap.Server.Services;

namespace ReCap.Tests.GameInstall;

public class GameInstallLocatorTests
{
    private static string MakeFakeInstall()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ds_{Guid.NewGuid():N}");
        var data = Path.Combine(root, "Data");
        Directory.CreateDirectory(data);
        File.WriteAllBytes(Path.Combine(data, "AssetData_Binary.package"), [0]);
        File.WriteAllBytes(Path.Combine(data, "ServerData.package"), [0]);
        return root;
    }

    [Fact]
    public void CliPathWinsAndNormalizesRootDataOrFile()
    {
        var root = MakeFakeInstall();
        foreach (var input in new[]
        {
            root,
            Path.Combine(root, "Data"),
            Path.Combine(root, "Data", "AssetData_Binary.package"),
            Path.Combine(root, "Data") + Path.DirectorySeparatorChar,
        })
        {
            var result = GameInstallLocator.Normalize(input);
            Assert.NotNull(result);
            Assert.Equal(Path.Combine(root, "Data"), result!.DataDir);
        }
    }

    [Fact]
    public void InvalidPathYieldsNull() =>
        Assert.Null(GameInstallLocator.Normalize(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));

    [Fact]
    public void PersistedPathRoundTrips()
    {
        var root = MakeFakeInstall();
        var store = Path.Combine(Path.GetTempPath(), $"gp_{Guid.NewGuid():N}.json");
        GameInstallLocator.Persist(store, root);
        var resolved = GameInstallLocator.Resolve(cliPath: null, persistencePath: store, probePaths: []);
        Assert.NotNull(resolved);
        Assert.Equal(Path.Combine(root, "Data"), resolved!.DataDir);
    }

    [Fact]
    public void ChainFallsThroughToNullWithNoSources()
    {
        var store = Path.Combine(Path.GetTempPath(), $"gp_{Guid.NewGuid():N}.json");
        var resolved = GameInstallLocator.Resolve(cliPath: null, persistencePath: store, probePaths: [], skipRegistry: true);
        Assert.Null(resolved);
    }
}
