using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace DoorSim.Mcp;

/// <summary>MCP tools wrapping <see cref="SimulationSettingsService" /> — global simulation timing.</summary>
[McpServerToolType]
internal static class SettingsTools
{
    [McpServerTool(Name = "get_simulation_settings", ReadOnly = true)]
    [Description(
        "Get the current simulation timing settings: card-to-door delay, door-open duration, REX lead " +
        "time, quick-REX duration, and queue item delay used by the simulate_* tools.")]
    public static SimulationTimingSettings GetSimulationSettings(SimulationSettingsService service) =>
        service.Current;

    [McpServerTool(Name = "update_simulation_settings", Destructive = false, Idempotent = true)]
    [Description("Update global simulation timing settings. Applies immediately to all subsequent simulated events — no restart required.")]
    public static async Task<SimulationTimingSettings> UpdateSimulationSettings(SimulationTimingSettings settings,
        SimulationSettingsService service)
    {
        var (result, error) = await service.UpdateAsync(settings);
        if (error is not null)
            throw new McpException(error);

        return result!;
    }
}
