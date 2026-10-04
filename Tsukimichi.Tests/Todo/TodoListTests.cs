using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Seasonal;
using Tsukimichi.Core.Todo;
using Tsukimichi.GameData;

namespace Tsukimichi.Tests.Todo;

public sealed class TodoListTests
{
    // Territories.
    private const uint Gridania = 132;
    private const uint Limsa = 129;

    // Jobs and categories (ClassJob row ids as in the sheet).
    private const byte Gladiator = 1;
    private const byte Paladin = 19;
    private const uint AllJobs = 1;
    private const uint GladiatorOnly = 2;
    private const uint PaladinOnly = 20;
    private const uint Tanks = 156;

    // Main scenario (section 0, journal order by row id).
    private const uint MsqOne = 65600;
    private const uint MsqTwo = 65601;
    private const uint MsqBranchOther = 65602;

    // Feature quests in Gridania, an ordinary side quest there and a feature quest elsewhere.
    private const uint FeatureA = 65610;
    private const uint FeatureB = 65611;
    private const uint SideQuest = 65612;
    private const uint FeatureFar = 65613;
    private const uint FeatureBlocked = 65614;

    // Job ladder: two gladiator quests, the paladin unlock, one paladin quest; one tank role quest.
    private const uint GladiatorOne = 65620;
    private const uint GladiatorTwo = 65621;
    private const uint Pledge = 65622;
    private const uint PaladinOne = 65623;
    private const uint TankRole = 65624;

    private static readonly LadderJob[] Jobs =
    [
        new(Gladiator, "gladiator", Gladiator, 0, 1, false, false),
        new(Paladin, "paladin", Gladiator, Pledge, 1, false, false),
    ];

    private static readonly ClassJobCategoryLookup Categories = ClassJobCategoryLookup.FromMembership(
    [
        new(AllJobs, [Gladiator, Paladin]),
        new(GladiatorOnly, [Gladiator]),
        new(PaladinOnly, [Paladin]),
        new(Tanks, [Paladin]),
    ]);

    private static readonly Dictionary<uint, string> JobNames = new() { [Gladiator] = "GLA", [Paladin] = "PLD" };

    private static QuestRecord Quest(uint rowId, string name, byte level, uint section, uint territory = 0, string giver = "", uint required = 0, uint category = 0, string genreName = "Genre", string categoryName = "Category", byte eventIcon = 0) => new()
    {
        RowId = rowId,
        QuestId = QuestRecord.ToQuestId(rowId),
        InternalId = $"Test_{rowId}",
        Name = name,
        Journal = new JournalRef(section, "Section", section * 10 + 1, categoryName, section * 100 + 1, genreName, (int)rowId),
        Level = level,
        ClassJobRequired = required,
        ClassJobCategory = category,
        EventIconType = eventIcon,
        Issuer = territory == 0 ? null : new Issuer(1000 + rowId, giver, territory, 1, 0f, 0f, 0f),
    };

    private static readonly QuestCatalog Catalog = QuestCatalog.Build(
    [
        Quest(MsqOne, "Coming to Gridania", 1, 0, Gridania, "Mother Miounne"),
        Quest(MsqTwo, "Close to Home", 1, 0, Gridania, "Mother Miounne"),
        Quest(MsqBranchOther, "Close to Home (Limsa)", 1, 0, Limsa, "Baderon"),
        Quest(FeatureA, "Zephyr", 15, 3, Gridania, "Nedrick", eventIcon: FeaturePresets.FeatureEventIconType),
        Quest(FeatureB, "Aurora", 10, 3, Gridania, "Bertennant", eventIcon: FeaturePresets.FeatureEventIconType),
        Quest(SideQuest, "Bramble", 5, 3, Gridania, "Leonnie"),
        Quest(FeatureFar, "Distant", 1, 3, Limsa, "Baderon", eventIcon: FeaturePresets.FeatureEventIconType),
        Quest(FeatureBlocked, "Corundum", 50, 3, Gridania, "Someone", eventIcon: FeaturePresets.FeatureEventIconType),
        Quest(GladiatorOne, "Way of the Gladiator", 1, JobLadder.ClassJobSectionId, Gridania, "Lulutsu", required: Gladiator, category: GladiatorOnly, genreName: "Gladiator Quests"),
        Quest(GladiatorTwo, "The Lone Sword", 15, JobLadder.ClassJobSectionId, Gridania, "Mylla", required: Gladiator, category: GladiatorOnly, genreName: "Gladiator Quests"),
        Quest(Pledge, "Paladin's Pledge", 30, JobLadder.ClassJobSectionId, Gridania, "Jenlyns", required: Gladiator, category: GladiatorOnly, genreName: "Paladin Quests"),
        Quest(PaladinOne, "Paladin's Resolve", 35, JobLadder.ClassJobSectionId, Gridania, "Jenlyns", required: Paladin, category: PaladinOnly, genreName: "Paladin Quests"),
        Quest(TankRole, "Tank Role Quest", 70, JobLadder.ClassJobSectionId, Gridania, "Granson", category: Tanks, genreName: "Tank Role Quests", categoryName: "Role Quests"),
    ]);

