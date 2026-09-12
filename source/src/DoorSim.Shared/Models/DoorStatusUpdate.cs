namespace DoorSim.Shared.Models;

/// <summary>
/// Snapshot of a single door's simulation, queue, and OSDP connectivity state.
/// Pushed to clients via the SSE stream endpoint.
/// </summary>
public record DoorStatusUpdate(
    int DoorId,
    SimulationStatus Status,
    bool IsConnected,
    ReaderLedState? LedState = null,
    bool DoorIsOpen = false,
    bool RexIsActive = false,
    int QueueDepth = 0,
    string? CurrentAction = null,
    IReadOnlyList<SimulationQueueItemDto>? Queue = null);
