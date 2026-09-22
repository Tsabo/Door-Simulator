using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace DoorSim.Client.Formatting;

/// <summary>
/// Shared regex-token-to-HTML renderer used by the SQL and JSON highlighters. Every literal
/// fragment — matched or not — is HTML-encoded before being wrapped in a hard-coded &lt;span&gt;,
/// so no highlighter needs to reimplement that rule itself.
/// </summary>
internal static class HighlightRenderer
{
    public static MarkupString Render(string source, Regex tokenPattern, IReadOnlyDictionary<string, string> groupToCssClass)
    {
        var sb = new StringBuilder();
        var lastIndex = 0;

        foreach (Match match in tokenPattern.Matches(source))
        {
            if (match.Index > lastIndex)
                sb.Append(WebUtility.HtmlEncode(source[lastIndex..match.Index]));

            var group = match.Groups.Cast<Group>().Skip(1).FirstOrDefault(p => p.Success);
            if (group is null || !groupToCssClass.TryGetValue(group.Name, out var cssClass))
                sb.Append(WebUtility.HtmlEncode(match.Value));
            else
            {
                sb.Append("<span class=\"")
                    .Append(cssClass)
                    .Append("\">")
                    .Append(WebUtility.HtmlEncode(match.Value))
                    .Append("</span>");
            }

            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < source.Length)
            sb.Append(WebUtility.HtmlEncode(source[lastIndex..]));

        return new MarkupString(sb.ToString());
    }
}
