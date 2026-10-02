using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.GamePanels;
using Tsukimichi.Core.Model;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.GamePanels;

public class NewlyOpenedTests
{
    private const uint Done = 65610;
    private const uint Needed = 65611;
    private const uint AlsoAfter = 65612;
    private const uint Locked = 65613;
    private const uint TooHigh = 65614;
    private const uint OtherJob = 65615;
    private const uint Feature = 65616;
    private const uint Side = 65617;

    /// <summary>A side quest (journal section 3), not a main scenario one as the fixture's default.</summary>
    private static QuestRecord SideQuest(uint rowId) => Quest(rowId) with
    {
        Journal = new JournalRef(3, "Side", 2, "Category", 3, "Genre", (int)rowId),
    };

    private static QuestCatalog Catalog() => Tsukimichi.Tests.Evaluation.Fixture.Catalog(
        Quest(A),
        Quest(Done),
        Quest(Needed),
        // Opened: needs A alone.
        SideQuest(B) with { PreviousQuests = new Prereq([A], JoinKind.All) },
        // Already open through Done (any of the two): not news.
        SideQuest(C) with { PreviousQuests = new Prereq([A, Done], JoinKind.Any) },
        // Needs A and Needed (not done): still shut.
        SideQuest(D) with { PreviousQuests = new Prereq([A, Needed], JoinKind.All) },
        // Opened: needs A and Done, the latter done.
        Quest(AlsoAfter) with { PreviousQuests = new Prereq([A, Done], JoinKind.All) },
        // Locked out by A.
        SideQuest(Locked) with { QuestLocks = [A] },
        // Needs A, but level 60 on a level-50 character.
        SideQuest(TooHigh) with { Level = 60, PreviousQuests = new Prereq([A], JoinKind.All) },
        // Needs A, Conjurer only.
        SideQuest(OtherJob) with { ClassJobRequired = Conjurer, PreviousQuests = new Prereq([A], JoinKind.All) },
        SideQuest(Feature) with { PreviousQuests = new Prereq([A], JoinKind.All) },
        SideQuest(Side) with { PreviousQuests = new Prereq([A], JoinKind.All) });

    private static readonly HashSet<uint> Features = [Feature];

    private static List<uint> Opened(CharacterSnapshot snapshot)
    {
        var catalog = Catalog();
        var states = StateResolver.ResolveAll(catalog, snapshot, EvalContext.Default);
        return NewlyOpened.By(A, catalog, ReversePrereqIndex.Build(catalog), states, Features).Select(o => o.Quest.RowId).ToList();
    }

    [Fact]
    public void Opens_the_quests_the_completion_was_the_last_gate_of()
    {
        // The quest-complete window is up: A is still in the journal.
        var before = Snapshot(Done) with { Accepted = [Accepted(A)] };

        var opened = Opened(before);

        Assert.Equal([AlsoAfter, Feature, B, OtherJob, Side], opened);
    }

    [Fact]
    public void The_answer_is_the_same_once_the_poller_applied_the_completion()
    {
        var before = Opened(Snapshot(Done) with { Accepted = [Accepted(A)] });
        var after = Opened(Snapshot(Done, A));

        Assert.Equal(before, after);
    }

    [Fact]
    public void A_quest_only_another_job_can_take_is_marked()
    {
        var catalog = Catalog();
        var states = StateResolver.ResolveAll(catalog, Snapshot(Done, A), EvalContext.Default);

        var opened = NewlyOpened.By(A, catalog, ReversePrereqIndex.Build(catalog), states, Features);

        Assert.True(Assert.Single(opened, o => o.Quest.RowId == OtherJob).OtherJob);
        Assert.False(Assert.Single(opened, o => o.Quest.RowId == B).OtherJob);
    }

    [Fact]
    public void Counts_split_main_scenario_feature_and_other()
    {
        var catalog = Catalog();
        var states = StateResolver.ResolveAll(catalog, Snapshot(Done, A), EvalContext.Default);
        var opened = NewlyOpened.By(A, catalog, ReversePrereqIndex.Build(catalog), states, Features);

        Assert.Equal((1, 1, 3), NewlyOpened.Count(opened, Features));
    }

    [Fact]
    public void Nothing_depends_on_a_quest_nothing_opens()
    {
        var catalog = Catalog();
        var states = StateResolver.ResolveAll(catalog, Snapshot(Done, A), EvalContext.Default);

        Assert.Empty(NewlyOpened.By(Side, catalog, ReversePrereqIndex.Build(catalog), states));
    }

    [Fact]
    public void A_dependent_already_done_or_in_the_journal_is_not_news()
    {
        var catalog = Catalog();
        var states = StateResolver.ResolveAll(catalog, Snapshot(Done, A, B) with { Accepted = [Accepted(Side)] }, EvalContext.Default);

        var opened = NewlyOpened.By(A, catalog, ReversePrereqIndex.Build(catalog), states, Features).Select(o => o.Quest.RowId).ToList();

        Assert.DoesNotContain(B, opened);
        Assert.DoesNotContain(Side, opened);
        Assert.Contains(Feature, opened);
    }
}
