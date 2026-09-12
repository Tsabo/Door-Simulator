using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace DoorSim.Client.Services;

/// <summary>HTTP client wrapper for the /api/simulate endpoints.</summary>
public class SimulationApiClient(HttpClient http)
{
    public async Task RunEventAsync(DoorEventRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/simulate/event", request, DoorSimJson.Options);
        response.EnsureSuccessStatusCode();
    }

    public async Task SendCardAsync(RawCardRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/simulate/send-card", request, DoorSimJson.Options);
        response.EnsureSuccessStatusCode();
    }

    public async Task SendBitsAsync(RawBitsRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/simulate/send-bits", request, DoorSimJson.Options);
        response.EnsureSuccessStatusCode();
    }

    public async Task RunRawEventAsync(RawDoorEventRequest request)
    {
        var response = await http.PostAsJsonAsync("/api/simulate/raw-event", request, DoorSimJson.Options);
        response.EnsureSuccessStatusCode();
    }

    public async Task OpenDoorAsync(int readerId)
    {
        var response = await http.PostAsync($"/api/simulate/door/{readerId}/open", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task CloseDoorAsync(int readerId)
    {
        var response = await http.PostAsync($"/api/simulate/door/{readerId}/close", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task QuickRexAsync(int readerId)
    {
        var response = await http.PostAsync($"/api/simulate/rex/{readerId}/quick", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task<SimulationStatus> GetStatusAsync(int readerId) =>
        await http.GetFromJsonAsync<SimulationStatus>($"/api/simulate/status/{readerId}", DoorSimJson.Options);

    public async Task<bool> GetConnectivityAsync(int readerId) =>
        await http.GetFromJsonAsync<bool>($"/api/simulate/connectivity/{readerId}", DoorSimJson.Options);

    /// <summary>
    /// Opens a persistent SSE connection and yields status snapshots as they arrive.
    /// Reconnects automatically on transient errors after a 2-second delay.
    /// </summary>
    public async IAsyncEnumerable<DoorStatusUpdate[]> SubscribeAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            // Drive the inner enumerator manually so the yield lives outside any try/catch.
            // C# forbids yield inside a try block that has a catch clause.
            var enumerator = ReadStreamOnceAsync(ct).GetAsyncEnumerator(ct);
            bool more;
            do
            {
                DoorStatusUpdate[]? batch = null;
                try
                {
                    more = await enumerator.MoveNextAsync();
                    if (more)
                        batch = enumerator.Current;
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
                if (batch is not null)
                    yield return batch;
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
    private async IAsyncEnumerable<DoorStatusUpdate[]> ReadStreamOnceAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/simulate/stream");
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

            var updates = JsonSerializer.Deserialize<DoorStatusUpdate[]>(json, DoorSimJson.Options);
            if (updates is not null)
                yield return updates;
        }
    }
}
