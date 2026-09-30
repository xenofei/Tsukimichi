using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// Story sidequests over the frozen catalog with the shipped curated and unique-reward data, the unlock set derived
/// the way the plugin derives it: the counts, the size of every side story, one known story and the Dawntrail zone
/// shape players describe ("every region has two mini storylines … once you do both you unlock an extra one").
/// </summary>
public class StorySidequestsFixtureTests(FixtureCatalog fixture, ITestOutputHelper output) : IClassFixture<FixtureCatalog>
{
    // Kozama'uka: two four-quest lines, each opened by an unlock (aether current) quest, joined by a ninth.
    private const uint RiteOfTheWindsChosen = 70617;
    private const uint AllGoodPotpacts = 70621;
    private const uint LandsguardsNewClothes = 70625;

    // Urqopacha: the same shape, every quest of it an unlock quest.
    private const uint ACrisisOfCorruption = 70587;
    private const uint BrainsAndBrawn = 70595;

    private QuestCatalog Catalog => fixture.Bundle.Catalog;

    private IReadOnlySet<uint> Features()
    {
        var unique = UniqueRewardsFile.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), "unique_quests.json"));
        return FeaturePresets.Derive(Catalog, fixture.Curated, unique.Entries);
    }

    [Fact]
    public void Fixture_counts_story_sidequests_and_side_stories()
    {
        var stories = StorySidequests.Build(Catalog, Features());
        var chained = stories.Chains.Sum(c => c.RowIds.Count);
        var sizes = stories.Chains.GroupBy(c => c.RowIds.Count).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count());
        output.WriteLine($"{stories.Count} story sidequests, {stories.Chains.Count} side stories holding {chained}, {stories.Count - chained} alone");
        output.WriteLine("sizes: " + string.Join(", ", sizes.Select(kv => $"{kv.Key}×{kv.Value}")));

        Assert.Equal(282, stories.Count);
        Assert.Equal(36, stories.Chains.Count);
        Assert.Equal(221, chained);
        Assert.Equal(
            new Dictionary<int, int> { [2] = 4, [3] = 5, [4] = 3, [5] = 4, [7] = 9, [8] = 5, [9] = 2, [10] = 1, [11] = 1, [12] = 2 },
            sizes);

        Assert.All(stories.RowIds, id =>
        {
            var quest = Catalog.ByRowId[id];
            Assert.False(quest.IsRemoved);
            Assert.False(quest.IsRepeatable);
            Assert.NotEqual(0u, quest.Icon);
            Assert.Equal(StorySidequests.SidequestSectionId, quest.Journal.SectionId);
        });
        Assert.All(stories.Chains, chain => Assert.StartsWith(StorySidequests.ChainNamePrefix, chain.Name, StringComparison.Ordinal));
    }

    [Fact]
    public void Kozamauka_story_is_two_lines_and_the_quest_that_joins_them()
    {
        var features = Features();
        var stories = StorySidequests.Build(Catalog, features);

        var chain = stories.ChainOf(LandsguardsNewClothes);
        Assert.NotNull(chain);
        Assert.Equal(StorySidequests.ChainNamePrefix + Catalog.ByRowId[70618].Name, chain.Name);
        Assert.Equal(new uint[] { 70618, 70619, 70620, 70622, 70623, 70624, 70625 }, chain.RowIds);

        // The last quest requires the end of both lines.
        Assert.Equal(new uint[] { 70620, 70624 }, Catalog.ByRowId[LandsguardsNewClothes].PreviousQuests.QuestIds.Order());

        // Each line opens with an unlock quest the story leaves out.
        Assert.Contains(RiteOfTheWindsChosen, features);
        Assert.Contains(AllGoodPotpacts, features);
        Assert.False(stories.Contains(RiteOfTheWindsChosen));
        Assert.Equal([RiteOfTheWindsChosen], Catalog.ByRowId[70618].PreviousQuests.QuestIds);
        Assert.Equal([AllGoodPotpacts], Catalog.ByRowId[70622].PreviousQuests.QuestIds);

        // The detail pane's chain line reaches it through the chain catalog.
        var chains = ChainCatalog.Build(Catalog, fixture.Curated, stories);
        Assert.Same(chain, chains.ForQuest(70622));
    }

    [Fact]
    public void Dawntrail_zones_have_two_lines_then_a_third_quest_but_unlocks_keep_some_out()
    {
        var features = Features();
        var stories = StorySidequests.Build(Catalog, features);

        // Shaaloani: the same 3 + 3 + 1 once the two unlock openers are left out.
        var shaaloani = stories.ChainOf(70684);
        Assert.NotNull(shaaloani);
        Assert.Equal(new uint[] { 70677, 70678, 70679, 70681, 70682, 70683, 70684 }, shaaloani.RowIds);
        Assert.Equal(new uint[] { 70679, 70683 }, Catalog.ByRowId[70684].PreviousQuests.QuestIds.Order());

        // Urqopacha has the shape too (70587–70590, 70591–70594, then 70595 requiring both), but every quest in it is an
        // unlock quest drawn blue, so none of it is a story sidequest.
        Assert.Equal(new uint[] { 70590, 70594 }, Catalog.ByRowId[BrainsAndBrawn].PreviousQuests.QuestIds.Order());
        for (var id = ACrisisOfCorruption; id <= BrainsAndBrawn; id++)
        {
            Assert.Contains(id, features);
            Assert.False(stories.Contains(id));
        }
    }

    [Fact]
    public void Play_order_puts_a_quest_after_what_it_requires()
    {
        var stories = StorySidequests.Build(Catalog, Features());

        // Delivery Moogle: Carline Memories (67018) requires A Debt Unpaid (67019), which the journal lists after it.
        var moogle = stories.ChainOf(67018);
        Assert.NotNull(moogle);
        Assert.True(ChainCatalog.IndexOf(moogle, 67019) < ChainCatalog.IndexOf(moogle, 67018));

        foreach (var chain in stories.Chains)
        {
            for (var i = 0; i < chain.RowIds.Count; i++)
            {
                foreach (var previous in Catalog.ByRowId[chain.RowIds[i]].PreviousQuests.QuestIds)
                {
                    var at = ChainCatalog.IndexOf(chain, previous);
                    Assert.True(at < i, $"{chain.Name}: {chain.RowIds[i]} comes before its prerequisite {previous}");
                }
            }
        }
    }
}
