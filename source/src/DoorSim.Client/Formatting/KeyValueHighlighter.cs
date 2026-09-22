using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace DoorSim.Client.Formatting;

/// <summary>
/// Highlights <c>name=value</c>, <c>name="value"</c>, and <c>name='value'</c> pairs embedded in
/// otherwise plain log text — e.g. EF Core's "CommandType='Text', CommandTimeout='30'" summary line.
/// Unlike <see cref="HighlightRenderer" />, each match here has multiple parts (key, quotes, value)
/// that need separate styling within a single match, so it builds its own markup.
/// </summary>
internal static class KeyValueHighlighter
{
    private static readonly Regex TokenPattern = new(
        """(?<key>\b[A-Za-z_][A-Za-z0-9_]*)=(?:"(?<dq>[^"]*)"|'(?<sq>[^']*)'|(?<bare>[^\s,\]\)]+))""",
        RegexOptions.Compiled);

    public static MarkupString Highlight(string text)
    {
        var sb = new StringBuilder();
        var lastIndex = 0;

        foreach (Match match in TokenPattern.Matches(text))
        {
            if (match.Index > lastIndex)
                sb.Append(WebUtility.HtmlEncode(text[lastIndex..match.Index]));

            sb.Append("<span class=\"kv-key\">").Append(WebUtility.HtmlEncode(match.Groups["key"].Value)).Append("</span>=");

            var dq = match.Groups["dq"];
            var sq = match.Groups["sq"];
            var bare = match.Groups["bare"];

            if (dq.Success)
                sb.Append('"').Append("<span class=\"kv-val\">").Append(WebUtility.HtmlEncode(dq.Value)).Append("</span>").Append('"');
            else if (sq.Success)
                sb.Append('\'').Append("<span class=\"kv-val\">").Append(WebUtility.HtmlEncode(sq.Value)).Append("</span>").Append('\'');
            else
                sb.Append("<span class=\"kv-val\">").Append(WebUtility.HtmlEncode(bare.Value)).Append("</span>");

            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < text.Length)
            sb.Append(WebUtility.HtmlEncode(text[lastIndex..]));

        return new MarkupString(sb.ToString());
    }
}
