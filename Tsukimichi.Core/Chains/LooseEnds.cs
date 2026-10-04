using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Chains;

/// <summary>What kind of storyline a loose end is: it picks the row's icon and the words a masked next quest prints.</summary>
public enum StoryLineKind : byte
{
    /// <summary>A class or job quest line (one "Class &amp; Job Quests" genre: Bard Quests, Archer Quests, Culinarian Quests).</summary>
    Job,

    /// <summary>A role quest line (Physical Ranged DPS Role Quests (Endwalker)).</summary>
    Role,

    /// <summary>A curated line (<c>chains.json</c>) or a Side Story Quests or Chronicles of Light genre.</summary>
    Chain,

    /// <summary>A side story of story sidequests (<see cref="StorySidequests"/>), named after its first quest.</summary>
    Story,
}

/// <summary>A storyline Loose ends looks at: its kind and its quests in play order (as a <see cref="Chains.Chain"/>).</summary>
/// <param name="Genre">The journal genre a job or role line is (for its icon); 0 for other lines.</param>
public sealed record StoryLine(Chain Chain, StoryLineKind Kind, uint Genre = 0);

/// <summary>
/// One loose end: a line the character started and never finished, with what is left and the next quest.
/// </summary>
/// <param name="Line">The line.</param>
/// <param name="Progress">The character's progress in it.</param>
/// <param name="Next">The next quest to do (the first counted quest not done).</param>
/// <param name="NextState">That quest's state for the character.</param>
/// <param name="FinaleMarkedYellow">The finale carries the plain side-quest marker (<see cref="LooseEnds.SideQuestMarker"/>), not the blue or job one: the game does not say it ends a line.</param>
public sealed record LooseEnd(StoryLine Line, ChainProgress Progress, QuestRecord Next, QuestState NextState, bool FinaleMarkedYellow)
{
    /// <summary>Quests left.</summary>
    public int Left => Progress.Total - Progress.Done;

    /// <summary>The next quest ends the line.</summary>
    public bool IsFinale => Left == 1;

    /// <summary>The next quest can be taken now (Ready, Ready on another job) or is in the journal.</summary>
    public bool NextIsReady => NextState is QuestState.Ready or QuestState.ReadyOnOtherJob or QuestState.Accepted;

    /// <summary>A finale the character can take now: what the optional notice announces.</summary>
    public bool IsReadyFinale => IsFinale && NextIsReady;
}

/// <summary>
/// Loose ends (feature plan v7 N8; spec-1.21 N8): the storylines a character started and never finished. The lines are
/// the job and role quest lines (each "Class &amp; Job Quests" genre), the curated chains, the side stories of story
/// sidequests, and the Side Story Quests and Chronicles of Light genres no curated chain names.
/// <para>
/// <b>Started</b> (<see cref="IsStarted"/>): at least two quests done, or one for a line of up to
/// <see cref="ShortLine"/> quests, so a lone intro quest does not count. <b>Not finished</b>: something counted is left
/// (<see cref="ChainCatalog.Progress"/>: repeatables, locked-out and out-of-season quests are in neither number).
/// </para>
/// <para>
/// <b>Order</b>: finales that can be taken now first, those the game marks only with the plain side-quest marker
/// before the rest (the Shadowbringers job finales, the Endwalker role finale, the Void quests); then the lines with
/// the fewest left; ties keep the lines' order. Pure.
/// </para>
/// </summary>
public static class LooseEnds
{
    /// <summary>A line of at most this many quests counts as started with one done.</summary>
    public const int ShortLine = 4;

    /// <summary><c>Quest.EventIconType</c> of the ordinary (yellow) side quest.</summary>
    public const byte SideQuestMarker = 1;

    /// <summary>JournalCategory rows whose genres are story lines of their own: Chronicles of Light (54) and Side Story Quests (58).</summary>
    public static readonly IReadOnlyList<uint> SideStoryCategories = [54, 58];

    /// <summary>Settings › Alerts › When a storyline's finale is Ready: off until the player turns it on.</summary>
    public const bool FinaleNoticeDefault = false;

    /// <summary>Settings › Overlay: the Loose ends section, off until the player turns it on.</summary>
    public const bool OverlaySectionDefault = false;

    /// <summary>
    /// The id the finale notice records once it has spoken of a finale ("finale:69286"), kept with the character's
    /// noticed ids (<c>CharacterSettings.PayoffGatesNoticed</c>, which also holds the payoff gates' ids): once per finale,
    /// across sessions and clients.
    /// </summary>
    public static string NoticeId(uint finaleRowId) => "finale:" + finaleRowId.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Whether <paramref name="done"/> of <paramref name="total"/> counted quests makes a line started.</summary>
    public static bool IsStarted(int done, int total) => done >= (total <= ShortLine ? 1 : 2);

