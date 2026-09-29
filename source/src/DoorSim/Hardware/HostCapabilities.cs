namespace DoorSim.Hardware;

/// <summary>
/// What physical I/O this host can drive, probed once at startup from the resolved
/// <see cref="GpioController" />. Features that need hardware the host lacks are rejected up front
/// instead of silently doing nothing.
/// </summary>
/// <param name="GpioAvailable">
/// True when a GPIO controller opened (a Raspberry Pi, or a container given <c>/dev/gpiochip0</c>).
/// Wiegand D0/D1 output and GPIO-driven DPS/REX contacts require it.
/// </param>
public sealed record HostCapabilities(bool GpioAvailable)
{
    public const string GpioUnavailableMessage =
        "GPIO is not available on this host (Wiegand output and GPIO DPS/REX contacts need a Raspberry Pi). " +
        "Use OSDP for card reads and Modbus for door contacts.";

    /// <summary>All hardware present — the default when no capabilities are supplied (e.g. unit tests).</summary>
    public static HostCapabilities Unrestricted { get; } = new(true);
}
