using DoorSim.Validation;

namespace DoorSim.Endpoints;

public static class MetricsEndpoints
{
    public static IEndpointRouteBuilder MapMetricsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/metrics").WithTags("Metrics");

        group.MapGet("/summary", async (SimulationMetricsService metrics,
                int? doorId, DateTimeOffset? from, DateTimeOffset? to, int topCards = 10) =>
            {
                var validationError = MetricsValidation.ValidateLimit(topCards);
                if (validationError is not null)
                    return Results.BadRequest(validationError);

                return Results.Ok(await metrics.GetSummaryAsync(doorId, from, to, topCards));
            })
            .WithName("GetMetricsSummary")
            .WithSummary("Aggregate simulation telemetry across all readers.")
            .WithDescription(
                "Returns combined and per-door counts, outcome breakdown, average duration and queue wait, " +
                "throughput (events per minute) and the min/average/max gap between consecutive events, plus " +
                "usage broken down by event kind, card, Wiegand format and protocol. Optionally filtered by " +
                "door and a completed-at time range.")
            .Produces<SimulationMetricsSummary>()
            .Produces<string>(StatusCodes.Status400BadRequest);

        group.MapGet("/timeseries", async (SimulationMetricsService metrics,
                int? doorId, int bucketMinutes = 5, int windowMinutes = 60) =>
            {
                var validationError = MetricsValidation.ValidateTimeSeries(bucketMinutes, windowMinutes);
                if (validationError is not null)
                    return Results.BadRequest(validationError);

                return Results.Ok(await metrics.GetTimeSeriesAsync(bucketMinutes, windowMinutes, doorId));
            })
            .WithName("GetMetricsTimeSeries")
            .WithSummary("Event counts bucketed over a trailing time window.")
            .WithDescription("Empty buckets are included so idle periods stay visible on a chart.")
            .Produces<MetricsBucket[]>()
            .Produces<string>(StatusCodes.Status400BadRequest);

        group.MapGet("/heatmap", async (SimulationMetricsService metrics, int? doorId, int windowDays = 30) =>
            {
                var validationError = MetricsValidation.ValidateWindowDays(windowDays);
                if (validationError is not null)
                    return Results.BadRequest(validationError);

                return Results.Ok(await metrics.GetHeatmapAsync(doorId, windowDays));
            })
            .WithName("GetMetricsHeatmap")
            .WithSummary("Activity counts binned by day-of-week and hour-of-day (server local time).")
            .Produces<HeatmapCell[]>()
            .Produces<string>(StatusCodes.Status400BadRequest);

        group.MapGet("/cards", async (SimulationMetricsService metrics, int limit = 10) =>
            {
                var validationError = MetricsValidation.ValidateLimit(limit);
                if (validationError is not null)
                    return Results.BadRequest(validationError);

                return Results.Ok(await metrics.GetTopCardsAsync(limit));
            })
            .WithName("GetTopCards")
            .WithSummary("Most frequently transmitted credentials, by card number, facility code and format.")
            .Produces<CardUsage[]>()
            .Produces<string>(StatusCodes.Status400BadRequest);

        group.MapGet("/recent", async (SimulationMetricsService metrics, int? doorId, int limit = 100) =>
            {
                var validationError = MetricsValidation.ValidateLimit(limit);
                if (validationError is not null)
                    return Results.BadRequest(validationError);

                return Results.Ok(await metrics.GetRecentAsync(doorId, limit));
            })
            .WithName("GetRecentEvents")
            .WithSummary("The most recently completed simulations, newest first.")
            .Produces<SimulationEventRecord[]>()
            .Produces<string>(StatusCodes.Status400BadRequest);

        group.MapGet("/connectivity", (SimulationMetricsService metrics) =>
                Results.Ok(metrics.GetConnectivity()))
            .WithName("GetConnectivityMetrics")
            .WithSummary("Per-reader transport uptime and disconnect count.")
            .WithDescription("Sampled in memory since the process started — these counters reset on restart, " +
                             "and are only meaningful for OSDP readers (Wiegand is always connected).")
            .Produces<ConnectivityMetrics[]>();

        group.MapDelete("/", async (SimulationMetricsService metrics, int? olderThanDays) =>
            {
                if (olderThanDays.HasValue)
                {
                    var validationError = MetricsValidation.ValidateWindowDays(olderThanDays.Value);
                    if (validationError is not null)
                        return Results.BadRequest(validationError);
                }

                await metrics.PurgeAsync(olderThanDays.HasValue
                    ? DateTimeOffset.UtcNow.AddDays(-olderThanDays.Value)
                    : null);

                return Results.NoContent();
            })
            .WithName("PurgeMetrics")
            .WithSummary("Delete recorded telemetry events.")
            .WithDescription("Omit olderThanDays to delete the entire event log. In-memory connectivity " +
                             "counters are not affected.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<string>(StatusCodes.Status400BadRequest);

        return app;
    }
}
