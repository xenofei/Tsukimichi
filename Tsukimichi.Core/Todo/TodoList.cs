using Tsukimichi.Core.Discovery;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Route;
using Tsukimichi.Core.Seasonal;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Localization;

namespace Tsukimichi.Core.Todo;

/// <summary>
/// The parts of the todo overlay. Display order is TurnIn, Route, NextStops, Pinned, Seasonal, NearbyFeature, Plan, Msq,
/// JobQuests; Seasonal (0.8.0), Plan (0.9.0), Route and NextStops (1.6.0) and TurnIn (1.19.0) were added last, so the
/// stored values of the others did not move.
/// </summary>
public enum TodoSection : byte
{
    Pinned,
    NearbyFeature,
    Msq,
    JobQuests,
    Seasonal,

    /// <summary>"Clear my blues" (P3): the Ready unlock quests of the expansion pinned from the plan.</summary>
    Plan,

    /// <summary>The followed route's next steps (1.6.0, R6 A).</summary>
    Route,

    /// <summary>"Next stops" (1.6.0, R6 B): Ready quests batched by aetheryte, one row per stop.</summary>
    NextStops,

    /// <summary>
    /// "Turn in on a job that isn't capped" (1.19.0, C8): journal quests at their turn-in step whose EXP the current,
    /// capped job would lose (<see cref="CappedTurnIns"/>). No setting of its own: it shows only while that is so.
    /// </summary>
    TurnIn,

    /// <summary>"Loose ends" (1.21.0 N8): storylines started and never finished, finales first; one row per line.</summary>
    LooseEnds,
}

/// <summary>Why a quest is on the list; one kind per section except job quests, which tell a job's own line from its role's.</summary>
public enum TodoRowKind : byte
{
    Pin,
    NearbyFeature,
    Msq,
    JobQuest,
    RoleQuest,
    Seasonal,
    Plan,

    /// <summary>A step of the followed route.</summary>
    Route,

    /// <summary>A stop of Next stops: the row names the place and stands on its first quest.</summary>
    Stop,

    /// <summary>A quest to hand in on a job that isn't capped (1.19.0, C8).</summary>
    TurnIn,

    /// <summary>A loose end: the row names the storyline and stands on its next quest.</summary>
    LooseEnd,
}

/// <summary>One line of the overlay: the quest, its state for the character and a short hint (the next step, or where to start it).</summary>
public sealed record TodoRow(uint RowId, string Name, QuestState State, string Hint, TodoRowKind Kind);

/// <summary>A non-empty section of the overlay.</summary>
public sealed record TodoSectionModel(TodoSection Section, IReadOnlyList<TodoRow> Rows)
{
    /// <summary>Lines shown under the section's header before its rows ("Ends Aug 28 (Lodestone)"); usually none.</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];

    /// <summary>
    /// Rows the section holds beyond <see cref="Rows"/>, left out by its cap (the Pinned section's
    /// <see cref="TodoInputs.PinLimit"/>); the overlay shows them as one "+N more" line. 0 when nothing was left out.
    /// </summary>
    public int More { get; init; }

