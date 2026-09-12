namespace DoorSim.Shared.Models;

/// <summary>
/// Represents an item currently in a reader's execution queue (running or pending).
/// </summary>
public record SimulationQueueItemDto(
    Guid Id,
    int ReaderId,
    string Description,
    DoorEventType? EventType,
    DateTimeOffset EnqueuedAt,
    bool IsRunning);
