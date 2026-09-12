using System.Runtime.CompilerServices;

namespace DoorSim.Services;

/// <summary>
/// Wires API endpoints to <see cref="IReaderBank" />.
/// Tracks per-door status for UI feedback.
/// Singleton — shares lifetime with the hardware bank.
/// Creates a DI scope for each card lookup so it can safely consume the scoped CardLibraryService.
/// </summary>
public class SimulationOrchestrator(
    IReaderBank bank,
    IServiceScopeFactory scopeFactory,
    SimulationSettingsService settings,
    ILogger<SimulationOrchestrator> logger)
{
    private readonly ConcurrentDictionary<int, SimulationStatus> _status = new();

    /// <summary>All door IDs currently loaded in the simulator bank.</summary>
    public IReadOnlyCollection<int> ActiveDoorIds => bank.ActiveDoorIds;

    // -------------------------------------------------------------------------
    // Library-backed send
    // -------------------------------------------------------------------------

    public async Task RunEventAsync(DoorEventRequest request)
    {
        if (!bank.TryGetReader(request.ReaderId, out var simulator))
            throw new KeyNotFoundException($"Reader {request.ReaderId} not found in the active bank.");

        SetStatus(request.ReaderId, SimulationStatus.Running);

        try
        {
            var t = settings.Current;
            if (request.EventType == DoorEventType.EgressCycle)
                await simulator.SimulateEgressCycleAsync(t.RexLeadMs, t.DoorOpenMs).ConfigureAwait(false);
            else
            {
                var card = await ResolveCardAsync(request.CardEntryId, request.ReaderId).ConfigureAwait(false);
                if (request.EventType == DoorEventType.CardReadOnly)
                    await simulator.SendCardAsync(card).ConfigureAwait(false);
                else
                    await simulator.SimulateAccessCycleAsync(card, t.CardToDoorDelayMs, t.DoorOpenMs).ConfigureAwait(false);
            }

            SetStatus(request.ReaderId, SimulationStatus.Success);
        }
        catch (Exception ex)
        {
            SetStatus(request.ReaderId, SimulationStatus.Error);
            logger.LogError(ex, "Reader {R}: simulation failed", request.ReaderId);
        }

        await Task.Delay(1500);
        SetStatus(request.ReaderId, SimulationStatus.Idle);
    }

    // -------------------------------------------------------------------------
    // Raw value send (no library lookup)
    // -------------------------------------------------------------------------

    public async Task SendCardAsync(RawCardRequest request)
    {
        if (!bank.TryGetReader(request.ReaderId, out var simulator))
            throw new KeyNotFoundException($"Reader {request.ReaderId} not found in the active bank.");

        SetStatus(request.ReaderId, SimulationStatus.Running);

        try
        {
            await simulator.SendCardAsync(request.CardNumber, request.FacilityCode, request.Format).ConfigureAwait(false);
            SetStatus(request.ReaderId, SimulationStatus.Success);
        }
        catch (Exception ex)
        {
            SetStatus(request.ReaderId, SimulationStatus.Error);
            logger.LogError(ex, "Reader {R}: raw send failed", request.ReaderId);
        }

        await Task.Delay(1500);
        SetStatus(request.ReaderId, SimulationStatus.Idle);
    }

    public async Task SendBitsAsync(RawBitsRequest request)
    {
        if (!bank.TryGetReader(request.ReaderId, out var simulator))
            throw new KeyNotFoundException($"Reader {request.ReaderId} not found in the active bank.");

        SetStatus(request.ReaderId, SimulationStatus.Running);

        try
        {
            await simulator.SendBitsAsync(request.Bits).ConfigureAwait(false);
            SetStatus(request.ReaderId, SimulationStatus.Success);
        }
        catch (Exception ex)
        {
            SetStatus(request.ReaderId, SimulationStatus.Error);
            logger.LogError(ex, "Reader {R}: raw bits send failed", request.ReaderId);
        }

        await Task.Delay(1500);
        SetStatus(request.ReaderId, SimulationStatus.Idle);
    }

    public async Task RunRawEventAsync(RawDoorEventRequest request)
    {
        if (!bank.TryGetReader(request.ReaderId, out var simulator))
            throw new KeyNotFoundException($"Reader {request.ReaderId} not found in the active bank.");

        SetStatus(request.ReaderId, SimulationStatus.Running);

        try
        {
            var t = settings.Current;
            if (request.EventType == DoorEventType.EgressCycle)
                await simulator.SimulateEgressCycleAsync(t.RexLeadMs, t.DoorOpenMs).ConfigureAwait(false);
            else if (request.EventType == DoorEventType.CardReadOnly)
                await simulator.SendCardAsync(request.CardNumber, request.FacilityCode, request.Format).ConfigureAwait(false);
            else
                await simulator.SimulateAccessCycleAsync(request.CardNumber, request.FacilityCode, request.Format, t.CardToDoorDelayMs, t.DoorOpenMs).ConfigureAwait(false);

            SetStatus(request.ReaderId, SimulationStatus.Success);
        }
        catch (Exception ex)
        {
            SetStatus(request.ReaderId, SimulationStatus.Error);
            logger.LogError(ex, "Reader {R}: raw event failed", request.ReaderId);
        }

        await Task.Delay(1500);
        SetStatus(request.ReaderId, SimulationStatus.Idle);
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
    /// Snapshot of every active door's status and connectivity.
    /// Used by the SSE stream endpoint to push updates to clients.
    /// </summary>
    public DoorStatusUpdate[] GetAllStatuses() =>
    [
        .. ActiveDoorIds.Select(id =>
        {
            bank.TryGetReader(id, out var sim);

            return new DoorStatusUpdate(
                id,
                GetStatus(id),
                GetConnectivity(id),
                GetLedState(id),
                sim?.IsDoorOpen ?? false,
                sim?.IsRexActive ?? false);
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
    // Helpers
    // -------------------------------------------------------------------------

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
