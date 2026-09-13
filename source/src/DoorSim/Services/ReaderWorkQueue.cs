using System.Threading.Channels;

namespace DoorSim.Services;

/// <summary>
/// Manages a FIFO queue of simulation tasks for a single reader.
/// Ensures all card transmissions and door cycle events on this reader
/// are serialized to avoid corrupting GPIO Wiegand bit streams or colliding
/// with ongoing door/REX timing cycles.
/// </summary>
public sealed class ReaderWorkQueue : IAsyncDisposable
{
    private readonly Channel<SimulationWorkItem> _channel;
    private readonly Lock _lock = new();
    private readonly ILogger _logger;
    private readonly Action<SimulationEventRecord>? _onCompleted;

    private readonly List<SimulationWorkItem> _pendingItems = [];
    private readonly Task _processingTask;
    private readonly int _readerId;
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly Action<int, SimulationStatus> _statusCallback;
    private SimulationWorkItem? _runningItem;

    public ReaderWorkQueue(int readerId,
        Action<int, SimulationStatus> statusCallback,
        ILogger logger,
        Action<SimulationEventRecord>? onCompleted = null)
    {
        _readerId = readerId;
        _statusCallback = statusCallback;
        _onCompleted = onCompleted;
        _logger = logger;
        _channel = Channel.CreateUnbounded<SimulationWorkItem>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

        _processingTask = Task.Run(ProcessQueueAsync);
    }

    /// <summary>Total number of active and pending items in this reader's queue.</summary>
    public int QueueDepth
    {
        get
        {
            lock (_lock)
            {
                return (_runningItem is not null
                    ? 1
                    : 0) + _pendingItems.Count;
            }
        }
    }

