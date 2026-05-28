namespace ReCap.Server.Util.Logging;

public static class Log
{
    public static LogCategory Server { get; } = new(LogCategories.Server);
    public static LogCategory RakNet { get; } = new(LogCategories.RakNet);
    public static LogCategory Blaze { get; } = new(LogCategories.Blaze);
    public static LogCategory Rest { get; } = new(LogCategories.Rest);
    public static LogCategory Db { get; } = new(LogCategories.Db);
    public static LogCategory Assets { get; } = new(LogCategories.Assets);
    public static LogCategory Game { get; } = new(LogCategories.Game);
}
