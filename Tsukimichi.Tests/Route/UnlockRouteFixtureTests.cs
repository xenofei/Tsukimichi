using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Route;
using Tsukimichi.Core.Storage;
using Tsukimichi.Tests.Data;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Route;

/// <summary>
/// <see cref="UnlockRoute"/> over the frozen catalog (feature plan v3 P6): the cases from the player research
/// (docs/research/player-gripes-2026.md §3 P6), a fresh alt rushing Blue Mage, a duty deep in Heavensward, a Moonlit
/// reward, and a target already done.
/// </summary>
public class UnlockRouteFixtureTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>
{
    private const uint OutOfTheBlue = 68728;
    private const uint TheUltimateWeapon = 70058;
    private const uint HeartsOnFire = 69408;
    private const uint RockTheCastrum = 69409;
    private const uint BeforeTheDawn = 65964;
    private const uint ToTheBannock = 65564;

    private const uint ComingToIshgard = 67116;
    private const uint LitanyOfPeace = 67783;
    private const uint NidhoggsRage = 67825;
    private const uint NidhoggsRageInstance = 20040;

    private const uint GridanianEnvoy = 66043;
    private const uint ThwackAMole = 67021;
    private const uint DoingTheDirtyWork = 66343;
    private const uint SpiritsMostFoul = 67067;

    private QuestCatalog Catalog => fixture.Bundle.Catalog;

    private EvalContext Context => new() { ClassJobs = fixture.Bundle.Jobs };

    /// <summary>Every quest on the path to <paramref name="rowId"/> as the path finder walks it with nothing done.</summary>
    private uint[] StoryThrough(uint rowId) =>
        PathFinder.PathTo(rowId, Catalog, new Dictionary<uint, QuestEvaluation>()).Select(s => s.RowId).ToArray();

    /// <summary>A level 50 Gladiator who has done <paramref name="completed"/>, resolved.</summary>
    private (CharacterSnapshot Snapshot, Dictionary<uint, QuestEvaluation> States) Alt(params uint[] completed)
    {
        var snapshot = Fixture.Snapshot(completed);
        return (snapshot, StateResolver.ResolveAll(Catalog, snapshot, Context));
    }

    private UnlockRoute Build(RouteTarget target, (CharacterSnapshot Snapshot, Dictionary<uint, QuestEvaluation> States) alt) =>
        UnlockRoute.Build(target, Catalog, alt.States, fixture.Bundle.BlockerNames(), RouteLevels.For(alt.Snapshot, Context));

    private static RouteTarget BlueMage => RouteTarget.ForJob("Blue Mage", OutOfTheBlue);

    [Fact]
    public void Blue_Mage_from_a_fresh_level_50_alt_after_ARR_is_the_unlock_quest_alone()
    {
        var job = fixture.Bundle.Names.ClassJobInfo(36)!;
        Assert.Equal("Blue Mage", job.Name);
        Assert.Equal(OutOfTheBlue, job.UnlockQuestRowId);

        var route = Build(RouteTarget.ForJob(job.ToLadderJob()), Alt(StoryThrough(BeforeTheDawn)));

        Assert.Equal(RouteOutcome.Route, route.Outcome);
        Assert.Equal(RouteTargetKind.Job, route.Target.Kind);
        var step = Assert.Single(route.Steps);
        Assert.Equal(OutOfTheBlue, step.RowId);
        Assert.True(step.IsTarget);
        Assert.Equal(50, step.LevelGate);
        Assert.True(step.LevelMet);
        Assert.Null(step.Milestone);
        Assert.Equal("Ready", step.StatusText);
        Assert.Equal("1 quest · Lv 50", route.Summary.Text);
    }

