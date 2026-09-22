namespace DoorSim.Client.Formatting;

/// <summary>
/// Wraps a <see cref="LogLine" /> with its rendering decisions precomputed once at ingestion time
/// (not on every re-render), so the log console can append thousands of lines without repeatedly
/// re-running regex-based highlighting over lines already on screen.
/// </summary>
internal sealed record LogLineView(
    LogLine Line,
    string Summary,
    MarkupString SummaryHtml,
    LogPayloadKind Kind,
    string? RawPayload,
    MarkupString? Highlighted)
{
    public static LogLineView Create(LogLine line)
    {
        var (kind, payload) = LogPayloadDetector.Detect(line);

        var (summary, highlighted) = kind switch
        {
            LogPayloadKind.Sql => (TruncateAtFirstNewline(line.Message), SqlHighlighter.Highlight(payload!)),
            LogPayloadKind.Json when payload == line.Message => ("", JsonHighlighter.Highlight(payload!)),
            LogPayloadKind.Json => (line.Message, JsonHighlighter.Highlight(payload!)),
            var _ => (line.Message, (MarkupString?)null),
        };

        return new LogLineView(line, summary, KeyValueHighlighter.Highlight(summary), kind, payload, highlighted);
    }

    private static string TruncateAtFirstNewline(string message)
    {
        var index = message.IndexOfAny(['\r', '\n']);
        return index >= 0
            ? message[..index]
            : message;
    }
}
