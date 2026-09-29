namespace DoorSim.Shared.Models;

/// <summary>Build and host details the UI and MCP clients use to adapt to the running server.</summary>
/// <param name="Version">Server build version, e.g. <c>1.2.3+7881449</c>.</param>
/// <param name="OperatingSystem">Host OS description, e.g. <c>Debian GNU/Linux 12 (bookworm)</c>.</param>
/// <param name="Architecture">Process architecture, e.g. <c>Arm64</c>.</param>
/// <param name="GpioAvailable">
/// False on hosts without GPIO (Windows, macOS, most containers). Wiegand doors and GPIO DPS/REX pins
/// are rejected there; use OSDP and Modbus instead.
/// </param>
public sealed record SystemInfo(string Version, string OperatingSystem, string Architecture, bool GpioAvailable);