    [Fact]
    public void Blue_Mage_from_an_alt_three_quests_short_of_the_end_of_ARR_crosses_one_milestone()
    {
        var story = StoryThrough(TheUltimateWeapon);
        Assert.Equal([HeartsOnFire, RockTheCastrum, TheUltimateWeapon], story[^3..]);

        var route = Build(BlueMage, Alt(story[..^3]));

        Assert.Equal([HeartsOnFire, RockTheCastrum, TheUltimateWeapon, OutOfTheBlue], route.Steps.Select(s => s.RowId));
        var milestone = Assert.Single(route.Summary.Milestones);
        Assert.Equal("Seventh Umbral Era", milestone.Name);
        Assert.Equal(TheUltimateWeapon, milestone.LastRowId);
        Assert.All(route.Steps.Take(3), s => Assert.Same(milestone, s.Milestone));
        Assert.Null(route.Steps[^1].Milestone);
        Assert.All(route.Steps.Take(3), s => Assert.True(s.IsMainScenario));
        Assert.Equal("Ready", route.Steps[0].StatusText);
        Assert.Equal("Blocked · after MSQ: Hearts on Fire", route.Steps[1].StatusText);
        Assert.Equal("4 quests · Lv 49–50 · MSQ: Seventh Umbral Era", route.Summary.Text);
    }

    [Fact]
    public void Blue_Mage_from_a_brand_new_character_walks_all_of_ARR_in_a_valid_order()
    {
        var alt = Alt();
        var route = Build(BlueMage, alt);

        Assert.Equal(OutOfTheBlue, route.Steps[^1].RowId);
        Assert.True(route.Steps.Count > 150, $"expected the whole 2.0 story, found {route.Steps.Count}");
        AssertValidOrder(route, alt.States);

        // The starting cities' first quests meet at To the Bannock: the route takes one and lists the other two.
        var bannock = Assert.Single(route.Steps, s => s.RowId == ToTheBannock);
        Assert.Equal(2, bannock.Alternatives.Count);
        Assert.All(bannock.Alternatives, a => Assert.DoesNotContain(route.Steps, s => s.RowId == a.RowId));
        Assert.Equal("Seventh Umbral Era", Assert.Single(route.Summary.Milestones).Name);
    }

    [Fact]
    public void A_duty_deep_in_Heavensward_routes_through_the_Heavensward_and_Dragonsong_stories()
    {
        var target = RouteTarget.ForDuty(Catalog, RewardKind.Instance, NidhoggsRageInstance, "the Minstrel's Ballad: Nidhogg's Rage");
        Assert.Equal([NidhoggsRage], target.QuestRowIds);

        var alt = Alt(StoryThrough(BeforeTheDawn));
        var route = Build(target, alt);

        Assert.Equal(RouteOutcome.Route, route.Outcome);
        Assert.Equal(ComingToIshgard, route.Steps[0].RowId);
        Assert.Equal("Ready", route.Steps[0].StatusText);
        Assert.Equal(LitanyOfPeace, route.Steps[^2].RowId);
        Assert.Equal(NidhoggsRage, route.Steps[^1].RowId);
        Assert.True(route.Steps[^1].IsTarget);
        AssertValidOrder(route, alt.States);

        // Nothing done since ARR, so the route is exactly the path finder's remaining quests.
        Assert.Equal(PathFinder.RemainingCount(NidhoggsRage, Catalog, alt.States), route.Steps.Count);
        Assert.Equal(["Heavensward", "Dragonsong"], route.Summary.Milestones.Select(m => m.Name));
        Assert.Equal($"{route.Steps.Count} quests · Lv 50–60 · MSQ: Heavensward, Dragonsong", route.Summary.Text);

        // Level gates: the first step asks for 50, which the alt has; every later gate asks for more and is not met yet.
        var gates = route.Steps.Where(s => s.LevelGate > 0).ToList();
        Assert.Equal(50, gates[0].LevelGate);
        Assert.True(gates[0].LevelMet);
        Assert.True(gates.Count > 1);
        Assert.All(gates.Skip(1), g => Assert.False(g.LevelMet));
        Assert.Equal(gates.Select(g => (int)g.LevelGate).Order(), gates.Select(g => (int)g.LevelGate));
    }

