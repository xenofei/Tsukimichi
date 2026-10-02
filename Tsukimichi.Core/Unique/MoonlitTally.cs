using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Unique;

/// <summary>
/// A <see cref="MoonlitGroup"/> for the viewed character.
/// </summary>
/// <param name="OnPath">
/// False when every quest of the group lies on a path the character did not take (another city, starting class or Grand
/// Company, an option not chosen: <see cref="QuestEvaluation.IsOtherPath"/> or
/// <see cref="QuestEvaluation.IsSpareAlternative"/>) and the reward is not obtained: the row is hidden and not counted.
/// </param>
/// <param name="Representative">Index into <see cref="MoonlitGroup.Quests"/> of the quest the row shows.</param>
/// <param name="Obtained">True when any entry of the group is obtained; false when every entry reads false; null otherwise.</param>
/// <param name="Availability">The representative quest's availability: the best of the group's quests on the character's path.</param>
/// <param name="QuestCompleted">Whether the character completed the representative quest.</param>
public readonly record struct MoonlitGroupState(bool OnPath, int Representative, bool? Obtained, RewardAvailabilityInfo Availability, bool QuestCompleted = false)
{
    /// <summary>
    /// Gone for good and known not to be obtained: a missed time-limited reward. A reward whose owned state cannot be
    /// read is not called missed (it stays in the totals as unknown), nor is one whose quest the character completed:
    /// the quest gave it, whatever became of it since.
    /// </summary>
    public bool Missed => Availability.IsGone && Obtained == false && !QuestCompleted;
}

/// <summary>Which counted rewards enter the totals.</summary>
/// <param name="CountGone">Count rewards that are gone for good and missed (off by default).</param>
/// <param name="ExcludeFoundElsewhere">Leave out rewards the Online Store also sells or a duty also drops ("Hide rewards found elsewhere").</param>
public readonly record struct MoonlitCountOptions(bool CountGone = false, bool ExcludeFoundElsewhere = false);

/// <summary>Obtained/total/unknown per reward kind and over every kind, and how many time-limited rewards were missed.</summary>
public sealed class MoonlitTotals
{
    private static readonly int KindCount = Enum.GetValues<RewardKind>().Length;

    private readonly int[] obtained = new int[KindCount];
    private readonly int[] total = new int[KindCount];
    private readonly int[] unknown = new int[KindCount];

    /// <summary>Counted rewards over every kind.</summary>
    public UniqueRewardCounts All { get; private set; }

    /// <summary>Rewards on the character's path that are gone for good and missed (<see cref="MoonlitGroupState.Missed"/>), whether or not they count.</summary>
    public int Missed { get; private set; }

    /// <summary>The counts of one kind; zeros for a kind with no counted reward.</summary>
    public UniqueRewardCounts For(RewardKind kind)
    {
        var k = (int)kind;
        return (uint)k < (uint)KindCount ? new UniqueRewardCounts(obtained[k], total[k], unknown[k]) : default;
    }

    internal void Add(RewardKind kind, bool? isObtained)
    {
        var k = (int)kind;
        if ((uint)k >= (uint)KindCount)
        {
            return;
        }

        total[k]++;
        var (o, t, u) = All;
        t++;
        switch (isObtained)
        {
            case true:
                obtained[k]++;
                o++;
                break;
            case null:
                unknown[k]++;
                u++;
                break;
        }

        All = new UniqueRewardCounts(o, t, u);
    }

    internal void AddMissed() => Missed++;
}

/// <summary>
/// The Moonlit totals of feature plan v5, decision 4, over <see cref="MoonlitGroups"/>: a reward counts once however
/// many quests give it, obtained when any of its entries is; rows on a path the character did not take are hidden;
/// a relic or special weapon quest counts once; rewards that are gone for good and missed
/// (<see cref="MoonlitGroupState.Missed"/>: known not obtained, quest not completed) leave the totals unless
/// <see cref="MoonlitCountOptions.CountGone"/> brings them back, while a gone reward whose owned state cannot be read
/// stays in them as unknown. Pure; the Moonlit pane, the Characters
/// dashboard (through the pane) and the tests share it.
/// </summary>
public static class MoonlitTally
{
    /// <summary>
    /// The group for the viewed character. The representative is, among the quests on the character's path (all of
    /// them when none is), the one through which the reward is obtained, else the one with the best availability,
    /// then the most advanced state (Completed, In journal, Ready, Ready on another job, Done this cycle, Blocked,
    /// Unknown, Locked out), then the first.
    /// </summary>
    /// <param name="isObtained">Whether the character has an entry's reward: true, false, or null when it cannot be told.</param>
    /// <param name="evaluationOf">A quest's evaluation for the character by row id; null when there is none.</param>
    /// <param name="availabilityOf">An entry's availability through its quest, given the quest's evaluation (<see cref="RewardAvailabilities.Classify"/>).</param>
    public static MoonlitGroupState Evaluate(
        MoonlitGroup group,
        Func<UniqueRewardEntry, bool?> isObtained,
        Func<uint, QuestEvaluation?> evaluationOf,
        Func<UniqueRewardEntry, QuestEvaluation?, RewardAvailabilityInfo> availabilityOf)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(isObtained);
        ArgumentNullException.ThrowIfNull(evaluationOf);
        ArgumentNullException.ThrowIfNull(availabilityOf);

