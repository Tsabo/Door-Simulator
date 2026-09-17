namespace DoorSim.Services;

/// <summary>
/// Telemetry detail attached to a queued work item so the completed record can
/// identify which credential was transmitted.
/// </summary>
public record SimulationEventContext(
    SimulationEventKind Kind,
    int? CardEntryId = null,
    uint? CardNumber = null,
    ushort? FacilityCode = null,
    WiegandFormat? Format = null,
    string? RawBits = null,
    int? CustomFormatId = null);
