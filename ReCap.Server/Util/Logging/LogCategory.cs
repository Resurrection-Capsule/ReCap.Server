using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace ReCap.Server.Util.Logging;

public sealed class LogCategory
{
    const string MessageProperty = "Msg";
    readonly string _name;

    public LogCategory(string name) => _name = name;

    ILogger Sink => Serilog.Log.ForContext(Constants.SourceContextPropertyName, _name);

    public bool IsEnabled(LogEventLevel level) => Sink.IsEnabled(level);

    public void Verbose(string message) => Sink.Verbose("{" + MessageProperty + "}", message);
    public void Debug(string message) => Sink.Debug("{" + MessageProperty + "}", message);
    public void Info(string message) => Sink.Information("{" + MessageProperty + "}", message);
    public void Warn(string message) => Sink.Warning("{" + MessageProperty + "}", message);
    public void Error(string message) => Sink.Error("{" + MessageProperty + "}", message);
    public void Error(Exception ex, string message) => Sink.Error(ex, "{" + MessageProperty + "}", message);
    public void Fatal(string message) => Sink.Fatal("{" + MessageProperty + "}", message);
    public void Fatal(Exception ex, string message) => Sink.Fatal(ex, "{" + MessageProperty + "}", message);
}