    /// <summary>The section's caption when it names something of its own ("Route: everything for Dragoon"); empty uses the section's name.</summary>
    public string Title { get; init; } = string.Empty;
}

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
/// <param name="Pinned">The character's pinned quest row ids in the order they were pinned (<c>user/pins.json</c>'s order); the Pinned section keeps that order.</param>
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
/// <param name="Names">Name lookups for the blocker hints (<see cref="BlockerText"/>); null names quests from <paramref name="Catalog"/> only.</param>
/// <param name="Running">The seasonal events running for the character (<see cref="SeasonalNow.Running"/>); null leaves the section out.</param>
/// <param name="ShowSeasonal">Include the "Event quests running now" section.</param>
/// <param name="NowUtc">Clock for the section's end-date line; null reads <see cref="DateTime.UtcNow"/>.</param>
/// <param name="Plan">The character's "Clear my blues" plan (<see cref="UnlockPlan"/>); null leaves the section out.</param>
/// <param name="PlanExpansion">The expansion pinned from the plan (ExVersion row id); negative leaves the section out.</param>
/// <param name="ShowPlan">Include the "Clear my blues" section.</param>
/// <param name="PinLimit">Most rows the Pinned section lists (<see cref="TodoList.MaxPinned"/>, the overlay's cap); the rest are counted in <see cref="TodoSectionModel.More"/>.</param>
/// <param name="Route">The followed route, built for this character (<see cref="ActiveRoute"/>); null leaves the section out.</param>
/// <param name="ShowRoute">Include the route section.</param>
/// <param name="Stops">Next stops (<see cref="StopPlanner.Plan"/>); null leaves the section out.</param>
/// <param name="ShowNextStops">Include the Next stops section.</param>
/// <param name="EndingSoon">The events ending soon (<see cref="EventWarnings.EndingSoon"/>, 1.19.0 C10): their quests in the journal lead the seasonal section; null keeps the events' order.</param>
/// <param name="TimeZone">The zone the end-date line prints its date in (<see cref="SeasonalNow.DateText"/>); null reads <see cref="TimeZoneInfo.Local"/>, the player's.</param>
/// <param name="CappedTurnIns">The journal quests to turn in on a job that isn't capped (<see cref="Jobs.CappedTurnIns.Find"/>, 1.19.0 C8); null or empty leaves the section out. Not a section the player enables, so it never counts in <see cref="TodoModel.EnabledSections"/>.</param>
/// <param name="TierOf">My blues' tier of an unlock quest (1.21.0, P4), added after the level of the Nearby and Clear my blues rows ("Lv 50 · Story needs it"); null adds none.</param>
/// <param name="SetAside">The quests the player set aside in My blues (P4): left out of the Nearby feature quests; null leaves none out.</param>
/// <param name="LooseEnds">The Loose ends rows (1.21.0 N8), built by the caller in display order (finales first), each standing on its line's next quest; null leaves the section out.</param>
/// <param name="ShowLooseEnds">Include the Loose ends section (off by default).</param>
public sealed record TodoInputs(
    QuestCatalog Catalog,
    IReadOnlyDictionary<uint, QuestEvaluation> States,
    IReadOnlyList<uint> Pinned,
    IReadOnlySet<uint> FeatureQuestIds,
    uint TerritoryId,
    byte CurrentJob,
    IReadOnlyDictionary<byte, short> JobLevels,
    JobLadder Ladder,
    IReadOnlyDictionary<uint, string> JobNames,
    bool ShowPins = true,
    bool ShowNearbyFeature = true,
    bool ShowMsq = true,
    bool ShowJobQuests = true,
    BlockerNames? Names = null,
    IReadOnlyList<RunningFestival>? Running = null,
    bool ShowSeasonal = true,
    DateTime? NowUtc = null,
    UnlockPlan? Plan = null,
    int PlanExpansion = -1,
    bool ShowPlan = true,
    int PinLimit = TodoList.MaxPinned,
    UnlockRoute? Route = null,
    bool ShowRoute = true,
    IReadOnlyList<Stop>? Stops = null,
    bool ShowNextStops = false,
    IReadOnlyList<EndingSoonEvent>? EndingSoon = null,
    TimeZoneInfo? TimeZone = null,
    IReadOnlyList<CappedTurnIn>? CappedTurnIns = null,
    Func<QuestRecord, UnlockTier?>? TierOf = null,
    IReadOnlySet<uint>? SetAside = null,
    IReadOnlyList<TodoRow>? LooseEnds = null,
    bool ShowLooseEnds = false);

