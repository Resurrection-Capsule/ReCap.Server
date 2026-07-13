namespace ReCap.Server.Adapters.Scripting.Api;

public static class StubNamespaces
{
    public static void RegisterAll(nint L)
    {
        LuaApiModule.RegisterNamespace(L, "nThreadData");
        LuaApiModule.RegisterNamespace(L, "nTimeManager");
        LuaApiModule.RegisterNamespace(L, "nThread");
        NBehaviorTreeModule.Register(L);
        LuaApiModule.RegisterNamespace(L, "nScenarioManager");
        LuaApiModule.RegisterNamespace(L, "nAbility");
        LuaApiModule.RegisterNamespace(L, "nModifier");
        LuaApiModule.RegisterNamespace(L, "nCondition");
        LuaApiModule.RegisterNamespace(L, "nUtil");
        LuaApiModule.RegisterNamespace(L, "nBit");
        LuaApiModule.RegisterNamespace(L, "nGameObject");
        LuaApiModule.RegisterNamespace(L, "nAttribute");
        LuaApiModule.RegisterNamespace(L, "nLocomotion");
        LuaApiModule.RegisterNamespace(L, "nObjectManager");
        LuaApiModule.RegisterNamespace(L, "nPlayer");
        LuaApiModule.RegisterNamespace(L, "nEvent");
        NAgentModule.Register(L);
        LuaApiModule.RegisterNamespace(L, "nDebug");
        LuaApiModule.RegisterNamespace(L, "nMathUtil");
        LuaApiModule.RegisterNamespace(L, "nGameSimulator");
        LuaApiModule.RegisterNamespace(L, "nLevel");
        LuaApiModule.RegisterNamespace(L, "nAffix");
        LuaApiModule.RegisterNamespace(L, "nJuggernaut");
        LuaApiModule.RegisterNamespace(L, "nTuning");
    }
}
