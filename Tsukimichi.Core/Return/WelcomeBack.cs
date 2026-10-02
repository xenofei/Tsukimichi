using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Seasonal;
using Tsukimichi.Core.Storage;

namespace Tsukimichi.Core.Return;

/// <summary>What became of a quest that was in the journal when the character was last captured.</summary>
public enum MidwayOutcome : byte
{
    /// <summary>Still in the journal at the step it had then.</summary>
    SameStep,

    /// <summary>Still in the journal, at another step.</summary>
    Moved,

    /// <summary>Completed since.</summary>
    Completed,

    /// <summary>No longer in the journal and not completed (abandoned, or a repeatable that expired).</summary>
    Dropped,

    /// <summary>No capture to compare with: the quest is simply in the journal now.</summary>
    InJournal,
}

/// <summary>One quest that was mid-way then (or, with no capture, is in the journal now), and where it stands.</summary>
/// <param name="ThenSequence">The journal sequence it had then (255 is the last step); 0 when there is no capture.</param>
/// <param name="NowSequence">The journal sequence it has now; 0 when it is no longer in the journal.</param>
public sealed record MidwayQuest(QuestRecord Quest, byte ThenSequence, byte NowSequence, MidwayOutcome Outcome)
{
    /// <summary>"step 3 of 5" then, or empty without a capture.</summary>
    public string ThenStep => ThenSequence == 0 ? string.Empty : BlockerText.StepText(ThenSequence, Quest.StepCount);

    /// <summary>"step 4 of 5" now, or empty once it left the journal.</summary>
    public string NowStep => NowSequence == 0 ? string.Empty : BlockerText.StepText(NowSequence, Quest.StepCount);
}

/// <summary>The quests one patch series added, by kind (seasonal event quests are left out: events running now are listed apart).</summary>
/// <param name="Series">The series ("7.5", shown "7.5x"): the unit of the Journal's "Added in" filter.</param>
public sealed record NewQuestsInSeries(string Series, int MainScenario, int Unlocks, int Side)
{
    public int Total => MainScenario + Unlocks + Side;
}

/// <summary>One job whose unsynced level differs between the capture then and now.</summary>
public sealed record JobLevelChange(byte Job, short Then, short Now);

/// <summary>Where "new since" is measured from.</summary>
public enum SincePatchSource : byte
{
    /// <summary>Nothing to measure from: no capture with a known patch, and no answer.</summary>
    None,

    /// <summary>The patch recorded when the stored capture was taken (<see cref="WelcomeBackState.SeenPatch"/>).</summary>
    Recorded,

    /// <summary>The newest patch among the quests the stored capture had done or had in its journal: a lower bound.</summary>
    Inferred,

    /// <summary>The player's "When did you last play?" answer, a patch series.</summary>
    Answer,
}

/// <summary>Everything <see cref="WelcomeBack.Compute"/> reads. Only <see cref="Catalog"/>, <see cref="Current"/>, <see cref="CurrentStates"/> and <see cref="NowUtc"/> are required.</summary>
/// <param name="Catalog">The catalog; the patch index and the main scenario graph are the ones cached with it.</param>
/// <param name="Current">The capture now.</param>
/// <param name="CurrentStates">Its evaluations.</param>
/// <param name="NowUtc">The clock for "days away" and for the events running now.</param>
public sealed record WelcomeBackInput(QuestCatalog Catalog, CharacterSnapshot Current, IReadOnlyDictionary<uint, QuestEvaluation> CurrentStates, DateTime NowUtc)
{
    private static readonly IReadOnlySet<uint> NoIds = new HashSet<uint>();
    private static readonly IReadOnlyDictionary<ushort, FestivalInfo> NoFestivals = new Dictionary<ushort, FestivalInfo>();

    /// <summary>The stored capture from before the absence; null when the character has none (a fresh install).</summary>
    public CharacterSnapshot? Previous { get; init; }

    /// <summary>The evaluations of <see cref="Previous"/>; null resolves it with <see cref="Context"/> (the current catalog, so a quest added since is simply not done).</summary>
    public IReadOnlyDictionary<uint, QuestEvaluation>? PreviousStates { get; init; }

    /// <summary>The context <see cref="Previous"/> is resolved with when <see cref="PreviousStates"/> is null.</summary>
    public EvalContext Context { get; init; } = EvalContext.Default;

