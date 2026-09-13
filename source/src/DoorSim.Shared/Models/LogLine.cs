namespace DoorSim.Shared.Models;

/// <summary>
/// One structured log event, as broadcast to the live log viewer over the SSE stream.
/// </summary>
public record LogLine(
    DateTimeOffset Timestamp,
    LogSeverity Level,
    string? SourceContext,
    string Message,
    string? Exception);
