using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Tests.Data;
using static Tsukimichi.Tests.Query.QueryTestData;

namespace Tsukimichi.Tests.Query;

/// <summary>
/// P14 regression over the frozen catalog: today's main scenario must give exactly the position the 0.9.0 journal
/// walk gave (<see cref="LegacyPosition"/>, kept here verbatim as the reference), and exactly the spoiler mask the
/// 0.9.0 shield built (<see cref="LegacyMask"/>), whatever the character completed.
/// </summary>
public class MsqGraphFixtureTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>
{
    private QuestCatalog Catalog => fixture.Bundle.Catalog;

    /// <summary>MsqProgress.Compute as shipped in 0.9.0: the first quest in journal order neither completed nor foreclosed.</summary>
    private static MsqPosition? LegacyPosition(QuestCatalog catalog, IReadOnlyDictionary<uint, QuestState> states)
    {
        QuestRecord? next = null;
        var nextState = QuestState.Completed;
        var done = 0;
        var total = 0;
        var any = false;
        foreach (var section in MsqProgress.MainScenarioSections)
        {
            if (catalog.BySection.GetValueOrDefault(section) is not { } quests)
            {
                continue;
            }

            foreach (var quest in quests)
            {
                if (quest.IsRemoved)
                {
                    continue;
                }

                any = true;
                var state = states.GetValueOrDefault(quest.RowId, QuestState.Unknown);
                if (state == QuestState.Foreclosed)
                {
                    continue;
                }

                total++;
                if (state == QuestState.Completed)
                {
                    done++;
                }
                else if (next is null)
                {
                    next = quest;
                    nextState = state;
                }
            }
        }

        return any ? new MsqPosition(next, nextState, done, total) : null;
    }

    /// <summary>
    /// SpoilerMask.Build as shipped in 0.9.0, over a state map: the position is the first quest in journal order
    /// neither completed nor foreclosed, and a quest more than <paramref name="options"/>' Ahead past it is masked
    /// unless completed or in the journal. Returns the masked row ids and the reach expansion.
    /// </summary>
    private static (HashSet<uint> Masked, byte Reach) LegacyMask(QuestCatalog catalog, IReadOnlyDictionary<uint, QuestState> states, SpoilerOptions options)
    {
        var noStates = states.Count == 0;
        var ahead = noStates ? 0 : options.AheadClamped;
        var masked = new HashSet<uint>();
        byte? reach = null;
        var ordinal = -1;
        int? position = null;
        var any = false;
        foreach (var section in MsqProgress.MainScenarioSections)
        {
            if (catalog.BySection.GetValueOrDefault(section) is not { } quests)
            {
                continue;
            }

            foreach (var quest in quests)
            {
                if (quest.IsRemoved)
                {
                    continue;
                }

                any = true;
                var state = states.GetValueOrDefault(quest.RowId, QuestState.Unknown);
                if (state != QuestState.Foreclosed)
                {
                    ordinal++;
                    if (position is null && state != QuestState.Completed)
                    {
                        position = ordinal;
                        reach = quest.Expansion;
                    }
                }

                if (!options.HideNames || position is not { } at || ordinal - at <= ahead || state is QuestState.Completed or QuestState.Accepted)
                {
                    continue;
                }

                masked.Add(quest.RowId);
            }
        }

        return (masked, !any ? byte.MaxValue : noStates ? (byte)0 : reach ?? byte.MaxValue);
    }

    /// <summary>The mask built today equals the 0.9.0 mask for these states, at 0, 3 and 10 quests ahead.</summary>
    private void AssertLegacyMask(List<QuestRecord> story, Dictionary<uint, QuestState> states, string label)
    {
        foreach (var ahead in new[] { 0, SpoilerOptions.DefaultAhead, SpoilerOptions.MaxAhead })
        {
            var options = SpoilerOptions.Default with { Ahead = ahead };
            var (expected, reach) = LegacyMask(Catalog, states, options);
            var actual = SpoilerMask.Build(Catalog, states, options);

            var differ = story.Where(q => expected.Contains(q.RowId) != actual.IsMasked(q.RowId)).Select(q => q.RowId).ToList();
            Assert.True(differ.Count == 0, $"{label}, {ahead} ahead: masked differently from 0.9.0 for {string.Join(", ", differ.Take(5))}");
            Assert.Equal(expected.Count, actual.MaskedCount);
            Assert.Equal(reach, actual.ReachExpansion);
        }
    }

    private List<QuestRecord> Story() => MsqGraph.For(Catalog).Story.ToList();

    /// <summary>The first <paramref name="count"/> story quests completed, the next Ready, the rest Blocked; the Grand Company not taken foreclosed.</summary>
    private static Dictionary<uint, QuestState> Prefix(List<QuestRecord> story, int count, bool foreclose)
    {
        var states = new Dictionary<uint, QuestState>(story.Count);
        for (var i = 0; i < story.Count; i++)
        {
            states[story[i].RowId] = i < count ? QuestState.Completed : i == count ? QuestState.Ready : QuestState.Blocked;
        }

        if (foreclose)
        {
            // The Maelstrom taken: the Twin Adder and Immortal Flames lines are locked out.
            foreach (var rowId in new uint[] { 66216, 66219, 66218, 66221 })
            {
                states[rowId] = QuestState.Foreclosed;
            }
        }

        return states;
    }

