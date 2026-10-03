using System.Globalization;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Localization;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Chains;

/// <summary>What the chain line under the Path header points at, seen from the quest shown.</summary>
public enum ChainNextKind : byte
{
    /// <summary>Every counted quest of the chain is done.</summary>
    Complete,

    /// <summary>A quest after the one shown is still to do: "Next in chain".</summary>
    Next,

    /// <summary>A quest before the one shown was skipped and is still open: "Still open earlier".</summary>
    EarlierOpen,

    /// <summary>The quest shown is the only one left.</summary>
    Last,
}

/// <summary>
/// The chain line of the detail pane's Path card (feature plan v6 U5): the chain's name, then what to do next in it,
/// relative to the quest shown, over a slim bar; the totals only in <see cref="Tooltip"/>. It never points at the quest
/// shown (the old "next:" named the open quest on its own page). Built by <see cref="For"/>.
/// </summary>
/// <param name="Kind">What <see cref="LinkRowId"/> is.</param>
/// <param name="LinkRowId">The quest the line links to; null for <see cref="ChainNextKind.Complete"/> and <see cref="ChainNextKind.Last"/>.</param>
/// <param name="Done">Counted quests completed (as <see cref="ChainCatalog.Progress"/> counts them).</param>
/// <param name="Total">Counted quests.</param>
public readonly record struct ChainLine(ChainNextKind Kind, uint? LinkRowId, int Done, int Total)
{
    /// <summary>The bar's fill.</summary>
    public float Fraction => Total == 0 ? 0f : (float)Done / Total;

    /// <summary>"Next in chain:", "Still open earlier:", "Last quest in this chain" or "Chain complete".</summary>
    public string Label => Kind switch
    {
        ChainNextKind.Next => CoreText.T("Core.Chain.Next", "Next in chain:"),
        ChainNextKind.EarlierOpen => CoreText.T("Core.Chain.EarlierOpen", "Still open earlier:"),
        ChainNextKind.Last => CoreText.T("Core.Chain.Last", "Last quest in this chain"),
        _ => CoreText.T("Core.Chain.Complete", "Chain complete"),
    };

    /// <summary>The totals, for the bar's hover: "16 of 47 quests done", or "All 47 quests done".</summary>
    public string Tooltip => Done >= Total
        ? string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Chain.AllDone", "All {0:N0} quests done"), Total)
        : string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Chain.Tally", "{0:N0} of {1:N0} quests done"), Done, Total);

    /// <summary>
    /// The line for <paramref name="selectedRowId"/> in <paramref name="chain"/>, counting as
    /// <see cref="ChainCatalog.Progress"/> does (repeatables and quests that leave the totals are in neither number and
    /// are never linked). A skipped earlier quest wins over the next one after the quest shown, since it is the one to do
    /// first. Null when nothing in the chain counts for this character.
    /// </summary>
    public static ChainLine? For(Chain chain, uint selectedRowId, IReadOnlyDictionary<uint, QuestEvaluation> states)
    {
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(states);

        // A quest the chain does not list sits before nothing: every open quest is "next".
        var passed = ChainCatalog.IndexOf(chain, selectedRowId) < 0;
        var done = 0;
        var total = 0;
        uint? earlier = null;
        uint? after = null;
        foreach (var rowId in chain.RowIds)
        {
            if (rowId == selectedRowId)
            {
                passed = true;
            }

            if (chain.Uncounted.Contains(rowId))
            {
                continue;
            }

            states.TryGetValue(rowId, out var evaluation);
            if (evaluation is { LeavesTotals: true })
            {
                continue;
            }

            total++;
            if (evaluation is { State: QuestState.Completed })
            {
                done++;
            }
            else if (rowId == selectedRowId)
            {
                continue;
            }
            else if (!passed)
            {
                earlier ??= rowId;
            }
            else
            {
                after ??= rowId;
            }
        }

        if (total == 0)
        {
            return null;
        }

        return earlier is { } open ? new ChainLine(ChainNextKind.EarlierOpen, open, done, total)
            : after is { } next ? new ChainLine(ChainNextKind.Next, next, done, total)
            : done >= total ? new ChainLine(ChainNextKind.Complete, null, done, total)
            : new ChainLine(ChainNextKind.Last, null, done, total);
    }
}
