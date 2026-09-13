using DoorSim.Services;
using DoorSim.Shared.Models;

namespace DoorSim.Tests;

public class MetricsAggregationTests
{
    private static readonly DateTimeOffset Base = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task BuildDoorMetrics_NoEvents_ReturnsZeroedMetrics()
    {
        var metrics = SimulationMetricsService.BuildDoorMetrics(4, "Front", []);

        await Assert.That(metrics.Total).IsEqualTo(0);
        await Assert.That(metrics.EventsPerMinute).IsEqualTo(0);
        await Assert.That(metrics.FirstEventAt).IsNull();
        await Assert.That(metrics.MinGapSeconds).IsNull();
    }

    [Test]
    public async Task BuildDoorMetrics_SingleEvent_HasNoGaps()
    {
        var metrics = SimulationMetricsService.BuildDoorMetrics(4, "Front", [Event(0)]);

        await Assert.That(metrics.Total).IsEqualTo(1);
        await Assert.That(metrics.MinGapSeconds).IsNull();
        await Assert.That(metrics.MaxGapSeconds).IsNull();
        await Assert.That(metrics.AvgGapSeconds).IsEqualTo(0);
    }

    [Test]
    public async Task BuildDoorMetrics_ComputesGapStatistics()
    {
        // Completed at 0s, 10s, 40s → gaps of 10s and 30s.
        var metrics = SimulationMetricsService.BuildDoorMetrics(1, "Front",
            [Event(0), Event(10), Event(40)]);

        await Assert.That(metrics.Total).IsEqualTo(3);
        await Assert.That(metrics.MinGapSeconds).IsEqualTo(10);
        await Assert.That(metrics.MaxGapSeconds).IsEqualTo(30);
        await Assert.That(metrics.AvgGapSeconds).IsEqualTo(20);
    }

    [Test]
    public async Task BuildDoorMetrics_ComputesThroughputOverObservedSpan()
    {
        // 3 events spanning 60 seconds → 3 per minute.
        var metrics = SimulationMetricsService.BuildDoorMetrics(1, "Front",
            [Event(0), Event(30), Event(60)]);

        await Assert.That(metrics.EventsPerMinute).IsEqualTo(3);
    }

    [Test]
    public async Task BuildDoorMetrics_CountsOutcomesSeparately()
    {
        var metrics = SimulationMetricsService.BuildDoorMetrics(1, "Front",
        [
            Event(0),
            Event(1, SimulationEventOutcome.Error),
            Event(2, SimulationEventOutcome.Cancelled),
            Event(3, SimulationEventOutcome.Error),
        ]);

        await Assert.That(metrics.Total).IsEqualTo(4);
        await Assert.That(metrics.Success).IsEqualTo(1);
        await Assert.That(metrics.Error).IsEqualTo(2);
        await Assert.That(metrics.Cancelled).IsEqualTo(1);
    }

    [Test]
    public async Task BuildDoorMetrics_UnorderedInput_StillOrdersByCompletion()
    {
        var metrics = SimulationMetricsService.BuildDoorMetrics(1, "Front",
            [Event(40), Event(0), Event(10)]);

        await Assert.That(metrics.FirstEventAt).IsEqualTo(Base);
        await Assert.That(metrics.LastEventAt).IsEqualTo(Base.AddSeconds(40));
        await Assert.That(metrics.MinGapSeconds).IsEqualTo(10);
    }

    [Test]
    public async Task BuildDoorMetrics_AveragesDurationAndQueueWait()
    {
        var metrics = SimulationMetricsService.BuildDoorMetrics(1, "Front",
            [Event(0, durationMs: 100, queueWaitMs: 0), Event(1, durationMs: 300, queueWaitMs: 40)]);

        await Assert.That(metrics.AvgDurationMs).IsEqualTo(200);
        await Assert.That(metrics.AvgQueueWaitMs).IsEqualTo(20);
    }

    private static SimulationEventRecord Event(int offsetSeconds,
        SimulationEventOutcome outcome = SimulationEventOutcome.Success,
        int durationMs = 0,
        int queueWaitMs = 0)
    {
        var completedAt = Base.AddSeconds(offsetSeconds);
        return new SimulationEventRecord(
            Guid.NewGuid(), 1, "Front", ProtocolType.Wiegand,
            SimulationEventKind.CardReadOnly, outcome,
            null, null, null, null, null,
            completedAt, completedAt, completedAt,
            queueWaitMs, durationMs, 0, null);
    }
}
