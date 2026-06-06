namespace ReCap.Server.Adapters.Scripting.Api;

public static class NMathUtilModule
{
    public static void Register(nint L) =>
        LuaApiModule.RegisterNamespace(L, "nMathUtil");
}
