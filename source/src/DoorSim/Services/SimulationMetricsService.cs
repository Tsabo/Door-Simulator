using System.Threading.Channels;

namespace DoorSim.Services;

/// <summary>
/// Singleton telemetry sink for simulation activity.
/// <see cref="Record" /> is called from the reader work queues and the orchestrator's direct
/// primitives; it never blocks or throws back into a running simulation. Events are drained by a
/// background writer into the <c>SimulationEvents</c> table, which is the single source of truth
/// for every aggregate this service reports.
/// Reader connectivity is the one exception — it is sampled in memory since process start,
/// because persisting a row per sample would hammer the Pi's SD card.
/// </summary>
public sealed class SimulationMetricsService(
    IServiceScopeFactory scopeFactory,
    IReaderBank bank,
    SimulationSettingsService settings,
    ILogger<SimulationMetricsService> logger) : IAsyncDisposable
{
    private const int MaxBatchSize = 256;
    private static readonly TimeSpan ConnectivitySampleInterval = TimeSpan.FromSeconds(2);

    private readonly Channel<SimulationEventRecord> _channel =
        Channel.CreateUnbounded<SimulationEventRecord>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

    private readonly ConcurrentDictionary<int, ConnectivityTracker> _connectivity = new();
    private readonly CancellationTokenSource _shutdownCts = new();
    private long _queued;
    private Task? _samplerTask;

    private Task? _writerTask;
    private long _written;

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        await _shutdownCts.CancelAsync().ConfigureAwait(false);

        foreach (var task in new[] { _writerTask, _samplerTask })
        {
            if (task is null)
                continue;

            try
            {
                await task.ConfigureAwait(false);
            }
            catch
            {
                // Ignore exceptions during teardown
            }
        }

        _shutdownCts.Dispose();
    }

    /// <summary>
    /// Purges past-retention rows, then starts the background writer and connectivity sampler.
    /// Call once at startup, after settings have loaded.
    /// </summary>
    public async Task LoadAsync()
    {
        var retentionDays = settings.Current.MetricsRetentionDays;
        if (retentionDays > 0)
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays);
            var purged = await PurgeAsync(cutoff).ConfigureAwait(false);
            logger.LogInformation(
                "Metrics retention {Days} day(s) — purged {Count} event(s) older than {Cutoff:u}",
                retentionDays, purged, cutoff);
        }
        else
            logger.LogInformation("Metrics retention disabled — telemetry is kept indefinitely");

        _writerTask = Task.Run(WriteLoopAsync);
        _samplerTask = Task.Run(SampleConnectivityLoopAsync);
    }

    /// <summary>
    /// Queues a completed simulation for persistence. Safe to call from a hardware timing path:
    /// it does no I/O, never blocks, and swallows its own failures.
    /// </summary>
    public void Record(SimulationEventRecord record)
    {
        try
        {
            Interlocked.Increment(ref _queued);
            if (!_channel.Writer.TryWrite(record))
                Interlocked.Increment(ref _written); // Channel closed — keep the flush counter balanced.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to queue telemetry for door {D}", record.DoorId);
        }
    }

    /// <summary>Drops the in-memory connectivity tracker for a door that has been removed.</summary>
    public void RemoveDoor(int doorId) => _connectivity.TryRemove(doorId, out var _);

    // -------------------------------------------------------------------------
    // Queries
    // -------------------------------------------------------------------------

    public async Task<SimulationMetricsSummary> GetSummaryAsync(int? doorId = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int topCards = 10)
    {
        var events = await QueryAsync(doorId, from, to).ConfigureAwait(false);

        var perDoor = events
            .GroupBy(p => p.DoorId)
            .Select(p => BuildDoorMetrics(p.Key, p.Last().Label, p))
            .OrderByDescending(p => p.Total)
            .ToArray();

        var byKind = events
            .GroupBy(p => p.Kind)
            .Select(p => new KindCount(p.Key, p.Count()))
            .OrderByDescending(p => p.Count)
            .ToArray();

        var cardReads = events.Where(p => p is { CardNumber: not null, Format: not null }).ToList();

        var topCardUsage = cardReads
            .GroupBy(p => (p.CardNumber!.Value, p.FacilityCode ?? 0, p.Format!.Value))
            .Select(p => new CardUsage(p.Key.Item1, p.Key.Item2, p.Key.Item3, p.Count()))
            .OrderByDescending(p => p.Count)
            .Take(topCards)
            .ToArray();

        var byFormat = cardReads
            .GroupBy(p => p.Format!.Value)
            .Select(p => new FormatUsage(p.Key, p.Count()))
            .OrderByDescending(p => p.Count)
            .ToArray();

        var byProtocol = events
            .GroupBy(p => p.Protocol)
            .Select(p => new ProtocolUsage(p.Key, p.Count()))
            .OrderByDescending(p => p.Count)
            .ToArray();

        return new SimulationMetricsSummary(
            BuildDoorMetrics(0, "All doors", events),
            perDoor,
            byKind,
            topCardUsage,
            byFormat,
            byProtocol,
            GetConnectivity(),
            DateTimeOffset.UtcNow);
    }

    public async Task<SimulationEventRecord[]> GetRecentAsync(int? doorId = null, int limit = 100)
    {
        await FlushAsync().ConfigureAwait(false);
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DoorSimDbContext>();

        var query = db.SimulationEvents.AsNoTracking();
        if (doorId.HasValue)
            query = query.Where(p => p.DoorId == doorId.Value);

        var rows = await query
            .OrderByDescending(p => p.CompletedAt)
            .Take(limit)
            .ToListAsync()
            .ConfigureAwait(false);

        return [.. rows.Select(r => r.ToDto())];
    }

    public async Task<MetricsBucket[]> GetTimeSeriesAsync(int bucketMinutes = 5,
        int windowMinutes = 60,
        int? doorId = null)
    {
        var now = DateTimeOffset.UtcNow;
        var windowStart = now.AddMinutes(-windowMinutes);
        var events = await QueryAsync(doorId, windowStart, now).ConfigureAwait(false);

        var bucketTicks = TimeSpan.FromMinutes(bucketMinutes).Ticks;
        var counts = events
            .GroupBy(p => new DateTimeOffset(p.CompletedAt.UtcTicks / bucketTicks * bucketTicks, TimeSpan.Zero))
            .ToDictionary(p => p.Key, p => p.ToList());

        // Emit empty buckets too, so the chart shows idle gaps rather than compressing them away.
        var firstBucket = new DateTimeOffset(windowStart.UtcTicks / bucketTicks * bucketTicks, TimeSpan.Zero);
        var buckets = new List<MetricsBucket>();
        for (var start = firstBucket; start <= now; start = start.AddTicks(bucketTicks))
        {
            counts.TryGetValue(start, out var inBucket);
            buckets.Add(new MetricsBucket(
                start,
                inBucket?.Count ?? 0,
                inBucket?.Count(p => p.Outcome == SimulationEventOutcome.Success) ?? 0,
                inBucket?.Count(p => p.Outcome == SimulationEventOutcome.Cancelled) ?? 0,
                inBucket?.Count(p => p.Outcome == SimulationEventOutcome.Error) ?? 0));
        }

        return [.. buckets];
    }

    public async Task<HeatmapCell[]> GetHeatmapAsync(int? doorId = null, int windowDays = 30)
    {
        var events = await QueryAsync(doorId, DateTimeOffset.UtcNow.AddDays(-windowDays), null).ConfigureAwait(false);

        return
        [
            .. events
                .GroupBy(p => (Day: (int)p.CompletedAt.ToLocalTime().DayOfWeek, p.CompletedAt.ToLocalTime().Hour))
                .Select(p => new HeatmapCell(p.Key.Day, p.Key.Hour, p.Count()))
                .OrderBy(p => p.DayOfWeek)
                .ThenBy(p => p.Hour),
        ];
    }

    public async Task<CardUsage[]> GetTopCardsAsync(int limit = 10)
    {
        var summary = await GetSummaryAsync(topCards: limit).ConfigureAwait(false);
        return summary.TopCards;
    }

    /// <summary>Current transport availability per door, measured since this process started.</summary>
    public ConnectivityMetrics[] GetConnectivity() =>
    [
        .. _connectivity
            .OrderBy(p => p.Key)
            .Select(p => p.Value.Snapshot(p.Key)),
    ];

    /// <summary>Deletes recorded events, optionally only those completed before <paramref name="olderThan" />.</summary>
    public async Task<int> PurgeAsync(DateTimeOffset? olderThan = null)
    {
        await FlushAsync().ConfigureAwait(false);
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DoorSimDbContext>();

        var query = db.SimulationEvents.AsQueryable();
        if (olderThan.HasValue)
            query = query.Where(e => e.CompletedAt < olderThan.Value);

        return await query.ExecuteDeleteAsync().ConfigureAwait(false);
    }

    // -------------------------------------------------------------------------
    // Background workers
    // -------------------------------------------------------------------------

    private async Task WriteLoopAsync()
    {
        var reader = _channel.Reader;
        var batch = new List<SimulationEventRecord>(MaxBatchSize);

        try
        {
            while (await reader.WaitToReadAsync(_shutdownCts.Token).ConfigureAwait(false))
            {
                batch.Clear();
                while (batch.Count < MaxBatchSize && reader.TryRead(out var record))
                    batch.Add(record);

                if (batch.Count == 0)
                    continue;

                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetRequiredService<DoorSimDbContext>();

                    db.SimulationEvents.AddRange(batch.Select(SimulationEventEntity.FromDto));
                    await db.SaveChangesAsync(_shutdownCts.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to persist {Count} telemetry event(s)", batch.Count);
                }
                finally
                {
                    // Advance even on failure, or FlushAsync would wait forever.
                    Interlocked.Add(ref _written, batch.Count);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Telemetry writer loop stopped unexpectedly");
        }
    }

    private async Task SampleConnectivityLoopAsync()
    {
        using var timer = new PeriodicTimer(ConnectivitySampleInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(_shutdownCts.Token).ConfigureAwait(false))
            {
                foreach (var doorId in bank.ActiveDoorIds)
                {
                    if (!bank.TryGetReader(doorId, out var sim))
                        continue;

                    _connectivity
                        .GetOrAdd(doorId, _ => new ConnectivityTracker())
                        .Sample(sim.IsConnected, sim.Config.Label, sim.Config.Protocol);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Connectivity sampler stopped unexpectedly");
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>Waits until everything queued so far has been handed to the database.</summary>
    private async Task FlushAsync()
    {
        var target = Interlocked.Read(ref _queued);
        while (Interlocked.Read(ref _written) < target && !_shutdownCts.IsCancellationRequested)
            await Task.Delay(15).ConfigureAwait(false);
    }

    private async Task<List<SimulationEventRecord>> QueryAsync(int? doorId,
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        await FlushAsync().ConfigureAwait(false);
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DoorSimDbContext>();

        var query = db.SimulationEvents.AsNoTracking();
        if (doorId.HasValue)
            query = query.Where(p => p.DoorId == doorId.Value);

        if (from.HasValue)
            query = query.Where(p => p.CompletedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(p => p.CompletedAt <= to.Value);

        var rows = await query
            .OrderBy(p => p.CompletedAt)
            .ToListAsync()
            .ConfigureAwait(false);

        return [.. rows.Select(p => p.ToDto())];
    }

    internal static DoorMetrics BuildDoorMetrics(int doorId, string label, IEnumerable<SimulationEventRecord> events)
    {
        var ordered = events.OrderBy(p => p.CompletedAt).ToList();
        if (ordered.Count == 0)
            return new DoorMetrics(doorId, label, 0, 0, 0, 0, 0, 0, 0, 0, null, null, null, null);

        var gaps = ordered
            .Zip(ordered.Skip(1), (a, b) => (b.CompletedAt - a.CompletedAt).TotalSeconds)
            .ToList();

        var first = ordered[0].CompletedAt;
        var last = ordered[^1].CompletedAt;
        var spanMinutes = (last - first).TotalMinutes;

        return new DoorMetrics(
            doorId,
            label,
            ordered.Count,
            ordered.Count(p => p.Outcome == SimulationEventOutcome.Success),
            ordered.Count(p => p.Outcome == SimulationEventOutcome.Cancelled),
            ordered.Count(p => p.Outcome == SimulationEventOutcome.Error),
            ordered.Average(p => p.DurationMs),
            ordered.Average(p => p.QueueWaitMs),
            spanMinutes > 0
                ? ordered.Count / spanMinutes
                : 0,
            gaps.Count > 0
                ? gaps.Average()
                : 0,
            gaps.Count > 0
                ? gaps.Min()
                : null,
            gaps.Count > 0
                ? gaps.Max()
                : null,
            first,
            last);
    }

    /// <summary>Rolling availability counters for one reader, reset on process restart.</summary>
    private sealed class ConnectivityTracker
    {
        private readonly Lock _lock = new();
        private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
        private int _connectedSamples;
        private int _disconnectCount;

        private string _label = string.Empty;
        private DateTimeOffset? _lastDisconnectAt;
        private ProtocolType _protocol;
        private int _samples;
        private bool? _wasConnected;

        public void Sample(bool isConnected, string label, ProtocolType protocol)
        {
            lock (_lock)
            {
                _label = label;
                _protocol = protocol;
                _samples++;

                if (isConnected)
                    _connectedSamples++;

                if (_wasConnected == true && !isConnected)
                {
                    _disconnectCount++;
                    _lastDisconnectAt = DateTimeOffset.UtcNow;
                }

                _wasConnected = isConnected;
            }
        }

        public ConnectivityMetrics Snapshot(int doorId)
        {
            lock (_lock)
            {
                return new ConnectivityMetrics(
                    doorId,
                    _label,
                    _protocol,
                    _wasConnected ?? false,
                    _samples > 0
                        ? _connectedSamples * 100d / _samples
                        : 0,
                    _disconnectCount,
                    _lastDisconnectAt,
                    DateTimeOffset.UtcNow - _startedAt);
            }
        }
    }
}
