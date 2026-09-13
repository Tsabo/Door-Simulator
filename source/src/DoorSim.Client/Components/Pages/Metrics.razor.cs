using MudBlazor;

namespace DoorSim.Client.Components.Pages;

public partial class Metrics : IDisposable
{
    private static readonly string[] DayLabels = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

    private readonly CancellationTokenSource _cts = new();
    private string[] _chartLabels = [];

    private List<ChartSeries<double>> _chartSeries = [];

    private ConnectivityMetrics[] _connectivity = [];

    private int? _doorFilter;
    private DoorConfiguration[] _doors = [];
    private string? _error;
    private HeatmapCell[] _heatmap = [];
    private bool _loading = true;
    private int _maxHeat;
    private SimulationEventRecord[] _recent = [];
    private MetricsBucket[] _series = [];
    private SimulationMetricsSummary? _summary;
    private int _windowMinutes = 60;

    private DoorMetrics? _busiest => _summary?.PerDoor.MaxBy(d => d.Total);

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _doors = await DoorClient.GetAllAsync();
        }
        catch (Exception ex)
        {
            _error = $"Failed to load door list: {ex.Message}";
        }

        await RefreshAsync();
        _ = AutoRefreshAsync();
    }

    private async Task AutoRefreshAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));

        try
        {
            while (await timer.WaitForNextTickAsync(_cts.Token))
            {
                await RefreshAsync();
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException)
        {
            // Page was disposed
        }
    }

    private async Task RefreshAsync()
    {
        _loading = true;
        _error = null;

        try
        {
            _summary = await MetricsClient.GetSummaryAsync(_doorFilter);
            _series = await MetricsClient.GetTimeSeriesAsync(BucketMinutesFor(_windowMinutes), _windowMinutes, _doorFilter);
            _heatmap = await MetricsClient.GetHeatmapAsync(doorId: _doorFilter);
            _recent = await MetricsClient.GetRecentEventsAsync(100, _doorFilter);
            _connectivity = _summary?.Connectivity ?? [];
            _maxHeat = _heatmap.Length > 0
                ? _heatmap.Max(c => c.Count)
                : 0;

            BuildChart();
        }
        catch (Exception ex)
        {
            _error = $"Failed to load telemetry: {ex.Message}";
        }
        finally
        {
            _loading = false;
        }
    }

    private void BuildChart()
    {
        _chartSeries =
        [
            new ChartSeries<double>([.. _series.Select(b => (double)b.Total)]) { Name = "Total" },
            new ChartSeries<double>([.. _series.Select(b => (double)b.Error)]) { Name = "Errors" },
        ];

        // Only label a handful of points, or the axis becomes an unreadable smear.
        var step = Math.Max(1, _series.Length / 8);
        _chartLabels =
        [
            .. _series.Select((b, i) => i % step == 0
                ? b.BucketStart.ToLocalTime()
                    .ToString(_windowMinutes > 1440
                        ? "MMM d"
                        : "HH:mm")
                : string.Empty),
        ];
    }

    /// <summary>Keeps the series around 30–60 points regardless of the selected window.</summary>
    private static int BucketMinutesFor(int windowMinutes) => windowMinutes switch
    {
        <= 60 => 2,
        <= 360 => 10,
        <= 1440 => 30,
        var _ => 240,
    };

    private async Task OnDoorFilterChanged(int? doorId)
    {
        _doorFilter = doorId;
        await RefreshAsync();
    }

    private async Task OnWindowChanged(int windowMinutes)
    {
        _windowMinutes = windowMinutes;
        await RefreshAsync();
    }

    private async Task PurgeAsync()
    {
        await MetricsClient.PurgeAsync();
        await RefreshAsync();
    }

    private int HeatCount(int day, int hour) =>
        _heatmap.FirstOrDefault(c => c.DayOfWeek == day && c.Hour == hour)?.Count ?? 0;

    private string HeatStyle(int count)
    {
        if (count == 0 || _maxHeat == 0)
            return "background: var(--mud-palette-lines-default);";

        var intensity = 0.15 + 0.85 * count / _maxHeat;
        return $"background: rgba(var(--mud-palette-primary-rgb), {intensity.ToString("0.##", CultureInfo.InvariantCulture)});";
    }

    private string ConnectivityLabel(int doorId)
    {
        var entry = _connectivity.FirstOrDefault(c => c.DoorId == doorId);
        if (entry is null)
            return "—";

        return entry.DisconnectCount > 0
            ? $"{entry.UptimePercent:0.#}% ({entry.DisconnectCount} drop{(entry.DisconnectCount == 1 ? "" : "s")})"
            : $"{entry.UptimePercent:0.#}%";
    }

    private static string DayLabel(int day) => DayLabels[day];

    private static string CredentialLabel(SimulationEventRecord record)
    {
        if (record.RawBits is not null)
            return $"{record.RawBits.Length} raw bits";

        return record.CardNumber.HasValue
            ? $"#{record.CardNumber} FC:{record.FacilityCode} ({record.Format})"
            : "—";
    }

    private static Color OutcomeColor(SimulationEventOutcome outcome) => outcome switch
    {
        SimulationEventOutcome.Success => Color.Success,
        SimulationEventOutcome.Cancelled => Color.Warning,
        var _ => Color.Error,
    };

    private static string FormatGap(double seconds) => seconds switch
    {
        <= 0 => "—",
        < 60 => $"{seconds:0.#}s",
        < 3600 => $"{seconds / 60:0.#}m",
        var _ => $"{seconds / 3600:0.#}h",
    };

    private static string SplitPascal(string value) =>
        string.Concat(value.Select((c, i) => i > 0 && char.IsUpper(c)
            ? " " + c
            : c.ToString()));
}
