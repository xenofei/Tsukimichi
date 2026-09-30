using System.Text;
using Tsukimichi.Verify.Verify;

namespace Tsukimichi.Verify.Output;

/// <summary>RFC 4180 writer/reader for the three CSVs. Rows are sorted by the callers; no timestamps appear in any row.</summary>
internal static class Csv
{
    public static readonly string[] QuestHeader = ["rowId", "name", "fact", "catalogValue", "source", "sourceValue", "sourceRef", "verdict", "reason", "fixedIn"];
    public static readonly string[] SummaryHeader = ["rowId", "name", "sourcesChecked", "overallVerdict", "worstFact", "facts", "gateFailures"];
    public static readonly string[] RewardHeader = ["questRowId", "questName", "kind", "rewardId", "itemId", "rewardName", "catalogClaim", "source", "sourceValue", "sourceRef", "verdict", "reason", "fixedIn"];

    public static string Field(string? value)
    {
        value ??= string.Empty;
        value = value.Replace("\r", " ").Replace("\n", " ");
        return value.IndexOfAny([',', '"']) >= 0 || value.StartsWith(' ') || value.EndsWith(' ')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }

    public static void WriteQuestRows(string path, IEnumerable<QuestRow> rows)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(',', QuestHeader)).Append('\n');
        foreach (var r in rows.OrderBy(r => r.RowId).ThenBy(r => r.Fact, StringComparer.Ordinal).ThenBy(r => r.Source, StringComparer.Ordinal))
        {
            sb.Append(string.Join(',', new[]
            {
                r.RowId.ToString(), Field(r.Name), r.Fact, Field(r.CatalogValue), r.Source, Field(r.SourceValue), Field(r.SourceRef), Verdicts.Name(r.Verdict), Field(r.Reason), Field(r.FixedIn),
            })).Append('\n');
        }

        Atomic(path, sb.ToString());
    }

    public static void WriteSummary(string path, IEnumerable<QuestRow> rows)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(',', SummaryHeader)).Append('\n');
        foreach (var group in rows.GroupBy(r => r.RowId).OrderBy(g => g.Key))
        {
            var list = group.ToList();
            var worst = list.OrderByDescending(r => Verdicts.Severity(r.Verdict)).ThenBy(r => r.Fact, StringComparer.Ordinal).First();
            var sources = string.Join(";", list.Select(r => r.Source).Distinct().OrderBy(s => s, StringComparer.Ordinal));
            var gate = list.Count(r => Verdicts.FailsGate(r.Verdict));
            sb.Append(string.Join(',', new[]
            {
                group.Key.ToString(), Field(list[0].Name), sources, Verdicts.Name(worst.Verdict), worst.Verdict == Verdict.Match ? string.Empty : worst.Fact, list.Count.ToString(), gate.ToString(),
            })).Append('\n');
        }

        Atomic(path, sb.ToString());
    }

    public static void WriteRewardRows(string path, IEnumerable<RewardRow> rows)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(',', RewardHeader)).Append('\n');
        foreach (var r in rows.OrderBy(r => r.QuestRowId).ThenBy(r => r.Kind, StringComparer.Ordinal).ThenBy(r => r.RewardId).ThenBy(r => r.ItemId).ThenBy(r => r.Source, StringComparer.Ordinal))
        {
            sb.Append(string.Join(',', new[]
            {
                r.QuestRowId.ToString(), Field(r.QuestName), r.Kind, r.RewardId.ToString(), r.ItemId.ToString(), Field(r.RewardName), Field(r.CatalogClaim), r.Source, Field(r.SourceValue), Field(r.SourceRef), Verdicts.Name(r.Verdict), Field(r.Reason), Field(r.FixedIn),
            })).Append('\n');
        }

        Atomic(path, sb.ToString());
    }

    public static List<QuestRow> ReadQuestRows(string path)
    {
        var rows = new List<QuestRow>();
        foreach (var f in ReadRecords(path, QuestHeader.Length))
        {
            rows.Add(new QuestRow(uint.Parse(f[0]), f[1], f[2], f[3], f[4], f[5], f[6], Verdicts.Parse(f[7]), f[8], f[9]));
        }

        return rows;
    }

    public static List<RewardRow> ReadRewardRows(string path)
    {
        var rows = new List<RewardRow>();
        foreach (var f in ReadRecords(path, RewardHeader.Length))
        {
            rows.Add(new RewardRow(uint.Parse(f[0]), f[1], f[2], uint.Parse(f[3]), uint.Parse(f[4]), f[5], f[6], f[7], f[8], f[9], Verdicts.Parse(f[10]), f[11], f[12]));
        }

        return rows;
    }

    private static IEnumerable<string[]> ReadRecords(string path, int width)
    {
        using var reader = new StreamReader(path, Encoding.UTF8);
        var header = true;
        while (ReadRecord(reader) is { } fields)
        {
            if (header)
            {
                header = false;
                continue;
            }

            if (fields.Length != width)
            {
                throw new FormatException($"{path}: expected {width} fields, found {fields.Length}: {string.Join('|', fields)}");
            }

            yield return fields;
        }
    }

    private static string[]? ReadRecord(TextReader reader)
    {
        var line = reader.ReadLine();
        if (line is null)
        {
            return null;
        }

        var fields = new List<string>();
        var sb = new StringBuilder();
        var quoted = false;
        var i = 0;
        while (true)
        {
            if (i >= line.Length)
            {
                if (quoted)
                {
                    var next = reader.ReadLine();
                    if (next is null)
                    {
                        break;
                    }

                    sb.Append('\n');
                    line = next;
                    i = 0;
                    continue;
                }

                break;
            }

            var c = line[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i += 2;
                        continue;
                    }

                    quoted = false;
                    i++;
                    continue;
                }

                sb.Append(c);
                i++;
                continue;
            }

            if (c == '"')
            {
                quoted = true;
                i++;
                continue;
            }

            if (c == ',')
            {
                fields.Add(sb.ToString());
                sb.Clear();
                i++;
                continue;
            }

            sb.Append(c);
            i++;
        }

        fields.Add(sb.ToString());
        return fields.ToArray();
    }

    /// <summary>Writes through a temp file so an interrupted run never leaves a truncated CSV.</summary>
    public static void Atomic(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content, new UTF8Encoding(false));
        File.Move(tmp, path, overwrite: true);
    }
}
