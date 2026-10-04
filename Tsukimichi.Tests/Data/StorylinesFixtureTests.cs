using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Unlocks;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// 1.21.0 storylines over the frozen 2026.09.15 catalog and the shipped curated data: the named side stories and Caught
/// up (P5), Loose ends with the yellow finales (N8), and the shield's "past the story point" for side quests.
/// </summary>
public class StorylinesFixtureTests(FixtureCatalog fixture, ITestOutputHelper output) : IClassFixture<FixtureCatalog>
{
    private const uint SmallBusinessBigDreams = 70119; // Tataru's Grand Endeavor, after Newfound Adventure (Endwalker)
    private const uint KeepingTheLedger = 67405;       // Scholasticate Quests (Heavensward)
    private const uint CornOfNecessity = 70556;
    private const uint WhenDeathComesToDinner = 70843;
    private const uint AHarmonyFromTheHeavens = 68750; // the Bard Quests finale, plain side-quest marker
    private const uint LaidToRest = 69661;             // the Physical Ranged DPS Role Quests (Endwalker) finale, blue
    private const uint OneFinalJourney = 69286;        // Tales from the Shadows: one quest
    private const uint StormbloodQuest = 68089;        // the last main scenario quest of Stormblood 4.0

    private QuestCatalog Catalog => fixture.Bundle.Catalog;

    private ChainCatalog Chains => ChainCatalog.Build(Catalog, fixture.Curated);

    private static Dictionary<uint, QuestEvaluation> Evaluations(IEnumerable<(uint RowId, QuestState State)> states) =>
        states.ToDictionary(s => s.RowId, s => new QuestEvaluation(s.State, [], null, null, null));

    [Fact]
    public void Every_curated_line_resolves_and_lines_by_quest_are_connected()
    {
        var chains = Chains;
        Assert.Empty(chains.Warnings);
        foreach (var entry in fixture.Curated.Chains)
        {
            var chain = chains.Chains.Single(c => c.Name == entry.Name);
            Assert.True(chain.IsCurated);
            Assert.Equal(entry.Ongoing, chain.Ongoing);
            output.WriteLine($"{chain.Name}: {chain.RowIds.Count} quests{(chain.Ongoing ? ", ongoing" : string.Empty)}");
            if (entry.StartQuest == 0 && entry.QuestIds.Count == 0)
            {
                continue;
            }

            // Connectedness: every quest after the first requires a quest before it in the line.
            for (var i = 1; i < chain.RowIds.Count; i++)
            {
                var quest = Catalog.GetByRowId(chain.RowIds[i])!;
                var earlier = chain.RowIds.Take(i).ToHashSet();
                Assert.True(quest.PreviousQuests.QuestIds.Any(earlier.Contains), $"{chain.Name}: {quest.Name} does not follow from the line");
            }
        }
    }

    [Fact]
    public void Named_side_stories_take_the_players_names_and_the_series_still_running_are_ongoing()
    {
        var chains = Chains;
        var tataru = chains.ForQuest(SmallBusinessBigDreams);
        Assert.NotNull(tataru);
        Assert.Equal("Tataru's Grand Endeavor", tataru.Name);
        Assert.Equal(7, tataru.RowIds.Count);

        var corn = chains.ForQuest(CornOfNecessity);
        Assert.NotNull(corn);
        Assert.Equal("Cornservant", corn.Name);
        Assert.Equal(8, corn.RowIds.Count);
        Assert.Equal(CornOfNecessity, corn.RowIds[0]);
        Assert.Equal(WhenDeathComesToDinner, corn.RowIds[^1]);
        Assert.Same(corn, chains.ForQuest(WhenDeathComesToDinner));

        Assert.True(chains.Chains.Single(c => c.Name == "Hildibrand").Ongoing);
        Assert.True(chains.Chains.Single(c => c.Name == "Cosmic Exploration Main Quests").Ongoing);
        Assert.True(chains.Chains.Single(c => c.Name == "The Occult Crescent").Ongoing);
        Assert.False(tataru.Ongoing);
    }

