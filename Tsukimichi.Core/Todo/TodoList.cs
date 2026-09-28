using Tsukimichi.Core.Discovery;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;

namespace Tsukimichi.Core.Todo;

/// <summary>The parts of the todo overlay, in display order.</summary>
public enum TodoSection : byte
{
    Pinned,
    NearbyFeature,
    Msq,
    JobQuests,
}

/// <summary>Why a quest is on the list; one kind per section except job quests, which tell a job's own line from its role's.</summary>
public enum TodoRowKind : byte
{
    Pin,
    NearbyFeature,
    Msq,
    JobQuest,
    RoleQuest,
}

/// <summary>One line of the overlay: the quest, its state for the character and a short hint (the next step, or where to start it).</summary>
public sealed record TodoRow(uint RowId, string Name, QuestState State, string Hint, TodoRowKind Kind);

/// <summary>A non-empty section of the overlay.</summary>
public sealed record TodoSectionModel(TodoSection Section, IReadOnlyList<TodoRow> Rows);

/// <summary>What the overlay shows: the non-empty sections in display order, plus how many sections were enabled at all.</summary>
/// <param name="Sections">Only sections with at least one row; a disabled or empty section is left out.</param>
/// <param name="EnabledSections">How many sections the inputs enabled, so an empty list can be told from "nothing turned on".</param>
public sealed record TodoModel(IReadOnlyList<TodoSectionModel> Sections, int EnabledSections)
{
    public static readonly TodoModel Empty = new([], 0);

    public bool IsEmpty => Sections.Count == 0;

    /// <summary>Rows across every section.</summary>
    public int Count
    {
        get
        {
            var count = 0;
            foreach (var section in Sections)
            {
                count += section.Rows.Count;
            }

            return count;
        }
    }
}

/// <summary>
/// Everything <see cref="TodoList.Build"/> reads. <paramref name="JobNames"/> maps ClassJob row ids to short names for
/// the "Ready on X" hint; an empty map falls back to a generic phrase.
/// </summary>
/// <param name="Catalog">The quest catalog.</param>
/// <param name="States">Evaluations for the character keyed by quest row id.</param>
/// <param name="Pinned">The character's pinned quest row ids.</param>
/// <param name="FeatureQuestIds">Row ids of the feature ("blue") quests.</param>
/// <param name="TerritoryId">The territory the character stands in; 0 when unknown.</param>
/// <param name="CurrentJob">ClassJob row id of the current class or job; 0 when unknown.</param>
/// <param name="JobLevels">Unsynced level per ClassJob row id.</param>
/// <param name="Ladder">Job ladders over <paramref name="Catalog"/>.</param>
/// <param name="JobNames">Short names (abbreviations) per ClassJob row id.</param>
/// <param name="ShowPins">Include the Pinned section.</param>
/// <param name="ShowNearbyFeature">Include the Nearby feature quests section.</param>
/// <param name="ShowMsq">Include the MSQ section.</param>
/// <param name="ShowJobQuests">Include the Job quests section.</param>
public sealed record TodoInputs(
    QuestCatalog Catalog,
    IReadOnlyDictionary<uint, QuestEvaluation> States,
    IReadOnlySet<uint> Pinned,
    IReadOnlySet<uint> FeatureQuestIds,
    uint TerritoryId,
    byte CurrentJob,
    IReadOnlyDictionary<byte, short> JobLevels,
    JobLadder Ladder,
    IReadOnlyDictionary<uint, string> JobNames,
    bool ShowPins = true,
    bool ShowNearbyFeature = true,
    bool ShowMsq = true,
    bool ShowJobQuests = true);

/// <summary>
/// Pure builder for the todo overlay (V2-13). Four sections, each only when enabled and non-empty: the character's
/// pins that are still to do (Ready first, then Ready on another job, Accepted, Blocked, Unknown; by level then name
/// within a state), the feature quests startable in the current zone (at most <see cref="MaxNearby"/>, by level then
/// name), the next main scenario quest with its blocker, and for the current job the next quest of its ladder and of
/// its role's ladder when either is open now (Ready, Ready on another job or Accepted). Completed, done-this-cycle and
/// foreclosed pins are not todos and are left out. Hints are the evaluator's next-step clause when something blocks,
/// otherwise the level and giver. The caller memoizes per session version, territory and settings.
/// </summary>
public static class TodoList
{
    /// <summary>Most feature quests the Nearby section lists.</summary>
    public const int MaxNearby = 8;

