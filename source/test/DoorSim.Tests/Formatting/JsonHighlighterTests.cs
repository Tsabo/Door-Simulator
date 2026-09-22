using DoorSim.Client.Formatting;

namespace DoorSim.Tests.Formatting;

public class JsonHighlighterTests
{
    [Test]
    public async Task Highlight_ObjectKey_IsWrappedInKeySpan()
    {
        var html = JsonHighlighter.Highlight("""{"name":"door"}""").Value!;

        await Assert.That(html).Contains("<span class=\"json-key\">&quot;name&quot;</span>");
    }

    [Test]
    public async Task Highlight_StringValue_IsWrappedInStrSpanNotKeySpan()
    {
        var html = JsonHighlighter.Highlight("""{"name":"door"}""").Value!;

        await Assert.That(html).Contains("<span class=\"json-str\">&quot;door&quot;</span>");
    }

    [Test]
    public async Task Highlight_Minified_IsPrettyPrinted()
    {
        var html = JsonHighlighter.Highlight("""{"a":1,"b":2}""").Value!;

        await Assert.That(html).Contains('\n');
    }

    [Test]
    public async Task Highlight_EmbeddedHtmlInStringValue_IsEncodedNotInjected()
    {
        // JsonSerializer's default encoder already Unicode-escapes '<'/'>' within string values
        // (</>) before the highlighter ever sees them, so no raw '<img' tag can appear.
        var html = JsonHighlighter.Highlight("""{"note":"<img src=x>"}""").Value!;

        await Assert.That(html).DoesNotContain("<img");
    }

    [Test]
    public async Task Highlight_Malformed_FallsBackWithoutThrowing()
    {
        var html = JsonHighlighter.Highlight("{not json").Value!;

        await Assert.That(html).Contains("not json");
    }
}