    /// <summary>The patch recorded with <see cref="Previous"/> (<see cref="WelcomeBackState.SeenPatch"/>); empty infers it.</summary>
    public string RecordedPatch { get; init; } = string.Empty;

    /// <summary>The "When did you last play?" answer: a patch series, <see cref="WelcomeBackState.NewPlayer"/> or empty.</summary>
    public string LastPlayedPatch { get; init; } = string.Empty;

    /// <summary>Unlock quests (<see cref="FeaturePresets.Derive"/>): the new quests' "unlock" kind.</summary>
    public IReadOnlySet<uint> FeatureQuestIds { get; init; } = NoIds;

    /// <summary>The events running on the server now.</summary>
    public ServerFestivals Festivals { get; init; } = ServerFestivals.None;

    /// <summary>The curated festival names and announced ends.</summary>
    public IReadOnlyDictionary<ushort, FestivalInfo> CuratedFestivals { get; init; } = NoFestivals;

    /// <summary>The duties the main scenario quests unlock, for the catch-up's duty count; null counts the required ones only.</summary>
    public CatchUpDutySource? CatchUpDuties { get; init; }
}

/// <summary>The "Since you were away" summary (feature plan v3 P7). See <see cref="WelcomeBack.Compute"/>.</summary>
public sealed record WelcomeBackSummary
{
    /// <summary>When the stored capture was taken; null without one.</summary>
    public DateTime? PreviousTakenUtc { get; init; }

    /// <summary>Whole days between the stored capture and now; null without one.</summary>
    public int? DaysAway { get; init; }

    /// <summary>The patch "new" is measured from: exact for a capture ("7.25"), a series for an answer ("7.2", read 7.2x). Empty with <see cref="SincePatchSource.None"/>.</summary>
    public string SincePatch { get; init; } = string.Empty;

    public SincePatchSource SinceSource { get; init; }

    /// <summary>The newest patch the catalog knows ("7.56").</summary>
    public string NewestPatch { get; init; } = string.Empty;

    /// <summary>The quests that were in the journal then (non-repeatable), in journal order; without a capture, the ones in it now.</summary>
    public IReadOnlyList<MidwayQuest> Midway { get; init; } = [];

    /// <summary>The main scenario position then; null without a capture.</summary>
    public MsqPosition? MsqThen { get; init; }

    /// <summary>The main scenario position now, route by route inside a branch region.</summary>
    public MsqPosition? MsqNow { get; init; }

    /// <summary>What is left of the main scenario now, per expansion (<see cref="MsqCatchUp"/>); null without main scenario quests.</summary>
    public MsqCatchUpSummary? CatchUp { get; init; }

    /// <summary>The quests added after <see cref="SincePatch"/>, one entry per series, newest first; empty when nothing is new or there is nothing to measure from.</summary>
    public IReadOnlyList<NewQuestsInSeries> NewQuests { get; init; } = [];

    /// <summary>The events running now, each with its quests' states.</summary>
    public IReadOnlyList<RunningFestival> Events { get; init; } = [];

    /// <summary>The jobs whose level changed since the capture, by job id; empty without one.</summary>
    public IReadOnlyList<JobLevelChange> JobChanges { get; init; } = [];

    public bool HasPrevious => PreviousTakenUtc is not null;

    public int NewQuestTotal => NewQuests.Sum(static s => s.Total);

    public int NewMainScenario => NewQuests.Sum(static s => s.MainScenario);

    public int NewUnlocks => NewQuests.Sum(static s => s.Unlocks);

    public int NewSide => NewQuests.Sum(static s => s.Side);

    /// <summary>Main scenario quests completed since the capture (0 without one).</summary>
    public int MsqDoneSince => MsqThen is { } then && MsqNow is { } now ? Math.Max(0, now.Done - then.Done) : 0;

    /// <summary>Whether the main scenario position moved since the capture.</summary>
    public bool MsqMoved => MsqThen is { } then && MsqNow is { } now
        && (now.Done != then.Done || !ReferenceEquals(now.Next, then.Next));

    /// <summary>
    /// Whether anything changed since the capture or was added since the measured patch: a journal quest moved,
    /// completed or dropped, the main scenario moved, a quest was added, a job levelled.
    /// </summary>
    public bool HasChanges =>
        Midway.Any(static m => m.Outcome is MidwayOutcome.Moved or MidwayOutcome.Completed or MidwayOutcome.Dropped)
        || MsqMoved
        || NewQuestTotal > 0
        || JobChanges.Count > 0;

