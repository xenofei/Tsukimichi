using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;

namespace Tsukimichi.Core.Ui;

/// <summary>
/// The quest the tour selects for its Read chapter (feature plan v5, 1.7.0; onboarding finding F1), so the detail
/// pane's requirements, path and giver are real rather than the Tonight card: the next main scenario quest when it is
/// Blocked (its blocker is the most useful thing to read), else the first Blocked quest in the table, else the first
/// row, else the next main scenario quest whatever its state; null when there is nothing at all.
/// </summary>
public static class TourSample
{
    /// <param name="nextMsq">The next main scenario quest (<c>MsqPosition.Next</c>), or null.</param>
    /// <param name="nextMsqState">Its state for the viewed character, or null when unknown.</param>
    /// <param name="rows">The quest table's rows in the order shown.</param>
    public static uint? Choose(QuestRecord? nextMsq, QuestState? nextMsqState, IReadOnlyList<QuestRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (nextMsq is not null && nextMsqState == QuestState.Blocked)
        {
            return nextMsq.RowId;
        }

        foreach (var row in rows)
        {
            if (row.State == QuestState.Blocked)
            {
                return row.Quest.RowId;
            }
        }

        if (rows.Count > 0)
        {
            return rows[0].Quest.RowId;
        }

        return nextMsq?.RowId;
    }
}
