namespace DoorSim.Shared.Models;

/// <summary>
/// Telemetry taxonomy covering both queued simulations and the direct hardware
/// primitives, which bypass the reader work queue.
/// </summary>
public enum SimulationEventKind
{
    CardReadOnly,
    AccessCycle,
    EgressCycle,
    RawBits,
    DoorOpen,
    DoorClose,
    QuickRex,
}
