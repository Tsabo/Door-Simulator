namespace DoorSim.Shared.Models;

/// <summary>
/// One recorded simulation, persisted to the telemetry event log.
/// </summary>
/// <param name="Id">Identifier of the originating queue item, or a fresh id for direct primitives.</param>
/// <param name="DoorId">Door the simulation ran against.</param>
/// <param name="Label">Door label captured at record time, so metrics survive a door rename or deletion.</param>
/// <param name="QueueWaitMs">Time spent waiting in the reader queue before execution started.</param>
/// <param name="QueueDepthAtEnqueue">Backlog on this reader at the moment the item was enqueued.</param>
public record SimulationEventRecord(
    Guid Id,
    int DoorId,
    string Label,
    ProtocolType Protocol,
    SimulationEventKind Kind,
    SimulationEventOutcome Outcome,
    int? CardEntryId,
    uint? CardNumber,
    ushort? FacilityCode,
    WiegandFormat? Format,
    string? RawBits,
    DateTimeOffset EnqueuedAt,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    int QueueWaitMs,
    int DurationMs,
    int QueueDepthAtEnqueue,
    string? Error,
    int? CustomFormatId = null);
