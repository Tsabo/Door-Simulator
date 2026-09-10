using System.ComponentModel;

namespace DoorSim.Shared.Models;

/// <summary>
/// LED colors as defined in the OSDP spec, mirrored here so the Shared project
/// has no dependency on OSDP.Net.
/// </summary>
[Description("LED color commanded by the panel, as defined in the OSDP spec.")]
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