/// <summary>
/// Pure builder for the todo overlay (V2-13). Six sections, each only when enabled and non-empty: the character's
/// pins that are still to do in the order they were pinned (so a route pinned with "Pin all" reads in the order to do
/// it; at most <see cref="TodoInputs.PinLimit"/>, the rest counted in <see cref="TodoSectionModel.More"/>), the quests of the seasonal events running now that can be started or are in the journal (P11,
/// with an "Ends Aug 28 (Lodestone)" line only when curated data announces the end), the feature quests startable in the current zone (at most <see cref="MaxNearby"/>, by level then
/// name), the Ready unlock quests of the expansion pinned from the "Clear my blues" plan (P3, at most
/// <see cref="MaxPlan"/>, in plan order), the next main scenario quest with its blocker, and for the current job the next quest of its ladder and of
/// its role's ladder when either is open now (Ready, Ready on another job or Accepted). Completed, done-this-cycle and
/// foreclosed pins are not todos and are left out, as are spare alternatives (an open choice's options other than the
/// presumed one) among the pins and the event quests. Hints are the evaluator's next-step clause when something blocks,
/// otherwise the level and giver. The caller memoizes per session version, territory and settings.
/// </summary>
public static class TodoList
{
    /// <summary>Most feature quests the Nearby section lists.</summary>
    public const int MaxNearby = 8;

    /// <summary>Most quests the "Clear my blues" section lists.</summary>
    public const int MaxPlan = 8;

    /// <summary>Most pins the overlay's Pinned section lists; the rest are one "+N more" line, so a 60-step route is not 60 rows.</summary>
    public const int MaxPinned = 8;

    // Hint fragments in the display vocabulary (English in Core; the overlay shows them as is). The row's moon already
    // carries the state, so a hint never repeats the state name: a blocked row shows its blocker, an accepted one its step.
    private static string ReadyOnJobFormat => CoreText.T("Core.Todo.ReadyOnJob", "Ready on {0}");
    private static string LevelFormat => CoreText.T("Core.Todo.Level", "Lv {0}");
    private const string Separator = " · ";

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

        var sections = new List<TodoSectionModel>(9);
        var enabled = 0;
        if (inputs.CappedTurnIns is { Count: > 0 } turnIns)
        {
            AddTurnIns(sections, inputs, turnIns);
        }

        if (inputs.ShowRoute && inputs.Route is { } route)
        {
            enabled++;
            AddRoute(sections, inputs, route);
        }

        if (inputs.ShowNextStops && inputs.Stops is { } stops)
        {
            enabled++;
            AddStops(sections, stops);
        }

        if (inputs.ShowPins)
        {
            enabled++;
            AddPinned(sections, inputs);
        }

        if (inputs.ShowSeasonal && inputs.Running is { } running)
        {
            enabled++;
            AddSeasonal(sections, inputs, running);
        }

        if (inputs.ShowNearbyFeature)
        {
            enabled++;
            Add(sections, TodoSection.NearbyFeature, BuildNearby(inputs));
        }

        if (inputs.ShowPlan && inputs.Plan is { } plan && inputs.PlanExpansion is >= 0 and <= byte.MaxValue)
        {
            enabled++;
            AddPlan(sections, inputs, plan, (byte)inputs.PlanExpansion);
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

        if (inputs.ShowLooseEnds && inputs.LooseEnds is { } looseEnds)
        {
            enabled++;
            AddLooseEnds(sections, looseEnds);
        }

        return sections.Count == 0 && enabled == 0 ? TodoModel.Empty : new TodoModel(sections, enabled);
    }

    /// <summary>Most stops the Next stops section lists.</summary>
    public const int MaxStops = 3;

    private static string TurnInFormat => CoreText.T("Core.Todo.TurnIn", "Turn in on a job that isn't capped: {0}");
    private static string TurnInHintFormat => CoreText.T("Core.Todo.TurnInHint", "{0} Lv {1} · {2} EXP");
    private static string TurnInHintNoJobFormat => CoreText.T("Core.Todo.TurnInHintNoJob", "{0} EXP elsewhere");