    // Hint fragments, in the same voice as RequirementResult.Detail (English in Core; the overlay shows them as is).
    private const string ReadyOnOtherJobHint = "Ready on another job";
    private const string ReadyOnJobPrefix = "Ready on ";
    private const string AcceptedHint = "In your journal";
    private const string AcceptedStepPrefix = "In your journal, step ";
    private const string LevelPrefix = "Lv ";
    private const string Separator = " · ";
    private const string UnknownHint = "State unknown";
    private const string BlockedHint = "Blocked";

    public static TodoModel Build(TodoInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(inputs.Catalog);
        ArgumentNullException.ThrowIfNull(inputs.States);
        ArgumentNullException.ThrowIfNull(inputs.Pinned);
        ArgumentNullException.ThrowIfNull(inputs.FeatureQuestIds);
        ArgumentNullException.ThrowIfNull(inputs.JobLevels);
        ArgumentNullException.ThrowIfNull(inputs.Ladder);
        ArgumentNullException.ThrowIfNull(inputs.JobNames);

        var sections = new List<TodoSectionModel>(4);
        var enabled = 0;
        if (inputs.ShowPins)
        {
            enabled++;
            Add(sections, TodoSection.Pinned, BuildPinned(inputs));
        }

        if (inputs.ShowNearbyFeature)
        {
            enabled++;
            Add(sections, TodoSection.NearbyFeature, BuildNearby(inputs));
        }

        if (inputs.ShowMsq)
        {
            enabled++;
            Add(sections, TodoSection.Msq, BuildMsq(inputs));
        }

        if (inputs.ShowJobQuests)
        {
            enabled++;
            Add(sections, TodoSection.JobQuests, BuildJobQuests(inputs));
        }

        return sections.Count == 0 && enabled == 0 ? TodoModel.Empty : new TodoModel(sections, enabled);
    }

    private static void Add(List<TodoSectionModel> sections, TodoSection section, List<TodoRow> rows)
    {
        if (rows.Count > 0)
        {
            sections.Add(new TodoSectionModel(section, rows));
        }
    }

    /// <summary>True for a state the character can still act on: a completed or foreclosed pin has nothing left to do.</summary>
    public static bool IsTodo(QuestState state) =>
        state is QuestState.Ready or QuestState.ReadyOnOtherJob or QuestState.Accepted or QuestState.Blocked or QuestState.Unknown;

    /// <summary>Display rank of a state in the Pinned section: what can be done now comes first.</summary>
    private static int Rank(QuestState state) => state switch
    {
        QuestState.Ready => 0,
        QuestState.ReadyOnOtherJob => 1,
        QuestState.Accepted => 2,
        QuestState.Blocked => 3,
        _ => 4,
    };

    private static List<TodoRow> BuildPinned(TodoInputs inputs)
    {
        var rows = new List<TodoRow>();
        if (inputs.Pinned.Count == 0)
        {
            return rows;
        }

        var picked = new List<(QuestRecord Quest, QuestState State)>();
        foreach (var rowId in inputs.Pinned)
        {
            if (!inputs.Catalog.TryGetByRowId(rowId, out var quest))
            {
                continue;
            }

            var state = StateOf(inputs.States, rowId);
            if (IsTodo(state))
            {
                picked.Add((quest, state));
            }
        }

        picked.Sort(static (a, b) =>
        {
            var byRank = Rank(a.State).CompareTo(Rank(b.State));
            if (byRank != 0)
            {
                return byRank;
            }

            var byLevel = a.Quest.Level.CompareTo(b.Quest.Level);
            return byLevel != 0 ? byLevel : string.Compare(a.Quest.Name, b.Quest.Name, StringComparison.CurrentCultureIgnoreCase);
        });

        foreach (var (quest, state) in picked)
        {
            rows.Add(Row(inputs, quest, state, TodoRowKind.Pin));
        }

        return rows;
    }