    /// <summary>Description of the currently executing action, or null if idle.</summary>
    public string? CurrentAction
    {
        get
        {
            lock (_lock)
            {
                return _runningItem?.Description;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        await _shutdownCts.CancelAsync().ConfigureAwait(false);

        try
        {
            await _processingTask.ConfigureAwait(false);
        }
        catch
        {
            // Ignore exceptions during teardown
        }

        _shutdownCts.Dispose();
    }

    /// <summary>
    /// Enqueues a simulation work item and returns a Task that completes when the item finishes executing.
    /// </summary>
    public Task EnqueueAsync(Func<CancellationToken, Task> work,
        string description,
        DoorEventType? eventType = null,
        CancellationToken callerCt = default,
        SimulationEventContext? telemetry = null)
    {
        var item = new SimulationWorkItem(
            _readerId,
            description,
            eventType,
            work,
            telemetry);

        lock (_lock)
        {
            item.QueueDepthAtEnqueue = (_runningItem is not null
                ? 1
                : 0) + _pendingItems.Count;

            _pendingItems.Add(item);
        }

        if (callerCt.CanBeCanceled)
        {
            callerCt.Register(() =>
            {
                TryCancel(item.Id);
            });
        }

        if (!_channel.Writer.TryWrite(item))
        {
            lock (_lock)
            {
                _pendingItems.Remove(item);
            }

            item.Tcs.TrySetException(new InvalidOperationException($"Queue for reader {_readerId} is closed."));
        }

        return item.Tcs.Task;
    }

    /// <summary>
    /// Gets a snapshot of the current queue (running item + all pending items in order).
    /// </summary>
    public IReadOnlyList<SimulationQueueItemDto> GetSnapshot()
    {
        lock (_lock)
        {
            var result = new List<SimulationQueueItemDto>();
            if (_runningItem is not null)
            {
                result.Add(new SimulationQueueItemDto(
                    _runningItem.Id,
                    _readerId,
                    _runningItem.Description,
                    _runningItem.EventType,
                    _runningItem.EnqueuedAt,
                    true));
            }

            foreach (var item in _pendingItems)
            {
                result.Add(new SimulationQueueItemDto(
                    item.Id,
                    _readerId,
                    item.Description,
                    item.EventType,
                    item.EnqueuedAt,
                    false));
            }

            return result;
        }
    }

    /// <summary>
    /// Cancels an item by ID. If currently running, signals its cancellation token.
    /// If pending in queue, removes it and cancels its completion source.
    /// </summary>
    public bool TryCancel(Guid itemId)
    {
        SimulationWorkItem? itemToCancel = null;
        var isRunning = false;

        lock (_lock)
        {
            if (_runningItem is not null && _runningItem.Id == itemId)
            {
                isRunning = true;
                itemToCancel = _runningItem;
            }
            else
            {
                var idx = _pendingItems.FindIndex(p => p.Id == itemId);
                if (idx >= 0)
                {
                    itemToCancel = _pendingItems[idx];
                    _pendingItems.RemoveAt(idx);
                }
            }
        }

        if (itemToCancel is null)
            return false;

        if (isRunning)
        {
            itemToCancel.Cancel();
            _logger.LogInformation("Reader {R}: cancelled active item {Id} ({Desc})",
                _readerId, itemToCancel.Id, itemToCancel.Description);
        }
        else
        {
            itemToCancel.Cancel();
            itemToCancel.Tcs.TrySetCanceled();
            _logger.LogInformation("Reader {R}: removed pending item {Id} ({Desc}) from queue",
                _readerId, itemToCancel.Id, itemToCancel.Description);
        }

        return true;
    }

    /// <summary>
    /// Clears all pending items from the queue (does not abort the currently running item).
    /// </summary>
    public int ClearPending()
    {
        List<SimulationWorkItem> removed;
        lock (_lock)
        {
            removed = [.. _pendingItems];
            _pendingItems.Clear();
        }

        foreach (var item in removed)
        {
            item.Cancel();
            item.Tcs.TrySetCanceled();
        }

        if (removed.Count > 0)
        {
            _logger.LogInformation("Reader {R}: cleared {Count} pending items from queue",
                _readerId, removed.Count);
        }

        return removed.Count;
    }

    private async Task ProcessQueueAsync()
    {
        var reader = _channel.Reader;

        try
        {
            while (await reader.WaitToReadAsync(_shutdownCts.Token).ConfigureAwait(false))
            {
                while (reader.TryRead(out var item))
                {
                    // Check if item was cancelled before it even began running
                    lock (_lock)
                    {
                        if (!_pendingItems.Remove(item))
                        {
                            // Already removed via TryCancel/ClearPending
                            continue;
                        }

                        _runningItem = item;
                    }

                    _statusCallback(_readerId, SimulationStatus.Running);

                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                        _shutdownCts.Token, item.Cts.Token);

                    Exception? workException = null;
                    var startedAt = DateTimeOffset.UtcNow;
                    var startTicks = Stopwatch.GetTimestamp();

                    try
                    {
                        await item.Work(linkedCts.Token).ConfigureAwait(false);
                        _statusCallback(_readerId, SimulationStatus.Success);
                    }
                    catch (OperationCanceledException) when (item.Cts.IsCancellationRequested || _shutdownCts.IsCancellationRequested)
                    {
                        _logger.LogInformation("Reader {R}: item {Desc} was cancelled", _readerId, item.Description);
                        _statusCallback(_readerId, SimulationStatus.Idle);
                        workException = new OperationCanceledException(item.Cts.Token);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Reader {R}: item {Desc} failed", _readerId, item.Description);
                        _statusCallback(_readerId, SimulationStatus.Error);
                        workException = ex;
                    }
                    finally
                    {
                        lock (_lock)
                        {
                            _runningItem = null;
                        }

                        RecordCompletion(item, startedAt, Stopwatch.GetElapsedTime(startTicks), workException);
                    }

                    if (workException is OperationCanceledException)
                        item.Tcs.TrySetCanceled();
                    else if (workException is not null)
                        item.Tcs.TrySetException(workException);
                    else
                        item.Tcs.TrySetResult(true);

                    // If more items are waiting, loop immediately. Otherwise, give a short moment to display Success/Error
                    // before returning to Idle.
                    bool hasMore;
                    lock (_lock)
                    {
                        hasMore = _pendingItems.Count > 0;
                    }

                    if (hasMore)
                        continue;

                    try
                    {
                        await Task.Delay(1500, _shutdownCts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // Shutdown requested
                    }

                    lock (_lock)
                    {
                        if (_pendingItems.Count == 0 && _runningItem is null)
                            _statusCallback(_readerId, SimulationStatus.Idle);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reader {R}: unexpected error in queue processing loop", _readerId);
        }
        finally
        {
            lock (_lock)
            {
                _runningItem = null;
                foreach (var item in _pendingItems)
                {
                    item.Cancel();
                    item.Tcs.TrySetCanceled();
                }

                _pendingItems.Clear();
            }

            _statusCallback(_readerId, SimulationStatus.Idle);
        }
    }

    private void RecordCompletion(SimulationWorkItem item,
        DateTimeOffset startedAt,
        TimeSpan duration,
        Exception? workException)
    {
        if (_onCompleted is null || item.Telemetry is null)
            return;

        try
        {
            var outcome = workException switch
            {
                null => SimulationEventOutcome.Success,
                OperationCanceledException => SimulationEventOutcome.Cancelled,
                var _ => SimulationEventOutcome.Error,
            };

            _onCompleted(new SimulationEventRecord(
                item.Id,
                _readerId,
                // Label and protocol are filled in by the orchestrator, which owns the door config.
                string.Empty,
                default,
                item.Telemetry.Kind,
                outcome,
                item.Telemetry.CardEntryId,
                item.Telemetry.CardNumber,
                item.Telemetry.FacilityCode,
                item.Telemetry.Format,
                item.Telemetry.RawBits,
                item.EnqueuedAt,
                startedAt,
                startedAt + duration,
                (int)(startedAt - item.EnqueuedAt).TotalMilliseconds,
                (int)duration.TotalMilliseconds,
                item.QueueDepthAtEnqueue,
                outcome == SimulationEventOutcome.Error
                    ? workException?.Message
                    : null));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reader {R}: failed to record telemetry for item {Id}", _readerId, item.Id);
        }
    }
}

/// <summary>
/// Internal representation of a work item in a reader's queue.
/// </summary>
internal sealed class SimulationWorkItem(
    int readerId,
    string description,
    DoorEventType? eventType,
    Func<CancellationToken, Task> work,
    SimulationEventContext? telemetry = null)
{
    public Guid Id { get; } = Guid.NewGuid();
    public int ReaderId { get; } = readerId;
    public string Description { get; } = description;
    public DoorEventType? EventType { get; } = eventType;
    public SimulationEventContext? Telemetry { get; } = telemetry;
    public int QueueDepthAtEnqueue { get; set; }
    public DateTimeOffset EnqueuedAt { get; } = DateTimeOffset.UtcNow;
    public Func<CancellationToken, Task> Work { get; } = work;
    public TaskCompletionSource<bool> Tcs { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public CancellationTokenSource Cts { get; } = new();

    public void Cancel()
    {
        try
        {
            Cts.Cancel();
        }
        catch (ObjectDisposedException) { }
    }
}