    /// <summary>Nothing to say at all: no change, nothing in the journal, no event running.</summary>
    public bool IsEmpty => !HasChanges && Midway.Count == 0 && Events.Count == 0;
}

/// <summary>
/// "Since you were away" (feature plan v3 P7, docs/research/player-gripes-2026.md grievance 6): what a returning
/// player was doing, what moved, and what the game gained while they were gone. Compares the character's stored
/// capture from before the absence with the capture now; with no stored capture (a fresh install) the player's
/// "When did you last play?" answer stands in for the "then" of the catalog half. Pure; the plugin runs it once per
/// return, off the framework thread.
/// </summary>
public static class WelcomeBack
{
    /// <summary>Builds the summary. See <see cref="WelcomeBackSummary"/> for each part.</summary>
    public static WelcomeBackSummary Compute(WelcomeBackInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var catalog = input.Catalog;
        var previous = input.Previous;
        var graph = MsqGraph.For(catalog);
        var patches = PatchIndex.For(catalog);

        IReadOnlyDictionary<uint, QuestEvaluation>? previousStates = null;
        if (previous is not null)
        {
            previousStates = input.PreviousStates ?? StateResolver.ResolveAll(catalog, previous, input.Context);
        }

        var (since, source) = SincePatch(input);
        return new WelcomeBackSummary
        {
            PreviousTakenUtc = previous?.TakenUtc,
            DaysAway = previous is null ? null : Math.Max(0, (int)Math.Floor((input.NowUtc - previous.TakenUtc).TotalDays)),
            SincePatch = since,
            SinceSource = source,
            NewestPatch = patches.Newest,
            Midway = Midway(catalog, previous, input.Current),
            MsqThen = previousStates is null ? null : graph.Position(previousStates),
            MsqNow = graph.Position(input.CurrentStates),
            CatchUp = MsqCatchUp.Compute(catalog, input.CurrentStates, input.Current, input.CatchUpDuties),
            NewQuests = NewSince(catalog, since, source == SincePatchSource.Answer, input.FeatureQuestIds),
            Events = SeasonalNow.Running(catalog, input.Festivals, input.CurrentStates, input.CuratedFestivals, input.NowUtc),
            JobChanges = previous is null ? [] : JobChanges(previous, input.Current),
        };
    }

    /// <summary>
    /// The patch a capture was taken on, at the least: the newest patch among the quests it had completed or had in
    /// its journal (removed quests and quests of unknown patch ignored). A character that had not done the newest
    /// content reads older than it was, so more quests count as new, never fewer. Empty when no quest dates it.
    /// </summary>
    public static string InferPatch(QuestCatalog catalog, CharacterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(snapshot);
        var newest = string.Empty;
        foreach (var quest in catalog.All)
        {
            if (quest.IsRemoved || !PatchVersion.IsPatch(quest.AddedIn) || (newest.Length > 0 && PatchVersion.Compare(quest.AddedIn, newest) <= 0))
            {
                continue;
            }

            if (snapshot.IsCompleted(quest.QuestId) || InJournal(snapshot, quest.QuestId) is not null)
            {
                newest = quest.AddedIn;
            }
        }

        return newest;
    }

    /// <summary>
    /// Whether a quest added in <paramref name="addedIn"/> is new since <paramref name="since"/>: a later patch, or
    /// with <paramref name="sinceIsSeries"/> a later series (an answer of "7.2" has seen 7.2 to 7.25). An unknown
    /// patch on either side is never new.
    /// </summary>
    public static bool IsNewSince(string addedIn, string since, bool sinceIsSeries)
    {
        if (!PatchVersion.IsPatch(addedIn) || !PatchVersion.IsPatch(since))
        {
            return false;
        }

        return sinceIsSeries
            ? PatchVersion.Compare(PatchVersion.Series(addedIn), PatchVersion.Series(since)) > 0
            : PatchVersion.Compare(addedIn, since) > 0;
    }

