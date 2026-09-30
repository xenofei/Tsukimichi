using Tsukimichi.Core.Diagnostics;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Ipc;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Evaluation;

/// <summary>
/// Feature plan v4 D1 on a small synthetic world: two start cities whose lines meet again through an Any join, a pair
/// of quests that lock each other, and three company versions of one quest. The choice groups
/// (<see cref="PathIndex"/>), their propagation, a character's choices (<see cref="PathChoice"/>) and what the
/// resolver, the counts, the position, the shield, the path walk and IPC make of them.
/// </summary>
public class PathChoiceTests
{
    private const uint AlphaRoot = 65600;   // "Coming to Alpha": no previous quest, a level-1 MSQ after it
    private const uint BetaRoot = 65601;
    private const uint AlphaMsq = 65610;
    private const uint BetaMsq = 65611;
    private const uint Meeting = 65620;     // ← ANY(AlphaMsq, BetaMsq)
    private const uint Onward = 65621;      // ← Meeting
    private const uint Heads = 65630;       // locks Tails
    private const uint Tails = 65631;       // locks Heads
    private const uint AfterHeads = 65632;  // ← Heads
    private const uint Maelstrom = 65640;   // three company versions, each locking the others
    private const uint TwinAdder = 65641;
    private const uint ImmortalFlames = 65642;

    private static QuestRecord Side(uint rowId, string name) =>
        Quest(rowId, name) with { Journal = new JournalRef(3, "Sidequests", 30, "Sidequests", 300, "Sidequests", (int)rowId) };

    private static QuestRecord Msq(uint rowId, string name, JoinKind join, params uint[] previous) =>
        Quest(rowId, name) with
        {
            Journal = new JournalRef(0, "Main Scenario", 1, "Seventh Umbral Era", 1, "Seventh Umbral Era", (int)rowId),
            PreviousQuests = new Prereq(previous, join),
        };

    private static readonly PathChoices Choices = new(
        [new CityPin(AlphaRoot, "Alpha", "test"), new CityPin(BetaRoot, "Beta", "test")],
        [],
        new Dictionary<uint, GrandCompanyTag>());

    private static QuestCatalog World()
    {
        var catalog = Catalog(
            Side(AlphaRoot, "Coming to Alpha"),
            Side(BetaRoot, "Coming to Beta"),
            Msq(AlphaMsq, "Alpha Story", JoinKind.All, AlphaRoot),
            Msq(BetaMsq, "Beta Story", JoinKind.All, BetaRoot),
            Msq(Meeting, "Meeting", JoinKind.Any, AlphaMsq, BetaMsq),
            Msq(Onward, "Onward", JoinKind.All, Meeting),
            Side(Heads, "Heads") with { QuestLocks = [Tails] },
            Side(Tails, "Tails") with { QuestLocks = [Heads] },
            Side(AfterHeads, "After Heads") with { PreviousQuests = new Prereq([Heads], JoinKind.All) },
            Side(Maelstrom, "Chocobo (Maelstrom)") with { GrandCompany = 1, QuestLocks = [TwinAdder, ImmortalFlames] },
            Side(TwinAdder, "Chocobo (Twin Adder)") with { GrandCompany = 2, QuestLocks = [Maelstrom, ImmortalFlames] },
            Side(ImmortalFlames, "Chocobo (Immortal Flames)") with { GrandCompany = 3, QuestLocks = [Maelstrom, TwinAdder] });
        PathIndex.Attach(catalog, Choices);
        return catalog;
    }

    private static Dictionary<uint, QuestEvaluation> Resolve(QuestCatalog catalog, CharacterSnapshot snapshot) =>
        StateResolver.ResolveAll(catalog, snapshot, EvalContext.Default);

