using DoorSim.Client.Formatting;

namespace DoorSim.Tests.Formatting;

public class KeyValueHighlighterTests
{
    [Test]
    public async Task Highlight_SingleQuotedValue_WrapsKeyAndValueSeparately()
    {
        var html = KeyValueHighlighter.Highlight("CommandType='Text'").Value!;

        await Assert.That(html).Contains("<span class=\"kv-key\">CommandType</span>=");
        await Assert.That(html).Contains("'<span class=\"kv-val\">Text</span>'");
    }

    [Test]
    public async Task Highlight_DoubleQuotedValue_WrapsKeyAndValueSeparately()
    {
        var html = KeyValueHighlighter.Highlight("""name="value" """).Value!;

        await Assert.That(html).Contains("<span class=\"kv-key\">name</span>=");
        await Assert.That(html).Contains("\"<span class=\"kv-val\">value</span>\"");
    }

    [Test]
    public async Task Highlight_BareValue_WrapsKeyAndValue()
    {
        var html = KeyValueHighlighter.Highlight("Timeout=30").Value!;

        await Assert.That(html).Contains("<span class=\"kv-key\">Timeout</span>=<span class=\"kv-val\">30</span>");
    }

    [Test]
    public async Task Highlight_PlainTextWithNoPairs_IsUnchangedButEncoded()
    {
        var html = KeyValueHighlighter.Highlight("Door opened").Value!;

        await Assert.That(html).IsEqualTo("Door opened");
    }

    [Test]
    public async Task Highlight_EmbeddedHtmlInValue_IsEncodedNotInjected()
    {
        var html = KeyValueHighlighter.Highlight("""note="<script>x</script>" """).Value!;

        await Assert.That(html).DoesNotContain("<script>");
        await Assert.That(html).Contains("&lt;script&gt;");
    }
}
