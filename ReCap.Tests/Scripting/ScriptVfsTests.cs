using System.IO;
using ReCap.Server.Adapters.Persistence;
using ReCap.Server.Adapters.Scripting;

namespace ReCap.Tests.Scripting;

public class ScriptVfsTests
{
    [Theory]
    [InlineData("Abilities", 0x7153BBB1u)]
    [InlineData("Modifiers", 0xFC0FF8F5u)]
    [InlineData("Lua", 0x3681D755u)]
    [InlineData("behaviors", 0xC130A42Au)]
    public void GroupHashMatchesVerifiedVectors(string group, uint expected) =>
        Assert.Equal(expected, ScriptVfs.Hash(group));

    [Fact]
    public void BareNameHashMatchesProbeVector() =>
        Assert.Equal(0xAD3290E1u, ScriptVfs.Hash("Affix_EnemyHealthRegen"));

    [Fact]
    public void ParsesGroupBangNameForm()
    {
        var key = ScriptVfs.ParseReference("Lua!GlobalDefinitions.lua");
        Assert.Equal(0x3681D755u, key.GroupId);
        Assert.Equal(ScriptVfs.Hash("GlobalDefinitions"), key.InstanceId);
        Assert.Equal(0x3681D755u, key.TypeId);
    }

    [Fact]
    public void ParsesBareNameAgainstSearchGroups()
    {
        var key = ScriptVfs.ParseReference("Affix_EnemyHealthRegen");
        Assert.Equal(0xAD3290E1u, key.InstanceId);
        Assert.Equal(0u, key.GroupId);
    }

    [Fact]
    public void GetChunkReturnsNullWhenMountsHasNoPackage()
    {
        var mounts = new PackageMounts(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var vfs = new ScriptVfs(mounts);
        var key = ScriptVfs.ParseReference("Lua!GlobalDefinitions.lua");
        Assert.Null(vfs.GetChunk(key));
    }

    [Fact]
    public void GetGroupReturnsEmptyWhenMountsHasNoPackage()
    {
        var mounts = new PackageMounts(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var vfs = new ScriptVfs(mounts);
        Assert.Empty(vfs.GetGroup(0x3681D755u));
    }

    [Fact]
    public void IndexesRealServerDataWhenPresent()
    {
        const string packagePath = @"C:\CodingProjects\Personal\Darkspore\Data\ServerData.package";
        if (!File.Exists(packagePath)) return;

        var dataDir = Path.GetDirectoryName(packagePath)!;
        var mounts = new PackageMounts(dataDir);
        var vfs = new ScriptVfs(mounts);

        var luaGroup = ScriptVfs.Hash("Lua");
        var luaChunks = vfs.GetGroup(luaGroup);
        Assert.True(luaChunks.Count >= 1, $"Expected at least 1 lua chunk in Lua group, got {luaChunks.Count}");

        var abilitiesGroup = ScriptVfs.Hash("Abilities");
        var abilitiesChunks = vfs.GetGroup(abilitiesGroup);
        var total = luaChunks.Count + abilitiesChunks.Count;
        Assert.True(total >= 100, $"Expected total Lua+Abilities chunks >= 100, got {total}");
    }
}