    [Fact]
    public void Options_carry_forward_intersecting_through_all_joins_and_uniting_through_any_joins()
    {
        var index = PathIndex.For(World());

        Assert.Equal([AlphaRoot, BetaRoot], index.RuleCityRoots);
        var city = index.Groups.Single(g => g.Kind == PathKind.StartCity);
        Assert.Equal([new PathTag(city.Id, 1), ], index.TagsOf(AlphaMsq));
        Assert.Equal([new PathTag(city.Id, 2)], index.TagsOf(BetaMsq));
        Assert.Empty(index.TagsOf(Meeting));   // Alpha or Beta: every city reaches it
        Assert.Empty(index.TagsOf(Onward));

        var coin = index.Groups.Single(g => g.Options.Any(o => o.Anchors.Contains(Heads)));
        Assert.Equal(PathKind.Choice, coin.Kind);
        Assert.Equal(index.TagsOf(Heads), index.TagsOf(AfterHeads));
        Assert.Equal(PathKind.GrandCompany, index.Groups.Single(g => g.Options.Any(o => o.Anchors.Contains(TwinAdder))).Kind);
        Assert.True(index.IsAnchor(AlphaRoot) && index.IsAnchor(Heads) && index.IsAnchor(TwinAdder));
        Assert.False(index.IsAnchor(Onward));
    }

    [Fact]
    public void Without_the_curated_pin_no_city_group_is_made()
    {
        var catalog = World();
        var bare = PathIndex.Build(catalog, PathChoices.Empty);
        Assert.Equal([AlphaRoot, BetaRoot], bare.RuleCityRoots);
        Assert.DoesNotContain(bare.Groups, g => g.Kind == PathKind.StartCity);
        Assert.Empty(bare.TagsOf(BetaMsq));
    }

    [Fact]
    public void The_city_taken_locks_the_other_city_s_line_out_by_name()
    {
        var catalog = World();
        var states = Resolve(catalog, Snapshot(AlphaRoot));

        Assert.True(states[BetaRoot].IsOtherPath);
        Assert.True(states[BetaMsq].IsOtherPath);
        Assert.Equal(QuestState.Foreclosed, states[BetaMsq].State);
        Assert.True(states[BetaMsq].LeavesTotals);
        Assert.Equal(PathKind.StartCity, states[BetaMsq].OtherPath!.Path);
        Assert.Equal([AlphaRoot], states[BetaMsq].OtherPath!.Evidence);
        Assert.False(states[AlphaMsq].IsOtherPath);
        Assert.False(states[Meeting].IsOtherPath);

        var names = BlockerNames.Default with { Catalog = catalog };
        Assert.Equal("Locked out · Another city's start (Beta)", BlockerText.StatusText(states[BetaMsq], catalog.ByRowId[BetaMsq], names, states));
        Assert.Equal("Only for Beta starters · you started in Alpha", states[BetaMsq].NextStep!.Detail);
        Assert.Equal("Only for Beta starters · you started in Alpha", RequirementDetail.Text(states[BetaMsq].NextStep!, names));
        Assert.Equal(RequirementKind.OtherPath, states[BetaMsq].Requirements[0].Req.Kind);
    }

    [Fact]
    public void A_city_s_first_quest_in_the_journal_decides_it_too()
    {
        var states = Resolve(World(), Snapshot() with { Accepted = [Accepted(AlphaRoot)] });

        Assert.True(states[BetaRoot].IsOtherPath);
        Assert.Equal(QuestState.Accepted, states[AlphaRoot].State);
    }

    [Fact]
    public void A_completed_quest_is_never_on_another_path_and_two_completed_options_make_a_group_not_exclusive()
    {
        var catalog = World();

        // Beta's story done although the character took Alpha: still Completed, and it counts.
        var odd = Resolve(catalog, Snapshot(AlphaRoot, BetaMsq));
        Assert.Equal(QuestState.Completed, odd[BetaMsq].State);
        Assert.True(odd[BetaRoot].IsOtherPath);

        // Both roots done: the character did more than one, so neither city's quests are locked out.
        var both = Resolve(catalog, Snapshot(AlphaRoot, BetaRoot));
        Assert.DoesNotContain(both.Values, e => e.IsOtherPath);
        Assert.True(PathIndex.For(catalog).Resolve(Snapshot(AlphaRoot, BetaRoot)).NotExclusive[0]);
    }