    /// <summary>
    /// Where "new" is measured from: with a capture, the patch recorded with it or else the one inferred from it (the
    /// answer only when neither dates it); without one, the answer. "I'm new" measures from nothing.
    /// </summary>
    private static (string Since, SincePatchSource Source) SincePatch(WelcomeBackInput input)
    {
        if (input.Previous is { } previous)
        {
            var recorded = PatchVersion.Normalize(input.RecordedPatch);
            if (PatchVersion.IsPatch(recorded))
            {
                return (recorded, SincePatchSource.Recorded);
            }

            var inferred = InferPatch(input.Catalog, previous);
            if (inferred.Length > 0)
            {
                return (inferred, SincePatchSource.Inferred);
            }
        }

        var answer = PatchVersion.Normalize(input.LastPlayedPatch);
        return PatchVersion.IsPatch(answer) ? (PatchVersion.Series(answer), SincePatchSource.Answer) : (string.Empty, SincePatchSource.None);
    }

    private static List<MidwayQuest> Midway(QuestCatalog catalog, CharacterSnapshot? previous, CharacterSnapshot current)
    {
        var list = new List<MidwayQuest>();
        var journal = previous ?? current;
        foreach (var accepted in journal.Accepted)
        {
            // Allied-society dailies and other repeatables expire on their own; they are not what the player was doing.
            if (!catalog.TryGetByQuestId(accepted.QuestId, out var quest) || quest.IsRepeatable)
            {
                continue;
            }

            if (previous is null)
            {
                list.Add(new MidwayQuest(quest, 0, accepted.Sequence, MidwayOutcome.InJournal));
                continue;
            }

            var now = InJournal(current, accepted.QuestId);
            var outcome = now is { } sequence
                ? sequence == accepted.Sequence ? MidwayOutcome.SameStep : MidwayOutcome.Moved
                : current.IsCompleted(accepted.QuestId) ? MidwayOutcome.Completed : MidwayOutcome.Dropped;
            list.Add(new MidwayQuest(quest, accepted.Sequence, now ?? 0, outcome));
        }

        list.Sort(static (a, b) =>
        {
            var bySort = a.Quest.Journal.SortKey.CompareTo(b.Quest.Journal.SortKey);
            return bySort != 0 ? bySort : a.Quest.RowId.CompareTo(b.Quest.RowId);
        });
        return list;
    }

    private static List<NewQuestsInSeries> NewSince(QuestCatalog catalog, string since, bool sinceIsSeries, IReadOnlySet<uint> featureQuestIds)
    {
        if (since.Length == 0)
        {
            return [];
        }

        var counts = new Dictionary<string, (int Msq, int Unlocks, int Side)>(StringComparer.Ordinal);
        foreach (var quest in catalog.All)
        {
            // Seasonal quests return with every edition and are listed under the events running now instead.
            if (quest.IsRemoved || quest.Festival != 0 || !IsNewSince(quest.AddedIn, since, sinceIsSeries))
            {
                continue;
            }

            var series = PatchVersion.Series(quest.AddedIn);
            var c = counts.GetValueOrDefault(series);
            if (FeaturePresets.IsMainScenario(quest))
            {
                c.Msq++;
            }
            else if (featureQuestIds.Contains(quest.RowId))
            {
                c.Unlocks++;
            }
            else
            {
                c.Side++;
            }

            counts[series] = c;
        }

        return counts
            .OrderBy(static kv => kv.Key, PatchVersion.NewestFirst)
            .Select(static kv => new NewQuestsInSeries(kv.Key, kv.Value.Msq, kv.Value.Unlocks, kv.Value.Side))
            .ToList();
    }

    private static List<JobLevelChange> JobChanges(CharacterSnapshot previous, CharacterSnapshot current)
    {
        var jobs = new SortedSet<byte>(previous.JobLevels.Keys);
        jobs.UnionWith(current.JobLevels.Keys);
        var list = new List<JobLevelChange>();
        foreach (var job in jobs)
        {
            var then = previous.JobLevels.GetValueOrDefault(job);
            var now = current.JobLevels.GetValueOrDefault(job);
            if (then != now)
            {
                list.Add(new JobLevelChange(job, then, now));
            }
        }

        return list;
    }

    private static byte? InJournal(CharacterSnapshot snapshot, ushort questId)
    {
        foreach (var quest in snapshot.Accepted)
        {
            if (quest.QuestId == questId)
            {
                return quest.Sequence;
            }
        }

        return null;
    }
}
