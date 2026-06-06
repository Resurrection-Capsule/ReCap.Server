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

        var abilitiesGroup = vfs.GetGroup(0x7153BBB1u);
        Assert.True(abilitiesGroup.Count >= 480, $"Expected Abilities >= 480, got {abilitiesGroup.Count}");

        var modifiersGroup = vfs.GetGroup(0xFC0FF8F5u);
        Assert.True(modifiersGroup.Count >= 400, $"Expected Modifiers >= 400, got {modifiersGroup.Count}");

        var luaGroup = vfs.GetGroup(0x3681D755u);
        Assert.True(luaGroup.Count >= 85, $"Expected Lua >= 85, got {luaGroup.Count}");

        var behaviorsGroup = vfs.GetGroup(0xC130A42Au);
        Assert.True(behaviorsGroup.Count >= 30, $"Expected behaviors >= 30, got {behaviorsGroup.Count}");
    }
}
