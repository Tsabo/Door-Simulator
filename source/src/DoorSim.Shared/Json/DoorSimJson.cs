using System.Text.Json;
using System.Text.Json.Serialization;

namespace DoorSim.Shared.Json;

/// <summary>
/// Shared JSON options for talking to the DoorSim API. Enums serialize as their string
/// name (e.g. "Wiegand26") rather than the default integer, so payloads are self-explanatory
/// and match what the generated OpenAPI schema documents.
/// </summary>
public static class DoorSimJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