    [Fact]
    public void A_choice_not_made_yet_counts_one_option_and_tags_each_one()
    {
        var catalog = World();
        var states = Resolve(catalog, Snapshot(AlphaRoot));

        // The first in journal order is presumed; the other is a spare alternative, as is what follows only from it.
        Assert.False(states[Heads].IsSpareAlternative);
        Assert.True(states[Tails].IsSpareAlternative);
        Assert.False(states[AfterHeads].IsSpareAlternative);
        Assert.Equal(QuestState.Ready, states[Tails].State);
        Assert.True(states[Tails].LeavesTotals);
        Assert.False(states[Tails].IsOtherPath);
        Assert.Equal(2, states[Heads].ChoiceOf);
        Assert.Equal(2, states[Tails].ChoiceOf);
        Assert.Equal(0, states[AfterHeads].ChoiceOf);

        var names = BlockerNames.Default with { Catalog = catalog };
        Assert.Equal("Ready · Choose one of 2", BlockerText.StatusText(states[Tails], catalog.ByRowId[Tails], names, states));

        var counts = TreeCounts.Compute(catalog, states, includeUnlisted: false);
        var sides = counts.Section(3);
        // Heads, After Heads, the presumed company version and Alpha's root; Beta's root is on another path.
        Assert.Equal(new NodeCount(1, 4, 4) { OtherPaths = 1 }, sides);
        Assert.Equal(1, counts.SectionReady(3)); // Heads (the presumed company version waits for a company); Tails lights nothing
    }

    [Fact]
    public void A_company_version_presumes_the_character_s_company()
    {
        var states = Resolve(World(), Snapshot(AlphaRoot) with { GrandCompany = 2 });

        Assert.False(states[TwinAdder].IsSpareAlternative);
        Assert.True(states[Maelstrom].IsSpareAlternative);
        Assert.True(states[ImmortalFlames].IsSpareAlternative);
        Assert.Equal(3, states[TwinAdder].ChoiceOf);
        Assert.Equal(QuestState.Blocked, states[Maelstrom].State);
    }

    [Fact]
    public void A_choice_made_locks_the_other_options_and_what_follows_them_out()
    {
        var catalog = World();
        var states = Resolve(catalog, Snapshot(AlphaRoot, Tails, TwinAdder) with { GrandCompany = 1 });
        var names = BlockerNames.Default with { Catalog = catalog };

        Assert.True(states[Heads].IsOtherPath);
        Assert.True(states[AfterHeads].IsOtherPath);
        Assert.Equal("Locked out · Another choice (Tails)", BlockerText.StatusText(states[AfterHeads], catalog.ByRowId[AfterHeads], names, states));
        Assert.Equal("Only one of these can be done · you did Tails", states[AfterHeads].NextStep!.Detail);

        // Switching companies later does not open another company's version once one is done.
        Assert.True(states[Maelstrom].IsOtherPath);
        Assert.Equal("Locked out · Another Grand Company (Maelstrom)", BlockerText.StatusText(states[Maelstrom], catalog.ByRowId[Maelstrom], names, states));
        Assert.Equal("For the Maelstrom · you chose the Order of the Twin Adder", states[Maelstrom].NextStep!.Detail);
        Assert.DoesNotContain(states.Values, e => e.IsSpareAlternative || e.ChoiceOf > 0);
    }

