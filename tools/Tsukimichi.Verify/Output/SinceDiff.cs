using Tsukimichi.Verify.Verify;

namespace Tsukimichi.Verify.Output;

/// <summary><c>--since &lt;csv&gt;</c>: rows whose verdict changed against a previous run, grouped by (old → new) verdict.</summary>
internal static class SinceDiff
{
    public static void Print(string previousPath, IReadOnlyList<QuestRow> current, TextWriter log)
    {
        if (!File.Exists(previousPath))
        {
            log.WriteLine($"since: {previousPath} does not exist; nothing to diff");
            return;
        }

        var previous = Csv.ReadQuestRows(previousPath).ToDictionary(r => r.Key);
        var now = current.ToDictionary(r => r.Key);
        var changes = new List<(string Group, QuestRow Row, string Old)>();
        foreach (var (key, row) in now)
        {
            if (previous.TryGetValue(key, out var old))
            {
                if (old.Verdict != row.Verdict)
                {
                    changes.Add(($"{Verdicts.Name(old.Verdict)} -> {Verdicts.Name(row.Verdict)}", row, old.SourceValue));
                }
            }
            else
            {
                changes.Add(($"(new) -> {Verdicts.Name(row.Verdict)}", row, string.Empty));
            }
        }

        var gone = previous.Keys.Where(k => !now.ContainsKey(k)).ToList();
        log.WriteLine($"since: {changes.Count} changed rows, {gone.Count} rows no longer produced, against {previousPath}");
        foreach (var group in changes.GroupBy(c => c.Group).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            log.WriteLine($"  {group.Key}: {group.Count()}");
            foreach (var (_, row, old) in group.OrderBy(c => c.Row.RowId).Take(50))
            {
                log.WriteLine($"    {row.RowId} {row.Name} [{row.Fact}/{row.Source}] catalog={row.CatalogValue} source={row.SourceValue}{(old.Length > 0 && old != row.SourceValue ? " (was " + old + ")" : string.Empty)} {row.Reason} {row.SourceRef}");
            }

            if (group.Count() > 50)
            {
                log.WriteLine($"    ... {group.Count() - 50} more");
            }
        }
    }
}
