using Tsukimichi.Core.Diagnostics;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Ipc;
using Tsukimichi.Core.Model;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Ipc;

/// <summary>
/// <see cref="IpcView"/>, the payload shaping behind Tsukimichi's IPC gates (docs/ipc.md): which quest an id names in
/// either id space, the not-ready answers, the state and blocker payloads, the main scenario position and the change
/// test that drives <c>Tsukimichi.StatesChanged</c>.
/// </summary>
public class IpcViewTests
{
    private const uint Msq1 = 65800;
    private const uint Msq2 = 65801;
    private const uint Msq3 = 65802;

    private static readonly BlockerNames Names = new()
    {
        JobAbbreviation = id => id switch { Gladiator => "GLA", Conjurer => "CNJ", Paladin => "PLD", _ => string.Empty },
    };

    private static QuestRecord Side(uint rowId, string? name = null) =>
        Quest(rowId, name) with { Journal = new JournalRef(3, "Side Quests", 1, "Category", 1, "Genre", (int)rowId) };

    private static QuestRecord Msq(uint rowId) =>
        Quest(rowId, $"Story {rowId}") with { Journal = new JournalRef(0, "Main Scenario", 1, "Seventh Umbral Era", 1, "Seventh Umbral Era", (int)rowId), Level = 1 };

    private static IpcView View(CharacterSnapshot snapshot, BlockerNames? names = null, params QuestRecord[] quests)
    {
        var catalog = Catalog(quests);
        return new IpcView(catalog, StateResolver.ResolveAll(catalog, snapshot, EvalContext.Default), names ?? Names);
    }

    [Fact]
    public void Channel_names_and_the_api_version_are_the_documented_ones()
    {
        Assert.Equal(1, IpcChannels.ApiVersion);
        Assert.Equal("Tsukimichi.ApiVersion", IpcChannels.ApiVersionGate);
        Assert.Equal("Tsukimichi.IsReady", IpcChannels.IsReadyGate);
        Assert.Equal("Tsukimichi.IsQuestAvailable", IpcChannels.IsQuestAvailableGate);
        Assert.Equal("Tsukimichi.GetState", IpcChannels.GetStateGate);
        Assert.Equal("Tsukimichi.GetStateName", IpcChannels.GetStateNameGate);
        Assert.Equal("Tsukimichi.GetBlockers", IpcChannels.GetBlockersGate);
        Assert.Equal("Tsukimichi.GetMsqPosition", IpcChannels.GetMsqPositionGate);
        Assert.Equal("Tsukimichi.GetMsqPositions", IpcChannels.GetMsqPositionsGate);
        Assert.Equal("Tsukimichi.OpenQuest", IpcChannels.OpenQuestGate);
        Assert.Equal("Tsukimichi.StatesChanged", IpcChannels.StatesChangedGate);
    }

    [Fact]
    public void A_row_id_and_its_quest_id_name_the_same_quest()
    {
        var catalog = Catalog(Side(A), Side(Target));

        Assert.Same(catalog.GetByRowId(Target), IpcView.FindQuest(catalog, Target));
        Assert.Same(catalog.GetByRowId(Target), IpcView.FindQuest(catalog, QuestRecord.ToQuestId(Target)));
        Assert.Same(catalog.GetByRowId(A), IpcView.FindQuest(catalog, A - IpcView.FirstRowId));
    }

    [Fact]
    public void Zero_unknown_and_out_of_range_ids_name_no_quest()
    {
        var catalog = Catalog(Side(Target));

        Assert.Null(IpcView.FindQuest(catalog, 0));
        Assert.Null(IpcView.FindQuest(catalog, A));
        Assert.Null(IpcView.FindQuest(catalog, QuestRecord.ToQuestId(A)));
        Assert.Null(IpcView.FindQuest(catalog, IpcView.FirstRowId));
        // A row id past the sheet is not folded into its low 16 bits: 0x20000 + Target's quest id is not Target.
        Assert.Null(IpcView.FindQuest(catalog, 0x20000u + QuestRecord.ToQuestId(Target)));
        Assert.Null(IpcView.FindQuest(catalog, uint.MaxValue));
        Assert.Null(IpcView.FindQuest(null, Target));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(Target)]
    [InlineData(uint.MaxValue)]
    public void Before_the_catalog_every_answer_is_the_not_ready_one(uint id)
    {
        var view = IpcView.Empty;

        Assert.False(view.IsReady);
        Assert.Null(view.Find(id));
        Assert.False(view.IsQuestAvailable(id));
        Assert.Equal(string.Empty, view.State(id));
        Assert.Equal(string.Empty, view.StateName(id));
        Assert.Empty(view.Blockers(id));
        Assert.Equal(0u, view.MsqNext());
    }

