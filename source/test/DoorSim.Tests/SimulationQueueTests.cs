using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using DoorSim.Hardware;
using DoorSim.Services;
using DoorSim.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DoorSim.Tests;

public class SimulationQueueTests
{
    [Test]
    public async Task ReaderWorkQueue_SerializesExecution()
    {
        await using var queue = new ReaderWorkQueue(
            1,
            (_, _) => { },
            NullLogger.Instance);

        var executionOrder = new List<int>();
        var startedTcs = new TaskCompletionSource<bool>();
        var tcs1 = new TaskCompletionSource<bool>();

        var task1 = queue.EnqueueAsync(async _ =>
        {
            startedTcs.SetResult(true);
            await tcs1.Task;
            executionOrder.Add(1);
        }, "Task 1");

        var task2 = queue.EnqueueAsync(async _ =>
        {
            executionOrder.Add(2);
            await Task.CompletedTask;
        }, "Task 2");

        await startedTcs.Task;
        await Assert.That(queue.QueueDepth).IsEqualTo(2);
        await Assert.That(queue.CurrentAction).IsEqualTo("Task 1");

        tcs1.SetResult(true);
        await Task.WhenAll(task1, task2);

        await Assert.That(executionOrder).IsEquivalentTo([1, 2]);
        await Assert.That(queue.QueueDepth).IsEqualTo(0);
    }

    [Test]
    public async Task ReaderWorkQueue_CancelPendingItem_SkipsExecution()
    {
        await using var queue = new ReaderWorkQueue(
            1,
            (_, _) => { },
            NullLogger.Instance);

        var startedTcs = new TaskCompletionSource<bool>();
        var tcs1 = new TaskCompletionSource<bool>();
        var task2Executed = false;

        var task1 = queue.EnqueueAsync(async _ =>
        {
            startedTcs.SetResult(true);
            await tcs1.Task;
        }, "Task 1");

        var task2 = queue.EnqueueAsync(async _ =>
        {
            task2Executed = true;
            await Task.CompletedTask;
        }, "Task 2");

        await startedTcs.Task;
        var snapshot = queue.GetSnapshot();
        await Assert.That(snapshot.Count).IsEqualTo(2);
        var pendingItem = snapshot.Single(p => !p.IsRunning);

        var cancelled = queue.TryCancel(pendingItem.Id);
        await Assert.That(cancelled).IsTrue();
        await Assert.That(queue.QueueDepth).IsEqualTo(1);

        tcs1.SetResult(true);
        await task1;

        // task2 should be cancelled
        await Assert.That(async () => await task2).Throws<OperationCanceledException>();
        await Assert.That(task2Executed).IsFalse();
    }

    [Test]
    public async Task ReaderWorkQueue_ClearPending_RemovesAllPending()
    {
        await using var queue = new ReaderWorkQueue(
            1,
            (_, _) => { },
            NullLogger.Instance);

        var startedTcs = new TaskCompletionSource<bool>();
        var tcs1 = new TaskCompletionSource<bool>();

        var task1 = queue.EnqueueAsync(async _ =>
        {
            startedTcs.SetResult(true);
            await tcs1.Task;
        }, "Task 1");

        var task2 = queue.EnqueueAsync(async _ => await Task.CompletedTask, "Task 2");
        var task3 = queue.EnqueueAsync(async _ => await Task.CompletedTask, "Task 3");

        await startedTcs.Task;
        await Assert.That(queue.QueueDepth).IsEqualTo(3);

        var cleared = queue.ClearPending();
        await Assert.That(cleared).IsEqualTo(2);
        await Assert.That(queue.QueueDepth).IsEqualTo(1);

        tcs1.SetResult(true);
        await task1;

        await Assert.That(async () => await task2).Throws<OperationCanceledException>();
        await Assert.That(async () => await task3).Throws<OperationCanceledException>();
    }