    private static readonly JobLadder Ladder = JobLadder.Build(Catalog, Jobs, Categories);

    private static readonly HashSet<uint> FeatureIds = [FeatureA, FeatureB, FeatureFar, FeatureBlocked];

    private static QuestEvaluation Eval(QuestState state, string? nextStep = null, byte? readyOn = null, byte? sequence = null)
    {
        RequirementResult? next = nextStep is null ? null : new RequirementResult(new LevelRequirement(50, 1), false, nextStep);
        return new QuestEvaluation(state, next is null ? [] : [next], next, readyOn, sequence);
    }

    /// <summary>Every catalog quest Blocked unless overridden; MSQ one done, two next; the feature quests in Gridania Ready.</summary>
    private static Dictionary<uint, QuestEvaluation> States(params (uint RowId, QuestEvaluation Eval)[] overrides)
    {
        var states = new Dictionary<uint, QuestEvaluation>();
        foreach (var quest in Catalog.All)
        {
            states[quest.RowId] = Eval(QuestState.Blocked, "Level 50 (you are 1)");
        }

        states[MsqOne] = Eval(QuestState.Completed);
        states[MsqTwo] = Eval(QuestState.Ready);
        states[MsqBranchOther] = Eval(QuestState.Foreclosed);
        states[FeatureA] = Eval(QuestState.Ready);
        states[FeatureB] = Eval(QuestState.ReadyOnOtherJob, readyOn: Paladin);
        states[SideQuest] = Eval(QuestState.Ready);
        states[FeatureFar] = Eval(QuestState.Ready);
        states[GladiatorOne] = Eval(QuestState.Ready);
        foreach (var (rowId, eval) in overrides)
        {
            states[rowId] = eval;
        }

        return states;
    }

    private static TodoInputs Inputs(
        Dictionary<uint, QuestEvaluation>? states = null,
        IReadOnlyList<uint>? pinned = null,
        uint territory = Gridania,
        byte job = Gladiator,
        short level = 1,
        bool pins = true,
        bool nearby = true,
        bool msq = true,
        bool jobs = true) =>
        new(Catalog, states ?? States(), pinned ?? [], FeatureIds, territory, job, new Dictionary<byte, short> { [job] = level }, Ladder, JobNames, pins, nearby, msq, jobs, Names);

    private static readonly BlockerNames Names = new()
    {
        Catalog = Catalog,
        JobAbbreviation = id => JobNames.GetValueOrDefault(id, string.Empty),
    };

    private static TodoSectionModel? Section(TodoModel model, TodoSection section)
    {
        foreach (var candidate in model.Sections)
        {
            if (candidate.Section == section)
            {
                return candidate;
            }
        }

        return null;
    }

