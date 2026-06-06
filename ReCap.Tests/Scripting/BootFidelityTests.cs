using ReCap.Server.Adapters.Scripting;

namespace ReCap.Tests.Scripting;

public class BootFidelityTests
{
    [Fact]
    public void ParseReferenceStripsRepeatedLuaExtensions()
    {
        var key = ScriptVfs.ParseReference("Modifiers!modifier_shadowravager_support_fear.lua.lua");
        Assert.Equal(ScriptVfs.Hash("modifier_shadowravager_support_fear"), key.InstanceId);
        Assert.Equal(ScriptVfs.Hash("Modifiers"), key.GroupId);
    }

    [Fact]
    public void ParseReferenceSingleExtensionUnchanged()
    {
        var key = ScriptVfs.ParseReference("Lua!GlobalDefinitions.lua");
        Assert.Equal(ScriptVfs.Hash("GlobalDefinitions"), key.InstanceId);
    }
}
