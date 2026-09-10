namespace DoorSim.Client.Services;

/// <summary>HTTP client wrapper for the /api/doors endpoints.</summary>
public class DoorApiClient(HttpClient http)
{
    public async Task<DoorConfiguration[]> GetAllAsync() =>
        await http.GetFromJsonAsync<DoorConfiguration[]>("/api/doors", DoorSimJson.Options) ?? [];

    public async Task<string[]> GetAvailableSerialPortsAsync(int? excludeDoorId = null)
    {
        var uri = excludeDoorId.HasValue
            ? $"/api/doors/serial-ports?excludeDoorId={excludeDoorId.Value}"
            : "/api/doors/serial-ports";

        return await http.GetFromJsonAsync<string[]>(uri, DoorSimJson.Options) ?? [];
    }

    public async Task<DoorConfiguration?> GetAsync(int id) =>
        await http.GetFromJsonAsync<DoorConfiguration>($"/api/doors/{id}", DoorSimJson.Options);

    public async Task<(DoorConfiguration? Result, string? Error)> CreateAsync(DoorConfiguration door)
    {
        var response = await http.PostAsJsonAsync("/api/doors", door, DoorSimJson.Options);
        if (!response.IsSuccessStatusCode)
            return (null, await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<DoorConfiguration>(DoorSimJson.Options), null);
    }

    public async Task<(DoorConfiguration? Result, string? Error)> UpdateAsync(int id, DoorConfiguration door)
    {
        var response = await http.PutAsJsonAsync($"/api/doors/{id}", door, DoorSimJson.Options);
        if (!response.IsSuccessStatusCode)
            return (null, await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<DoorConfiguration>(DoorSimJson.Options), null);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var response = await http.DeleteAsync($"/api/doors/{id}");
        return response.IsSuccessStatusCode;
    }
}
