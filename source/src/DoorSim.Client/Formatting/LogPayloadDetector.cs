using System.Text.Json;

namespace DoorSim.Client.Formatting;

/// <summary>Picks out a SQL or JSON payload worth rendering separately from a log line's summary text.</summary>
internal static class LogPayloadDetector
{
    private const string EfCommandSourceContext = "Microsoft.EntityFrameworkCore.Database.Command";

    // EF Core's relational logger's CommandExecuted message template is:
    // "Executed DbCommand ({elapsed}ms) [Parameters=[{parameters}], CommandType='{commandType}',
    // CommandTimeout='{commandTimeout}']{newLine}{commandText}" (confirmed against the shipped
    // Microsoft.EntityFrameworkCore.Relational.dll) — the property name is "commandText".
    private const string CommandTextPropertyKey = "commandText";

    public static (LogPayloadKind Kind, string? Payload) Detect(LogLine line)
    {
        if (line is { SourceContext: EfCommandSourceContext, Properties: { } efProperties }
            && efProperties.TryGetValue(CommandTextPropertyKey, out var commandText))
            return (LogPayloadKind.Sql, commandText);

        if (line.Properties is not { } properties)
        {
            return LooksLikeJson(line.Message)
                ? (LogPayloadKind.Json, line.Message)
                : (LogPayloadKind.None, null);
        }

        foreach (var value in properties.Values)
        {
            if (LooksLikeJson(value))
                return (LogPayloadKind.Json, value);
        }

        return LooksLikeJson(line.Message)
            ? (LogPayloadKind.Json, line.Message)
            : (LogPayloadKind.None, null);
    }

    private static bool LooksLikeJson(string text)
    {
        var trimmed = text.AsSpan().Trim();
        if (trimmed.IsEmpty || trimmed[0] != '{' && trimmed[0] != '[')
            return false;

        try
        {
            using var doc = JsonDocument.Parse(text);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