    /// <summary>Every line Loose ends looks at, job and role lines first (journal order), then the chains in their catalog order.</summary>
    public static IReadOnlyList<StoryLine> Lines(QuestCatalog catalog, ChainCatalog chains)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(chains);

        var lines = new List<StoryLine>();
        foreach (var (genre, quests) in catalog.ByGenre.OrderBy(kv => kv.Value[0].Journal.SortKey))
        {
            var first = quests[0];
            if (genre == 0 || first.Journal.SectionId != JobLadder.ClassJobSectionId)
            {
                continue;
            }

            if (Steps(quests, catalog) is { Length: > 0 } steps)
            {
                var kind = JobLadder.IsRoleQuest(first) ? StoryLineKind.Role : StoryLineKind.Job;
                lines.Add(new StoryLine(new Chain(first.Journal.GenreName, steps), kind, genre));
            }
        }

        foreach (var chain in chains.Chains)
        {
            if (chain.IsCurated || chain.IsStory)
            {
                lines.Add(new StoryLine(chain, chain.IsStory ? StoryLineKind.Story : StoryLineKind.Chain));
            }
        }

        // The Side Story Quests and Chronicles of Light genres no curated chain names (a later patch's new series).
        foreach (var (genre, quests) in catalog.ByGenre.OrderBy(kv => kv.Value[0].Journal.SortKey))
        {
            var first = quests[0];
            if (genre == 0 || !SideStoryCategories.Contains(first.Journal.CategoryId) || chains.ForQuest(first.RowId) is { IsCurated: true })
            {
                continue;
            }

            if (Steps(quests, catalog) is { Length: > 0 } steps)
            {
                lines.Add(new StoryLine(new Chain(first.Journal.GenreName, steps), StoryLineKind.Chain, genre));
            }
        }

        return lines;
    }

    /// <summary>The loose ends among <paramref name="lines"/> for one character, in display order.</summary>
    /// <param name="setAside">
    /// The character's set-aside quests (P4: "Not for me" from the card's menu, or "Set aside for later" in My blues): a
    /// line whose next quest is one of them leaves the card, the overlay and the finale notice. Null sets none aside.
    /// </param>
    public static IReadOnlyList<LooseEnd> Find(IReadOnlyList<StoryLine> lines, IReadOnlyDictionary<uint, QuestEvaluation> states, QuestCatalog catalog, IReadOnlySet<uint>? setAside = null)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(catalog);

        var found = new List<(LooseEnd End, int Index)>();
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var progress = ChainCatalog.Progress(line.Chain, states);
            if (progress.IsEmpty || progress.IsComplete || !IsStarted(progress.Done, progress.Total)
                || progress.NextRowId is not { } nextRowId || setAside?.Contains(nextRowId) == true || catalog.GetByRowId(nextRowId) is not { } next)
            {
                continue;
            }

            var state = states.TryGetValue(nextRowId, out var evaluation) ? evaluation.State : QuestState.Unknown;
            var end = new LooseEnd(line, progress, next, state, false);
            found.Add((end with { FinaleMarkedYellow = end.IsFinale && next.EventIconType == SideQuestMarker }, i));
        }

        found.Sort(static (a, b) =>
        {
            var tier = Tier(a.End).CompareTo(Tier(b.End));
            if (tier != 0)
            {
                return tier;
            }

            var left = a.End.Left.CompareTo(b.End.Left);
            return left != 0 ? left : a.Index.CompareTo(b.Index);
        });

        var result = new LooseEnd[found.Count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = found[i].End;
        }

        return result;
    }

    /// <summary>0: a finale to take now marked only in yellow; 1: any other finale to take now; 2: the rest.</summary>
    private static int Tier(LooseEnd end) => end.IsReadyFinale ? (end.FinaleMarkedYellow ? 0 : 1) : 2;

    /// <summary>A genre's quests that can be steps, in prerequisite order; empty when fewer than two.</summary>
    private static uint[] Steps(IReadOnlyList<QuestRecord> quests, QuestCatalog catalog)
    {
        var steps = new List<uint>(quests.Count);
        foreach (var quest in quests)
        {
            if (!quest.IsRetired && !quest.IsProgressTracker && !quest.IsRepeatable)
            {
                steps.Add(quest.RowId);
            }
        }

        return steps.Count < ChainCatalog.MinChainLength ? [] : ChainCatalog.InPrerequisiteOrder(steps, catalog);
    }
}
