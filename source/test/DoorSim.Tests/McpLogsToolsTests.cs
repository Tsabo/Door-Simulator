using DoorSim.Logging;
using DoorSim.Mcp;
using DoorSim.Shared.Models;

namespace DoorSim.Tests;

public class McpLogsToolsTests
{
    [Test]
    public async Task GetRecentLogs_NoCount_ReturnsFullBacklog()
    {
        var bus = new LogEventBus();
        bus.Publish(new LogLine(DateTimeOffset.UtcNow, LogSeverity.Information, "Test", "one", null));
        bus.Publish(new LogLine(DateTimeOffset.UtcNow, LogSeverity.Information, "Test", "two", null));

        var lines = LogsTools.GetRecentLogs(null, bus);

        await Assert.That(lines.Length).IsEqualTo(2);
    }

    [Test]
    public async Task GetRecentLogs_WithCount_ReturnsMostRecentTail()
    {
        var bus = new LogEventBus();
        bus.Publish(new LogLine(DateTimeOffset.UtcNow, LogSeverity.Information, "Test", "one", null));
        bus.Publish(new LogLine(DateTimeOffset.UtcNow, LogSeverity.Information, "Test", "two", null));
        bus.Publish(new LogLine(DateTimeOffset.UtcNow, LogSeverity.Information, "Test", "three", null));

        var lines = LogsTools.GetRecentLogs(2, bus);

        await Assert.That(lines.Length).IsEqualTo(2);
        await Assert.That(lines[0].Message).IsEqualTo("two");
        await Assert.That(lines[1].Message).IsEqualTo("three");
    }

    [Test]
    public async Task GetRecentLogs_EmptyBacklog_ReturnsEmptyArray()
    {
        var bus = new LogEventBus();

        var lines = LogsTools.GetRecentLogs(10, bus);

        await Assert.That(lines).IsEmpty();
    }
}
