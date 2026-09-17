namespace DoorSim.Data.Entities;

/// <summary>Persisted telemetry row for a single completed simulation.</summary>
public class SimulationEventEntity
{
    public int Id { get; set; }

    public Guid ItemId { get; set; }

    public int DoorId { get; set; }

    public string Label { get; set; } = string.Empty;

    public ProtocolType Protocol { get; set; }

    public SimulationEventKind Kind { get; set; }

    public SimulationEventOutcome Outcome { get; set; }

    public int? CardEntryId { get; set; }

    public uint? CardNumber { get; set; }

    public ushort? FacilityCode { get; set; }

    public WiegandFormat? Format { get; set; }

    public int? CustomFormatId { get; set; }

    public string? RawBits { get; set; }

    public DateTimeOffset EnqueuedAt { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset CompletedAt { get; set; }

    public int QueueWaitMs { get; set; }

    public int DurationMs { get; set; }

    public int QueueDepthAtEnqueue { get; set; }

    public string? Error { get; set; }

    public SimulationEventRecord ToDto() => new(
        ItemId, DoorId, Label, Protocol, Kind, Outcome,
        CardEntryId, CardNumber, FacilityCode, Format, RawBits,
        EnqueuedAt, StartedAt, CompletedAt,
        QueueWaitMs, DurationMs, QueueDepthAtEnqueue, Error, CustomFormatId);

    public static SimulationEventEntity FromDto(SimulationEventRecord dto) => new()
    {
        ItemId = dto.Id,
        DoorId = dto.DoorId,
        Label = dto.Label,
        Protocol = dto.Protocol,
        Kind = dto.Kind,
        Outcome = dto.Outcome,
        CardEntryId = dto.CardEntryId,
        CardNumber = dto.CardNumber,
        FacilityCode = dto.FacilityCode,
        Format = dto.Format,
        CustomFormatId = dto.CustomFormatId,
        RawBits = dto.RawBits,
        EnqueuedAt = dto.EnqueuedAt,
        StartedAt = dto.StartedAt,
        CompletedAt = dto.CompletedAt,
        QueueWaitMs = dto.QueueWaitMs,
        DurationMs = dto.DurationMs,
        QueueDepthAtEnqueue = dto.QueueDepthAtEnqueue,
        Error = dto.Error
    };
}
