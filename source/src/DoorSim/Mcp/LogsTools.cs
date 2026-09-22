using System.ComponentModel;
using ModelContextProtocol.Server;

namespace DoorSim.Mcp;

/// <summary>
/// MCP tools wrapping <see cref="LogEventBus" /> — lets an agent see what DoorSim actually logged
/// after driving a scenario, since it has no other way to observe PACS-side outcomes.
/// </summary>
[McpServerToolType]
internal static class LogsTools
{
    [McpServerTool(Name = "get_recent_logs", ReadOnly = true)]
    [Description(
        "Get recently logged application lines, oldest first. Backed by the same in-memory buffer " +
        "(up to 200 lines) the live log viewer replays on connect.")]
    public static LogLine[] GetRecentLogs([Description("Maximum number of lines to return, counting from the most recent. Omit for the full buffer.")] int? count,
        LogEventBus bus)
    {
        var backlog = bus.GetBacklog();

        return count is > 0 && count < backlog.Length
            ? backlog[^count.Value..]
            : backlog;
    }
}
