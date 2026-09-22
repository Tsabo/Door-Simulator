using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace DoorSim.Mcp;

/// <summary>
/// MCP tools wrapping <see cref="DoorConfigService" /> — door/reader hardware configuration.
/// Create/update/delete take effect in the live simulator bank immediately, with no restart and
/// no confirmation step, so their descriptions call that out explicitly.
/// </summary>
[McpServerToolType]
internal static class DoorTools
{
    [McpServerTool(Name = "list_serial_ports", ReadOnly = true)]
    [Description(
        "List serial ports available for a new OSDP or Modbus RTU door. Scans for ttyRS485*/ttyACM*/" +
        "ttyAMA*/ttyUSB*/ttyS* devices and excludes ports already claimed by other doors.")]
    public static Task<string[]> ListSerialPorts([Description("Pass the door's own ID when re-checking ports for an existing door, so its current port isn't excluded.")] int? excludeDoorId,
        DoorConfigService service) =>
        service.GetAvailableSerialPortsAsync(excludeDoorId);

    [McpServerTool(Name = "list_doors", ReadOnly = true)]
    [Description("List all configured doors/readers.")]
    public static Task<DoorConfiguration[]> ListDoors(DoorConfigService service) =>
        service.GetAllAsync();

    [McpServerTool(Name = "get_door", ReadOnly = true)]
    [Description("Get a single door's configuration by ID.")]
    public static async Task<DoorConfiguration> GetDoor(int id, DoorConfigService service) =>
        await service.GetAsync(id) ?? throw new McpException($"Door {id} not found.");

    [McpServerTool(Name = "create_door")]
    [Description(
        "Create a new door/reader configuration. Validates GPIO pin and Modbus channel uniqueness across " +
        "all doors before saving. On success, the door goes live in the running simulator bank immediately " +
        "— no restart, no confirmation step. Most OsdpCap*/OsdpId* advanced fields can be left null for " +
        "stock simulator behavior.")]
    public static async Task<DoorConfiguration> CreateDoor(DoorConfiguration door,
        DoorConfigService service,
        DynamicSimulatorBank bank)
    {
        var (result, error) = await service.CreateAsync(door);
        if (error is not null)
            throw new McpException(error);

        await bank.AddDoor(result!);

        return result!;
    }

    [McpServerTool(Name = "update_door", Idempotent = true)]
    [Description(
        "Replace an existing door's configuration. Re-validates pin/channel uniqueness and rebuilds the " +
        "door's simulator in the running bank immediately — no restart, no confirmation step. This can " +
        "release and reassign serial ports live if the wiring changed.")]
    public static async Task<DoorConfiguration> UpdateDoor(int id,
        DoorConfiguration door,
        DoorConfigService service,
        DynamicSimulatorBank bank)
    {
        var (result, error) = await service.UpdateAsync(id, door);
        if (error is not null)
            throw new McpException(error);

        if (result is null)
            throw new McpException($"Door {id} not found.");

        await bank.AddDoor(result);

        return result;
    }

    [McpServerTool(Name = "delete_door")]
    [Description(
        "Delete a door/reader configuration. Irreversible: removes the door from the running simulator " +
        "bank, disposes its simulator, and releases its OSDP serial port immediately.")]
    public static async Task<string> DeleteDoor(int id,
        DoorConfigService service,
        DynamicSimulatorBank bank,
        SimulationOrchestrator orchestrator)
    {
        if (!await service.DeleteAsync(id))
            throw new McpException($"Door {id} not found.");

        await bank.RemoveDoor(id);
        await orchestrator.RemoveReaderQueueAsync(id);

        return $"Deleted door {id} and released its resources.";
    }
}
