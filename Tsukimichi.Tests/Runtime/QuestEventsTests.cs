using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Runtime;

public sealed class QuestEventsTests
{
    private static readonly DateTime Now = new(2026, 9, 27, 21, 14, 0, DateTimeKind.Utc);

    private static QuestEvaluation Eval(QuestState state) => new(state, [], null, null, null);

    private static Dictionary<uint, QuestEvaluation> States(params (uint RowId, QuestState State)[] rows) =>
        rows.ToDictionary(r => r.RowId, r => Eval(r.State));

    [Fact]
    public void Completed_bit_set_yields_Completed_with_the_catalog_row_id()
    {
        var catalog = Fixture.Catalog(Fixture.Quest(Fixture.A));
        var old = Fixture.Snapshot();
        var @new = Fixture.Snapshot(Fixture.A);
        var diff = SnapshotDiff.Compute(old, @new);

        var events = QuestEvents.Derive(diff, old, @new, catalog, States(), States(), Now);

        var e = Assert.Single(events);
        Assert.Equal(new QuestEvent(QuestEventKind.Completed, Fixture.A, Now), e);
    }

    [Fact]
    public void A_spare_alternative_becoming_ready_is_not_announced()
    {
        // Another city's version of a quest while the choice is open: Ready, but out of the counts and not news.
        var catalog = Fixture.Catalog(Fixture.Quest(Fixture.A), Fixture.Quest(Fixture.B));
        var old = Fixture.Snapshot();
        var @new = Fixture.Snapshot();
        var before = States((Fixture.A, QuestState.Blocked), (Fixture.B, QuestState.Blocked));
        var after = new Dictionary<uint, QuestEvaluation>
        {
            [Fixture.A] = Eval(QuestState.Ready) with { IsSpareAlternative = true, ChoiceOf = 2 },
            [Fixture.B] = Eval(QuestState.Ready),
        };

        var events = QuestEvents.Derive(SnapshotDiff.Compute(old, @new), old, @new, catalog, before, after, Now);

        Assert.Equal([new QuestEvent(QuestEventKind.NewlyAvailable, Fixture.B, Now)], events);
    }

    [Fact]
    public void Journal_entry_and_exit_yield_Accepted_and_Abandoned()
    {
        var catalog = Fixture.Catalog(Fixture.Quest(Fixture.A), Fixture.Quest(Fixture.B));
        var old = Fixture.Snapshot() with { Accepted = [Fixture.Accepted(Fixture.B)] };
        var @new = Fixture.Snapshot() with { Accepted = [Fixture.Accepted(Fixture.A)] };
        var diff = SnapshotDiff.Compute(old, @new);

        var events = QuestEvents.Derive(diff, old, @new, catalog, States(), States(), Now);

        Assert.Equal(
        [
            new QuestEvent(QuestEventKind.Accepted, Fixture.A, Now),
            new QuestEvent(QuestEventKind.Abandoned, Fixture.B, Now),
        ], events);
    }

    [Fact]
    public void A_seasonal_quest_cleared_at_the_end_of_its_event_is_not_Abandoned()
    {
        const ushort Festival = 9;
        var catalog = Fixture.Catalog(Fixture.Quest(Fixture.A) with { Festival = Festival }, Fixture.Quest(Fixture.B) with { Festival = Festival });
        var old = Fixture.Snapshot() with { Accepted = [Fixture.Accepted(Fixture.A, 2)], ActiveFestivals = [Festival] };

        // The event ended and the game took the quest out of the journal: the evaluator reads the festival inactive.
        var ended = Fixture.Snapshot() with { ActiveFestivals = [] };
        var endedStates = StateResolver.ResolveAll(catalog, ended, EvalContext.Default);
        Assert.Empty(QuestEvents.Derive(SnapshotDiff.Compute(old, ended), old, ended, catalog, States(), endedStates, Now));

        // Without an evaluation for the row, the snapshot's running festivals decide the same way.
        Assert.Empty(QuestEvents.Derive(SnapshotDiff.Compute(old, ended), old, ended, catalog, States(), States(), Now));

        // While the event runs, dropping the quest is the player's doing.
        var running = Fixture.Snapshot() with { ActiveFestivals = [Festival] };
        var runningStates = StateResolver.ResolveAll(catalog, running, EvalContext.Default);
        var e = Assert.Single(QuestEvents.Derive(SnapshotDiff.Compute(old, running), old, running, catalog, States(), runningStates, Now));
        Assert.Equal(new QuestEvent(QuestEventKind.Abandoned, Fixture.A, Now), e);
    }

