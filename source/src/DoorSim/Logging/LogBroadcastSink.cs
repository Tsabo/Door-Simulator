using Serilog.Core;
using Serilog.Events;

namespace DoorSim.Logging;

/// <summary>Serilog sink that forwards every log event into the in-memory <see cref="LogEventBus" /> for the live viewer.</summary>
public sealed class LogBroadcastSink(LogEventBus bus) : ILogEventSink
{
    // Serilog's default rendering quotes and JSON-escapes string properties embedded in a message
    // template unless the template uses the ":l" literal specifier. EF Core's CommandExecuted
    // template doesn't, so logEvent.RenderMessage() mangles the SQL text. Capturing the raw
    // property value here (before it's template-rendered) sidesteps that entirely.
    private const int MinCapturedValueLength = 50;

    public void Emit(LogEvent logEvent)
    {
        string? sourceContext = null;
        if (logEvent.Properties.TryGetValue("SourceContext", out var sc))
            sourceContext = sc.ToString().Trim('"');

        bus.Publish(new LogLine(
            logEvent.Timestamp,
            MapLevel(logEvent.Level),
            sourceContext,
            logEvent.RenderMessage(),
            logEvent.Exception?.ToString(),
            CaptureRawProperties(logEvent)));
    }

    private static IReadOnlyDictionary<string, string>? CaptureRawProperties(LogEvent logEvent)
    {
        Dictionary<string, string>? result = null;

        foreach (var (key, value) in logEvent.Properties)
        {
            if (key == "SourceContext")
                continue;

            if (value is not ScalarValue { Value: string raw })
                continue;

            if (raw.Length < MinCapturedValueLength && !raw.Contains('\n'))
                continue;

            (result ??= [])[key] = raw;
        }

        return result;
    }

    private static LogSeverity MapLevel(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose => LogSeverity.Verbose,
        LogEventLevel.Debug => LogSeverity.Debug,
        LogEventLevel.Information => LogSeverity.Information,
        LogEventLevel.Warning => LogSeverity.Warning,
        LogEventLevel.Error => LogSeverity.Error,
        LogEventLevel.Fatal => LogSeverity.Fatal,
        var _ => LogSeverity.Information,
    };
}
