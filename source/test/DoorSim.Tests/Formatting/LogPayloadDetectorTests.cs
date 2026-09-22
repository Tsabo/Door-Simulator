using DoorSim.Client.Formatting;
using DoorSim.Shared.Models;

namespace DoorSim.Tests.Formatting;

public class LogPayloadDetectorTests
{
    [Test]
    public async Task Detect_EfCommandWithCommandTextProperty_ReturnsSql()
    {
        var line = new LogLine(
            DateTimeOffset.UtcNow, LogSeverity.Information,
            "Microsoft.EntityFrameworkCore.Database.Command",
            "Executed DbCommand (1ms)", null,
            new Dictionary<string, string> { ["commandText"] = "SELECT 1" });

        var (kind, payload) = LogPayloadDetector.Detect(line);

        await Assert.That(kind).IsEqualTo(LogPayloadKind.Sql);
        await Assert.That(payload).IsEqualTo("SELECT 1");
    }

    [Test]
    public async Task Detect_PropertyHoldingJson_ReturnsJson()
    {
        var line = new LogLine(
            DateTimeOffset.UtcNow, LogSeverity.Information, "Some.Source",
            "Settings updated", null,
            new Dictionary<string, string> { ["settings"] = """{"a":1}""" });

        var (kind, payload) = LogPayloadDetector.Detect(line);

        await Assert.That(kind).IsEqualTo(LogPayloadKind.Json);
        await Assert.That(payload).IsEqualTo("""{"a":1}""");
    }

    [Test]
    public async Task Detect_MessageIsWholeJsonArray_ReturnsJson()
    {
        var line = new LogLine(
            DateTimeOffset.UtcNow, LogSeverity.Information, "Some.Source", "[1,2,3]", null);

        var (kind, _) = LogPayloadDetector.Detect(line);

        await Assert.That(kind).IsEqualTo(LogPayloadKind.Json);
    }

    [Test]
    public async Task Detect_OrdinaryLine_ReturnsNone()
    {
        var line = new LogLine(
            DateTimeOffset.UtcNow, LogSeverity.Information, "Some.Source", "Door opened", null);

        var (kind, payload) = LogPayloadDetector.Detect(line);

        await Assert.That(kind).IsEqualTo(LogPayloadKind.None);
        await Assert.That(payload).IsNull();
    }
}