    private static List<TodoRow> BuildNearby(TodoInputs inputs)
    {
        var rows = new List<TodoRow>();
        if (inputs.TerritoryId == 0 || inputs.FeatureQuestIds.Count == 0)
        {
            return rows;
        }

        foreach (var quest in QuestDiscovery.StartableInZone(inputs.Catalog, inputs.States, inputs.TerritoryId, includeOtherJob: true))
        {
            if (!inputs.FeatureQuestIds.Contains(quest.RowId))
            {
                continue;
            }

            rows.Add(Row(inputs, quest, StateOf(inputs.States, quest.RowId), TodoRowKind.NearbyFeature));
            if (rows.Count >= MaxNearby)
            {
                break;
            }
        }

        return rows;
    }

    private static List<TodoRow> BuildMsq(TodoInputs inputs)
    {
        var rows = new List<TodoRow>();
        if (inputs.States.Count == 0 || MsqProgress.Compute(inputs.Catalog, inputs.States) is not { Next: { } next } position)
        {
            return rows;
        }

        rows.Add(Row(inputs, next, position.State, TodoRowKind.Msq));
        return rows;
    }

    private static List<TodoRow> BuildJobQuests(TodoInputs inputs)
    {
        var rows = new List<TodoRow>();
        var job = inputs.CurrentJob;
        if (job == 0 || inputs.States.Count == 0)
        {
            return rows;
        }

        var level = inputs.JobLevels.GetValueOrDefault(job, (short)0);
        if (inputs.Ladder.ForJob(job) is { } entry)
        {
            AddLadderRow(rows, inputs, entry.QuestRowIds, level, TodoRowKind.JobQuest);
        }

        if (inputs.Ladder.RoleOf(job) is { } role)
        {
            AddLadderRow(rows, inputs, inputs.Ladder.RoleLadder(role), level, TodoRowKind.RoleQuest);
        }

        return rows;
    }

    /// <summary>The ladder's next quest when it is open now (Ready, Ready on another job or Accepted) and not listed yet.</summary>
    private static void AddLadderRow(List<TodoRow> rows, TodoInputs inputs, IReadOnlyList<uint> ladder, short level, TodoRowKind kind)
    {
        if (ladder.Count == 0)
        {
            return;
        }

        var progress = inputs.Ladder.Progress(ladder, inputs.States, level);
        if (progress.NextRowId is not { } next || !progress.IsReadyNow || !inputs.Catalog.TryGetByRowId(next, out var quest))
        {
            return;
        }

        foreach (var row in rows)
        {
            if (row.RowId == next)
            {
                return;
            }
        }

        rows.Add(Row(inputs, quest, StateOf(inputs.States, next), kind));
    }

    private static QuestState StateOf(IReadOnlyDictionary<uint, QuestEvaluation> states, uint rowId) =>
        states.TryGetValue(rowId, out var evaluation) ? evaluation.State : QuestState.Unknown;

    private static TodoRow Row(TodoInputs inputs, QuestRecord quest, QuestState state, TodoRowKind kind) =>
        new(quest.RowId, quest.Name, state, Hint(inputs, quest, state), kind);

    /// <summary>
    /// The row's hint: the evaluator's next-step clause when the quest is blocked, "Ready on JOB" when another job can
    /// take it, the journal step when accepted, otherwise the level and the giver's name.
    /// </summary>
    public static string Hint(TodoInputs inputs, QuestRecord quest, QuestState state)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(quest);
        inputs.States.TryGetValue(quest.RowId, out var evaluation);
        switch (state)
        {
            case QuestState.Ready:
                return LevelAndGiver(quest);

            case QuestState.ReadyOnOtherJob:
                if (evaluation?.ReadyOnJob is { } jobId && inputs.JobNames.TryGetValue(jobId, out var jobName) && jobName.Length > 0)
                {
                    return ReadyOnJobPrefix + jobName;
                }

                return ReadyOnOtherJobHint;

            case QuestState.Accepted:
                return evaluation?.Sequence is { } sequence ? AcceptedStepPrefix + sequence.ToString(System.Globalization.CultureInfo.InvariantCulture) : AcceptedHint;

            case QuestState.Blocked:
                return evaluation?.NextStep?.Detail is { Length: > 0 } detail ? detail : BlockedHint;

            default:
                return UnknownHint;
        }
    }

    private static string LevelAndGiver(QuestRecord quest)
    {
        var level = LevelPrefix + quest.Level.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return quest.Issuer is { Name.Length: > 0 } issuer ? level + Separator + issuer.Name : level;
    }
}
