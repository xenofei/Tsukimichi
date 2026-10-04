using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Runtime;

/// <summary>
/// The reverse check of "the game confirms it" (feature plan v7, C1): Ready quests whose giver stands in the zone the
/// character is in, while the game's "available quest" markers list other quests of that zone but not them. Counted per
/// poll; a quest seen, or no longer Ready, starts over. Suspicion only (<see cref="Diagnostics.GameOfferVerdict.Unseen"/>):
/// it never changes a state. Kept in memory for the session, for the live character. Not thread-safe.
/// </summary>
public sealed class UnseenMarkers
{
    private readonly Dictionary<ushort, int> misses = [];

    /// <summary>Quest id to the polls in its giver's zone without its marker.</summary>
    public IReadOnlyDictionary<ushort, int> Misses => misses;

    /// <summary>Forgets every count (a logout, another character).</summary>
    public void Clear() => misses.Clear();

    /// <summary>
    /// Counts one poll in <paramref name="territory"/>. Only judged when the game listed at least one marker in that
    /// zone (<paramref name="markerTerritories"/>): an empty list means the game has not built it, not that nothing is
    /// available. Each candidate (<see cref="Candidates"/>) that has a marker starts over; each without one counts one
    /// more poll. Counts of quests no longer Ready are dropped. Returns whether a count changed.
    /// </summary>
    public bool Observe(
        uint territory,
        IReadOnlyList<QuestRecord> candidates,
        IReadOnlyDictionary<uint, QuestEvaluation> states,
        IReadOnlySet<uint> markerRows,
        IReadOnlySet<uint> markerTerritories)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(markerRows);
        ArgumentNullException.ThrowIfNull(markerTerritories);

        var changed = DropSettled(states);
        if (territory == 0 || !markerTerritories.Contains(territory))
        {
            return changed;
        }

        foreach (var quest in candidates)
        {
            if (!states.TryGetValue(quest.RowId, out var evaluation) || evaluation.State != QuestState.Ready)
            {
                continue;
            }

            if (markerRows.Contains(quest.RowId))
            {
                changed |= misses.Remove(quest.QuestId);
            }
            else
            {
                misses[quest.QuestId] = misses.GetValueOrDefault(quest.QuestId) + 1;
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// The quests whose markers the game is expected to list in <paramref name="territory"/>: listed in the journal,
    /// not repeatable, not seasonal, not hidden, with a giver standing there. Ascending by row id.
    /// </summary>
    public static List<QuestRecord> Candidates(QuestCatalog catalog, uint territory)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var result = new List<QuestRecord>();
        if (territory == 0)
        {
            return result;
        }

        foreach (var quest in catalog.All)
        {
            if (quest.Issuer is { } issuer && issuer.TerritoryId == territory
                && !quest.IsRemoved && !quest.IsHidden && !quest.IsRepeatable && quest.Festival == 0)
            {
                result.Add(quest);
            }
        }

        result.Sort(static (a, b) => a.RowId.CompareTo(b.RowId));
        return result;
    }

    private bool DropSettled(IReadOnlyDictionary<uint, QuestEvaluation> states)
    {
        if (misses.Count == 0)
        {
            return false;
        }

        List<ushort>? settled = null;
        foreach (var questId in misses.Keys)
        {
            if (!states.TryGetValue(0x10000u | questId, out var evaluation) || evaluation.State != QuestState.Ready)
            {
                (settled ??= []).Add(questId);
            }
        }

        if (settled is null)
        {
            return false;
        }

        foreach (var questId in settled)
        {
            misses.Remove(questId);
        }

        return true;
    }
}
