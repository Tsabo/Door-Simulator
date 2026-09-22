using System.Diagnostics.CodeAnalysis;
using DoorSim.Data;
using DoorSim.Hardware;
using DoorSim.Mcp;
using DoorSim.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;

namespace DoorSim.Tests;

public class McpMetricsToolsTests
{
    [Test]
    public async Task GetMetricsSummary_InvalidTopCards_ThrowsMcpException()
    {
        using var fixture = new TestDbFixture();
        var metrics = fixture.MakeMetrics();

        await Assert.That(async () => await MetricsTools.GetMetricsSummary(metrics, topCards: 0))
            .Throws<McpException>()
            .WithMessageContaining("Limit must be between");
    }

    [Test]
    public async Task GetMetricsSummary_NoEvents_ReturnsEmptySummary()
    {
        using var fixture = new TestDbFixture();
        var metrics = fixture.MakeMetrics();

        var summary = await MetricsTools.GetMetricsSummary(metrics);

        await Assert.That(summary.Totals.Total).IsEqualTo(0);
    }

    [Test]
    public async Task GetMetricsTimeSeries_BucketExceedsWindow_ThrowsMcpException()
    {
        using var fixture = new TestDbFixture();
        var metrics = fixture.MakeMetrics();

        await Assert.That(async () => await MetricsTools.GetMetricsTimeSeries(metrics, bucketMinutes: 60, windowMinutes: 5))
            .Throws<McpException>()
            .WithMessageContaining("cannot exceed the window");
    }

    [Test]
    public async Task GetMetricsHeatmap_InvalidWindowDays_ThrowsMcpException()
    {
        using var fixture = new TestDbFixture();
        var metrics = fixture.MakeMetrics();

        await Assert.That(async () => await MetricsTools.GetMetricsHeatmap(metrics, windowDays: 0))
            .Throws<McpException>()
            .WithMessageContaining("Window must be between");
    }

    [Test]
    public async Task GetTopCards_NoEvents_ReturnsEmptyArray()
    {
        using var fixture = new TestDbFixture();
        var metrics = fixture.MakeMetrics();

        var top = await MetricsTools.GetTopCards(metrics);

        await Assert.That(top).IsEmpty();
    }

    [Test]
    public async Task GetRecentEvents_InvalidLimit_ThrowsMcpException()
    {
        using var fixture = new TestDbFixture();
        var metrics = fixture.MakeMetrics();

        await Assert.That(async () => await MetricsTools.GetRecentEvents(metrics, limit: 10_000))
            .Throws<McpException>()
            .WithMessageContaining("Limit must be between");
    }

    [Test]
    public async Task GetConnectivityMetrics_NoSamplesYet_ReturnsEmptyArray()
    {
        using var fixture = new TestDbFixture();
        var metrics = fixture.MakeMetrics();

        var connectivity = MetricsTools.GetConnectivityMetrics(metrics);

        await Assert.That(connectivity).IsEmpty();
    }

    [Test]
    public async Task PurgeMetrics_InvalidOlderThanDays_ThrowsMcpException()
    {
        using var fixture = new TestDbFixture();
        var metrics = fixture.MakeMetrics();

        await Assert.That(async () => await MetricsTools.PurgeMetrics(metrics, 0))
            .Throws<McpException>()
            .WithMessageContaining("Window must be between");
    }

    [Test]
    public async Task PurgeMetrics_NoEvents_ReturnsZeroCount()
    {
        using var fixture = new TestDbFixture();
        var metrics = fixture.MakeMetrics();

        var result = await MetricsTools.PurgeMetrics(metrics);

        await Assert.That(result).Contains("0");
    }

    private sealed class TestReaderBank : IReaderBank
    {
        public IReaderSimulator GetReader(int doorId) => throw new KeyNotFoundException();

        public bool TryGetReader(int doorId, [NotNullWhen(true)] out IReaderSimulator? simulator)
        {
            simulator = null;
            return false;
        }

        public bool ContainsReader(int doorId) => false;

        public IReadOnlyCollection<int> ActiveDoorIds => [];
    }

    private sealed class TestDbFixture : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _serviceProvider;

        public TestDbFixture()
        {
            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open();

            var services = new ServiceCollection();
            services.AddDbContext<DoorSimDbContext>(opt => opt.UseSqlite(_connection));
            _serviceProvider = services.BuildServiceProvider();

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DoorSimDbContext>();
            db.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _serviceProvider.Dispose();
            _connection.Dispose();
        }

        public SimulationMetricsService MakeMetrics()
        {
            var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
            var settings = new SimulationSettingsService(scopeFactory, NullLogger<SimulationSettingsService>.Instance);

            return new SimulationMetricsService(scopeFactory, new TestReaderBank(), settings, NullLogger<SimulationMetricsService>.Instance);
        }
    }
}
