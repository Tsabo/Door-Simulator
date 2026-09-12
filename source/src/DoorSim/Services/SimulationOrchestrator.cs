using System.Runtime.CompilerServices;

namespace DoorSim.Services;

/// <summary>
/// Wires API endpoints to <see cref="IReaderBank" />.
/// Tracks per-door status and per-door serialized work queues for UI feedback and concurrency safety.
/// Singleton — shares lifetime with the hardware bank.
/// Creates a DI scope for each card lookup so it can safely consume the scoped CardLibraryService.
/// </summary>
public class SimulationOrchestrator(
    IReaderBank bank,
    IServiceScopeFactory scopeFactory,
    SimulationSettingsService settings,
    ILogger<SimulationOrchestrator> logger) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<int, ReaderWorkQueue> _queues = new();
    private readonly ConcurrentDictionary<int, SimulationStatus> _status = new();

    /// <summary>All door IDs currently loaded in the simulator bank.</summary>
    public IReadOnlyCollection<int> ActiveDoorIds => bank.ActiveDoorIds;

    public async ValueTask DisposeAsync()
    {
        foreach (var queue in _queues.Values)
            await queue.DisposeAsync().ConfigureAwait(false);

        _queues.Clear();
        _status.Clear();
    }

    // -------------------------------------------------------------------------
    // Library-backed send
    // -------------------------------------------------------------------------

    public async Task RunEventAsync(DoorEventRequest request, CancellationToken ct = default)
    {
        if (!bank.TryGetReader(request.ReaderId, out var simulator))
            throw new KeyNotFoundException($"Reader {request.ReaderId} not found in the active bank.");

        CardEntry? card = null;
        if (request.EventType != DoorEventType.EgressCycle)
            card = await ResolveCardAsync(request.CardEntryId, request.ReaderId).ConfigureAwait(false);

        var description = request.EventType switch
        {
            DoorEventType.EgressCycle => "Egress Cycle",
            DoorEventType.AccessCycle => $"Access Cycle ({card?.Label ?? $"#{card?.CardNumber}"})",
            var _ => $"Card Read ({card?.Label ?? $"#{card?.CardNumber}"})",
        };

        var queue = GetOrCreateQueue(request.ReaderId);
        await queue.EnqueueAsync(async itemCt =>
            {
                itemCt.ThrowIfCancellationRequested();
                var t = settings.Current;

                if (request.EventType == DoorEventType.EgressCycle)
                    await simulator.SimulateEgressCycleAsync(t.RexLeadMs, t.DoorOpenMs).ConfigureAwait(false);
                else
                {
                    if (request.EventType == DoorEventType.CardReadOnly)
                        await simulator.SendCardAsync(card!).ConfigureAwait(false);
                    else
                        await simulator.SimulateAccessCycleAsync(card!, t.CardToDoorDelayMs, t.DoorOpenMs).ConfigureAwait(false);
                }
            }, description, request.EventType, ct)
            .ConfigureAwait(false);
    }

    // -------------------------------------------------------------------------
    // Raw value send (no library lookup)
    // -------------------------------------------------------------------------

    public async Task SendCardAsync(RawCardRequest request, CancellationToken ct = default)
    {
        if (!bank.TryGetReader(request.ReaderId, out var simulator))
            throw new KeyNotFoundException($"Reader {request.ReaderId} not found in the active bank.");

        var description = $"Raw Card {request.Format} FC:{request.FacilityCode} #{request.CardNumber}";
        var queue = GetOrCreateQueue(request.ReaderId);

        await queue.EnqueueAsync(async itemCt =>
            {
                itemCt.ThrowIfCancellationRequested();
                await simulator.SendCardAsync(request.CardNumber, request.FacilityCode, request.Format).ConfigureAwait(false);
            }, description, DoorEventType.CardReadOnly, ct)
            .ConfigureAwait(false);
    }

    public async Task SendBitsAsync(RawBitsRequest request, CancellationToken ct = default)
    {
        if (!bank.TryGetReader(request.ReaderId, out var simulator))
            throw new KeyNotFoundException($"Reader {request.ReaderId} not found in the active bank.");

        var description = $"Raw Bits ({request.Bits.Length} bits)";
        var queue = GetOrCreateQueue(request.ReaderId);

        await queue.EnqueueAsync(async itemCt =>
            {
                itemCt.ThrowIfCancellationRequested();
                await simulator.SendBitsAsync(request.Bits).ConfigureAwait(false);
            }, description, DoorEventType.CardReadOnly, ct)
            .ConfigureAwait(false);
    }

    public async Task RunRawEventAsync(RawDoorEventRequest request, CancellationToken ct = default)
    {
        if (!bank.TryGetReader(request.ReaderId, out var simulator))
            throw new KeyNotFoundException($"Reader {request.ReaderId} not found in the active bank.");

        var description = request.EventType switch
        {
            DoorEventType.EgressCycle => "Raw Egress Cycle",
            DoorEventType.AccessCycle => $"Raw Access Cycle ({request.Format} FC:{request.FacilityCode} #{request.CardNumber})",
            var _ => $"Raw Card Read ({request.Format} FC:{request.FacilityCode} #{request.CardNumber})",
        };

        var queue = GetOrCreateQueue(request.ReaderId);
        await queue.EnqueueAsync(async itemCt =>
            {
                itemCt.ThrowIfCancellationRequested();
                var t = settings.Current;

                if (request.EventType == DoorEventType.EgressCycle)
                    await simulator.SimulateEgressCycleAsync(t.RexLeadMs, t.DoorOpenMs).ConfigureAwait(false);
                else if (request.EventType == DoorEventType.CardReadOnly)
                    await simulator.SendCardAsync(request.CardNumber, request.FacilityCode, request.Format).ConfigureAwait(false);
                else
                    await simulator.SimulateAccessCycleAsync(request.CardNumber, request.FacilityCode, request.Format, t.CardToDoorDelayMs, t.DoorOpenMs).ConfigureAwait(false);
            }, description, request.EventType, ct)
            .ConfigureAwait(false);
    }

    // -------------------------------------------------------------------------
    // Direct primitives — no status tracking, instant effect
    // -------------------------------------------------------------------------

    public async Task OpenDoorAsync(int readerId)
    {
        try
        {
            if (bank.TryGetReader(readerId, out var simulator))
                await simulator.OpenDoorAsync().ConfigureAwait(false);
            else
                logger.LogWarning("Reader {R}: open door ignored — reader not found in bank", readerId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Reader {R}: open door failed", readerId);
        }
    }

    public async Task CloseDoorAsync(int readerId)
    {
        try
        {
            if (bank.TryGetReader(readerId, out var simulator))
                await simulator.CloseDoorAsync().ConfigureAwait(false);
            else
                logger.LogWarning("Reader {R}: close door ignored — reader not found in bank", readerId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Reader {R}: close door failed", readerId);
        }
    }

    /// <summary>Trip REX, hold for the global QuickRexMs, then reset.</summary>
    public async Task QuickRexAsync(int readerId)
    {
        try
        {
            if (!bank.TryGetReader(readerId, out var sim))
            {
                logger.LogWarning("Reader {R}: quick REX ignored — reader not found in bank", readerId);
                return;
            }

            await sim.TripRexAsync().ConfigureAwait(false);
            await Task.Delay(settings.Current.QuickRexMs).ConfigureAwait(false);
            await sim.ResetRexAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Reader {R}: quick REX failed", readerId);
        }
    }

    public SimulationStatus GetStatus(int readerId) =>
        _status.GetValueOrDefault(readerId, SimulationStatus.Idle);

    /// <summary>Returns true if the reader's transport is connected and responding.</summary>
    /// <remarks>Always true for Wiegand. For OSDP, reflects the 8-second heartbeat window.</remarks>
    public bool GetConnectivity(int readerId) =>
        bank.TryGetReader(readerId, out var sim) && sim.IsConnected;

    /// <summary>
    /// Returns the current LED/buzzer state as commanded by the panel.
    /// Only meaningful for OSDP readers; returns null for Wiegand.
    /// </summary>
    public ReaderLedState? GetLedState(int readerId) =>
        bank.TryGetReader(readerId, out var sim)
            ? sim.LedState
            : null;

    /// <summary>
    /// Snapshot of every active door's status, queue depth, active action, queue snapshot, and connectivity.
    /// Used by the SSE stream endpoint to push updates to clients.
    /// </summary>
    public DoorStatusUpdate[] GetAllStatuses() =>
    [
        .. ActiveDoorIds.Select(id =>
        {
            bank.TryGetReader(id, out var sim);
            _queues.TryGetValue(id, out var queue);

            return new DoorStatusUpdate(
                id,
                GetStatus(id),
                GetConnectivity(id),
                GetLedState(id),
                sim?.IsDoorOpen ?? false,
                sim?.IsRexActive ?? false,
                queue?.QueueDepth ?? 0,
                queue?.CurrentAction,
                queue?.GetSnapshot() ?? []);
        }),
    ];

    /// <summary>
    /// Async stream that yields a status snapshot for all doors every
    /// <paramref name="intervalMs" /> milliseconds until cancelled.
    /// </summary>
    public async IAsyncEnumerable<DoorStatusUpdate[]> StreamStatusAsync(int intervalMs = 500,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            yield return GetAllStatuses();
            try
            {
                await Task.Delay(intervalMs, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
        }
    }

    // -------------------------------------------------------------------------
    // Queue management & inspection
    // -------------------------------------------------------------------------

    /// <summary>Gets a snapshot of items in the reader's processing queue.</summary>
    public IReadOnlyList<SimulationQueueItemDto> GetQueue(int readerId) =>
        _queues.TryGetValue(readerId, out var queue)
            ? queue.GetSnapshot()
            : [];

    /// <summary>Cancels a specific queue item for a reader.</summary>
    public bool CancelQueueItem(int readerId, Guid itemId) =>
        _queues.TryGetValue(readerId, out var queue) && queue.TryCancel(itemId);

    /// <summary>Clears all pending (not-yet-running) queue items for a reader.</summary>
    public int ClearQueue(int readerId) =>
        _queues.TryGetValue(readerId, out var queue)
            ? queue.ClearPending()
            : 0;

    /// <summary>Removes and disposes a reader's queue when a door is removed.</summary>
    public async ValueTask RemoveReaderQueueAsync(int readerId)
    {
        if (_queues.TryRemove(readerId, out var queue))
            await queue.DisposeAsync().ConfigureAwait(false);

        _status.TryRemove(readerId, out var _);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private ReaderWorkQueue GetOrCreateQueue(int readerId) =>
        _queues.GetOrAdd(readerId, id => new ReaderWorkQueue(
            id,
            SetStatus,
            logger));

    private void SetStatus(int readerId, SimulationStatus status) =>
        _status[readerId] = status;

    private async Task<CardEntry> ResolveCardAsync(int? cardEntryId, int readerId)
    {
        if (cardEntryId is null)
            throw new InvalidOperationException($"Reader {readerId}: CardEntryId is required for this event type.");

        await using var scope = scopeFactory.CreateAsyncScope();
        var cards = scope.ServiceProvider.GetRequiredService<CardLibraryService>();

        return await cards.GetAsync(cardEntryId.Value).ConfigureAwait(false)
               ?? throw new KeyNotFoundException($"Card {cardEntryId} not found in library.");
    }
}