    [Fact]
    public void A_Moonlit_reward_routes_to_the_quest_that_grants_it_in_level_order()
    {
        var unique = UniqueRewardsFile.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), "unique_quests.json"));
        var entry = unique.Entries.Single(e => e.Kind == RewardKind.Minion && e.RewardName == "Mini Mole");
        Assert.Equal(ThwackAMole, entry.QuestRowId);

        var target = RouteTarget.ForReward(entry, unique.Entries);
        Assert.Equal(RouteTargetKind.Reward, target.Kind);
        Assert.Equal("Mini Mole", target.Label);

        var alt = Alt(StoryThrough(BeforeTheDawn));
        var route = Build(target, alt);

        // Thwack-a-Mole needs two chains; the level 29 one comes first, then the level 50 one, then the reward quest.
        AssertValidOrder(route, alt.States);
        Assert.Equal(ThwackAMole, route.Steps[^1].RowId);
        var dirtyWork = route.Steps.ToList().FindIndex(s => s.RowId == DoingTheDirtyWork);
        var spirits = route.Steps.ToList().FindIndex(s => s.RowId == SpiritsMostFoul);
        Assert.InRange(dirtyWork, 0, spirits - 1);
        Assert.Equal(route.Steps.Select(s => (int)s.Level).Order(), route.Steps.Select(s => (int)s.Level));
        Assert.Empty(route.Summary.Milestones);
    }

    [Fact]
    public void A_reward_several_quests_hand_out_routes_to_the_nearest_and_lists_the_others()
    {
        var unique = UniqueRewardsFile.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), "unique_quests.json"));
        var airship = unique.Entries.First(e => e.RewardName == "Wind-up Airship");
        var target = RouteTarget.ForReward(airship, unique.Entries);
        Assert.Equal(3, target.QuestRowIds.Count);

        var route = Build(target, Alt());
        Assert.Equal(GridanianEnvoy, route.TargetRowId);
        Assert.Equal(2, route.TargetAlternatives.Count);

        // Done on an alt that finished ARR: nothing to do.
        Assert.Equal(RouteOutcome.AlreadyDone, Build(target, Alt(StoryThrough(BeforeTheDawn))).Outcome);
    }

    [Fact]
    public void A_completed_target_yields_an_empty_route()
    {
        var route = Build(BlueMage, Alt([.. StoryThrough(TheUltimateWeapon), OutOfTheBlue]));

        Assert.Equal(RouteOutcome.AlreadyDone, route.Outcome);
        Assert.Equal(OutOfTheBlue, route.TargetRowId);
        Assert.Empty(route.Steps);
        Assert.Equal(0, route.Summary.Count);
        Assert.Equal("Nothing left to do", route.Summary.Text);
        Assert.Equal("**Route to Blue Mage** · already unlocked", RouteMarkdown.Write(route, Catalog, q => q.Name));
    }

    /// <summary>Every step comes after each previous quest it still needs (All: every one; Any: one, unless one is completed).</summary>
    private void AssertValidOrder(UnlockRoute route, IReadOnlyDictionary<uint, QuestEvaluation> states)
    {
        var index = new Dictionary<uint, int>();
        for (var i = 0; i < route.Steps.Count; i++)
        {
            index.Add(route.Steps[i].RowId, i);
        }

        bool Done(uint id) => states.TryGetValue(id, out var e) && e.State == QuestState.Completed;

        for (var i = 0; i < route.Steps.Count; i++)
        {
            var quest = Catalog.GetByRowId(route.Steps[i].RowId)!;
            Assert.False(Done(quest.RowId), quest.Name);
            var prereqs = quest.PreviousQuests.QuestIds.Where(id => id != quest.RowId && Catalog.ByRowId.ContainsKey(id)).ToArray();
            if (prereqs.Length == 0)
            {
                continue;
            }

            if (quest.PreviousQuests.Join == JoinKind.All)
            {
                Assert.All(prereqs, p => Assert.True(Done(p) || (index.TryGetValue(p, out var at) && at < i), $"{quest.Name} before its prerequisite {p}"));
            }
            else
            {
                Assert.True(prereqs.Any(p => Done(p) || (index.TryGetValue(p, out var at) && at < i)), $"{quest.Name} before any of its prerequisites");
            }
        }
    }
}
