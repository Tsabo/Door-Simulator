namespace DoorSim.Shared.Models;

/// <summary>Log severity, mirroring Serilog.Events.LogEventLevel without referencing Serilog from Shared.</summary>
public enum LogSeverity
{
    Verbose,
    Debug,
    Information,
    Warning,
    Error,
    Fatal,
}