    [Fact]
    public void Pinned_lists_todo_pins_in_pin_order_and_leaves_out_completed_and_foreclosed()
    {
        var states = States(
            (SideQuest, Eval(QuestState.Completed)),
            (FeatureFar, Eval(QuestState.Accepted, sequence: 2)),
            (MsqBranchOther, Eval(QuestState.Foreclosed)));
        uint[] pinned = [FeatureBlocked, FeatureA, FeatureFar, SideQuest, MsqBranchOther, FeatureB, 99];

        var section = Section(TodoList.Build(Inputs(states, pinned)), TodoSection.Pinned);

        Assert.NotNull(section);
        Assert.Equal(["Corundum", "Zephyr", "Distant", "Aurora"], section.Rows.Select(r => r.Name));
        Assert.All(section.Rows, r => Assert.Equal(TodoRowKind.Pin, r.Kind));
        Assert.Equal("Lv 50", section.Rows[0].Hint);
        Assert.Equal("Lv 15 · Nedrick", section.Rows[1].Hint);
        Assert.Equal("step 2", section.Rows[2].Hint);
        Assert.Equal("Ready on PLD", section.Rows[3].Hint);
        Assert.Equal(0, section.More);
    }

    [Fact]
    public void Hints_are_the_blocker_line_and_the_step_without_the_state_name()
    {
        // The moon carries the state, so the hint is what follows it in the Status column: "step 3 of 7" for a quest in
        // the journal, the decisive blocker ("Lv 50 on GLA") for a blocked one.
        var accepted = Catalog.GetByRowId(FeatureA)! with { StepCount = 7 };
        var inputs = Inputs(States(
            (FeatureA, Eval(QuestState.Accepted, sequence: 3)),
            (GladiatorTwo, Eval(QuestState.Blocked, "Level 50 (you are 1)"))));

        Assert.Equal("step 3 of 7", TodoList.Hint(inputs, accepted, QuestState.Accepted));
        Assert.Equal("Lv 50 on GLA", TodoList.Hint(inputs, Catalog.GetByRowId(GladiatorTwo)!, QuestState.Blocked));
    }

    [Fact]
    public void Pinned_keeps_the_order_quests_were_pinned_not_level_or_name()
    {
        // "Pin all" on a route appends its steps in route order; the section must not re-sort them by level or name.
        var states = States((FeatureB, Eval(QuestState.Ready)), (FeatureFar, Eval(QuestState.Ready)));
        uint[] pinned = [FeatureA, FeatureB, FeatureFar, SideQuest];
        uint[] reversed = [SideQuest, FeatureFar, FeatureB, FeatureA];

        var section = Section(TodoList.Build(Inputs(states, pinned)), TodoSection.Pinned)!;
        var other = Section(TodoList.Build(Inputs(states, reversed)), TodoSection.Pinned)!;

        Assert.Equal(["Zephyr", "Aurora", "Distant", "Bramble"], section.Rows.Select(r => r.Name));
        Assert.Equal(["Bramble", "Distant", "Aurora", "Zephyr"], other.Rows.Select(r => r.Name));
    }

    [Fact]
    public void Pinned_lists_a_repeated_row_id_once()
    {
        uint[] pinned = [FeatureA, FeatureB, FeatureA];

        var section = Section(TodoList.Build(Inputs(pinned: pinned)), TodoSection.Pinned)!;

        Assert.Equal([FeatureA, FeatureB], section.Rows.Select(r => r.RowId));
        Assert.Equal(0, section.More);
    }

    [Fact]
    public void Pinned_shows_the_first_pins_up_to_the_cap_and_counts_the_rest()
    {
        // A long route pinned whole: the overlay lists the first MaxPinned steps still to do, the rest are "+N more".
        var quests = new List<QuestRecord>();
        var states = new Dictionary<uint, QuestEvaluation>();
        var pinned = new List<uint>();
        for (uint i = 0; i < 60; i++)
        {
            var rowId = 70000 + i;
            quests.Add(Quest(rowId, $"Step {i:00}", (byte)(60 - i), 3, Gridania, "Giver"));
            states[rowId] = Eval(i < 5 ? QuestState.Completed : QuestState.Blocked, "Level 50 (you are 1)");
            pinned.Add(rowId);
        }

        var catalog = QuestCatalog.Build(quests);
        var inputs = new TodoInputs(catalog, states, pinned, new HashSet<uint>(), 0, 0, new Dictionary<byte, short>(), JobLadder.Empty, JobNames);

        var section = Section(TodoList.Build(inputs), TodoSection.Pinned)!;

        Assert.Equal(TodoList.MaxPinned, section.Rows.Count);
        Assert.Equal(Enumerable.Range(5, TodoList.MaxPinned).Select(i => 70000u + (uint)i), section.Rows.Select(r => r.RowId));
        Assert.Equal(55 - TodoList.MaxPinned, section.More);

        var uncapped = Section(TodoList.Build(inputs with { PinLimit = int.MaxValue }), TodoSection.Pinned)!;
        Assert.Equal(55, uncapped.Rows.Count);
        Assert.Equal(0, uncapped.More);
    }

