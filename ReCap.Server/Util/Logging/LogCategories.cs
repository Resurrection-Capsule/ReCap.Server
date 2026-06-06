namespace ReCap.Server.Util.Logging;

public static class LogCategories
{
    public const string Server = "Server";
    public const string RakNet = "RakNet";
    public const string Blaze = "Blaze";
    public const string Rest = "Rest";
    public const string Db = "Db";
    public const string Assets = "Assets";
    public const string Game = "Game";
    public const string Lua = "Lua";

    public static readonly string[] All = { Server, RakNet, Blaze, Rest, Db, Assets, Game, Lua };
}
