namespace DoorSim.Shared.Models;

/// <summary>
/// One structured log event, as broadcast to the live log viewer over the SSE stream.
/// </summary>
/// <param name="Properties">
/// Raw (unescaped) values of selected Serilog scalar properties, keyed by property name. Populated
/// only for properties worth showing separately from <paramref name="Message" /> — e.g. EF Core's
/// logged SQL text, which Serilog's default rendering otherwise quotes/escapes inside Message.
/// </param>
public record LogLine(
    DateTimeOffset Timestamp,
    LogSeverity Level,
    string? SourceContext,
    string Message,
    string? Exception,
    IReadOnlyDictionary<string, string>? Properties = null);
