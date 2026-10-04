using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Query;

/// <summary>
/// The side quests the story requires and the true story meter (feature plan v7 N3), on a small synthetic story: one
/// main scenario quest needs a side line through its previous quests, one a duty only a side quest unlocks, one (curated)
/// any of two role lines. The catch-up counts them beside the main scenario's quests, and the meter adds them to its
/// totals.
/// </summary>
public class StoryRequirementsTests
{
    private const uint S1 = 65800, S2 = 65801, S3 = 65802, S4 = 65803;
    private const uint Side1 = 66000, Side2 = 66001, Side3 = 66002, DutyQuest = 66010;
    private const uint LineA1 = 66020, LineA2 = 66021, LineB1 = 66030, LineB2 = 66031;
    private const uint Instance = 10, Condition = 20;

    private static readonly JournalRef Msq = new(0, "Main Scenario", 1, "Story", 1, "Story", 0);
    private static readonly JournalRef Side = new(2, "Sidequests", 2, "Side", 2, "Side", 0);

    private static QuestRecord Story(uint id, int order, byte expansion, byte level) =>
        Fixture.Quest(id) with { Journal = Msq with { SortKey = order }, Expansion = expansion, Level = level };

    private static QuestRecord SideQuest(uint id, byte level, params uint[] previous) =>
        Fixture.Quest(id) with { Journal = Side with { SortKey = (int)id }, Level = level, PreviousQuests = new Prereq(previous, JoinKind.All) };

    private static QuestCatalog Catalog(params QuestRecord[] extra) => Fixture.Catalog(
    [
        Story(S1, 1, 0, 50),
        Story(S2, 2, 0, 50) with { PreviousQuests = new Prereq([S1, Side3], JoinKind.All) },
        Story(S3, 3, 0, 50) with { PreviousQuests = new Prereq([S2], JoinKind.All), InstanceContentRequired = [Instance] },
        Story(S4, 4, 1, 79) with { PreviousQuests = new Prereq([S3], JoinKind.All) },
        SideQuest(Side1, 30),
        SideQuest(Side2, 32, Side1),
        SideQuest(Side3, 34, Side2, S1),
        SideQuest(DutyQuest, 50, S1),
        SideQuest(LineA1, 70, S1),
        SideQuest(LineA2, 72, LineA1),
        SideQuest(LineB1, 70, S1),
        SideQuest(LineB2, 72, LineB1),
        .. extra,
    ]);

    private static readonly Dictionary<uint, StoryRequiredEntry> Curated = new()
    {
        [S4] = new StoryRequiredEntry([LineA2, LineB2], JoinKind.Any, "one role line", "https://example.invalid/"),
    };

    private static CatchUpDutySource Duties() =>
        new(_ => [], instance => instance == Instance ? Condition : 0u)
        {
            QuestsUnlocking = condition => condition == Condition ? [DutyQuest] : [],
            StoryRequired = Curated,
        };

    private static IReadOnlyDictionary<uint, QuestEvaluation> States(QuestCatalog catalog, params uint[] completed) =>
        StateResolver.ResolveAll(catalog, Fixture.Snapshot(completed) with { JobLevels = Fixture.Levels((Fixture.Gladiator, 90)) }, EvalContext.Default);

    [Fact]
    public void The_three_sources_each_name_their_side_quests()
    {
        var catalog = Catalog();
        var required = StoryRequirements.For(catalog, Duties());
        Assert.Equal(3, required.All.Count);

        var line = required.All[0];
        Assert.Equal(S2, line.StoryQuest.RowId);
        Assert.Equal(StoryRequirementSource.Prerequisite, line.Source);
        Assert.Equal(JoinKind.All, line.Join);
        Assert.Equal([Side1, Side2, Side3], Assert.Single(line.Options));

        var duty = required.All[1];
        Assert.Equal(S3, duty.StoryQuest.RowId);
        Assert.Equal(StoryRequirementSource.Duty, duty.Source);
        Assert.Equal([DutyQuest], Assert.Single(duty.Options));

        var curated = required.All[2];
        Assert.Equal(S4, curated.StoryQuest.RowId);
        Assert.Equal(StoryRequirementSource.Curated, curated.Source);
        Assert.Equal(JoinKind.Any, curated.Join);
        Assert.Equal([[LineA1, LineA2], [LineB1, LineB2]], curated.Options);

        Assert.Equal(8, required.SideQuests.Count);
    }

    [Fact]
    public void Without_a_duty_source_only_the_previous_quests_count()
    {
        var required = StoryRequirements.For(Catalog(), null);
        Assert.Equal(StoryRequirementSource.Prerequisite, Assert.Single(required.All).Source);
    }

    [Fact]
    public void A_duty_the_story_unlocks_itself_needs_no_side_quest()
    {
        var catalog = Catalog();
        var duties = Duties() with { QuestsUnlocking = condition => condition == Condition ? [DutyQuest, S1] : [] };
        Assert.DoesNotContain(StoryRequirements.For(catalog, duties).All, r => r.Source == StoryRequirementSource.Duty);
    }

