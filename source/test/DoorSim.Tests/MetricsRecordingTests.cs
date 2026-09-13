using DoorSim.Services;
using DoorSim.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace DoorSim.Tests;

public class MetricsRecordingTests
{
    [Test]
    public async Task Queue_SuccessfulItem_RecordsSuccess()
    {
        var records = new List<SimulationEventRecord>();
        await using var queue = new ReaderWorkQueue(7, (_, _) => { }, NullLogger.Instance, records.Add);

        await queue.EnqueueAsync(_ => Task.CompletedTask, "Card Read", DoorEventType.CardReadOnly,
            telemetry: new SimulationEventContext(SimulationEventKind.CardReadOnly, CardNumber: 1234,
                FacilityCode: 7, Format: WiegandFormat.Wiegand26));

        await Assert.That(records.Count).IsEqualTo(1);
        await Assert.That(records[0].Outcome).IsEqualTo(SimulationEventOutcome.Success);
        await Assert.That(records[0].DoorId).IsEqualTo(7);
        await Assert.That(records[0].Kind).IsEqualTo(SimulationEventKind.CardReadOnly);
        await Assert.That(records[0].CardNumber).IsEqualTo((uint?)1234);
        await Assert.That(records[0].FacilityCode).IsEqualTo((ushort?)7);
        await Assert.That(records[0].Format).IsEqualTo(WiegandFormat.Wiegand26);
        await Assert.That(records[0].Error).IsNull();
        await Assert.That(records[0].DurationMs).IsGreaterThanOrEqualTo(0);
        await Assert.That(records[0].QueueWaitMs).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Queue_FailingItem_RecordsErrorWithMessage()
    {
        var records = new List<SimulationEventRecord>();
        await using var queue = new ReaderWorkQueue(3, (_, _) => { }, NullLogger.Instance, records.Add);

        var work = queue.EnqueueAsync(_ => throw new InvalidOperationException("no D0/D1 pins"),
            "Card Read", DoorEventType.CardReadOnly,
            telemetry: new SimulationEventContext(SimulationEventKind.CardReadOnly));

        await Assert.That(async () => await work).Throws<InvalidOperationException>();

        await Assert.That(records.Count).IsEqualTo(1);
        await Assert.That(records[0].Outcome).IsEqualTo(SimulationEventOutcome.Error);
        await Assert.That(records[0].Error).IsEqualTo("no D0/D1 pins");
    }

    [Test]
    public async Task Queue_CancelledRunningItem_RecordsCancelled()
    {
        var records = new List<SimulationEventRecord>();
        var started = new TaskCompletionSource();
        using var cts = new CancellationTokenSource();
        await using var queue = new ReaderWorkQueue(5, (_, _) => { }, NullLogger.Instance, records.Add);

        var work = queue.EnqueueAsync(async itemCt =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.Infinite, itemCt);
            }, "Access Cycle", DoorEventType.AccessCycle, cts.Token,
            new SimulationEventContext(SimulationEventKind.AccessCycle));

        await started.Task;
        await cts.CancelAsync();

        await Assert.That(async () => await work).Throws<OperationCanceledException>();

        await Assert.That(records.Count).IsEqualTo(1);
        await Assert.That(records[0].Outcome).IsEqualTo(SimulationEventOutcome.Cancelled);
        await Assert.That(records[0].Error).IsNull();
    }

    [Test]
    public async Task Queue_WithoutTelemetryContext_RecordsNothing()
    {
        var records = new List<SimulationEventRecord>();
        await using var queue = new ReaderWorkQueue(1, (_, _) => { }, NullLogger.Instance, records.Add);

        await queue.EnqueueAsync(_ => Task.CompletedTask, "Untracked");

        await Assert.That(records).IsEmpty();
    }

    [Test]
    public async Task Queue_ThrowingTelemetryCallback_DoesNotFailSimulation()
    {
        await using var queue = new ReaderWorkQueue(2, (_, _) => { }, NullLogger.Instance,
            _ => throw new InvalidOperationException("telemetry sink is down"));

        await queue.EnqueueAsync(_ => Task.CompletedTask, "Card Read", DoorEventType.CardReadOnly,
            telemetry: new SimulationEventContext(SimulationEventKind.CardReadOnly));
    }
}
