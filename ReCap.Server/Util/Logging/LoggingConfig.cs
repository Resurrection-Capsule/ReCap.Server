using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Templates;
using Serilog.Templates.Themes;

namespace ReCap.Server.Util.Logging;

public static class LoggingConfig
{
    const string Template =
        "[{@t:HH:mm:ss.fff} {@l:u3}] " +
        "{#if SourceContext is not null}{SourceContext,-6} {#end}" +
        "{#if Phase is not null}·{Phase} {#end}" +
        "{#if Player is not null}({Player}) {#end}" +
        "{Msg}\n{@x}";

    static readonly LoggingLevelSwitch _global = new(LogEventLevel.Information);
    static readonly Dictionary<string, LoggingLevelSwitch> _switches = new(StringComparer.OrdinalIgnoreCase);

    public static void Bootstrap(LogEventLevel globalLevel, IReadOnlyDictionary<string, LogEventLevel>? overrides = null)
    {
        _global.MinimumLevel = globalLevel;

        var cfg = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(_global)
            .Enrich.FromLogContext();

        if (overrides is not null)
        {
            foreach (var (category, level) in overrides)
            {
                var sw = new LoggingLevelSwitch(level);
                _switches[category] = sw;
                cfg.MinimumLevel.Override(category, sw);
            }
        }

        var logDir = Path.Combine(AppContext.BaseDirectory, "logs");

        cfg.WriteTo.Console(new ExpressionTemplate(Template, theme: TemplateTheme.Code));
        cfg.WriteTo.File(
            new ExpressionTemplate(Template),
            Path.Combine(logDir, "recap-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14,
            shared: true);

        Serilog.Log.Logger = cfg.CreateLogger();
    }

    public static void SetGlobalLevel(LogEventLevel level) => _global.MinimumLevel = level;

    public static void SetCategoryLevel(string category, LogEventLevel level)
    {
        if (_switches.TryGetValue(category, out var sw))
            sw.MinimumLevel = level;
    }

    public static void CloseAndFlush() => Serilog.Log.CloseAndFlush();

    public static bool TryParseLevel(string text, out LogEventLevel level)
    {
        switch (text.Trim().ToLowerInvariant())
        {
            case "verbose": case "trace": case "v": level = LogEventLevel.Verbose; return true;
            case "debug": case "d": level = LogEventLevel.Debug; return true;
            case "info": case "information": case "i": level = LogEventLevel.Information; return true;
            case "warn": case "warning": case "w": level = LogEventLevel.Warning; return true;
            case "error": case "err": case "e": level = LogEventLevel.Error; return true;
            case "fatal": case "f": level = LogEventLevel.Fatal; return true;
            case "off": case "none": case "silent": case "mute": level = LogEventLevel.Fatal; return true;
            default: level = LogEventLevel.Information; return false;
        }
    }
}
