using DoorSim.Client.Formatting;

namespace DoorSim.Tests.Formatting;

public class SqlHighlighterTests
{
    [Test]
    public async Task Highlight_Keyword_IsWrappedInKeywordSpan()
    {
        var html = SqlHighlighter.Highlight("SELECT 1").Value!;

        await Assert.That(html).Contains("<span class=\"sql-kw\">SELECT</span>");
    }

    [Test]
    public async Task Highlight_QuotedIdentifier_IsWrappedInIdentSpan()
    {
        var html = SqlHighlighter.Highlight("SELECT \"d\".\"Id\" FROM \"Doors\" AS \"d\"").Value!;

        await Assert.That(html).Contains("<span class=\"sql-ident\">&quot;d&quot;</span>");
    }

    [Test]
    public async Task Highlight_EmbeddedHtml_IsEncodedNotInjected()
    {
        var html = SqlHighlighter.Highlight("SELECT '<script>alert(1)</script>'").Value!;

        await Assert.That(html).DoesNotContain("<script>");
        await Assert.That(html).Contains("&lt;script&gt;");
    }
}
