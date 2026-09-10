namespace DoorSim.Client.Services;

/// <summary>HTTP client wrapper for the /api/cards endpoints.</summary>
public class CardApiClient(HttpClient http)
{
    public async Task<CardEntry[]> GetAllAsync() =>
        await http.GetFromJsonAsync<CardEntry[]>("/api/cards", DoorSimJson.Options) ?? [];

    public async Task<CardEntry?> GetAsync(int id) =>
        await http.GetFromJsonAsync<CardEntry>($"/api/cards/{id}", DoorSimJson.Options);

    public async Task<CardEntry?> CreateAsync(CardEntry card)
    {
        var response = await http.PostAsJsonAsync("/api/cards", card, DoorSimJson.Options);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<CardEntry>(DoorSimJson.Options);
    }

    public async Task<CardEntry?> UpdateAsync(int id, CardEntry card)
    {
        var response = await http.PutAsJsonAsync($"/api/cards/{id}", card, DoorSimJson.Options);

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<CardEntry>(DoorSimJson.Options);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var response = await http.DeleteAsync($"/api/cards/{id}");

        return response.IsSuccessStatusCode;
    }
}
