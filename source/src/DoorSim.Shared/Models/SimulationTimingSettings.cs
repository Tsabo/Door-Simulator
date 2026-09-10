namespace DoorSim.Shared.Models;

/// <summary>
/// Global timing parameters applied to all door simulation cycles.
/// Stored as a single row in the database; defaults match a realistic walk-through scenario.
/// </summary>
public record SimulationTimingSettings(
    int CardToDoorDelayMs = 500,
    int DoorOpenMs        = 5000,
    int RexLeadMs         = 500,
    int QuickRexMs        = 2000);