    [Test]
    public async Task Orchestrator_MultiReader_ExecutesInParallel()
    {
        var bank = new QueueTestReaderBank();
        var sim1 = new RecordingReaderSimulator();
        var sim2 = new RecordingReaderSimulator();
        bank.Add(1, sim1);
        bank.Add(2, sim2);

        var services = new ServiceCollection().BuildServiceProvider();
        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
        var settings = new SimulationSettingsService(scopeFactory, NullLogger<SimulationSettingsService>.Instance);
        await using var metrics = new SimulationMetricsService(scopeFactory, bank, settings, NullLogger<SimulationMetricsService>.Instance);
        await using var orchestrator = new SimulationOrchestrator(bank, scopeFactory, settings, metrics, NullLogger<SimulationOrchestrator>.Instance);

        var r1Entered = new TaskCompletionSource<bool>();
        var r2Entered = new TaskCompletionSource<bool>();
        var r1Release = new TaskCompletionSource<bool>();
        var r2Release = new TaskCompletionSource<bool>();

        sim1.OnSendCard = async () =>
        {
            r1Entered.SetResult(true);
            await r1Release.Task;
        };

        sim2.OnSendCard = async () =>
        {
            r2Entered.SetResult(true);
            await r2Release.Task;
        };

        var send1 = orchestrator.SendCardAsync(new RawCardRequest(1, 100, 1, WiegandFormat.Wiegand26));
        var send2 = orchestrator.SendCardAsync(new RawCardRequest(2, 200, 2, WiegandFormat.Wiegand26));

        // Both readers should enter their send concurrently
        await Task.WhenAll(r1Entered.Task, r2Entered.Task);

        var queue1 = orchestrator.GetQueue(1);
        var queue2 = orchestrator.GetQueue(2);

        await Assert.That(queue1.Count).IsEqualTo(1);
        await Assert.That(queue2.Count).IsEqualTo(1);
        await Assert.That(queue1[0].IsRunning).IsTrue();
        await Assert.That(queue2[0].IsRunning).IsTrue();

        r1Release.SetResult(true);
        r2Release.SetResult(true);

        await Task.WhenAll(send1, send2);

        await Assert.That(sim1.SendCount).IsEqualTo(1);
        await Assert.That(sim2.SendCount).IsEqualTo(1);
    }

    [Test]
    public async Task Orchestrator_QueueManagement_ClearAndCancel()
    {
        var bank = new QueueTestReaderBank();
        var sim = new RecordingReaderSimulator();
        bank.Add(1, sim);

        var services = new ServiceCollection().BuildServiceProvider();
        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
        var settings = new SimulationSettingsService(scopeFactory, NullLogger<SimulationSettingsService>.Instance);
        await using var metrics = new SimulationMetricsService(scopeFactory, bank, settings, NullLogger<SimulationMetricsService>.Instance);
        await using var orchestrator = new SimulationOrchestrator(bank, scopeFactory, settings, metrics, NullLogger<SimulationOrchestrator>.Instance);

        var startedTcs = new TaskCompletionSource<bool>();
        var releaseTcs = new TaskCompletionSource<bool>();

        sim.OnSendCard = async () =>
        {
            startedTcs.SetResult(true);
            await releaseTcs.Task;
        };

        var send1 = orchestrator.SendCardAsync(new RawCardRequest(1, 100, 1, WiegandFormat.Wiegand26));
        var send2 = orchestrator.SendCardAsync(new RawCardRequest(1, 101, 1, WiegandFormat.Wiegand26));
        var send3 = orchestrator.SendCardAsync(new RawCardRequest(1, 102, 1, WiegandFormat.Wiegand26));

        await startedTcs.Task;

        var queue = orchestrator.GetQueue(1);
        await Assert.That(queue.Count).IsEqualTo(3);
        await Assert.That(queue[0].IsRunning).IsTrue();
        await Assert.That(queue[1].IsRunning).IsFalse();
        await Assert.That(queue[2].IsRunning).IsFalse();

        var statuses = orchestrator.GetAllStatuses();
        var doorStatus = statuses.Single(s => s.DoorId == 1);
        await Assert.That(doorStatus.QueueDepth).IsEqualTo(3);
        await Assert.That(doorStatus.CurrentAction).IsNotNull();

        // Cancel the second item
        var cancelled = orchestrator.CancelQueueItem(1, queue[1].Id);
        await Assert.That(cancelled).IsTrue();

        var queueAfterCancel = orchestrator.GetQueue(1);
        await Assert.That(queueAfterCancel.Count).IsEqualTo(2);

        // Clear remaining pending
        var cleared = orchestrator.ClearQueue(1);
        await Assert.That(cleared).IsEqualTo(1);

        releaseTcs.SetResult(true);
        await send1;

        // Cancelled and cleared items should throw OperationCanceledException when awaited
        await Assert.That(async () => await send2).Throws<OperationCanceledException>();
        await Assert.That(async () => await send3).Throws<OperationCanceledException>();
    }