    [Fact]
    public void Every_completion_prefix_gives_the_old_single_position()
    {
        var story = Story();
        Assert.True(story.Count > 1000, "the fixture holds the whole main scenario");
        for (var count = 0; count <= story.Count; count++)
        {
            foreach (var foreclose in new[] { false, true })
            {
                var states = Prefix(story, count, foreclose);
                var expected = LegacyPosition(Catalog, states);
                var actual = MsqProgress.Compute(Catalog, states);

                Assert.Equal(expected, actual);
                Assert.False(actual!.IsBranched, $"prefix {count}: today's data is never reported route by route");
                Assert.Equal(expected!.Next is { } next ? [next] : Array.Empty<QuestRecord>(), actual.Positions);
                Assert.Equal(actual, MsqProgress.Compute(Catalog, Evaluations(states)));
            }
        }
    }

    [Fact]
    public void Random_completion_sets_give_the_old_single_position()
    {
        var story = Story();
        var random = new Random(8_0);
        var pick = new[] { QuestState.Completed, QuestState.Completed, QuestState.Completed, QuestState.Ready, QuestState.Blocked, QuestState.Accepted, QuestState.Foreclosed, QuestState.Unknown };
        for (var run = 0; run < 300; run++)
        {
            var states = new Dictionary<uint, QuestState>();
            var cut = random.Next(story.Count);
            for (var i = 0; i < story.Count; i++)
            {
                // Mostly done before the cut, mostly not after it, with noise either side.
                states[story[i].RowId] = random.Next(10) == 0 ? pick[random.Next(pick.Length)] : i < cut ? QuestState.Completed : QuestState.Blocked;
            }

            Assert.Equal(LegacyPosition(Catalog, states), MsqProgress.Compute(Catalog, states));
        }
    }

    [Fact]
    public void Every_completion_prefix_gives_the_old_spoiler_mask()
    {
        var story = Story();
        for (var count = 0; count <= story.Count; count++)
        {
            AssertLegacyMask(story, Prefix(story, count, foreclose: false), $"prefix {count}");
            AssertLegacyMask(story, Prefix(story, count, foreclose: true), $"prefix {count}, Maelstrom");
        }

        AssertLegacyMask(story, [], "no states");
    }

    [Fact]
    public void Random_completion_sets_give_the_old_spoiler_mask()
    {
        var story = Story();
        var random = new Random(7);
        var pick = new[] { QuestState.Completed, QuestState.Ready, QuestState.Blocked, QuestState.Accepted, QuestState.Foreclosed, QuestState.Unknown };
        for (var run = 0; run < 300; run++)
        {
            var states = new Dictionary<uint, QuestState>();
            var cut = random.Next(story.Count);
            for (var i = 0; i < story.Count; i++)
            {
                states[story[i].RowId] = random.Next(6) == 0 ? pick[random.Next(pick.Length)] : i < cut ? QuestState.Completed : QuestState.Blocked;
            }

            AssertLegacyMask(story, states, $"run {run}");
        }
    }

    [Fact]
    public void Todays_branch_regions_are_found_but_walked_in_journal_order()
    {
        var graph = MsqGraph.For(Catalog);

        Assert.NotEmpty(graph.Branches);
        Assert.All(graph.Branches, b => Assert.False(b.IsRouted));
        Assert.All(graph.Story, q => Assert.Null(graph.RoutedBranchOf(q.RowId)));

        // Shadowbringers: Travelers of Norvrandt splits into the Alphinaud and Alisaie lines, nine quests each, that
        // meet at The Lightwardens (both needed).
        var norvrandt = Assert.Single(graph.Branches, b => b.Start.RowId == 68817);
        Assert.Equal(68836u, norvrandt.Join.RowId);
        Assert.Equal(JoinKind.All, norvrandt.JoinKind);
        Assert.Equal(["In Search of Alphinaud", "In Search of Alisaie"], norvrandt.Routes.Select(r => r.Name));
        Assert.All(norvrandt.Routes, r => Assert.Equal(9, r.Quests.Count));

        // Dawntrail: The Rite of Succession to Kozama'uka (6) and Urqopacha (7), meeting at The Success of Others.
        var succession = Assert.Single(graph.Branches, b => b.Start.RowId == 70400);
        Assert.Equal(70414u, succession.Join.RowId);
        Assert.Equal([6, 7], succession.Routes.Select(r => r.Quests.Count));

        // The Grand Company choice is an Any join: one company's two quests open Sylph-management.
        var company = Assert.Single(graph.Branches, b => b.Start.RowId == 66047);
        Assert.Equal(JoinKind.Any, company.JoinKind);
        Assert.Equal(3, company.Routes.Count);
    }

    [Fact]
    public void Previewing_todays_regions_as_routes_reports_the_shadowbringers_lines()
    {
        // With the gate lowered to A Realm Reborn every region is routed: what Evercold's routes will look like,
        // shown on the Alphinaud and Alisaie lines.
        var graph = MsqGraph.Build(Catalog, routedFromExpansion: 0);
        var story = graph.Story.ToList();
        var fork = story.FindIndex(q => q.RowId == 68817);
        var states = Prefix(story, fork + 4, foreclose: true);

        var position = graph.Position(states);

        Assert.NotNull(position);
        Assert.True(position.IsBranched);
        Assert.Equal(68836u, position.Branch?.Join.RowId);
        Assert.Equal(LegacyPosition(Catalog, states)?.Next, position.Next);
        Assert.Equal([(3, 9, MsqRouteStatus.InProgress), (0, 9, MsqRouteStatus.NotStarted)], position.Routes.Select(r => (r.Done, r.Total, r.Status)));
        Assert.Equal([68821u, 68827u], position.Positions.Select(q => q.RowId));
        Assert.Equal(2, position.RoutesToJoin);
    }
}