    [Fact]
    public void Before_a_character_is_evaluated_quests_are_found_but_nothing_is_answered()
    {
        var catalog = Catalog(Side(Target), Msq(Msq1));
        var view = new IpcView(catalog, null, Names);

        Assert.False(view.IsReady);
        Assert.NotNull(view.Find(Target));
        Assert.False(view.IsQuestAvailable(Target));
        Assert.Equal(string.Empty, view.State(Target));
        Assert.Equal(string.Empty, view.StateName(Target));
        Assert.Empty(view.Blockers(Target));
        Assert.Equal(0u, view.MsqNext());

        var empty = new IpcView(catalog, new Dictionary<uint, QuestEvaluation>(), Names);
        Assert.False(empty.IsReady);
        Assert.Empty(empty.Blockers(Target));
    }

    [Fact]
    public void States_without_a_catalog_are_dropped()
    {
        var catalog = Catalog(Side(Target));
        var states = StateResolver.ResolveAll(catalog, Snapshot(), EvalContext.Default);

        var view = new IpcView(null, states, Names);

        Assert.False(view.IsReady);
        Assert.Empty(view.States);
        Assert.Equal(string.Empty, view.State(Target));
    }

    [Fact]
    public void Available_means_Ready_or_Ready_on_another_job()
    {
        var view = View(
            Snapshot(A) with { JobLevels = Levels((Gladiator, 50), (Conjurer, 60)) },
            null,
            Side(A),
            Side(B),
            Side(C) with { Level = 60 },
            Side(D) with { Level = 70 },
            Side(Target) with { PreviousQuests = new Prereq([B], JoinKind.All) });

        Assert.True(view.IsReady);
        Assert.True(view.IsQuestAvailable(B));
        Assert.True(view.IsQuestAvailable(QuestRecord.ToQuestId(B)));
        Assert.Equal("ReadyOnOtherJob", view.State(C));
        Assert.True(view.IsQuestAvailable(C));
        Assert.False(view.IsQuestAvailable(A));
        Assert.False(view.IsQuestAvailable(D));
        Assert.False(view.IsQuestAvailable(Target));
        Assert.False(view.IsQuestAvailable(E));
    }

    [Fact]
    public void GetState_is_the_enum_name_and_GetStateName_the_display_name()
    {
        var daily = Side(B) with { IsRepeatable = true, RepeatInterval = 1 };
        var snapshot = Snapshot(A) with
        {
            Accepted = [Accepted(C, sequence: 2)],
            DailyDone = new Dictionary<ushort, byte> { [daily.QuestId] = 1 },
        };
        var view = View(snapshot, null, Side(A), daily, Side(C) with { StepCount = 4 }, Side(Target) with { Level = 80 });

        Assert.Equal("Completed", view.State(A));
        Assert.Equal("Completed", view.StateName(A));
        Assert.Equal("DoneThisCycle", view.State(B));
        Assert.Equal("Done today", view.StateName(B));
        Assert.Equal("Accepted", view.State(C));
        Assert.Equal("In journal", view.StateName(C));
        Assert.Equal("Blocked", view.State(Target));
        Assert.Equal("Blocked", view.StateName(QuestRecord.ToQuestId(Target)));
    }

    [Fact]
    public void Blockers_open_with_the_status_line_and_list_every_requirement_in_the_diagnostic_format()
    {
        var target = Side(Target) with { Level = 80, PreviousQuests = new Prereq([A], JoinKind.All) };
        var view = View(Snapshot(), null, Side(A, "Peace for Thanalan"), target);

        var lines = view.Blockers(Target);

        Assert.True(view.TryEvaluate(Target, out var quest, out var evaluation));
        Assert.Equal(BlockerText.StatusText(evaluation, quest, view.Names, view.States), lines[0]);
        Assert.Equal("Blocked · after: Peace for Thanalan", lines[0]);
        Assert.Equal(1 + evaluation.Requirements.Count, lines.Length);
        Assert.Equal(evaluation.Requirements.Select(r => QuestDiagnostic.RequirementLine(r, view.Names)), lines.Skip(1));
        Assert.Contains("Level: unmet (80 > 50)", lines);
        Assert.Contains($"PreviousQuests: unmet ({A} Peace for Thanalan: not done)", lines);
        Assert.Equal(lines, view.Blockers(QuestRecord.ToQuestId(Target)));
    }

    [Fact]
    public void A_ready_quest_has_its_status_alone_then_its_met_requirements()
    {
        var view = View(Snapshot(), null, Side(Target) with { Level = 30 });

        var lines = view.Blockers(Target);

        Assert.Equal("Ready", lines[0]);
        Assert.Contains("Level: met (30 ≤ 50)", lines);
    }

    [Fact]
    public void Ready_on_another_job_names_the_job_after_the_status_as_the_diagnostic_block_does()
    {
        var view = View(Snapshot() with { JobLevels = Levels((Gladiator, 50), (Conjurer, 60)) }, null, Side(Target) with { Level = 60 });

        Assert.Equal("Ready on another job (CNJ)", view.Blockers(Target)[0]);

        var unnamed = View(Snapshot() with { JobLevels = Levels((Gladiator, 50), (Conjurer, 60)) }, BlockerNames.Default, Side(Target) with { Level = 60 });
        Assert.Equal("Ready on another job", unnamed.Blockers(Target)[0]);
    }