    [Fact]
    public void Turning_in_a_quest_is_Completed_not_Abandoned()
    {
        var catalog = Fixture.Catalog(Fixture.Quest(Fixture.A));
        var old = Fixture.Snapshot() with { Accepted = [Fixture.Accepted(Fixture.A, 5)] };
        var @new = Fixture.Snapshot(Fixture.A);
        var diff = SnapshotDiff.Compute(old, @new);

        var events = QuestEvents.Derive(diff, old, @new, catalog, States(), States(), Now);

        var e = Assert.Single(events);
        Assert.Equal(QuestEventKind.Completed, e.Kind);
    }

    [Fact]
    public void Sequence_change_alone_yields_no_event()
    {
        var catalog = Fixture.Catalog(Fixture.Quest(Fixture.A));
        var old = Fixture.Snapshot() with { Accepted = [Fixture.Accepted(Fixture.A, 1)] };
        var @new = Fixture.Snapshot() with { Accepted = [Fixture.Accepted(Fixture.A, 2)] };
        var diff = SnapshotDiff.Compute(old, @new);

        Assert.Empty(QuestEvents.Derive(diff, old, @new, catalog, States(), States(), Now));
    }

    [Fact]
    public void Quests_the_catalog_does_not_know_are_ignored()
    {
        var catalog = Fixture.Catalog(Fixture.Quest(Fixture.A));
        var old = Fixture.Snapshot();
        var @new = Fixture.Snapshot(Fixture.B);
        var diff = SnapshotDiff.Compute(old, @new);

        Assert.Empty(QuestEvents.Derive(diff, old, @new, catalog, States(), States(), Now));
    }

    [Fact]
    public void State_moving_into_Ready_or_ReadyOnOtherJob_is_NewlyAvailable_in_row_order()
    {
        var catalog = Fixture.Catalog(Fixture.Quest(Fixture.A), Fixture.Quest(Fixture.B), Fixture.Quest(Fixture.C), Fixture.Quest(Fixture.D));
        var snapshot = Fixture.Snapshot();
        var oldStates = States((Fixture.A, QuestState.Blocked), (Fixture.B, QuestState.Blocked), (Fixture.C, QuestState.Ready), (Fixture.D, QuestState.Ready));
        var newStates = States((Fixture.A, QuestState.ReadyOnOtherJob), (Fixture.B, QuestState.Ready), (Fixture.C, QuestState.ReadyOnOtherJob), (Fixture.D, QuestState.Blocked));

        var events = QuestEvents.Derive(SnapshotDiff.Empty, snapshot, snapshot, catalog, oldStates, newStates, Now);

        Assert.Equal(
        [
            new QuestEvent(QuestEventKind.NewlyAvailable, Fixture.A, Now),
            new QuestEvent(QuestEventKind.NewlyAvailable, Fixture.B, Now),
        ], events);
    }

    [Fact]
    public void Rows_missing_from_the_previous_states_are_not_newly_available()
    {
        var catalog = Fixture.Catalog(Fixture.Quest(Fixture.A));
        var snapshot = Fixture.Snapshot();

        var events = QuestEvents.Derive(SnapshotDiff.Empty, snapshot, snapshot, catalog, States(), States((Fixture.A, QuestState.Ready)), Now);

        Assert.Empty(events);
    }

    [Fact]
    public void Unchanged_evaluation_instances_are_skipped()
    {
        var catalog = Fixture.Catalog(Fixture.Quest(Fixture.A));
        var snapshot = Fixture.Snapshot();
        var shared = Eval(QuestState.Ready);
        var oldStates = new Dictionary<uint, QuestEvaluation> { [Fixture.A] = shared };
        var newStates = new Dictionary<uint, QuestEvaluation> { [Fixture.A] = shared };

        Assert.Empty(QuestEvents.Derive(SnapshotDiff.Empty, snapshot, snapshot, catalog, oldStates, newStates, Now));
    }

    [Fact]
    public void Quest_events_precede_availability_events()
    {
        var catalog = Fixture.Catalog(Fixture.Quest(Fixture.A), Fixture.Quest(Fixture.B));
        var old = Fixture.Snapshot();
        var @new = Fixture.Snapshot(Fixture.A);
        var diff = SnapshotDiff.Compute(old, @new);
        var oldStates = States((Fixture.B, QuestState.Blocked));
        var newStates = States((Fixture.B, QuestState.Ready));

        var events = QuestEvents.Derive(diff, old, @new, catalog, oldStates, newStates, Now);

        Assert.Equal([QuestEventKind.Completed, QuestEventKind.NewlyAvailable], events.Select(e => e.Kind));
    }
}
