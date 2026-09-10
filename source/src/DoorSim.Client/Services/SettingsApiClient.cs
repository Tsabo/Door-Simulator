namespace DoorSim.Client.Services;

/// <summary>HTTP client wrapper for the /api/settings endpoints.</summary>
public class SettingsApiClient(HttpClient http)
{
    public async Task<SimulationTimingSettings> GetTimingAsync() =>
        await http.GetFromJsonAsync<SimulationTimingSettings>("/api/settings/simulation") ?? new();

    public async Task<(SimulationTimingSettings? Result, string? Error)> UpdateTimingAsync(SimulationTimingSettings settings)
    {
        var response = await http.PutAsJsonAsync("/api/settings/simulation", settings);
        if (!response.IsSuccessStatusCode)
            return (null, await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<SimulationTimingSettings>(), null);
    }
}
