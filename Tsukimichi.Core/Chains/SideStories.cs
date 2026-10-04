using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;

namespace Tsukimichi.Core.Chains;

/// <summary>Where a character stands in one named side story (feature plan v7 P5).</summary>
public enum SideStoryStatus : byte
{
    /// <summary>Something is left: "3 left · next: Forever in Our Hearts · Ready".</summary>
    ToGo,

    /// <summary>An ongoing series with every released quest done: "Caught up · continues in a later patch".</summary>
    CaughtUp,

    /// <summary>Every quest done and the series is not ongoing: it folds into "7 lines done".</summary>
    Finished,
}

/// <summary>
/// One row of the Side stories card: the line, the character's progress in it, its status, and whether it opens past
/// the story point (<see cref="Ahead"/>: the shield names neither it nor its quests).
/// </summary>
public sealed record SideStoryRow(Chain Chain, ChainProgress Progress, SideStoryStatus Status, bool Ahead)
{
    /// <summary>Quests left: the counted ones not done.</summary>
    public int Left => Progress.Total - Progress.Done;

    /// <summary>At least one quest of the line done.</summary>
    public bool Started => Progress.Done > 0;
}

/// <summary>A line that gained quests in the newest patch the character has started (feature plan v7 P5's new-chapter line).</summary>
/// <param name="Chain">The line.</param>
/// <param name="NewQuests">Its quests added in the newest patch series and not done, in play order.</param>
public sealed record NewChapter(Chain Chain, IReadOnlyList<uint> NewQuests);

/// <summary>
/// The dashboard's Side stories card (feature plan v7 P5; spec-1.21 P5): every curated line
/// (<see cref="Chain.IsCurated"/>) with its progress for one character. Lines with something left come first, the
/// started ones before the rest, each group in <c>chains.json</c> order; then the ongoing series the character is
/// caught up on; finished lines last, which the card folds into one line. A line with nothing that counts for the
/// character (every step locked out or out of season) is left out. A line nothing of which is done and whose first quest
/// lies past the story point (<paramref name="isAhead"/>) is marked <see cref="SideStoryRow.Ahead"/>. Pure.
/// </summary>
public static class SideStories
{
    public static IReadOnlyList<SideStoryRow> Build(ChainCatalog chains, IReadOnlyDictionary<uint, QuestEvaluation> states, Func<uint, bool> isAhead)
    {
        ArgumentNullException.ThrowIfNull(chains);
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(isAhead);

        var started = new List<SideStoryRow>();
        var fresh = new List<SideStoryRow>();
        var caughtUp = new List<SideStoryRow>();
        var finished = new List<SideStoryRow>();
        foreach (var chain in chains.Chains)
        {
            if (!chain.IsCurated)
            {
                continue;
            }

            var progress = ChainCatalog.Progress(chain, states);
            if (progress.IsEmpty)
            {
                continue;
            }

            if (progress.IsComplete)
            {
                var status = chain.Ongoing ? SideStoryStatus.CaughtUp : SideStoryStatus.Finished;
                (chain.Ongoing ? caughtUp : finished).Add(new SideStoryRow(chain, progress, status, false));
                continue;
            }

            var ahead = progress.Done == 0 && chain.RowIds.Count > 0 && isAhead(chain.RowIds[0]);
            (progress.Done > 0 ? started : fresh).Add(new SideStoryRow(chain, progress, SideStoryStatus.ToGo, ahead));
        }

        return [.. started, .. fresh, .. caughtUp, .. finished];
    }

    /// <summary>
    /// The lines the character has started (a quest done) that gained quests in the newest patch series
    /// (<see cref="PatchIndex.IsNew"/>) still to do: what the What's new card's "New chapters" line names.
    /// </summary>
    public static IReadOnlyList<NewChapter> NewChapters(ChainCatalog chains, QuestCatalog catalog, IReadOnlyDictionary<uint, QuestEvaluation> states)
    {
        ArgumentNullException.ThrowIfNull(chains);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(states);

        var patches = PatchIndex.For(catalog);
        if (patches.NewestSeries.Length == 0)
        {
            return [];
        }

        var result = new List<NewChapter>();
        foreach (var chain in chains.Chains)
        {
            if (!chain.IsCurated)
            {
                continue;
            }

            var started = false;
            List<uint>? added = null;
            foreach (var rowId in chain.RowIds)
            {
                var done = states.TryGetValue(rowId, out var evaluation) && evaluation.State == QuestState.Completed;
                if (done)
                {
                    started = true;
                    continue;
                }

                if (catalog.GetByRowId(rowId) is { } quest && patches.IsNew(quest) && evaluation is not { LeavesTotals: true })
                {
                    (added ??= []).Add(rowId);
                }
            }

            if (started && added is not null)
            {
                result.Add(new NewChapter(chain, added));
            }
        }

        return result;
    }
}