        var quests = group.Quests;
        var anyTrue = false;
        var allFalse = true;
        Span<bool> questObtained = quests.Count <= 64 ? stackalloc bool[quests.Count] : new bool[quests.Count];
        foreach (var entry in group.Entries)
        {
            switch (isObtained(entry))
            {
                case true:
                    anyTrue = true;
                    allFalse = false;
                    var at = IndexOf(quests, entry.QuestRowId);
                    if (at >= 0)
                    {
                        questObtained[at] = true;
                    }

                    break;
                case null:
                    allFalse = false;
                    break;
            }
        }

        bool? obtained = anyTrue ? true : allFalse ? false : null;

        var anyOnPath = false;
        for (var i = 0; i < quests.Count; i++)
        {
            if (!IsOffPath(evaluationOf(quests[i])))
            {
                anyOnPath = true;
                break;
            }
        }

        var best = -1;
        (int Obtained, int Availability, int State) bestKey = default;
        var bestAvailability = new RewardAvailabilityInfo(RewardAvailability.GetNow);
        var bestCompleted = false;
        for (var i = 0; i < quests.Count; i++)
        {
            var evaluation = evaluationOf(quests[i]);
            if (anyOnPath && IsOffPath(evaluation))
            {
                continue;
            }

            var entry = group.FirstOf(quests[i])!;
            var availability = availabilityOf(entry, evaluation);
            var key = (questObtained[i] ? 0 : 1, RewardAvailabilities.Rank(availability.Kind), StateRank(evaluation?.State));
            if (best < 0 || key.CompareTo(bestKey) < 0)
            {
                best = i;
                bestKey = key;
                bestAvailability = availability;
                bestCompleted = evaluation?.State == QuestState.Completed;
            }
        }

        return new MoonlitGroupState(anyOnPath || anyTrue, Math.Max(0, best), obtained, bestAvailability, bestCompleted);
    }

    /// <summary>Whether the group enters the totals under <paramref name="options"/>.</summary>
    public static bool Counts(MoonlitGroup group, in MoonlitGroupState state, MoonlitCountOptions options)
    {
        ArgumentNullException.ThrowIfNull(group);
        return state.OnPath
               && !(options.ExcludeFoundElsewhere && group.FoundElsewhere)
               && (options.CountGone || !state.Missed);
    }

    /// <summary>The totals of <paramref name="groups"/> with their states (same order, same length).</summary>
    public static MoonlitTotals Totals(IReadOnlyList<MoonlitGroup> groups, IReadOnlyList<MoonlitGroupState> states, MoonlitCountOptions options)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(states);
        if (groups.Count != states.Count)
        {
            throw new ArgumentException("One state per group.", nameof(states));
        }

        var totals = new MoonlitTotals();
        for (var i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            var state = states[i];
            if (state.OnPath && state.Missed && !(options.ExcludeFoundElsewhere && group.FoundElsewhere))
            {
                totals.AddMissed();
            }

            if (Counts(group, state, options))
            {
                totals.Add(group.Kind, state.Obtained);
            }
        }

        return totals;
    }

    /// <summary>Evaluates every group and totals them in one go.</summary>
    public static (MoonlitGroupState[] States, MoonlitTotals Totals) Run(
        MoonlitGroups groups,
        Func<UniqueRewardEntry, bool?> isObtained,
        Func<uint, QuestEvaluation?> evaluationOf,
        Func<UniqueRewardEntry, QuestEvaluation?, RewardAvailabilityInfo> availabilityOf,
        MoonlitCountOptions options)
    {
        ArgumentNullException.ThrowIfNull(groups);
        var states = new MoonlitGroupState[groups.All.Count];
        for (var i = 0; i < states.Length; i++)
        {
            states[i] = Evaluate(groups.All[i], isObtained, evaluationOf, availabilityOf);
        }

        return (states, Totals(groups.All, states, options));
    }

    /// <summary>A quest on a path the character did not take, or an option of a choice not made that is not the presumed one.</summary>
    public static bool IsOffPath(QuestEvaluation? evaluation) => evaluation is { IsOtherPath: true } or { IsSpareAlternative: true };

    /// <summary>Lower is further along: Completed, In journal, Ready, Ready on another job, Done this cycle, Blocked, Unknown (or none), Locked out.</summary>
    public static int StateRank(QuestState? state) => state switch
    {
        QuestState.Completed => 0,
        QuestState.Accepted => 1,
        QuestState.Ready => 2,
        QuestState.ReadyOnOtherJob => 3,
        QuestState.DoneThisCycle => 4,
        QuestState.Blocked => 5,
        QuestState.Foreclosed => 7,
        _ => 6,
    };

    private static int IndexOf(IReadOnlyList<uint> quests, uint quest)
    {
        for (var i = 0; i < quests.Count; i++)
        {
            if (quests[i] == quest)
            {
                return i;
            }
        }

        return -1;
    }
}
