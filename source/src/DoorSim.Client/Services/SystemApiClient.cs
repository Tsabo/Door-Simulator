namespace DoorSim.Client.Services;

/// <summary>
/// HTTP client wrapper for the /api/system endpoints. The server's version and hardware
/// capabilities don't change while it runs, so the first successful response is cached.
/// </summary>
public class SystemApiClient(HttpClient http)
{
    private Task<SystemInfo?>? _info;

    /// <summary>Server version and host capabilities, or null when the server could not be reached.</summary>
    public Task<SystemInfo?> GetInfoAsync() => _info ??= LoadAsync();

    /// <summary>
    /// Whether GPIO features can be offered. Assumes true when the info is unavailable, so a transient
    /// failure never hides options — the server still rejects anything the host can't drive.
    /// </summary>
    public async Task<bool> IsGpioAvailableAsync() => (await GetInfoAsync())?.GpioAvailable ?? true;

    private async Task<SystemInfo?> LoadAsync()
    {
        try
        {
            return await http.GetFromJsonAsync<SystemInfo>("/api/system/info", DoorSimJson.Options);
        }
        catch (HttpRequestException)
        {
            _info = null; // retry on the next call rather than caching the failure

            return null;
        }
    }
}
