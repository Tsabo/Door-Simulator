using System.Text.RegularExpressions;

namespace DoorSim.Client.Formatting;

internal static class SqlHighlighter
{
    private static readonly string[] Keywords =
    [
        "SELECT", "FROM", "WHERE", "INSERT", "INTO", "VALUES", "UPDATE", "SET", "DELETE", "JOIN",
        "INNER", "LEFT", "RIGHT", "OUTER", "ON", "AND", "OR", "NOT", "NULL", "IS", "IN", "LIKE",
        "ORDER", "BY", "GROUP", "HAVING", "AS", "DISTINCT", "LIMIT", "OFFSET", "CREATE", "TABLE",
        "ALTER", "DROP", "EXEC", "DECLARE", "BEGIN", "END", "TRANSACTION", "COMMIT", "ROLLBACK",
        "CASE", "WHEN", "THEN", "ELSE", "UNION", "ALL", "EXISTS", "COUNT", "SUM", "AVG", "MIN",
        "MAX", "ASC", "DESC", "DEFAULT", "PRIMARY", "KEY", "FOREIGN", "REFERENCES", "WITH",
    ];

    private static readonly Regex TokenPattern = new(
        $"""(?<comment>--[^\n]*)|(?<str>'(?:[^']|'')*')|(?<ident>"(?:[^"]|"")*")|(?<num>\b\d+(\.\d+)?\b)|(?<kw>\b(?:{string.Join('|', Keywords)})\b)|(?<punct>[(),;=<>*.])""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Dictionary<string, string> Classes = new()
    {
        ["comment"] = "sql-comment",
        ["str"] = "sql-str",
        ["ident"] = "sql-ident",
        ["num"] = "sql-num",
        ["kw"] = "sql-kw",
        ["punct"] = "sql-punct",
    };

    public static MarkupString Highlight(string sql) => HighlightRenderer.Render(sql, TokenPattern, Classes);
}
