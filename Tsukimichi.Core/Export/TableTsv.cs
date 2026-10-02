using System.Text;

namespace Tsukimichi.Core.Export;

/// <summary>
/// Copy table as TSV (1.8.0, research C9 #4): the Journal table's rows or the Moonlit view as tab-separated text to
/// paste into a spreadsheet. The columns are the export's CSV columns (<see cref="ExportWriter.QuestColumns"/>,
/// <see cref="ExportWriter.MoonlitColumns"/>) plus <c>url</c>, one header row, rows joined by CRLF. A value holding a
/// tab, a line break or a double quote is quoted with its quotes doubled, which Excel, LibreOffice and Google Sheets
/// all read back as one cell. Pure.
/// </summary>
public static class TableTsv
{
    /// <summary>The name of the column every copy ends with.</summary>
    public const string UrlColumn = "url";

    /// <summary>One cell: as is, or quoted when it holds a tab, a line break or a quote.</summary>
    public static string Field(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.AsSpan().IndexOfAny("\t\r\n\"") < 0)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    /// <summary>
    /// The quest rows under the quest columns and <c>url</c> (<paramref name="url"/> per row; empty when null). A row
    /// <paramref name="masked"/> says the spoiler shield hides carries no link, as Copy for Discord drops the link on
    /// a masked name: its <c>lodestoneId</c> and <c>url</c> stay empty, since either page names the quest.
    /// </summary>
    public static string Quests(IReadOnlyList<QuestExportRow> rows, Func<QuestExportRow, string?> url, Func<QuestExportRow, bool>? masked = null)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(url);
        var sb = Header(ExportWriter.QuestColumns);
        foreach (var row in rows)
        {
            if (masked?.Invoke(row) == true)
            {
                Line(sb, ExportWriter.Fields(row with { LodestoneId = string.Empty }), null);
            }
            else
            {
                Line(sb, ExportWriter.Fields(row), url(row));
            }
        }

        return sb.ToString();
    }

    /// <summary>The reward rows under the Moonlit columns and <c>url</c> (<paramref name="url"/> per row; empty when null).</summary>
    public static string Moonlit(IReadOnlyList<MoonlitExportRow> rows, Func<MoonlitExportRow, string?> url)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(url);
        var sb = Header(ExportWriter.MoonlitColumns);
        foreach (var row in rows)
        {
            Line(sb, ExportWriter.Fields(row), url(row));
        }

        return sb.ToString();
    }

    private static StringBuilder Header(IReadOnlyList<string> columns)
    {
        var sb = new StringBuilder();
        Line(sb, columns, UrlColumn);
        return sb;
    }

    private static void Line(StringBuilder sb, IReadOnlyList<string> fields, string? last)
    {
        for (var i = 0; i < fields.Count; i++)
        {
            sb.Append(Field(fields[i])).Append('\t');
        }

        sb.Append(Field(last)).Append("\r\n");
    }
}
