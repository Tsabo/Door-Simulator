using System.Net;
using System.Text.Json;

namespace DoorSim.Client.Services;

/// <summary>HTTP client wrapper for the /api/card-formats endpoints.</summary>
/// <remarks>
/// Returns the server's validation string rather than throwing, because a rejected mask set is an
/// expected outcome on the format editor and the message is what the user needs to see.
/// </remarks>
public class CardFormatApiClient(HttpClient http)
{
    public async Task<CustomCardFormat[]> GetAllAsync() =>
        await http.GetFromJsonAsync<CustomCardFormat[]>("/api/card-formats", DoorSimJson.Options) ?? [];

    public async Task<(CustomCardFormat? Result, string? Error)> CreateAsync(CustomCardFormat format)
    {
        var response = await http.PostAsJsonAsync("/api/card-formats", format, DoorSimJson.Options);

        if (!response.IsSuccessStatusCode)
            return (null, await ReadErrorAsync(response));

        return (await response.Content.ReadFromJsonAsync<CustomCardFormat>(DoorSimJson.Options), null);
    }

    public async Task<(CustomCardFormat? Result, string? Error)> UpdateAsync(int id, CustomCardFormat format)
    {
        var response = await http.PutAsJsonAsync($"/api/card-formats/{id}", format, DoorSimJson.Options);

        if (!response.IsSuccessStatusCode)
            return (null, await ReadErrorAsync(response));

        return (await response.Content.ReadFromJsonAsync<CustomCardFormat>(DoorSimJson.Options), null);
    }

    public async Task<(bool Deleted, string? Error)> DeleteAsync(int id)
    {
        var response = await http.DeleteAsync($"/api/card-formats/{id}");

        if (response.IsSuccessStatusCode)
            return (true, null);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return (false, null);

        return (false, await ReadErrorAsync(response));
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