    [Fact]
    public void An_ongoing_series_done_so_far_is_caught_up_and_a_finished_line_folds()
    {
        var chains = Chains;
        var hildibrand = chains.Chains.Single(c => c.Name == "Hildibrand");
        var tataru = chains.ForQuest(SmallBusinessBigDreams)!;
        var states = Evaluations(hildibrand.RowIds.Concat(tataru.RowIds).Select(id => (id, QuestState.Completed)));

        var rows = SideStories.Build(chains, states, _ => false);

        Assert.Equal(SideStoryStatus.CaughtUp, rows.Single(r => r.Chain == hildibrand).Status);
        Assert.Equal(SideStoryStatus.Finished, rows.Single(r => r.Chain == tataru).Status);
        // Finished lines come last, after every line with something left and the caught-up ones.
        Assert.Equal(SideStoryStatus.Finished, rows[^1].Status);
        Assert.All(rows, r => Assert.True(r.Chain.IsCurated));
    }

    [Fact]
    public void Loose_ends_put_the_yellow_finale_first_then_the_blue_one_then_the_fewest_left()
    {
        var lines = LooseEnds.Lines(Catalog, Chains);
        var bard = lines.Single(l => l.Chain.RowIds.Contains(AHarmonyFromTheHeavens));
        var ranged = lines.Single(l => l.Chain.RowIds.Contains(LaidToRest));
        var tataru = lines.Single(l => l.Chain.RowIds.Contains(SmallBusinessBigDreams));
        Assert.Equal(StoryLineKind.Job, bard.Kind);
        Assert.Equal(StoryLineKind.Role, ranged.Kind);
        Assert.Equal(StoryLineKind.Chain, tataru.Kind);
        Assert.Equal(AHarmonyFromTheHeavens, bard.Chain.RowIds[^1]);
        Assert.Equal(LaidToRest, ranged.Chain.RowIds[^1]);

        var states = new List<(uint, QuestState)>();
        foreach (var line in new[] { bard, ranged })
        {
            states.AddRange(line.Chain.RowIds.Select(id => (id, id == line.Chain.RowIds[^1] ? QuestState.Ready : QuestState.Completed)));
        }

        // Tataru's: four of seven done, three left.
        states.AddRange(tataru.Chain.RowIds.Select((id, i) => (id, i < 4 ? QuestState.Completed : QuestState.Blocked)));

        var ends = LooseEnds.Find(lines, Evaluations(states), Catalog);

        Assert.Equal(3, ends.Count);
        Assert.Equal(AHarmonyFromTheHeavens, ends[0].Next.RowId);
        Assert.True(ends[0].FinaleMarkedYellow);
        Assert.Equal(LaidToRest, ends[1].Next.RowId);
        Assert.False(ends[1].FinaleMarkedYellow);
        Assert.True(ends[1].IsReadyFinale);
        Assert.Same(tataru, ends[2].Line);
        Assert.Equal(3, ends[2].Left);
    }

    [Fact]
    public void A_one_quest_line_is_never_a_loose_end()
    {
        // Tales from the Shadows is One Final Journey alone (it needs only the main scenario): never started and unfinished.
        var lines = LooseEnds.Lines(Catalog, Chains);
        var shadows = lines.Single(l => l.Chain.Name == "Tales from the Shadows");
        Assert.Equal([OneFinalJourney], shadows.Chain.RowIds);
        foreach (var state in new[] { QuestState.Ready, QuestState.Accepted, QuestState.Completed })
        {
            Assert.Empty(LooseEnds.Find([shadows], Evaluations([(OneFinalJourney, state)]), Catalog));
        }
    }

    [Fact]
    public void A_side_quest_whose_story_anchor_is_masked_lies_ahead()
    {
        var graph = MsqGraph.For(Catalog);
        var states = new Dictionary<uint, QuestState>();
        foreach (var quest in graph.Story)
        {
            states[quest.RowId] = QuestState.Completed;
            if (quest.RowId == StormbloodQuest)
            {
                break;
            }
        }

        Assert.True(states.ContainsKey(StormbloodQuest));
        var mask = SpoilerMask.Build(Catalog, states, SpoilerOptions.Default, names: SpoilerNames.Build(Catalog, QuestUnlocks.Empty));

        Assert.True(mask.IsAhead(SmallBusinessBigDreams));
        Assert.False(mask.IsAhead(KeepingTheLedger));
        Assert.False(SpoilerMask.Build(Catalog, states, SpoilerOptions.Off, names: SpoilerNames.Build(Catalog, QuestUnlocks.Empty)).IsAhead(SmallBusinessBigDreams));

        var rows = SideStories.Build(Chains, Evaluations(states.Select(kv => (kv.Key, kv.Value))), mask.IsAhead);
        Assert.True(rows.Single(r => r.Chain.Name == "Tataru's Grand Endeavor").Ahead);
        Assert.False(rows.Single(r => r.Chain.Name == "Scholasticate Quests").Ahead);
    }
}
