using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Query;

/// <summary>
/// Settings › Main window › Journal badge (plan v7, spec Revision 3 R3.2): what the number on the rail's Journal station
/// counts. The stored values are fixed; new modes go at the end.
/// </summary>
public enum JournalBadgeMode
{
    /// <summary>Quests that became available since the player last looked (the default): small, and it clears.</summary>
    NewlyReady = 0,

    /// <summary>Main scenario and unlock (feature) quests that are Ready: small, and it falls as the player plays.</summary>
    StoryAndUnlock = 1,

    /// <summary>Every Ready quest, as 1.13 counted it: for most players it sits at "99+".</summary>
    EveryReady = 2,

    /// <summary>No badge.</summary>
    Nothing = 3,
}

/// <summary>What one evaluation of a character offers the Journal badge (<see cref="NewlyReady.Tally"/>).</summary>
/// <param name="Available">Every available quest's row id, ascending: Ready or Ready on another job, counted, not done.</param>
/// <param name="Ready">The quests Ready on the current job, as the tree's Ready badges count them.</param>
/// <param name="StoryReady">Of <paramref name="Ready"/>, the main scenario and unlock (feature) quests.</param>
public sealed record ReadyTally(uint[] Available, int Ready, int StoryReady)
{
    public static readonly ReadyTally Empty = new([], 0, 0);
}

/// <summary>Where the newly-ready set stands after <see cref="NewlyReady.Reconcile"/>.</summary>
/// <param name="New">The available quests not seen yet, ascending.</param>
/// <param name="Seen">The seen set to keep: only quests still available, ascending.</param>
/// <param name="SeenChanged">Whether <paramref name="Seen"/> differs from what was stored (seeded or pruned), so it is saved.</param>
public readonly record struct NewlyReadyState(uint[] New, uint[] Seen, bool SeenChanged);

/// <summary>
/// The Journal badge's "newly ready" count (plan v7, spec Revision 3 R3.2). A quest is <em>new</em> when it is
/// available (Ready, or Ready on another job) and the player has not seen it since it became so:
/// <list type="bullet">
/// <item>a level-up, a finished prerequisite, a new patch or a reset can make quests new; switching jobs never does,
/// since Ready and Ready on another job are both available;</item>
/// <item>a quest stops being new when the player selects it, when the badge's "Newly ready" list is closed, or when it
/// stops being available; once it is gone for good (accepted, completed, locked out: <see cref="IsGone"/>) the seen set
/// lets it go, so that set stays about the size of the available list;</item>
/// <item>the first look at a character seeds the seen set with everything available, so a returning player starts at 0,
/// not at 300.</item>
/// </list>
/// The seen set is stored per character (<see cref="Storage.CharacterSettings.SeenReady"/>). Pure; the window recomputes
/// it when the evaluation changes, never per frame.
/// </summary>
public static class NewlyReady
{
    /// <summary>The most the badge spells out; past it the badge reads "99+" at the same size.</summary>
    public const int MaxShown = 99;

    /// <summary>
    /// Whether the quest is available to the badge: listed and counted (as the tree counts it: not removed, not a class
    /// intro or a tracker, not a repeatable other than an allied society daily), not done, not out of the totals (another
    /// path, out of season, a spare alternative), and Ready or Ready on another job.
    /// </summary>
    public static bool IsAvailable(QuestRecord quest, QuestEvaluation? evaluation)
    {
        ArgumentNullException.ThrowIfNull(quest);
        return evaluation is { State: QuestState.Ready or QuestState.ReadyOnOtherJob, CountsAsDone: false, LeavesTotals: false }
            && !quest.IsRemoved
            && quest.EntersCounts;
    }

    /// <summary>
    /// The badge's tallies from one evaluation: every available quest, the Ready ones, and the Ready main scenario and
    /// unlock quests (<paramref name="featureQuestIds"/>: the Feature Unlocks node, which holds the job quests' unlocks).
    /// </summary>
    public static ReadyTally Tally(QuestCatalog catalog, IReadOnlyDictionary<uint, QuestEvaluation> states, IReadOnlySet<uint>? featureQuestIds)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(states);
        if (states.Count == 0)
        {
            return ReadyTally.Empty;
        }