    [Fact]
    public void An_any_join_a_story_quest_meets_needs_no_side_quest_and_removed_quests_never_count()
    {
        var catalog = Fixture.Catalog(
            Story(S1, 1, 0, 50),
            Story(S2, 2, 0, 50) with { PreviousQuests = new Prereq([S1, Side1], JoinKind.Any) },
            Story(S3, 3, 0, 50) with { PreviousQuests = new Prereq([S2, Side2], JoinKind.All) },
            SideQuest(Side1, 30),
            SideQuest(Side2, 30, Side3),
            SideQuest(Side3, 30) with { IsRetired = true });
        var required = StoryRequirements.Build(catalog, null, null, null);
        var single = Assert.Single(required.All);
        Assert.Equal(S3, single.StoryQuest.RowId);
        Assert.Equal([Side2], Assert.Single(single.Options));
    }

    [Fact]
    public void Progress_takes_the_option_closest_to_done()
    {
        var catalog = Catalog();
        var required = StoryRequirements.For(catalog, Duties());

        var fresh = required.Progress(States(catalog, S1));
        Assert.Equal(0, fresh.Done);
        Assert.Equal(3 + 1 + 2, fresh.Total);
        Assert.Equal([Side1, Side2, Side3], fresh.LeftFor[S2]);
        Assert.Equal([DutyQuest], fresh.LeftFor[S3]);
        Assert.Equal([LineA1, LineA2], fresh.LeftFor[S4]);

        // Half of role line B done: B is the one that counts.
        var started = required.Progress(States(catalog, S1, LineB1, Side1));
        Assert.Equal(2, started.Done);
        Assert.Equal(6, started.Total);
        Assert.Equal([LineB2], started.LeftFor[S4]);
        Assert.Equal([Side2, Side3], started.LeftFor[S2]);

        // Everything done: nothing left for any quest.
        var done = required.Progress(States(catalog, S1, S2, S3, Side1, Side2, Side3, DutyQuest, LineA1, LineA2));
        Assert.Equal(6, done.Done);
        Assert.Empty(done.LeftFor);
    }

    [Fact]
    public void The_catch_up_counts_the_side_quests_beside_the_story()
    {
        var catalog = Catalog();
        var snapshot = Fixture.Snapshot(S1) with { JobLevels = Fixture.Levels((Fixture.Gladiator, 90)) };
        var states = StateResolver.ResolveAll(catalog, snapshot, EvalContext.Default);
        var summary = MsqCatchUp.Compute(catalog, states, snapshot, Duties());
        Assert.NotNull(summary);

        // The main scenario count is the position's as before; the side quests are apart.
        Assert.Equal(3, summary.Quests);
        Assert.Equal(6, summary.SideQuests);
        Assert.Equal(9, summary.AllQuests);
        var arr = summary.Expansions[0];
        Assert.Equal(2, arr.Quests);
        Assert.Equal(4, arr.SideQuests);
        Assert.Equal(30, arr.MinLevel);
        Assert.Equal(50, arr.MaxLevel);
        var next = summary.Expansions[1];
        Assert.Equal(2, next.SideQuests);
        Assert.Equal(70, next.MinLevel);
        Assert.Equal(79, next.MaxLevel);

        // A main scenario quest done takes its side quests out of the catch-up.
        var later = Fixture.Snapshot(S1, S2, S3, Side1, Side2, Side3, DutyQuest) with { JobLevels = Fixture.Levels((Fixture.Gladiator, 90)) };
        var laterStates = StateResolver.ResolveAll(catalog, later, EvalContext.Default);
        var rest = MsqCatchUp.Compute(catalog, laterStates, later, Duties());
        Assert.NotNull(rest);
        Assert.Equal(1, rest.Quests);
        Assert.Equal(2, rest.SideQuests);
    }

    [Fact]
    public void The_story_meter_adds_the_side_quests_to_its_totals()
    {
        var catalog = Catalog();
        var meter = StoryMeter.Compute(catalog, States(catalog, S1, Side1), Duties());
        Assert.NotNull(meter);
        Assert.Equal(1, meter.StoryDone);
        Assert.Equal(4, meter.StoryTotal);
        Assert.Equal(1, meter.SideDone);
        Assert.Equal(6, meter.SideTotal);
        Assert.Equal(2, meter.Done);
        Assert.Equal(10, meter.Total);
        Assert.Equal(20, meter.Percent);

        // Rounded down: 99.9% never reads 100.
        Assert.Equal(99, new StoryMeter(999, 1000, 0, 1).Percent);
        Assert.Equal(100, new StoryMeter(0, 0, 0, 0).Percent);
    }

    [Fact]
    public void The_requirements_are_cached_per_catalog_and_source()
    {
        var catalog = Catalog();
        var duties = Duties();
        Assert.Same(StoryRequirements.For(catalog, duties), StoryRequirements.For(catalog, duties));
        Assert.NotSame(StoryRequirements.For(catalog, duties), StoryRequirements.For(catalog, Duties()));
    }
}
