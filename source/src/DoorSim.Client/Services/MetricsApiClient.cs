namespace DoorSim.Client.Services;

/// <summary>HTTP client wrapper for the /api/metrics endpoints.</summary>
public class MetricsApiClient(HttpClient http)
{
    public async Task<SimulationMetricsSummary?> GetSummaryAsync(int? doorId = null, int topCards = 10)
    {
        var query = $"/api/metrics/summary?topCards={topCards}" + (doorId.HasValue
            ? $"&doorId={doorId}"
            : string.Empty);

        return await http.GetFromJsonAsync<SimulationMetricsSummary>(query, DoorSimJson.Options);
    }

    public async Task<MetricsBucket[]> GetTimeSeriesAsync(int bucketMinutes, int windowMinutes, int? doorId = null)
    {
        var query = $"/api/metrics/timeseries?bucketMinutes={bucketMinutes}&windowMinutes={windowMinutes}"
                    + (doorId.HasValue
                        ? $"&doorId={doorId}"
                        : string.Empty);

        return await http.GetFromJsonAsync<MetricsBucket[]>(query, DoorSimJson.Options) ?? [];
    }

    public async Task<HeatmapCell[]> GetHeatmapAsync(int windowDays = 30, int? doorId = null)
    {
        var query = $"/api/metrics/heatmap?windowDays={windowDays}" + (doorId.HasValue
            ? $"&doorId={doorId}"
            : string.Empty);

        return await http.GetFromJsonAsync<HeatmapCell[]>(query, DoorSimJson.Options) ?? [];
    }

    public async Task<SimulationEventRecord[]> GetRecentEventsAsync(int limit = 100, int? doorId = null)
    {
        var query = $"/api/metrics/recent?limit={limit}" + (doorId.HasValue
            ? $"&doorId={doorId}"
            : string.Empty);

        return await http.GetFromJsonAsync<SimulationEventRecord[]>(query, DoorSimJson.Options) ?? [];
    }

    public async Task<ConnectivityMetrics[]> GetConnectivityAsync() =>
        await http.GetFromJsonAsync<ConnectivityMetrics[]>("/api/metrics/connectivity", DoorSimJson.Options) ?? [];

    public async Task PurgeAsync(int? olderThanDays = null)
    {
        var query = "/api/metrics" + (olderThanDays.HasValue
            ? $"?olderThanDays={olderThanDays}"
            : string.Empty);

        await http.DeleteAsync(query);
    }
}