    [Fact]
    public void Pinned_counts_more_only_past_the_limit()
    {
        uint[] pinned = [FeatureA, FeatureB];

        var section = Section(TodoList.Build(Inputs(pinned: pinned) with { PinLimit = 1 }), TodoSection.Pinned)!;

        Assert.Equal([FeatureA], section.Rows.Select(r => r.RowId));
        Assert.Equal(1, section.More);
        Assert.Equal(0, Section(TodoList.Build(Inputs(pinned: pinned) with { PinLimit = 2 }), TodoSection.Pinned)!.More);
    }

    [Fact]
    public void Nearby_keeps_only_feature_quests_startable_in_the_territory()
    {
        var section = Section(TodoList.Build(Inputs()), TodoSection.NearbyFeature);

        Assert.NotNull(section);
        Assert.Equal(["Aurora", "Zephyr"], section.Rows.Select(r => r.Name));
        Assert.All(section.Rows, r => Assert.Equal(TodoRowKind.NearbyFeature, r.Kind));
        Assert.Equal(QuestState.ReadyOnOtherJob, section.Rows[0].State);
    }

    [Fact]
    public void Nearby_is_empty_without_a_territory_or_in_a_zone_with_nothing()
    {
        Assert.Null(Section(TodoList.Build(Inputs(territory: 0)), TodoSection.NearbyFeature));
        Assert.Null(Section(TodoList.Build(Inputs(territory: 9999)), TodoSection.NearbyFeature));
    }

    [Fact]
    public void Nearby_is_capped_at_eight()
    {
        var quests = new List<QuestRecord>();
        var feature = new HashSet<uint>();
        var states = new Dictionary<uint, QuestEvaluation>();
        for (uint i = 0; i < 12; i++)
        {
            var rowId = 65700 + i;
            quests.Add(Quest(rowId, $"Feature {i:00}", (byte)(i + 1), 3, Gridania, "Giver", eventIcon: FeaturePresets.FeatureEventIconType));
            feature.Add(rowId);
            states[rowId] = Eval(QuestState.Ready);
        }

        var catalog = QuestCatalog.Build(quests);
        var inputs = new TodoInputs(catalog, states, [], feature, Gridania, 0, new Dictionary<byte, short>(), JobLadder.Empty, JobNames);

        var section = Section(TodoList.Build(inputs), TodoSection.NearbyFeature)!;

        Assert.Equal(TodoList.MaxNearby, section.Rows.Count);
        Assert.Equal("Feature 00", section.Rows[0].Name);
        Assert.Equal("Feature 07", section.Rows[^1].Name);
    }

    [Fact]
    public void Msq_lists_the_next_main_scenario_quest_with_its_state_and_blocker()
    {
        var ready = Section(TodoList.Build(Inputs()), TodoSection.Msq)!;
        Assert.Single(ready.Rows);
        Assert.Equal(MsqTwo, ready.Rows[0].RowId);
        Assert.Equal(QuestState.Ready, ready.Rows[0].State);
        Assert.Equal(TodoRowKind.Msq, ready.Rows[0].Kind);
        Assert.Equal("Lv 1 · Mother Miounne", ready.Rows[0].Hint);

        var blocked = Section(TodoList.Build(Inputs(States((MsqTwo, Eval(QuestState.Blocked, "Complete Coming to Gridania"))))), TodoSection.Msq)!;
        Assert.Equal(QuestState.Blocked, blocked.Rows[0].State);
        Assert.Equal("Lv 50", blocked.Rows[0].Hint);
    }

