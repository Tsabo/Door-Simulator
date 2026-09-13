namespace DoorSim.Shared.Models;

/// <summary>Aggregate counters for one door, or for every door combined when <paramref name="DoorId" /> is 0.</summary>
/// <param name="EventsPerMinute">Throughput across the observed window (first event to last).</param>
/// <param name="AvgGapSeconds">Mean spacing between consecutive events — answers "every few seconds or minutes?".</param>
public record DoorMetrics(
    int DoorId,
    string Label,
    int Total,
    int Success,
    int Cancelled,
    int Error,
    double AvgDurationMs,
    double AvgQueueWaitMs,
    double EventsPerMinute,
    double AvgGapSeconds,
    double? MinGapSeconds,
    double? MaxGapSeconds,
    DateTimeOffset? FirstEventAt,
    DateTimeOffset? LastEventAt);

public record KindCount(SimulationEventKind Kind, int Count);

public record CardUsage(uint CardNumber, ushort FacilityCode, WiegandFormat Format, int Count);

public record FormatUsage(WiegandFormat Format, int Count);

public record ProtocolUsage(ProtocolType Protocol, int Count);

/// <param name="DayOfWeek">0 = Sunday, matching <see cref="System.DayOfWeek" />.</param>
public record HeatmapCell(int DayOfWeek, int Hour, int Count);

public record MetricsBucket(DateTimeOffset BucketStart, int Total, int Success, int Cancelled, int Error);

/// <summary>
/// Transport availability for one door, sampled in memory since process start — not persisted.
/// </summary>
public record ConnectivityMetrics(
    int DoorId,
    string Label,
    ProtocolType Protocol,
    bool IsConnected,
    double UptimePercent,
    int DisconnectCount,
    DateTimeOffset? LastDisconnectAt,
    TimeSpan Observed);

public record SimulationMetricsSummary(
    DoorMetrics Totals,
    DoorMetrics[] PerDoor,
    KindCount[] ByKind,
    CardUsage[] TopCards,
    FormatUsage[] ByFormat,
    ProtocolUsage[] ByProtocol,
    ConnectivityMetrics[] Connectivity,
    DateTimeOffset GeneratedAt);