    /// <summary>
    /// "Turn in on a job that isn't capped: Into the Aery" (spec-1.19 C8), one row per capped turn-in in journal order,
    /// the quest named through the spoiler shield (<see cref="TodoInputs.Names"/>), the hint naming the job that gets
    /// the most ("DRG Lv 56 · 50,700 EXP"). A click selects the quest, as every row does.
    /// </summary>
    private static void AddTurnIns(List<TodoSectionModel> sections, TodoInputs inputs, IReadOnlyList<CappedTurnIn> turnIns)
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        var rows = new List<TodoRow>(turnIns.Count);
        foreach (var turnIn in turnIns)
        {
            var name = string.Format(culture, TurnInFormat, QuestName(inputs, turnIn.Quest));
            var hint = string.Empty;
            if (turnIn.Advice.Best is { } best)
            {
                var amount = best.Exp.ToString("N0", culture);
                hint = inputs.JobNames.TryGetValue(best.Job, out var job) && job.Length > 0 && best.Level > 0
                    ? string.Format(culture, TurnInHintFormat, job, best.Level, amount)
                    : string.Format(culture, TurnInHintNoJobFormat, amount);
            }

            rows.Add(new TodoRow(turnIn.Quest.RowId, name, QuestState.Accepted, hint, TodoRowKind.TurnIn));
        }

