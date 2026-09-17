using System.Text.Json;

namespace DoorSim.Client.Services;

/// <summary>HTTP client wrapper for the /api/cards endpoints.</summary>
public class CardApiClient(HttpClient http)
{
    public async Task<CardEntry[]> GetAllAsync() =>
        await http.GetFromJsonAsync<CardEntry[]>("/api/cards", DoorSimJson.Options) ?? [];

    public async Task<CardEntry?> GetAsync(int id) =>
        await http.GetFromJsonAsync<CardEntry>($"/api/cards/{id}", DoorSimJson.Options);

    public async Task<(CardEntry? Result, string? Error)> CreateAsync(CardEntry card)
    {
        var response = await http.PostAsJsonAsync("/api/cards", card, DoorSimJson.Options);

        if (!response.IsSuccessStatusCode)
            return (null, await ReadErrorAsync(response));

        return (await response.Content.ReadFromJsonAsync<CardEntry>(DoorSimJson.Options), null);
    }

    public async Task<(CardEntry? Result, string? Error)> UpdateAsync(int id, CardEntry card)
    {
        var response = await http.PutAsJsonAsync($"/api/cards/{id}", card, DoorSimJson.Options);

        if (!response.IsSuccessStatusCode)
            return (null, await ReadErrorAsync(response));

        return (await response.Content.ReadFromJsonAsync<CardEntry>(DoorSimJson.Options), null);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var response = await http.DeleteAsync($"/api/cards/{id}");

        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// The endpoints return the message via <c>Results.BadRequest(string)</c>, which serialises it
    /// as a JSON string — so the raw body arrives wrapped in quotes. Unwrap it before it reaches
    /// the UI, falling back to the raw body if it isn't JSON.
    /// </summary>
    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        try
        {
            return JsonSerializer.Deserialize<string>(body) ?? body;
        }
        catch (JsonException)
        {
            return body;
        }
    }
}
