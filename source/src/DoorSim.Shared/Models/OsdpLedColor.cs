namespace DoorSim.Shared.Models;

/// <summary>
/// LED colors as defined in the OSDP spec, mirrored here so the Shared project
/// has no dependency on OSDP.Net.
/// </summary>
public enum OsdpLedColor
{
    Off,
    Red,
    Green,
    Amber,
    Blue,
    Magenta,
    Cyan,
    White,
}
