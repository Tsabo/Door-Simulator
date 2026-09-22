using DoorSim.Logging;
using Serilog;

namespace DoorSim.Tests.Logging;

public class LogBroadcastSinkTests
{
    [Test]
    public async Task Emit_LongStringProperty_IsCapturedRaw()
    {
        var bus = new LogEventBus();
        var log = new LoggerConfiguration()
            .WriteTo.Sink(new LogBroadcastSink(bus))
            .CreateLogger();

        var sql = "SELECT \"d\".\"Id\" FROM \"Doors\" AS \"d\" WHERE \"d\".\"Id\" = @id ORDER BY \"d\".\"Label\"";
        log.Information("Executed DbCommand {commandText}", sql);

        var line = bus.GetBacklog().Single();

        await Assert.That(line.Properties).IsNotNull();
        await Assert.That(line.Properties!["commandText"]).IsEqualTo(sql);
    }

    [Test]
    public async Task Emit_ShortStringProperty_IsNotCaptured()
    {
        var bus = new LogEventBus();
        var log = new LoggerConfiguration()
            .WriteTo.Sink(new LogBroadcastSink(bus))
            .CreateLogger();

        log.Information("Door {label} opened", "Front Door");

        var line = bus.GetBacklog().Single();

        var hasLabel = line.Properties?.ContainsKey("label") ?? false;
        await Assert.That(hasLabel).IsFalse();
    }

    [Test]
    public async Task Emit_ShortStringPropertyWithNewline_IsCaptured()
    {
        var bus = new LogEventBus();
        var log = new LoggerConfiguration()
            .WriteTo.Sink(new LogBroadcastSink(bus))
            .CreateLogger();

        log.Information("Payload {payload}", "a\nb");

        var line = bus.GetBacklog().Single();

        await Assert.That(line.Properties!["payload"]).IsEqualTo("a\nb");
    }

    [Test]
    public async Task Emit_SourceContext_IsNotDuplicatedIntoProperties()
    {
        var bus = new LogEventBus();
        var log = new LoggerConfiguration()
            .WriteTo.Sink(new LogBroadcastSink(bus))
            .Enrich.WithProperty("SourceContext", "Some.Very.Long.Namespace.That.Exceeds.The.Length.Threshold")
            .CreateLogger();

        log.Information("hello");

        var line = bus.GetBacklog().Single();

        var hasSourceContext = line.Properties?.ContainsKey("SourceContext") ?? false;
        await Assert.That(hasSourceContext).IsFalse();
    }
}
