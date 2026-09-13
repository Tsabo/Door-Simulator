namespace DoorSim.Shared.Models;

/// <summary>
/// Global timing parameters applied to all door simulation cycles.
/// Stored as a single row in the database; defaults match a realistic walk-through scenario.
/// </summary>
/// <param name="MetricsRetentionDays">Telemetry events older than this are purged at startup; 0 keeps them forever.</param>
/// <param name="QueueItemDelayMs">Delay between consecutive queued actions on the same reader.</param>
public record SimulationTimingSettings(
    int CardToDoorDelayMs = 500,
    int DoorOpenMs = 5000,
    int RexLeadMs = 500,
    int QuickRexMs = 2000,
    int MetricsRetentionDays = 30,
    int QueueItemDelayMs = 1000);
