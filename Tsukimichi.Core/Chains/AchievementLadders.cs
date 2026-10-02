using System.Collections.Frozen;

namespace Tsukimichi.Core.Chains;

/// <summary>
/// An achievement that needs several quests done (feature plan v5, collector extras; R5 F6, C5 #7): the game's
/// "complete every listed quest" achievements that name more than one quest, such as "Tales of War" (five class
/// quests) or "The War Still Wageth On". No single quest earns one, so the Moonlit tab credits none of them to a quest
/// (D10); the Characters tab lists them beside the story chains instead, and each listed quest's detail pane says how
/// far the set is.
/// </summary>
/// <param name="AchievementId">The Achievement sheet row id.</param>
/// <param name="Name">The achievement's name in the client language.</param>
/// <param name="RowIds">The quests it names, as Quest sheet row ids in sheet order, each once.</param>
public sealed record AchievementLadder(uint AchievementId, string Name, IReadOnlyList<uint> RowIds);

/// <summary>Where a character stands on an <see cref="AchievementLadder"/>.</summary>
/// <param name="Done">Listed quests completed.</param>
/// <param name="Total">Listed quests.</param>
/// <param name="Remaining">The listed quests not completed yet, in the ladder's order.</param>
/// <param name="Earned">
/// Whether the achievement is earned: the game's own achievement list when it was loaded, else every listed quest done
/// (a "complete every listed quest" achievement is earned exactly then).
/// </param>
/// <param name="FromGame">Whether <paramref name="Earned"/> came from the game's achievement list rather than the quests.</param>
public sealed record AchievementLadderProgress(int Done, int Total, IReadOnlyList<uint> Remaining, bool Earned, bool FromGame)
{
    /// <summary>Done over total, 0 to 1, for the filling moon.</summary>
    public float Fraction => Total == 0 ? 0f : (float)Done / Total;

    /// <summary>Every listed quest is done.</summary>
    public bool IsComplete => Total > 0 && Done >= Total;
}

/// <summary>
/// Every <see cref="AchievementLadder"/> of a catalog, in sheet order, with a lookup from a quest to the ladders that
/// list it. Built once per catalog (by GameData from the Achievement sheet); <see cref="Progress"/> reads only quest
/// completion, so a stored character's progress is as exact as the live one's.
/// </summary>
public sealed class AchievementLadders
{
    public static readonly AchievementLadders Empty = new([], FrozenDictionary<uint, AchievementLadder[]>.Empty);

    private readonly FrozenDictionary<uint, AchievementLadder[]> byQuest;

    private AchievementLadders(IReadOnlyList<AchievementLadder> all, FrozenDictionary<uint, AchievementLadder[]> byQuest)
    {
        All = all;
        this.byQuest = byQuest;
    }

    /// <summary>Every ladder in the order given (the sheet's).</summary>
    public IReadOnlyList<AchievementLadder> All { get; }

    /// <summary>How many ladders there are.</summary>
    public int Count => All.Count;

    /// <summary>The ladders that list <paramref name="rowId"/>; empty for most quests.</summary>
    public IReadOnlyList<AchievementLadder> ForQuest(uint rowId) => byQuest.TryGetValue(rowId, out var ladders) ? ladders : [];

    /// <summary>
    /// Builds the lookup. A ladder naming fewer than two distinct quests is no ladder and is left out; a quest listed
    /// twice in one ladder counts once.
    /// </summary>
    public static AchievementLadders Build(IEnumerable<AchievementLadder> ladders)
    {
        ArgumentNullException.ThrowIfNull(ladders);
        var kept = new List<AchievementLadder>();
        var byQuest = new Dictionary<uint, List<AchievementLadder>>();
        foreach (var ladder in ladders)
        {
            var distinct = ladder.RowIds.Where(id => id != 0).Distinct().ToArray();
            if (distinct.Length < 2)
            {
                continue;
            }

            var clean = distinct.Length == ladder.RowIds.Count ? ladder : ladder with { RowIds = distinct };
            kept.Add(clean);
            foreach (var rowId in distinct)
            {
                if (!byQuest.TryGetValue(rowId, out var list))
                {
                    byQuest[rowId] = list = [];
                }

                list.Add(clean);
            }
        }

        return kept.Count == 0 ? Empty : new AchievementLadders(kept, byQuest.ToFrozenDictionary(kv => kv.Key, kv => kv.Value.ToArray()));
    }

    /// <summary>
    /// The character's progress on <paramref name="ladder"/>: <paramref name="isCompleted"/> answers by Quest sheet row
    /// id (the completion bitmask, so a stored character works), <paramref name="earnedFromGame"/> is the game's own
    /// achievement flag when its achievement list was loaded, null otherwise.
    /// </summary>
    public static AchievementLadderProgress Progress(AchievementLadder ladder, Func<uint, bool> isCompleted, bool? earnedFromGame = null)
    {
        ArgumentNullException.ThrowIfNull(ladder);
        ArgumentNullException.ThrowIfNull(isCompleted);
        var done = 0;
        var remaining = new List<uint>();
        foreach (var rowId in ladder.RowIds)
        {
            if (isCompleted(rowId))
            {
                done++;
            }
            else
            {
                remaining.Add(rowId);
            }
        }

        var total = ladder.RowIds.Count;
        var earned = earnedFromGame ?? (total > 0 && remaining.Count == 0);
        return new AchievementLadderProgress(done, total, remaining, earned, earnedFromGame is not null);
    }
}
