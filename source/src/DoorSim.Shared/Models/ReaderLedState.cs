namespace DoorSim.Shared.Models;

/// <summary>
/// Snapshot of the LED and buzzer state as last commanded by the panel via OSDP.
/// Only populated for OSDP readers; null for Wiegand (one-directional protocol).
/// </summary>
public record ReaderLedState(OsdpLedColor Color, bool Blink, bool BuzzerActive);