    [Fact]
    public void Quest_names_in_blockers_go_through_the_names_the_view_was_given()
    {
        // The plugin routes QuestName through the logged-in character's spoiler mask; any function stands in for it here.
        var masked = Names with { QuestName = quest => quest.RowId == A ? "Main scenario quest (Lv 1)" : quest.Name };
        var view = View(Snapshot(), masked, Side(A, "The Ultimate Weapon"), Side(Target) with { PreviousQuests = new Prereq([A], JoinKind.All) });

        var lines = view.Blockers(Target);

        Assert.Equal("Blocked · after: Main scenario quest (Lv 1)", lines[0]);
        Assert.Contains($"PreviousQuests: unmet ({A} Main scenario quest (Lv 1): not done)", lines);
        Assert.DoesNotContain(lines, line => line.Contains("Ultimate", StringComparison.Ordinal));
    }

    [Fact]
    public void The_view_prints_quest_names_from_its_own_catalog()
    {
        var catalog = Catalog(Side(A, "Peace for Thanalan"), Side(Target) with { PreviousQuests = new Prereq([A], JoinKind.All) });
        var states = StateResolver.ResolveAll(catalog, Snapshot(), EvalContext.Default);

        // Names built without a catalog would print "quest 65600"; the view points them at its own.
        var view = new IpcView(catalog, states, BlockerNames.Default);

        Assert.Same(catalog, view.Names.Catalog);
        Assert.Equal("Blocked · after: Peace for Thanalan", view.Blockers(Target)[0]);
    }

    [Fact]
    public void MsqNext_is_the_first_main_scenario_quest_not_completed_and_0_once_the_story_is_done()
    {
        QuestRecord[] quests =
        [
            Msq(Msq1),
            Msq(Msq2) with { PreviousQuests = new Prereq([Msq1], JoinKind.All) },
            Msq(Msq3) with { PreviousQuests = new Prereq([Msq2], JoinKind.All) },
            Side(Target),
        ];

        Assert.Equal(Msq1, View(Snapshot(), null, quests).MsqNext());
        Assert.Equal(Msq2, View(Snapshot(Msq1), null, quests).MsqNext());
        Assert.Equal(Msq3, View(Snapshot(Msq1, Msq2), null, quests).MsqNext());

        var done = View(Snapshot(Msq1, Msq2, Msq3), null, quests);
        Assert.True(done.IsReady);
        Assert.Equal(0u, done.MsqNext());
    }

    [Fact]
    public void States_differ_when_a_quest_moves_and_not_for_a_copy()
    {
        var catalog = Catalog(Side(A), Side(Target) with { PreviousQuests = new Prereq([A], JoinKind.All) });
        var before = StateResolver.ResolveAll(catalog, Snapshot(), EvalContext.Default);
        var after = StateResolver.ResolveAll(catalog, Snapshot(A), EvalContext.Default);
        var copy = new Dictionary<uint, QuestEvaluation>(before);
        var reResolved = StateResolver.ResolveAll(catalog, Snapshot(), EvalContext.Default);

        Assert.False(IpcView.StatesDiffer(before, before));
        Assert.False(IpcView.StatesDiffer(before, copy));
        Assert.False(IpcView.StatesDiffer(before, reResolved));
        Assert.True(IpcView.StatesDiffer(before, after));
        Assert.False(IpcView.StatesDiffer(null, new Dictionary<uint, QuestEvaluation>()));
        Assert.True(IpcView.StatesDiffer(null, before));
        Assert.True(IpcView.StatesDiffer(before, null));
    }

    [Fact]
    public void States_differ_on_a_journal_step_or_the_ready_on_job()
    {
        var catalog = Catalog(Side(Target) with { StepCount = 4 });
        var step1 = StateResolver.ResolveAll(catalog, Snapshot() with { Accepted = [Accepted(Target, sequence: 1)] }, EvalContext.Default);
        var step2 = StateResolver.ResolveAll(catalog, Snapshot() with { Accepted = [Accepted(Target, sequence: 2)] }, EvalContext.Default);
        Assert.True(IpcView.StatesDiffer(step1, step2));

        var level60 = Catalog(Side(Target) with { Level = 60 });
        var onConjurer = StateResolver.ResolveAll(level60, Snapshot() with { JobLevels = Levels((Gladiator, 50), (Conjurer, 60)) }, EvalContext.Default);
        var onPaladin = StateResolver.ResolveAll(level60, Snapshot() with { JobLevels = Levels((Gladiator, 50), (Paladin, 60)) }, EvalContext.Default);
        Assert.Equal(QuestState.ReadyOnOtherJob, onConjurer[Target].State);
        Assert.True(IpcView.StatesDiffer(onConjurer, onPaladin));

        var extra = new Dictionary<uint, QuestEvaluation>(step1) { [A] = step1[Target] };
        Assert.True(IpcView.StatesDiffer(step1, extra));
    }
}