    [Test]
    public async Task ReaderWorkQueue_ObservesQueueItemDelay()
    {
        const int delayMs = 100;
        await using var queue = new ReaderWorkQueue(
            1,
            (_, _) => { },
            NullLogger.Instance,
            getQueueItemDelayMs: () => delayMs);

        var startedTcs = new TaskCompletionSource<bool>();
        var releaseTcs = new TaskCompletionSource<bool>();
        var item2StartTicks = 0L;

        var task1 = queue.EnqueueAsync(async _ =>
        {
            startedTcs.SetResult(true);
            await releaseTcs.Task;
        }, "Task 1");

        var task2 = queue.EnqueueAsync(async _ =>
        {
            item2StartTicks = Stopwatch.GetTimestamp();
            await Task.CompletedTask;
        }, "Task 2");

        await startedTcs.Task;
        var t1FinishedTicks = Stopwatch.GetTimestamp();
        releaseTcs.SetResult(true);

        await Task.WhenAll(task1, task2);

        var elapsedMs = Stopwatch.GetElapsedTime(t1FinishedTicks, item2StartTicks).TotalMilliseconds;
        await Assert.That(elapsedMs).IsGreaterThanOrEqualTo(delayMs - 30); // Allow standard timer jitter
    }

    private sealed class RecordingReaderSimulator : IReaderSimulator
    {
        public int SendCount { get; private set; }
        public Func<Task>? OnSendCard { get; set; }

        public DoorConfiguration Config { get; } = new(1, "Recording", ProtocolType.Wiegand,
            null, null, null, null, null, null, null, null, null, null, null, null, null);

        public bool IsConnected => true;
        public ReaderLedState? LedState => null;
        public bool IsDoorOpen => false;
        public bool IsRexActive => false;

        public Task SendCardAsync(CardEntry card) => SendCardAsync(card.CardNumber, card.FacilityCode, card.Format);

        public async Task SendCardAsync(uint cardNumber, ushort facilityCode, WiegandFormat format)
        {
            SendCount++;
            if (OnSendCard is not null)
                await OnSendCard();
        }

        public Task SendBitsAsync(string bits) => Task.CompletedTask;
        public Task SimulateAccessCycleAsync(CardEntry card, int cardToDoorDelayMs, int doorOpenMs) => Task.CompletedTask;
        public Task SimulateAccessCycleAsync(uint cardNumber, ushort facilityCode, WiegandFormat format, int cardToDoorDelayMs, int doorOpenMs) => Task.CompletedTask;
        public Task SimulateEgressCycleAsync(int rexLeadMs, int doorOpenMs) => Task.CompletedTask;
        public Task OpenDoorAsync() => Task.CompletedTask;
        public Task CloseDoorAsync() => Task.CompletedTask;
        public Task TripRexAsync() => Task.CompletedTask;
        public Task ResetRexAsync() => Task.CompletedTask;
    }

    private sealed class QueueTestReaderBank : IReaderBank
    {
        private readonly Dictionary<int, IReaderSimulator> _simulators = new();

        public IReaderSimulator GetReader(int doorId) =>
            _simulators.TryGetValue(doorId, out var sim)
                ? sim
                : throw new KeyNotFoundException($"Door {doorId} not found");

        public bool TryGetReader(int doorId, [NotNullWhen(true)] out IReaderSimulator? simulator) =>
            _simulators.TryGetValue(doorId, out simulator);

        public bool ContainsReader(int doorId) =>
            _simulators.ContainsKey(doorId);

        public IReadOnlyCollection<int> ActiveDoorIds => _simulators.Keys;

        public void Add(int doorId, IReaderSimulator sim) => _simulators[doorId] = sim;
    }
}
