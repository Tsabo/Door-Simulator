using System.ComponentModel;
using DoorSim.Validation;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace DoorSim.Mcp;

/// <summary>
/// MCP tools wrapping <see cref="SimulationMetricsService" /> — read-only telemetry queries, plus
/// purge. Since DoorSim cannot see PACS-side outcomes, these counts reflect what DoorSim itself
/// transmitted, not what the panel decided.
/// </summary>
[McpServerToolType]
internal static class MetricsTools
{
    [McpServerTool(Name = "get_metrics_summary", ReadOnly = true)]
    [Description(
        "Aggregate simulation telemetry across all readers: combined and per-door counts, outcome " +
        "breakdown, average duration and queue wait, throughput, and usage broken down by event kind, " +
        "card, Wiegand format and protocol. Optionally filtered by door and a completed-at time range.")]
    public static async Task<SimulationMetricsSummary> GetMetricsSummary(SimulationMetricsService metrics,
        int? doorId = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        [Description("How many top cards to include.")]
        int topCards = 10)
    {
        var validationError = MetricsValidation.ValidateLimit(topCards);
        if (validationError is not null)
            throw new McpException(validationError);

        return await metrics.GetSummaryAsync(doorId, from, to, topCards);
    }

    [McpServerTool(Name = "get_metrics_timeseries", ReadOnly = true)]
    [Description("Event counts bucketed over a trailing time window. Empty buckets are included so idle periods stay visible.")]
    public static async Task<MetricsBucket[]> GetMetricsTimeSeries(SimulationMetricsService metrics,
        int? doorId = null,
        [Description("Bucket width in minutes.")]
        int bucketMinutes = 5,
        [Description("Trailing window size in minutes.")]
        int windowMinutes = 60)
    {
        var validationError = MetricsValidation.ValidateTimeSeries(bucketMinutes, windowMinutes);
        if (validationError is not null)
            throw new McpException(validationError);

        return await metrics.GetTimeSeriesAsync(bucketMinutes, windowMinutes, doorId);
    }

    [McpServerTool(Name = "get_metrics_heatmap", ReadOnly = true)]
    [Description("Activity counts binned by day-of-week and hour-of-day (server local time).")]
    public static async Task<HeatmapCell[]> GetMetricsHeatmap(SimulationMetricsService metrics,
        int? doorId = null,
        [Description("Trailing window size in days.")]
        int windowDays = 30)
    {
        var validationError = MetricsValidation.ValidateWindowDays(windowDays);
        if (validationError is not null)
            throw new McpException(validationError);

        return await metrics.GetHeatmapAsync(doorId, windowDays);
    }

    [McpServerTool(Name = "get_top_cards", ReadOnly = true)]
    [Description("Most frequently transmitted credentials, by card number, facility code and format.")]
    public static async Task<CardUsage[]> GetTopCards(SimulationMetricsService metrics,
        [Description("Maximum number of cards to return.")]
        int limit = 10)
    {
        var validationError = MetricsValidation.ValidateLimit(limit);
        if (validationError is not null)
            throw new McpException(validationError);

        return await metrics.GetTopCardsAsync(limit);
    }

    [McpServerTool(Name = "get_recent_events", ReadOnly = true)]
    [Description("The most recently completed simulations, newest first.")]
    public static async Task<SimulationEventRecord[]> GetRecentEvents(SimulationMetricsService metrics,
        int? doorId = null,
        [Description("Maximum number of events to return.")]
        int limit = 100)
    {
        var validationError = MetricsValidation.ValidateLimit(limit);
        if (validationError is not null)
            throw new McpException(validationError);

        return await metrics.GetRecentAsync(doorId, limit);
    }

    [McpServerTool(Name = "get_connectivity_metrics", ReadOnly = true)]
    [Description(
        "Per-reader transport uptime and disconnect count, sampled in memory since the process started " +
        "— these counters reset on restart, and are only meaningful for OSDP readers (Wiegand is always connected).")]
    public static ConnectivityMetrics[] GetConnectivityMetrics(SimulationMetricsService metrics) =>
        metrics.GetConnectivity();

    [McpServerTool(Name = "purge_metrics")]
    [Description(
        "Delete recorded telemetry events. Omit olderThanDays to delete the entire event log. " +
        "Irreversible. In-memory connectivity counters are not affected.")]
    public static async Task<string> PurgeMetrics(SimulationMetricsService metrics,
        [Description("Only delete events older than this many days. Omit to delete everything.")]
        int? olderThanDays = null)
    {
        DateTimeOffset? cutoff = null;
        if (olderThanDays.HasValue)
        {
            var validationError = MetricsValidation.ValidateWindowDays(olderThanDays.Value);
            if (validationError is not null)
                throw new McpException(validationError);

            cutoff = DateTimeOffset.UtcNow.AddDays(-olderThanDays.Value);
        }

        var purged = await metrics.PurgeAsync(cutoff);

        return olderThanDays.HasValue
            ? $"Purged {purged} telemetry event(s) older than {olderThanDays} day(s)."
            : $"Purged {purged} telemetry event(s) — the entire event log.";
    }
}
