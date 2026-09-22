using DoorSim.Client.Formatting;
using DoorSim.Logging;
using Serilog;

namespace DoorSim.Tests.Formatting;

/// <summary>
/// End-to-end check tying the sink's raw-property capture to the client's detection and
/// highlighting, using a real EF Core CommandExecuted message template and a real captured
/// SQL statement (from the live deployment's logs) that previously rendered as escaped garbage.
/// </summary>
public class EfCommandPipelineTests
{
    [Test]
    public async Task DoorsQuery_RendersCleanHighlightedSql_NotEscaped()
    {
        var bus = new LogEventBus();
        var log = new LoggerConfiguration()
            .WriteTo.Sink(new LogBroadcastSink(bus))
            .Enrich.WithProperty("SourceContext", "Microsoft.EntityFrameworkCore.Database.Command")
            .CreateLogger();

        const string sql =
            """
            SELECT "d"."Id", "d"."D0Pin", "d"."Label", "d"."ModbusSerialPort"
            FROM "Doors" AS "d"
            ORDER BY "d"."Label"
            """;

        // commandType is EF Core's real System.Data.CommandType enum, not a string — Serilog only
        // quotes/escapes string-typed scalars, so this renders as clean "Text" unlike commandText.
        log.Information(
            "Executed DbCommand ({elapsed}ms) [Parameters=[{parameters}], CommandType='{commandType}', CommandTimeout='{commandTimeout}']{newLine}{commandText}",
            1, "", System.Data.CommandType.Text, 30, Environment.NewLine, sql);

        var line = bus.GetBacklog().Single();
        var (kind, payload) = LogPayloadDetector.Detect(line);
        await Assert.That(kind).IsEqualTo(LogPayloadKind.Sql);
        await Assert.That(payload).IsEqualTo(sql);

        var view = LogLineView.Create(line);
        var html = view.Highlighted!.Value.Value!;

        // The old, broken behavior escaped every embedded quote (\"d\") and wrapped the whole
        // thing in an extra pair of quotes — none of that should survive into the highlighted output.
        await Assert.That(html).DoesNotContain("\\\"");
        await Assert.That(html).Contains("<span class=\"sql-kw\">SELECT</span>");
        await Assert.That(html).Contains("<span class=\"sql-ident\">&quot;Doors&quot;</span>");
        await Assert.That(view.Summary).DoesNotContain(sql);

        // The one-line summary above the SQL block ("Executed DbCommand (...) [Parameters=[...],
        // CommandType='Text', CommandTimeout='30']") is full of name=value pairs — confirm those
        // get highlighted too.
        var summaryHtml = view.SummaryHtml.Value!;
        await Assert.That(summaryHtml).Contains("<span class=\"kv-key\">CommandType</span>=");
        await Assert.That(summaryHtml).Contains("'<span class=\"kv-val\">Text</span>'");
        await Assert.That(summaryHtml).Contains("<span class=\"kv-key\">CommandTimeout</span>=");
    }
}
