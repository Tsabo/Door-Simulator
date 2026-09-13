using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace DoorSim.Client.Services;

/// <summary>HTTP client wrapper for the /api/logs endpoints.</summary>
public class LogsApiClient(HttpClient http)
{
    /// <summary>
    /// Opens a persistent SSE connection and yields log lines as they arrive (backlog first,
    /// then live). Reconnects automatically on transient errors after a 2-second delay.
    /// </summary>
    public async IAsyncEnumerable<LogLine> SubscribeAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            // Drive the inner enumerator manually so the yield lives outside any try/catch.
            // C# forbids yield inside a try block that has a catch clause.
            var enumerator = ReadStreamOnceAsync(ct).GetAsyncEnumerator(ct);
            bool more;
            do
            {
                LogLine? line = null;
                try
                {
                    more = await enumerator.MoveNextAsync();
                    if (more)
                        line = enumerator.Current;
                }
                catch (OperationCanceledException)
                {
                    await enumerator.DisposeAsync();
                    yield break;
                }
                catch
                {
                    more = false; // stop inner loop, reconnect below
                }

                // yield is outside the try/catch
                if (line is not null)
                    yield return line;
            } while (more);

            await enumerator.DisposeAsync();

            if (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(2000, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    yield break;
                }
            }
        }
    }

    /// <summary>
    /// Opens one SSE connection and streams lines until the connection closes or
    /// the token is cancelled. No catch clause — exceptions propagate to the caller.
    /// </summary>
    private async IAsyncEnumerable<LogLine> ReadStreamOnceAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/logs/stream");
        request.SetBrowserResponseStreamingEnabled(true);

        using var response = await http.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null)
                yield break; // server closed — outer loop will reconnect

            if (!line.StartsWith("data: "))
                continue;

            var json = line["data: ".Length..];
            if (string.IsNullOrWhiteSpace(json))
                continue;

            var logLine = JsonSerializer.Deserialize<LogLine>(json, DoorSimJson.Options);
            if (logLine is not null)
                yield return logLine;
        }
    }
}