    [Fact]
    public void Msq_section_is_absent_once_the_main_scenario_is_complete_or_without_states()
    {
        var done = States((MsqTwo, Eval(QuestState.Completed)));
        Assert.Null(Section(TodoList.Build(Inputs(done)), TodoSection.Msq));

        var empty = TodoList.Build(Inputs(new Dictionary<uint, QuestEvaluation>()));
        Assert.Null(Section(empty, TodoSection.Msq));
    }

    [Fact]
    public void Job_quests_lists_the_ladders_next_quest_when_it_is_open_now()
    {
        var section = Section(TodoList.Build(Inputs(job: Gladiator, level: 1)), TodoSection.JobQuests);

        Assert.NotNull(section);
        var row = Assert.Single(section.Rows);
        Assert.Equal(GladiatorOne, row.RowId);
        Assert.Equal(TodoRowKind.JobQuest, row.Kind);
        Assert.Equal("Lv 1 · Lulutsu", row.Hint);
    }

    [Fact]
    public void Job_quests_is_absent_when_the_next_quest_is_not_open_yet()
    {
        var states = States((GladiatorOne, Eval(QuestState.Completed)), (GladiatorTwo, Eval(QuestState.Blocked, "Level 15 (you are 10)")));

        Assert.Null(Section(TodoList.Build(Inputs(states, job: Gladiator, level: 10)), TodoSection.JobQuests));
    }

    [Fact]
    public void Job_quests_adds_the_role_quest_when_it_is_open_and_lists_a_quest_once()
    {
        var states = States(
            (GladiatorOne, Eval(QuestState.Completed)),
            (GladiatorTwo, Eval(QuestState.Completed)),
            (Pledge, Eval(QuestState.Completed)),
            (PaladinOne, Eval(QuestState.Accepted, sequence: 1)),
            (TankRole, Eval(QuestState.Ready)));

        var section = Section(TodoList.Build(Inputs(states, job: Paladin, level: 70)), TodoSection.JobQuests)!;

        Assert.Equal([PaladinOne, TankRole], section.Rows.Select(r => r.RowId));
        Assert.Equal([TodoRowKind.JobQuest, TodoRowKind.RoleQuest], section.Rows.Select(r => r.Kind));
        Assert.Equal("step 1", section.Rows[0].Hint);
    }

    [Fact]
    public void Job_quests_is_absent_without_a_current_job()
    {
        Assert.Null(Section(TodoList.Build(Inputs(job: 0)), TodoSection.JobQuests));
    }

    [Fact]
    public void Disabled_sections_are_left_out_and_counted_as_not_enabled()
    {
        uint[] pinned = [FeatureA];

        var all = TodoList.Build(Inputs(pinned: pinned));
        Assert.Equal([TodoSection.Pinned, TodoSection.NearbyFeature, TodoSection.Msq, TodoSection.JobQuests], all.Sections.Select(s => s.Section));
        Assert.Equal(4, all.EnabledSections);
        Assert.Equal(5, all.Count);

        var some = TodoList.Build(Inputs(pinned: pinned, pins: false, nearby: false));
        Assert.Equal([TodoSection.Msq, TodoSection.JobQuests], some.Sections.Select(s => s.Section));
        Assert.Equal(2, some.EnabledSections);

        var none = TodoList.Build(Inputs(pinned: pinned, pins: false, nearby: false, msq: false, jobs: false));
        Assert.True(none.IsEmpty);
        Assert.Equal(0, none.EnabledSections);
        Assert.Same(TodoModel.Empty, none);
    }

    [Fact]
    public void Enabled_sections_with_nothing_to_show_give_an_empty_model_that_is_not_the_disabled_one()
    {
        var states = States((MsqTwo, Eval(QuestState.Completed)), (GladiatorOne, Eval(QuestState.Completed)));
        var model = TodoList.Build(Inputs(states, territory: 9999, job: 0));

        Assert.Empty(model.Sections);
        Assert.True(model.IsEmpty);
        Assert.Equal(4, model.EnabledSections);
    }