        var available = new List<uint>();
        var ready = 0;
        var story = 0;
        foreach (var quest in catalog.All)
        {
            states.TryGetValue(quest.RowId, out var evaluation);
            if (!IsAvailable(quest, evaluation))
            {
                continue;
            }

            available.Add(quest.RowId);
            if (evaluation!.State != QuestState.Ready)
            {
                continue;
            }

            ready++;
            if (FeaturePresets.IsMainScenario(quest) || featureQuestIds?.Contains(quest.RowId) == true)
            {
                story++;
            }
        }

        var ids = available.ToArray();
        Array.Sort(ids);
        return new ReadyTally(ids, ready, story);
    }

    /// <summary>
    /// Whether a seen quest has left availability for good, so the seen set lets it go (<see cref="Reconcile"/>): it is
    /// gone from the catalog or removed, in the journal, done, or locked out. A quest merely Blocked (a weapon swapped, a
    /// season over, or a capture read before the game's quest data) is kept, so it does not come back as new.
    /// </summary>
    public static bool IsGone(QuestRecord? quest, QuestEvaluation? evaluation) =>
        quest is null || quest.IsRemoved
        || evaluation is { State: QuestState.Accepted or QuestState.DoneThisCycle or QuestState.Completed or QuestState.Foreclosed }
        || evaluation is { CountsAsDone: true };

    /// <summary>
    /// The new quests and the seen set to keep for <paramref name="available"/> against what was stored. Nothing stored
    /// (<paramref name="seen"/> null: the character's first look) seeds the set with everything available, so nothing is
    /// new. Otherwise what is available but not in the set is new, and the set lets go of the quests no longer available
    /// that <paramref name="gone"/> names (every one of them when it is null).
    /// </summary>
    public static NewlyReadyState Reconcile(IReadOnlyList<uint> available, IReadOnlyCollection<uint>? seen, Func<uint, bool>? gone = null)
    {
        ArgumentNullException.ThrowIfNull(available);
        var ids = Ascending(available);
        if (seen is null)
        {
            return new NewlyReadyState([], ids, SeenChanged: true);
        }

        var stored = seen as IReadOnlySet<uint> ?? new HashSet<uint>(seen);
        var fresh = new List<uint>();
        foreach (var id in ids)
        {
            if (!stored.Contains(id))
            {
                fresh.Add(id);
            }
        }

        var open = new HashSet<uint>(ids);
        var kept = new List<uint>(stored.Count);
        foreach (var id in stored)
        {
            if (open.Contains(id) || (gone is not null && !gone(id)))
            {
                kept.Add(id);
            }
        }

        var keptIds = kept.ToArray();
        Array.Sort(keptIds);
        return new NewlyReadyState([.. fresh], keptIds, keptIds.Length != stored.Count);
    }

    /// <summary>
    /// The seen set with <paramref name="rowIds"/> added, those of them that are available (<paramref name="available"/>),
    /// ascending; null when that adds nothing, so there is nothing to save.
    /// </summary>
    public static uint[]? MarkSeen(IReadOnlyCollection<uint> seen, IEnumerable<uint> rowIds, IReadOnlySet<uint> available)
    {
        ArgumentNullException.ThrowIfNull(seen);
        ArgumentNullException.ThrowIfNull(rowIds);
        ArgumentNullException.ThrowIfNull(available);
        var set = new HashSet<uint>(seen);
        var added = false;
        foreach (var id in rowIds)
        {
            added |= available.Contains(id) && set.Add(id);
        }

        if (!added)
        {
            return null;
        }

        var ids = set.ToArray();
        Array.Sort(ids);
        return ids;
    }

    /// <summary>The number on the badge for <paramref name="mode"/>; 0 shows no badge.</summary>
    public static int BadgeCount(JournalBadgeMode mode, int newlyReady, int storyReady, int everyReady) => mode switch
    {
        JournalBadgeMode.NewlyReady => Math.Max(0, newlyReady),
        JournalBadgeMode.StoryAndUnlock => Math.Max(0, storyReady),
        JournalBadgeMode.EveryReady => Math.Max(0, everyReady),
        _ => 0,
    };

    /// <summary>The badge's text for <paramref name="count"/>: the number up to <see cref="MaxShown"/>, "99+" past it; empty for none.</summary>
    public static string BadgeText(int count) =>
        count <= 0 ? string.Empty
        : count > MaxShown ? "99+"
        : count.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static uint[] Ascending(IReadOnlyList<uint> ids)
    {
        var copy = new uint[ids.Count];
        for (var i = 0; i < copy.Length; i++)
        {
            copy[i] = ids[i];
        }

        Array.Sort(copy);
        return copy;
    }
}
