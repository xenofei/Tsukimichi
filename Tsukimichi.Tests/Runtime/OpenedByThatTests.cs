using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Runtime;
using static Tsukimichi.Tests.Query.QueryTestData;

namespace Tsukimichi.Tests.Runtime;

/// <summary>
/// "Opened by that" (feature plan v5, 1.7.0; R9 F1): <see cref="OpenedBatcher"/> gathers what became available after
/// completions and closes a batch once it has been quiet, and <see cref="OpenedSummary"/> counts and words it, "2
/// unlock quests, 8 side quests (3 with a story)". The <see cref="QuestScope.JustOpened"/> scope lists the quests.
/// </summary>
public sealed class OpenedByThatTests
{
    private static readonly DateTime T0 = new(2026, 10, 1, 20, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Quiet = TimeSpan.FromSeconds(4);

    private static QuestEvent Completed(uint rowId) => new(QuestEventKind.Completed, rowId, T0);

    private static QuestEvent Opened(uint rowId) => new(QuestEventKind.NewlyAvailable, rowId, T0);

    private static QuestEvent Accepted(uint rowId) => new(QuestEventKind.Accepted, rowId, T0);

    // ---- Batching ----

    [Fact]
    public void A_completion_and_what_it_opened_become_one_batch_once_quiet()
    {
        var batcher = new OpenedBatcher();
        batcher.Add([Completed(1), Opened(2), Opened(3)], T0);

        Assert.True(batcher.Pending);
        Assert.Null(batcher.Take(T0.AddSeconds(3), Quiet));

        var batch = batcher.Take(T0.AddSeconds(4), Quiet);
        Assert.NotNull(batch);
        Assert.Equal(new uint[] { 1 }, batch.Completed);
        Assert.Equal(new uint[] { 2, 3 }, batch.Opened);
        Assert.False(batcher.Pending);
        Assert.Null(batcher.Take(T0.AddSeconds(10), Quiet));
    }

    [Fact]
    public void Availability_that_lands_a_poll_later_joins_and_extends_the_batch()
    {
        var batcher = new OpenedBatcher();
        batcher.Add([Completed(1)], T0);
        batcher.Add([Opened(2)], T0.AddSeconds(3));

        // Quiet is measured from the last thing that joined.
        Assert.Null(batcher.Take(T0.AddSeconds(5), Quiet));
        var batch = batcher.Take(T0.AddSeconds(7), Quiet);
        Assert.Equal(new uint[] { 2 }, batch!.Opened);
    }

    [Fact]
    public void Turn_ins_a_few_seconds_apart_share_one_batch()
    {
        var batcher = new OpenedBatcher();
        batcher.Add([Completed(1), Opened(10)], T0);
        batcher.Add([Completed(2), Opened(11), Opened(12)], T0.AddSeconds(2));

        Assert.Null(batcher.Take(T0.AddSeconds(4), Quiet));
        var batch = batcher.Take(T0.AddSeconds(6), Quiet);
        Assert.Equal(new uint[] { 1, 2 }, batch!.Completed);
        Assert.Equal(new uint[] { 10, 11, 12 }, batch.Opened);
    }

    [Fact]
    public void Availability_without_a_completion_before_it_is_not_news()
    {
        // A level-up or a new day opens quests too; this line is about turn-ins.
        var batcher = new OpenedBatcher();
        batcher.Add([Opened(2), Accepted(3)], T0);

        Assert.False(batcher.Pending);
        Assert.Null(batcher.Take(T0.AddMinutes(1), Quiet));
    }

    [Fact]
    public void Only_availability_of_the_completions_poll_or_later_joins()
    {
        // An earlier poll's availability (before any turn-in) stays out; the turn-in's own poll joins in any order.
        var batcher = new OpenedBatcher();
        var earlier = T0.AddSeconds(-2);
        batcher.Add([new(QuestEventKind.NewlyAvailable, 5, earlier), Opened(6), Completed(1), Opened(7)], T0);

        var batch = batcher.Take(T0.AddSeconds(5), Quiet);
        Assert.Equal(new uint[] { 6, 7 }, batch!.Opened);
    }

    [Fact]
    public void Within_one_poll_the_completion_counts_first_whatever_the_order()
    {
        var batcher = new OpenedBatcher();
        batcher.Add([Opened(2), Opened(3), Completed(1)], T0);

        var batch = batcher.Take(T0.AddSeconds(5), Quiet);
        Assert.NotNull(batch);
        Assert.Equal(new uint[] { 1 }, batch.Completed);
        Assert.Equal(new uint[] { 2, 3 }, batch.Opened);
    }

    [Fact]
    public void A_turn_in_and_what_its_poll_opened_reach_the_line_through_the_recent_list_and_the_scan()
    {
        // The real pipeline: QuestEvents.Derive lists a poll's events completions first, the recent list keeps that
        // order newest first, and NoticeTracker.ScanEvents walks it backwards, so the batcher sees them reversed.
        const ulong contentId = 42;
        var recent = new RecentEventsTracker();
        var tracker = new NoticeTracker();
        var batcher = new OpenedBatcher();
        tracker.ScanEvents(recent.Events, contentId);

        var before = T0.AddSeconds(-10);
        recent.Add(contentId, [new QuestEvent(QuestEventKind.NewlyAvailable, 9, before)]);
        batcher.Add(tracker.ScanEvents(recent.Events, contentId), before);
        Assert.False(batcher.Pending);

        recent.Add(contentId, [Completed(1), Accepted(4), Opened(2), Opened(3)]);
        var fresh = tracker.ScanEvents(recent.Events, contentId);
        Assert.Equal(QuestEventKind.Completed, fresh[^1].Kind);
        batcher.Add(fresh, T0);

        var batch = batcher.Take(T0.AddSeconds(5), Quiet);
        Assert.NotNull(batch);
        Assert.Equal(new uint[] { 1 }, batch.Completed);
        Assert.Equal(new uint[] { 2, 3 }, batch.Opened.Order());
    }

    [Fact]
    public void A_batch_that_opened_nothing_is_dropped()
    {
        var batcher = new OpenedBatcher();
        batcher.Add([Completed(1)], T0);

        Assert.Null(batcher.Take(T0.AddSeconds(5), Quiet));
        Assert.False(batcher.Pending);
    }

    [Fact]
    public void A_quest_completed_in_the_batch_never_counts_as_opened_and_repeats_count_once()
    {
        var batcher = new OpenedBatcher();
        batcher.Add([Completed(1), Opened(2), Opened(2), Opened(1)], T0);
        batcher.Add([Completed(2), Opened(3)], T0.AddSeconds(1));

        var batch = batcher.Take(T0.AddSeconds(6), Quiet);
        Assert.Equal(new uint[] { 1, 2 }, batch!.Completed);
        Assert.Equal(new uint[] { 3 }, batch.Opened);
    }

    [Fact]
    public void A_busy_stretch_closes_after_the_longest_wait()
    {
        var batcher = new OpenedBatcher();
        batcher.Add([Completed(1), Opened(100)], T0);
        uint next = 101;
        for (var s = 2; s < 40; s += 2)
        {
            batcher.Add([Opened(next++)], T0.AddSeconds(s));
            var at = T0.AddSeconds(s);
            if (at - T0 < OpenedBatcher.MaxWait)
            {
                Assert.Null(batcher.Take(at, Quiet));
            }
            else
            {
                Assert.NotNull(batcher.Take(at, Quiet));
                return;
            }
        }

        Assert.Fail("The batch never closed.");
    }

    [Fact]
    public void Each_batch_gets_a_new_serial_and_reset_drops_the_open_one()
    {
        var batcher = new OpenedBatcher();
        batcher.Add([Completed(1), Opened(2)], T0);
        var first = batcher.Take(T0.AddSeconds(5), Quiet)!;
        batcher.Add([Completed(3), Opened(4)], T0.AddSeconds(10));
        var second = batcher.Take(T0.AddSeconds(15), Quiet)!;
        Assert.NotEqual(first.Serial, second.Serial);
        Assert.True(first.Serial > 0);

        batcher.Add([Completed(5), Opened(6)], T0.AddSeconds(20));
        batcher.Reset();
        Assert.False(batcher.Pending);
        Assert.Null(batcher.Take(T0.AddMinutes(5), Quiet));
    }

    // ---- Summary ----

    private static readonly QuestCatalog Catalog = QuestCatalog.Build(
    [
        Quest(1, "Main One", section: 1, genre: 101),
        Quest(2, "Feature One", section: 2, genre: 102),
        Quest(3, "Feature Two", section: 2, genre: 102),
        Quest(4, "Side Story", section: 3, genre: 103),
        Quest(5, "Side Plain", section: 3, genre: 103),
        Quest(6, "Job Quest", section: 2, genre: 104),
        Quest(7, "Main Feature", section: 0, genre: 100),
    ]);

    private static readonly HashSet<uint> Features = [2, 3, 7];
    private static readonly HashSet<uint> Stories = [4];

    [Fact]
    public void Counts_split_main_scenario_feature_side_and_other_quests()
    {
        var counts = OpenedSummary.Count(Catalog, [1, 2, 3, 4, 5, 6, 7, 999], Features, Stories);

        // A main scenario quest that is also an unlock quest counts as main scenario; an unknown id is skipped.
        Assert.Equal(new OpenedCounts(MainScenario: 2, Feature: 2, Side: 2, SideStories: 1, Other: 1), counts);
        Assert.Equal(7, counts.Total);
    }

    [Fact]
    public void The_text_names_each_kind_that_opened_in_a_fixed_order()
    {
        Assert.Equal(
            "2 unlock quests, 8 side quests (3 with a story)",
            OpenedSummary.Text(new OpenedCounts(0, 2, 8, 3, 0)));
        Assert.Equal(
            "1 main scenario quest, 1 unlock quest, 1 side quest, 1 other quest",
            OpenedSummary.Text(new OpenedCounts(1, 1, 1, 0, 1)));
        Assert.Equal(
            "3 main scenario quests, 1 side quest (1 with a story), 4 other quests",
            OpenedSummary.Text(new OpenedCounts(3, 0, 1, 1, 4)));
    }

    [Fact]
    public void Nothing_opened_reads_as_nothing()
    {
        Assert.Equal(string.Empty, OpenedSummary.Text(default));
        Assert.Equal(0, OpenedSummary.Count(Catalog, [], Features, Stories).Total);
    }

    // ---- The Just opened scope ----

    [Fact]
    public void The_just_opened_scope_lists_the_batch_in_journal_order()
    {
        var states = States(Catalog, QuestState.Ready);
        var ctx = QueryContext.Empty with { JustOpened = new HashSet<uint> { 5, 2, 1 } };

        var result = Run(Catalog, states, scope: QuestScope.JustOpened(7), ctx: ctx);

        Assert.Equal(new uint[] { 1, 2, 5 }, RowIds(result));
        Assert.Equal(ScopeKind.VirtualJustOpened, QuestScope.JustOpened(7).Kind);
    }

    [Fact]
    public void The_just_opened_scope_is_empty_without_a_batch()
    {
        var result = Run(Catalog, States(Catalog, QuestState.Ready), scope: QuestScope.JustOpened(1));

        Assert.Empty(result.Rows);
    }
}