    [Fact]
    public void Hint_falls_back_to_generic_phrases_without_evaluator_detail()
    {
        var quest = Catalog.GetByRowId(FeatureA)!;
        var inputs = Inputs(new Dictionary<uint, QuestEvaluation>
        {
            [FeatureA] = Eval(QuestState.ReadyOnOtherJob, readyOn: 77),
        });

        Assert.Equal("Ready on another job", TodoList.Hint(inputs, quest, QuestState.ReadyOnOtherJob));
        Assert.Equal(string.Empty, TodoList.Hint(inputs, quest, QuestState.Accepted));
        Assert.Equal("Blocked", TodoList.Hint(inputs, quest, QuestState.Blocked));
        Assert.Equal("Not checked", TodoList.Hint(inputs, quest, QuestState.Unknown));
        Assert.Equal("Lv 15 · Nedrick", TodoList.Hint(inputs, quest, QuestState.Ready));
    }

    [Fact]
    public void Hint_prints_the_displayed_level()
    {
        var quest = Catalog.GetByRowId(FeatureA)! with { LevelOffset = 2 };

        Assert.Equal("Lv 17 · Nedrick", TodoList.Hint(Inputs(), quest, QuestState.Ready));
    }

    // ---- Event quests running now (P11) ----

    private static readonly DateTime Now = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);

    private static RunningFestival Festival(ushort id, string name, DateTime? end, params (uint RowId, QuestState State)[] quests) =>
        new(id, name, quests.Select(q => new SeasonalQuest(Catalog.GetByRowId(q.RowId)!, q.State)).ToList(), quests.Count(q => q.State == QuestState.Ready), end, end is null ? null : "https://na.finalfantasyxiv.com/lodestone/");

    private static TodoInputs SeasonalInputs(IReadOnlyList<RunningFestival>? running, Dictionary<uint, QuestEvaluation>? states = null, bool seasonal = true) =>
        Inputs(states) with { Running = running, ShowSeasonal = seasonal, NowUtc = Now, TimeZone = TimeZoneInfo.Utc };

    [Fact]
    public void Seasonal_lists_the_ready_and_in_journal_event_quests_between_pins_and_unlocks_with_their_giver()
    {
        var states = States((FeatureFar, Eval(QuestState.Accepted, sequence: 2)));
        var moonfire = Festival(174, "Moonfire Faire", new DateTime(2026, 8, 28, 23, 59, 59, DateTimeKind.Utc),
            (SideQuest, QuestState.Ready), (FeatureFar, QuestState.Accepted), (FeatureBlocked, QuestState.Blocked), (MsqOne, QuestState.Completed));

        var model = TodoList.Build(SeasonalInputs([moonfire], states) with { Pinned = [FeatureA] });

        Assert.Equal([TodoSection.Pinned, TodoSection.Seasonal, TodoSection.NearbyFeature, TodoSection.Msq, TodoSection.JobQuests], model.Sections.Select(s => s.Section));
        Assert.Equal(5, model.EnabledSections);
        var section = Section(model, TodoSection.Seasonal)!;
        Assert.Equal([SideQuest, FeatureFar], section.Rows.Select(r => r.RowId));
        Assert.All(section.Rows, r => Assert.Equal(TodoRowKind.Seasonal, r.Kind));
        Assert.Equal("Lv 5 · Leonnie", section.Rows[0].Hint);
        Assert.Equal("step 2 · Baderon", section.Rows[1].Hint);
        Assert.Equal(["Ends Aug 28 (Lodestone)"], section.Notes);
    }

    [Fact]
    public void An_ending_event_s_quests_in_the_journal_lead_the_seasonal_section()
    {
        // 1.19.0, C10: the game takes them out of the journal when the event ends.
        var end = new DateTime(2026, 8, 22, 14, 59, 0, DateTimeKind.Utc);
        var ready = new SeasonalQuest(Catalog.GetByRowId(SideQuest)! with { Festival = 174 }, QuestState.Ready);
        var accepted = new SeasonalQuest(Catalog.GetByRowId(FeatureFar)! with { Festival = 174 }, QuestState.Accepted);
        var moonfire = new RunningFestival(174, "Moonfire Faire", [ready, accepted], 1, end, "https://na.finalfantasyxiv.com/lodestone/");
        var soon = EventWarnings.EndingSoon([moonfire], Now, 3, TimeZoneInfo.Utc);

        Assert.Equal([SideQuest, FeatureFar], Section(TodoList.Build(SeasonalInputs([moonfire])), TodoSection.Seasonal)!.Rows.Select(r => r.RowId));
        Assert.Equal([FeatureFar, SideQuest], Section(TodoList.Build(SeasonalInputs([moonfire]) with { EndingSoon = soon }), TodoSection.Seasonal)!.Rows.Select(r => r.RowId));
    }

    [Fact]
    public void Spare_alternatives_are_not_todos_among_pins_nearby_quests_or_event_quests()
    {
        // An open choice's options other than the presumed one leave the counts, so no list offers them as a to-do.
        var spare = Eval(QuestState.Ready) with { IsSpareAlternative = true, ChoiceOf = 3 };
        var states = States((FeatureA, spare), (SideQuest, spare));
        var moonfire = new RunningFestival(174, "Moonfire Faire",
            [new SeasonalQuest(Catalog.GetByRowId(SideQuest)!, QuestState.Ready) { IsSpareAlternative = true }, new SeasonalQuest(Catalog.GetByRowId(FeatureFar)!, QuestState.Ready)],
            1, null, null);

        var model = TodoList.Build(SeasonalInputs([moonfire], states) with { Pinned = [FeatureA, FeatureB] });

        Assert.Equal([FeatureB], Section(model, TodoSection.Pinned)!.Rows.Select(r => r.RowId));
        Assert.Equal([FeatureB], Section(model, TodoSection.NearbyFeature)!.Rows.Select(r => r.RowId));
        Assert.Equal([FeatureFar], Section(model, TodoSection.Seasonal)!.Rows.Select(r => r.RowId));
        Assert.False(TodoList.IsTodo(spare));
        Assert.True(TodoList.IsTodo((QuestEvaluation?)null));
    }

    [Fact]
    public void Seasonal_shows_no_end_line_without_an_announced_end_and_names_events_when_several_list_rows()
    {
        var undated = Festival(84, "A Nocturne for Heroes", null, (SideQuest, QuestState.Ready));
        var single = Section(TodoList.Build(SeasonalInputs([undated])), TodoSection.Seasonal)!;
        Assert.Empty(single.Notes);

        var dated = Festival(174, "Moonfire Faire", new DateTime(2026, 8, 28, 23, 59, 59, DateTimeKind.Utc), (FeatureFar, QuestState.Ready));
        var both = Section(TodoList.Build(SeasonalInputs([undated, dated])), TodoSection.Seasonal)!;
        Assert.Equal([SideQuest, FeatureFar], both.Rows.Select(r => r.RowId));
        Assert.Equal(["Moonfire Faire: ends Aug 28 (Lodestone)"], both.Notes);
    }

    [Fact]
    public void Seasonal_is_absent_when_nothing_is_actionable_disabled_or_not_supplied()
    {
        var done = Festival(174, "Moonfire Faire", null, (SideQuest, QuestState.Completed), (FeatureFar, QuestState.Blocked));
        var nothing = TodoList.Build(SeasonalInputs([done]));
        Assert.Null(Section(nothing, TodoSection.Seasonal));
        Assert.Equal(5, nothing.EnabledSections);

        var ready = Festival(174, "Moonfire Faire", null, (SideQuest, QuestState.Ready));
        var off = TodoList.Build(SeasonalInputs([ready], seasonal: false));
        Assert.Null(Section(off, TodoSection.Seasonal));
        Assert.Equal(4, off.EnabledSections);

        // A caller that does not supply the running events (null) keeps the four sections it had.
        Assert.Equal(4, TodoList.Build(SeasonalInputs(null)).EnabledSections);
    }
}
