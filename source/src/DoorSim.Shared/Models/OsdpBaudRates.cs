namespace DoorSim.Shared.Models;

/// <summary>
/// Supported OSDP RS-485 bus baud rates.
/// </summary>
/// <remarks>
/// A door with no explicit rate falls back to <see cref="Default"/>, which is the
/// rate mandated for OSDP discovery and the one virtually all panels ship with.
/// A per-door override exists because a bus is only as capable as its adapter
/// channel: some USB-RS485 channels silently fail to apply a requested rate, and
/// panels are often reconfigurable to a rate that does work.
/// </remarks>
public static class OsdpBaudRates
{
    /// <summary>Default OSDP baud rate used when a door specifies none.</summary>
    public const int Default = 9600;

    /// <summary>Baud rates defined by the OSDP specification, ascending.</summary>
    public static readonly int[] Supported = [9600, 19200, 38400, 57600, 115200];

    /// <summary>True if <paramref name="baudRate"/> is an OSDP-defined rate.</summary>
    public static bool IsSupported(int baudRate) => Supported.Contains(baudRate);

    /// <summary>
    /// Resolves a nullable configured rate to the rate to actually use.
    /// </summary>
    public static int Resolve(int? baudRate) => baudRate ?? Default;
}
