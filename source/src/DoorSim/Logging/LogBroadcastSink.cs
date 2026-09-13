using Serilog.Core;
using Serilog.Events;

namespace DoorSim.Logging;

/// <summary>Serilog sink that forwards every log event into the in-memory <see cref="LogEventBus" /> for the live viewer.</summary>
public sealed class LogBroadcastSink(LogEventBus bus) : ILogEventSink
{
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
            logEvent.Exception?.ToString()));
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