        Add(sections, TodoSection.TurnIn, rows);
    }

    /// <summary>Most lines the Loose ends section lists; the rest are one "+N more" line.</summary>
    public const int MaxLooseEnds = 3;

    /// <summary>"Loose ends": the first <see cref="MaxLooseEnds"/> rows as given (finales first), the rest counted in <see cref="TodoSectionModel.More"/>.</summary>
    private static void AddLooseEnds(List<TodoSectionModel> sections, IReadOnlyList<TodoRow> rows)
    {
        if (rows.Count == 0)
        {
            return;
        }

        var shown = new List<TodoRow>(Math.Min(MaxLooseEnds, rows.Count));
        for (var i = 0; i < rows.Count && shown.Count < MaxLooseEnds; i++)
        {
            shown.Add(rows[i]);
        }

        sections.Add(new TodoSectionModel(TodoSection.LooseEnds, shown) { More = rows.Count - shown.Count });
    }

    /// <summary>
    /// "Route: everything for Dragoon": the followed route's next <see cref="ActiveRoute.Shown"/> steps in route order,
    /// the rest counted in <see cref="TodoSectionModel.More"/>, with the level gate line as a note when one of them
    /// waits on a level. A route with nothing left has no section (the plugin says so in chat and stops following).
    /// </summary>
    private static void AddRoute(List<TodoSectionModel> sections, TodoInputs inputs, UnlockRoute route)
    {
        var glance = ActiveRoute.Glance(route);
        if (glance.Next.Count == 0)
        {
            return;
        }

        var rows = new List<TodoRow>(glance.Next.Count);
        foreach (var step in glance.Next)
        {
            if (inputs.Catalog.TryGetByRowId(step.RowId, out var quest))
            {
                rows.Add(Row(inputs, quest, StateOf(inputs.States, step.RowId), TodoRowKind.Route));
            }
        }

        if (rows.Count == 0)
        {
            return;
        }

        var gate = glance.GateText;
        sections.Add(new TodoSectionModel(TodoSection.Route, rows)
        {
            More = glance.More,
            Notes = gate.Length > 0 ? [gate] : [],
            Title = string.Format(System.Globalization.CultureInfo.CurrentCulture, RouteTitleFormat, route.Target.Label),
        });
    }

    private static string RouteTitleFormat => CoreText.T("Core.Todo.RouteTitle", "Route: {0}");

    /// <summary>"Next stops": the first <see cref="MaxStops"/> stops, one row each, standing on the stop's first quest.</summary>
    private static void AddStops(List<TodoSectionModel> sections, IReadOnlyList<Stop> stops)
    {
        var rows = new List<TodoRow>(MaxStops);
        foreach (var stop in stops)
        {
            if (stop.Quests.Count == 0)
            {
                continue;
            }

            rows.Add(new TodoRow(stop.Quests[0].Quest.RowId, stop.Place.Name, QuestState.Ready, stop.CountText, TodoRowKind.Stop));
            if (rows.Count >= MaxStops)
            {
                break;
            }
        }

        Add(sections, TodoSection.NextStops, rows);
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

    /// <summary>
    /// <see cref="IsTodo(QuestState)"/> for an evaluation, and not a spare alternative (an option of a choice not made
    /// yet other than the one presumed, which leaves the counts); a quest without an evaluation reads Unknown.
    /// </summary>
    public static bool IsTodo(QuestEvaluation? evaluation) =>
        evaluation is null ? IsTodo(QuestState.Unknown) : !evaluation.IsSpareAlternative && IsTodo(evaluation.State);

    /// <summary>
    /// The pins still to do, in the order they were pinned (oldest first, so a route pinned with "Pin all" reads in
    /// route order), at most <see cref="TodoInputs.PinLimit"/>, the others counted in <see cref="TodoSectionModel.More"/>.
    /// Completed, done-this-cycle and foreclosed pins, spare alternatives, quests the catalog lacks and repeats of a row
    /// id are left out and not counted, so finishing a step brings the next pin into view.
    /// </summary>
    private static void AddPinned(List<TodoSectionModel> sections, TodoInputs inputs)
    {
        if (inputs.Pinned.Count == 0)
        {
            return;
        }

        var limit = Math.Max(0, inputs.PinLimit);
        var rows = new List<TodoRow>(Math.Min(inputs.Pinned.Count, limit));
        var more = 0;
        var seen = new HashSet<uint>();
        foreach (var rowId in inputs.Pinned)
        {
            if (!seen.Add(rowId) || !inputs.Catalog.TryGetByRowId(rowId, out var quest))
            {
                continue;
            }

            inputs.States.TryGetValue(rowId, out var evaluation);
            if (!IsTodo(evaluation))
            {
                continue;
            }

            if (rows.Count < limit)
            {
                rows.Add(Row(inputs, quest, StateOf(inputs.States, rowId), TodoRowKind.Pin));
            }
            else
            {
                more++;
            }
        }

        if (rows.Count > 0)
        {
            sections.Add(new TodoSectionModel(TodoSection.Pinned, rows) { More = more });
        }
    }

    /// <summary>
    /// "Event quests running now": every quest of a running event that can be started now (here or on another job) or
    /// is in the journal, event by event in <see cref="SeasonalNow.Running"/>'s order. One line per event with an
    /// announced end heads the rows ("Ends Aug 28 (Lodestone)", named when several events have rows).
    /// </summary>
    private static void AddSeasonal(List<TodoSectionModel> sections, TodoInputs inputs, IReadOnlyList<RunningFestival> running)
    {
        var entries = new List<SeasonalQuest>();
        var contributing = new List<RunningFestival>();
        foreach (var festival in running)
        {
            var before = entries.Count;
            foreach (var entry in festival.Quests)
            {
                if (entry.IsActionable)
                {
                    entries.Add(entry);
                }
            }

            if (entries.Count > before)
            {
                contributing.Add(festival);
            }
        }

        if (entries.Count == 0)
        {
            return;
        }

        // The quests in the journal of an event that ends soon go first: the game takes them away when it ends (C10).
        if (inputs.EndingSoon is { Count: > 0 } soon)
        {
            entries = EventWarnings.JournalFirst(entries, static e => e.Quest, static e => e.State, soon);
        }

        var rows = new List<TodoRow>(entries.Count);
        foreach (var entry in entries)
        {
            rows.Add(new TodoRow(entry.Quest.RowId, QuestName(inputs, entry.Quest), entry.State, SeasonalHint(inputs, entry.Quest, entry.State), TodoRowKind.Seasonal));
        }

        var now = inputs.NowUtc ?? DateTime.UtcNow;
        var notes = new List<string>();
        foreach (var festival in contributing)
        {
            if (SeasonalNow.EndsLine(festival, contributing.Count > 1, now, inputs.TimeZone) is { } line)
            {
                notes.Add(line);
            }
        }

        sections.Add(new TodoSectionModel(TodoSection.Seasonal, rows) { Notes = notes });
    }

    /// <summary>
    /// An event quest's hint always names its giver, since event givers stand in unusual places: "Lv 30 · Mayaru Moyaru"
    /// when it can be started, "step 2 of 3 · giver" in the journal, "Ready on DRG · giver" on another job.
    /// </summary>
    public static string SeasonalHint(TodoInputs inputs, QuestRecord quest, QuestState state)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(quest);
        var hint = Hint(inputs, quest, state);
        if (state == QuestState.Ready || quest.Issuer is not { Name.Length: > 0 } issuer)
        {
            return hint;
        }

        return hint.Length == 0 ? issuer.Name : hint + Separator + issuer.Name;
    }

    private static string QuestName(TodoInputs inputs, QuestRecord quest) => inputs.Names is { } names ? names.QuestName(quest) : quest.Name;

    private static List<TodoRow> BuildNearby(TodoInputs inputs)
    {
        var rows = new List<TodoRow>();
        if (inputs.TerritoryId == 0 || inputs.FeatureQuestIds.Count == 0)
        {
            return rows;
        }

        foreach (var quest in QuestDiscovery.StartableInZone(inputs.Catalog, inputs.States, inputs.TerritoryId, includeOtherJob: true))
        {
            if (!inputs.FeatureQuestIds.Contains(quest.RowId) || inputs.SetAside?.Contains(quest.RowId) == true)
            {
                continue;
            }

            var row = Row(inputs, quest, StateOf(inputs.States, quest.RowId), TodoRowKind.NearbyFeature);
            rows.Add(row with { Hint = WithTier(inputs, quest, row.Hint) });
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

        if (!position.IsBranched)
        {
            rows.Add(Row(inputs, next, position.State, TodoRowKind.Msq));
            return rows;
        }

        // Inside a branch region: one row per route still open, in route order, its hint led by the route and how
        // far along it is ("route The Ember Road · 2 of 3 · Lv 41 · …").
        foreach (var route in position.Routes)
        {
            if (route.Next is not { } routeNext)
            {
                continue;
            }

            var row = Row(inputs, routeNext, route.State, TodoRowKind.Msq);
            var prefix = MsqText.RowPrefix(route, quest => QuestName(inputs, quest));
            rows.Add(row with { Hint = row.Hint.Length > 0 ? prefix + MsqText.Separator + row.Hint : prefix });
        }

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

    /// <summary>
    /// The pinned expansion's block of the plan (P3): its quests that can be started now, in plan order (zone by zone,
    /// in story order), at most <see cref="MaxPlan"/>, with a note naming the expansion and how many unlock quests are
    /// left there. A Ready row's hint is its level and what it unlocks ("Lv 20 · Dungeon: Halatali").
    /// </summary>
    private static void AddPlan(List<TodoSectionModel> sections, TodoInputs inputs, UnlockPlan plan, byte expansion)
    {
        if (plan.Expansion(expansion) is not { } block)
        {
            return;
        }

        var rows = new List<TodoRow>();
        foreach (var entry in block.Entries)
        {
            if (!entry.IsReady)
            {
                continue;
            }

            var hint = entry.State == QuestState.Ready
                ? WithTier(inputs, entry.Quest, PlanHint(entry))
                : Hint(inputs, entry.Quest, entry.State);
            rows.Add(new TodoRow(entry.Quest.RowId, entry.Name, entry.State, hint, TodoRowKind.Plan));
            if (rows.Count >= MaxPlan)
            {
                break;
            }
        }

        if (rows.Count > 0)
        {
            var note = block.Name + Separator + string.Format(System.Globalization.CultureInfo.CurrentCulture, PlanLeftFormat, block.Count);
            sections.Add(new TodoSectionModel(TodoSection.Plan, rows) { Notes = [note] });
        }
    }

    private static string PlanLeftFormat => CoreText.T("Core.Todo.PlanLeft", "{0} left");

    /// <summary>"Lv 20 · Dungeon: Halatali": the level and the entry's first unlock.</summary>
    public static string PlanHint(PlanEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var level = string.Format(System.Globalization.CultureInfo.InvariantCulture, LevelFormat, entry.Quest.DisplayLevel);
        return entry.Unlocks.Count > 0 ? level + Separator + entry.Unlocks[0].Label : level;
    }

    /// <summary>
    /// The hint with the quest's tier word after its level (P4: "Lv 50 · Story needs it · …"), when the hint opens with
    /// the level and the inputs know the tier; otherwise the hint as it is.
    /// </summary>
    public static string WithTier(TodoInputs inputs, QuestRecord quest, string hint)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(hint);
        if (inputs.TierOf?.Invoke(quest) is not { } tier)
        {
            return hint;
        }

        var level = string.Format(System.Globalization.CultureInfo.InvariantCulture, LevelFormat, quest.DisplayLevel);
        if (!hint.StartsWith(level, StringComparison.Ordinal) || (hint.Length > level.Length && !hint.AsSpan(level.Length).StartsWith(Separator, StringComparison.Ordinal)))
        {
            return hint;
        }

        return level + Separator + UnlockTiers.Name(tier) + hint[level.Length..];
    }

    private static QuestState StateOf(IReadOnlyDictionary<uint, QuestEvaluation> states, uint rowId) =>
        states.TryGetValue(rowId, out var evaluation) ? evaluation.State : QuestState.Unknown;

    private static TodoRow Row(TodoInputs inputs, QuestRecord quest, QuestState state, TodoRowKind kind) =>
        new(quest.RowId, QuestName(inputs, quest), state, Hint(inputs, quest, state), kind);

    /// <summary>
    /// The row's hint: the decisive blocker (<see cref="BlockerText.For"/>) when the quest is blocked or not checked,
    /// "Ready on JOB" when another job can take it, "step 3 of 7" when accepted, otherwise the level and the giver's
    /// name. The state name itself is never in the hint (the moon and the tooltip carry it), so a state with nothing
    /// to add gets the display name as a fallback only when no evaluation exists.
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
                    return string.Format(System.Globalization.CultureInfo.CurrentCulture, ReadyOnJobFormat, jobName);
                }

                return StateNames.ReadyOnOtherJob;

            case QuestState.Accepted:
                return evaluation is null ? string.Empty : BlockerText.StepText(evaluation.Sequence, quest.StepCount);

            case QuestState.Blocked:
            case QuestState.Unknown:
                if (evaluation is null)
                {
                    return StateNames.Name(state, quest);
                }

                var blocker = BlockerText.For(evaluation, quest, inputs.Names ?? BlockerNames.Default with { Catalog = inputs.Catalog }, inputs.States);
                return blocker.Length > 0 ? blocker : StateNames.Name(state, quest);

            default:
                return StateNames.Name(state, quest);
        }
    }

    private static string LevelAndGiver(QuestRecord quest)
    {
        var level = string.Format(System.Globalization.CultureInfo.InvariantCulture, LevelFormat, quest.DisplayLevel);
        return quest.Issuer is { Name.Length: > 0 } issuer ? level + Separator + issuer.Name : level;
    }
}
