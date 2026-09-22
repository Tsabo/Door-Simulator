using System.Text.Json;
using System.Text.RegularExpressions;

namespace DoorSim.Client.Formatting;

internal static class JsonHighlighter
{
    private static readonly Regex TokenPattern = new(
        """(?<key>"(?:\\.|[^"\\])*"(?=\s*:))|(?<str>"(?:\\.|[^"\\])*")|(?<num>-?\d+(\.\d+)?([eE][+-]?\d+)?)|(?<bool>\btrue\b|\bfalse\b|\bnull\b)|(?<punct>[{}\[\],:])""",
        RegexOptions.Compiled);

    private static readonly Dictionary<string, string> Classes = new()
    {
        ["key"] = "json-key",
        ["str"] = "json-str",
        ["num"] = "json-num",
        ["bool"] = "json-bool",
        ["punct"] = "json-punct",
    };

    private static readonly JsonSerializerOptions PrettyPrintOptions = new() { WriteIndented = true };

    public static MarkupString Highlight(string rawJson)
    {
        string pretty;
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            pretty = JsonSerializer.Serialize(doc.RootElement, PrettyPrintOptions);
        }
        catch (JsonException)
        {
            pretty = rawJson;
        }

        return HighlightRenderer.Render(pretty, TokenPattern, Classes);
    }
}
