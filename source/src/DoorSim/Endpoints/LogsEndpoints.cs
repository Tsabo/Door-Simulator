using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace DoorSim.Endpoints;

public static class LogsEndpoints
{
    public static IEndpointRouteBuilder MapLogsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/logs").WithTags("Logs");

        group.MapGet("/stream", (LogEventBus bus, CancellationToken ct) =>
                TypedResults.ServerSentEvents(StreamAsync(bus, ct)))
            .WithName("StreamLogs")
            .WithSummary("Server-Sent Events stream of application log lines.")
            .WithDescription(
                "Replays the last 200 buffered lines on connect, then pushes new lines as they're " +
                "logged, over a single persistent connection.");

        group.MapGet("/download", async (IConfiguration config, IWebHostEnvironment env) =>
            {
                var logDirectory = config["Logs:Directory"]
                                    ?? Path.Combine(env.ContentRootPath, "logs");

                if (!Directory.Exists(logDirectory))
                    return Results.NotFound("No log files on disk yet.");

                // Built into a MemoryStream rather than streamed straight to the response: ZipArchive
                // finalizes each entry (and the central directory) with synchronous Stream.Write calls,
                // which Kestrel's response body rejects (AllowSynchronousIO is false by default).
                var buffer = new MemoryStream();
                await using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach (var file in Directory.EnumerateFiles(logDirectory, "*.log"))
                    {
                        var entry = zip.CreateEntry(Path.GetFileName(file), CompressionLevel.Fastest);
                        await using var entryStream = entry.Open();
                        // Serilog still holds this file open for writing (FileAccess.Write). Our own
                        // open must declare FileShare.Write too, or Windows refuses it even though we
                        // only want to read — declaring FileShare.Read alone isn't enough to coexist.
                        await using var fileStream = new FileStream(
                            file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        await fileStream.CopyToAsync(entryStream);
                    }
                }

                buffer.Position = 0;
                return Results.Stream(buffer, "application/zip",
                    $"doorsim-logs-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
            })
            .WithName("DownloadLogs")
            .WithSummary("Download all rolling log files as a zip archive.")
            .Produces(StatusCodes.Status200OK)
            .Produces<string>(StatusCodes.Status404NotFound);

        return app;
    }

    private static async IAsyncEnumerable<LogLine> StreamAsync(
        LogEventBus bus, [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var line in bus.GetBacklog())
            yield return line;

        var reader = bus.Subscribe(out var channel);
        try
        {
            await foreach (var line in reader.ReadAllAsync(ct))
                yield return line;
        }
        finally
        {
            bus.Unsubscribe(channel);
        }
    }
}
