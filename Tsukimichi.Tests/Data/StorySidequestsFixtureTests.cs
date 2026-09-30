using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// Story sidequests over the frozen catalog with the shipped curated and unique-reward data, built the way the plugin
/// builds them (unlock set from <see cref="FeaturePresets.Derive"/>, aether current lines let in): the counts, the
/// size of every side story, one known story and the Dawntrail zone shape players describe ("every region has two
/// mini storylines … once you do both you unlock an extra one").
/// </summary>
public class StorySidequestsFixtureTests(FixtureCatalog fixture, ITestOutputHelper output) : IClassFixture<FixtureCatalog>
{
    /// <summary>
    /// The four Dawntrail field zones with two story lines: the first quest of each four-quest line (it grants an
    /// aether current) and the ninth quest, which requires the last quest of both lines.
    /// </summary>
    public static readonly TheoryData<string, uint, uint, uint> DawntrailZones = new()
    {
        { "Urqopacha", 70587, 70591, 70595 },
        { "Kozama'uka", 70617, 70621, 70625 },
        { "Yak T'el", 70646, 70650, 70654 },
        { "Shaaloani", 70676, 70680, 70684 },
    };

    private QuestCatalog Catalog => fixture.Bundle.Catalog;

    private UniqueRewardsData Unique() => UniqueRewardsFile.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), "unique_quests.json"));

    private (StorySidequests Stories, IReadOnlySet<uint> Features) Build()
    {
        var unique = Unique();
        var features = FeaturePresets.Derive(Catalog, fixture.Curated, unique.Entries);
        return (StorySidequests.Build(Catalog, features, fixture.Curated, unique.Entries), features);
    }

    [Fact]
    public void Fixture_counts_story_sidequests_and_side_stories()
    {
        var (stories, features) = Build();
        var chained = stories.Chains.Sum(c => c.RowIds.Count);
        var blue = stories.RowIds.Count(features.Contains);
        var sizes = stories.Chains.GroupBy(c => c.RowIds.Count).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count());
        output.WriteLine($"{stories.Count} story sidequests ({blue} of them blue aether current line quests), {stories.Chains.Count} side stories holding {chained}, {stories.Count - chained} alone");
        output.WriteLine("sizes: " + string.Join(", ", sizes.Select(kv => $"{kv.Key}×{kv.Value}")));

        // 0.9.0 (P13) curated For All the Nights to Come as the Dusk Vigil's unlock: it grants a Coerthas Western
        // Highlands aether current too, but a dungeon unlock is never a story, so it left the blue lines (was 379 / 97).
        Assert.Equal(378, stories.Count);
        Assert.Equal(96, blue);
        Assert.Equal(44, stories.Chains.Count);
        Assert.Equal(308, chained);
        Assert.Equal(
            new Dictionary<int, int> { [2] = 4, [3] = 1, [4] = 7, [5] = 5, [6] = 1, [7] = 1, [8] = 3, [9] = 18, [10] = 1, [11] = 1, [12] = 2 },
            sizes);

        // The base rule alone (every unlock quest left out): 282 quests in 36 stories.
        var baseRule = StorySidequests.Build(Catalog, features);
        Assert.Equal(282, baseRule.Count);
        Assert.Equal(36, baseRule.Chains.Count);

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
    public void Unlock_quests_other_than_aether_current_lines_stay_out()
    {
        var unique = Unique();
        var (stories, features) = Build();
        var currents = FeaturePresets.AetherCurrentQuests(Catalog, unique.Entries);
        var otherUnlocks = FeaturePresets.NonCurrentUnlockQuests(unique.Entries);

        foreach (var id in stories.RowIds.Where(features.Contains))
        {
            var quest = Catalog.ByRowId[id];
            Assert.True(FeaturePresets.UnlocksOnlyAetherCurrents(quest, fixture.Curated, currents, otherUnlocks), $"{id} {quest.Name}");
            Assert.False(fixture.Curated.DutyUnlocks.ContainsKey(id));
            Assert.False(fixture.Curated.SystemUnlocks.ContainsKey(id));
        }

        // Dungeon unlocks among the sidequests with artwork (Halatali, the Aurum Vale, …) are not stories.
        var dutyUnlocks = Catalog.All.Where(q => q.Journal.SectionId == StorySidequests.SidequestSectionId && q.Icon != 0 && fixture.Curated.DutyUnlocks.ContainsKey(q.RowId)).ToArray();
        Assert.NotEmpty(dutyUnlocks);
        Assert.All(dutyUnlocks, q => Assert.False(stories.Contains(q.RowId), q.Name));
    }

    [Theory]
    [MemberData(nameof(DawntrailZones))]
    public void Dawntrail_zone_is_two_lines_and_the_quest_that_joins_them(string zone, uint firstLine, uint secondLine, uint last)
    {
        var (stories, features) = Build();

        var chain = stories.ChainOf(last);
        Assert.NotNull(chain);
        output.WriteLine($"{zone}: {chain.Name}: {string.Join(", ", chain.RowIds)}");

        // Named after the true first quest, the one that grants the first line's aether current.
        Assert.Equal(StorySidequests.ChainNamePrefix + Catalog.ByRowId[firstLine].Name, chain.Name);
        Assert.Contains(firstLine, features);

        // Four quests of the first line, four of the second, then the one that requires the end of both.
        uint[] expected = [firstLine, firstLine + 1, firstLine + 2, firstLine + 3, secondLine, secondLine + 1, secondLine + 2, secondLine + 3, last];
        Assert.Equal(expected, chain.RowIds);
        Assert.Equal(new[] { firstLine + 3, secondLine + 3 }, Catalog.ByRowId[last].PreviousQuests.QuestIds.Order());
        Assert.All(chain.RowIds, id => Assert.Equal(Catalog.ByRowId[last].Journal.GenreId, Catalog.ByRowId[id].Journal.GenreId));

        // The detail pane's chain line reaches it through the chain catalog.
        var chains = ChainCatalog.Build(Catalog, fixture.Curated, stories);
        Assert.Same(chain, chains.ForQuest(secondLine));
    }

    [Fact]
    public void Shadowbringers_and_Endwalker_zones_stay_whole()
    {
        var (stories, _) = Build();

        // Nine quests each, opened by the aether current quest: Kholusia, Amh Araeng, Il Mheg, Rak'tika, Labyrinthos,
        // Thavnair (whose second line opens one quest before its current), Garlemald, Elpis.
        foreach (var (first, last) in new (uint, uint)[] { (68907, 69096), (68911, 69065), (68940, 68967), (69192, 69018), (70016, 70024), (70025, 70033), (70034, 70042), (70043, 70051) })
        {
            var chain = stories.ChainOf(last);
            Assert.NotNull(chain);
            Assert.Equal(first, chain.RowIds[0]);
            Assert.Equal(9, chain.RowIds.Count);
        }
    }

    [Fact]
    public void Play_order_puts_a_quest_after_what_it_requires()
    {
        var (stories, _) = Build();

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