    [Fact]
    public void A_change_to_an_anchor_quest_resolves_everything()
    {
        var catalog = World();
        var before = Snapshot();
        var previous = Resolve(catalog, before);
        Assert.DoesNotContain(previous.Values, e => e.IsOtherPath);

        // Completing Alpha's root reaches Beta's line only through the choice, not through any previous-quest edge.
        var after = Snapshot(AlphaRoot);
        var index = ReversePrereqIndex.Build(catalog);
        var partial = StateResolver.ResolveDependents(previous, [AlphaRoot], index, catalog, after, EvalContext.Default);
        var full = Resolve(catalog, after);

        Assert.True(partial[BetaMsq].IsOtherPath);
        Assert.Equal(full.ToDictionary(p => p.Key, p => (p.Value.State, p.Value.IsSpareAlternative)), partial.ToDictionary(p => p.Key, p => (p.Value.State, p.Value.IsSpareAlternative)));
    }

    [Fact]
    public void The_position_the_shield_and_the_path_walk_skip_the_other_city()
    {
        var catalog = World();
        var states = Resolve(catalog, Snapshot(AlphaRoot));

        var position = MsqProgress.Compute(catalog, states)!;
        Assert.Equal(AlphaMsq, position.Next!.RowId);
        Assert.Equal(3, position.Total); // Alpha Story, Meeting, Onward

        var mask = SpoilerMask.Build(catalog, states, SpoilerOptions.Default with { Ahead = 0 });
        Assert.False(mask.IsMasked(BetaMsq));
        Assert.True(mask.IsMasked(Onward));

        var path = PathFinder.PathTo(Onward, catalog, states);
        Assert.Equal([AlphaRoot, AlphaMsq, Meeting, Onward], path.Select(s => s.RowId));
        Assert.Empty(PathFinder.Alternatives(path, catalog, states));

        var withBeta = Resolve(catalog, Snapshot());
        Assert.NotEmpty(PathFinder.Alternatives(PathFinder.PathTo(Onward, catalog, withBeta), catalog, withBeta));
    }

    [Fact]
    public void Ipc_answers_foreclosed_with_the_path_line_and_the_other_paths_node_lists_it()
    {
        var catalog = World();
        var states = Resolve(catalog, Snapshot(AlphaRoot));
        var view = new IpcView(catalog, states, BlockerNames.Default);

        Assert.Equal("Foreclosed", view.State(BetaMsq));
        Assert.Equal("Locked out", view.StateName(BetaMsq));
        Assert.False(view.IsQuestAvailable(BetaMsq));
        Assert.Equal(["Locked out · Another city's start (Beta)", "OtherPath: unmet (StartCity Beta, chosen Alpha; by 65600 Coming to Alpha)", "Level: met (1 ≤ 50)", "PreviousQuests: unmet (65601 Coming to Beta: not done)"], view.Blockers(BetaMsq));
        Assert.Equal(AlphaMsq, view.MsqNext());

        var rows = QuestQuery.Apply(catalog, states, new FilterSet(), QuestScope.VirtualOtherPaths, SortSpec.Default, null, QueryContext.Empty);
        Assert.Equal([BetaRoot, BetaMsq], rows.Rows.Select(r => r.Quest.RowId));
        var all = QuestQuery.Apply(catalog, states, new FilterSet(), QuestScope.None, SortSpec.Default, null, QueryContext.Empty);
        Assert.DoesNotContain(all.Rows, r => r.Quest.RowId == BetaMsq);
        var included = QuestQuery.Apply(catalog, states, new FilterSet { IncludeOtherPaths = true }, QuestScope.None, SortSpec.Default, null, QueryContext.Empty);
        Assert.Contains(included.Rows, r => r.Quest.RowId == BetaMsq);

        var counts = TreeCounts.Compute(catalog, states, includeUnlisted: false);
        Assert.Equal(new PathTally(2, 0, 0, 0), counts.OtherPathsNode);
        Assert.Equal("1 on other paths: another city's start 1", PathText.Tally(counts.OtherPathsIn(QuestScope.Section(0))));
        Assert.Equal(1, counts.Section(0).OtherPaths);
    }
}
