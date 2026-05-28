using Serilog.Context;

namespace ReCap.Server.Util.Logging;

public static class LogScope
{
    public static IDisposable Player(string name) => LogContext.PushProperty("Player", name);

    public static IDisposable Phase(object phase) => LogContext.PushProperty("Phase", phase);

    public static IDisposable Peer(ulong guid) => LogContext.PushProperty("Player", $"0x{guid:X16}");
}
