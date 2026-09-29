using System.ComponentModel;
using DoorSim.Endpoints;
using ModelContextProtocol.Server;

namespace DoorSim.Mcp;

/// <summary>MCP tools describing the running server — version and which hardware features it can drive.</summary>
[McpServerToolType]
internal static class SystemTools
{
    [McpServerTool(Name = "get_system_info", ReadOnly = true)]
    [Description(
        "Get the DoorSim server version, host OS/architecture, and whether GPIO is available. When " +
        "gpioAvailable is false, create_door/update_door reject Wiegand doors and GPIO DPS/REX pins — " +
        "use OSDP for card reads and Modbus for door contacts instead.")]
    public static SystemInfo GetSystemInfo(HostCapabilities capabilities) =>
        SystemEndpoints.GetSystemInfo(capabilities);
}
