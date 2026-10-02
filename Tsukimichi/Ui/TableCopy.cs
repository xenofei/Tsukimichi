using System.Collections.Generic;
using Tsukimichi.Core.Export;

namespace Tsukimichi.Ui;

/// <summary>
/// Copy table as TSV (1.8.0, research C9 #4) for the Journal table: the rows it shows now (scope, filters, search and
/// sort applied) under the export's quest columns plus a link (<see cref="TableTsv"/>). Names are the spoiler shield's,
/// as the table prints them, and a masked quest carries no link (no url, no lodestoneId), as in Copy for Discord;
/// states, completion and dates are the viewed character's.
/// </summary>
internal static class TableCopy
{
    public static string Quests(QueryRunner runner, GameLinks links)
    {
        if (runner.Session is not { Bundle: { } bundle } session)
        {
            return string.Empty;
        }

        var snapshot = session.ViewedSnapshot;
        var states = session.States;
        var spoilers = session.Spoilers;
        var ids = links.ExternalIds;
        var rows = runner.Rows;
        var exported = new List<QuestExportRow>(rows.Length);
        var masked = new HashSet<uint>();
        foreach (var row in rows)
        {
            var quest = row.Quest;
            if (spoilers.IsMasked(quest))
            {
                masked.Add(quest.RowId);
            }

            exported.Add(ExportWriter.Row(quest, snapshot, spoilers.DisplayName(quest), id => bundle.Names.Expansion(id), states.GetValueOrDefault(quest.RowId), ids));
        }

        return TableTsv.Quests(exported, r => links.PreferredLink(r.RowId), r => masked.Contains(r.RowId));
    }
}
