using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Storage;
using Tsukimichi.GameData;
using Tsukimichi.Tests.Data;

namespace Tsukimichi.Tests.Plan;

/// <summary>The frozen catalog dated with the shipped patches, and its chains, built once for the story page tests.</summary>
public sealed class StoryPageFixture
{
    private readonly Lazy<(CatalogBundle Bundle, ChainCatalog Chains)> built;

    public StoryPageFixture()
    {
        Catalog = new FixtureCatalog();
        built = new Lazy<(CatalogBundle, ChainCatalog)>(() =>
        {
            var patches = QuestPatches.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), QuestPatches.FileName));
            var bundle = CatalogFixtureFile.Read(FixtureCatalog.CatalogPath(), JournalFiling.Refiled, Catalog.Curated, patches).Bundle;
            var unique = UniqueRewardsFile.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), "unique_quests.json"));
            return (bundle, CatalogIndexes.Build(bundle.Catalog, Catalog.Curated, unique.Entries).Chains);
        }, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public FixtureCatalog Catalog { get; }

    public QuestCatalog Quests => built.Value.Bundle.Catalog;

    public ChainCatalog Chains => built.Value.Chains;
}

/// <summary>
/// Your story on one page (feature plan v7 N9): patch bands from the main scenario's journal categories, each optional
/// line placed after the story quest that opens it, and nothing past the story point named.
/// </summary>
public class StoryPageTests(StoryPageFixture fixture) : IClassFixture<StoryPageFixture>
{
    private QuestCatalog Catalog => fixture.Quests;

    private IReadOnlyList<QuestRecord> Story => MsqGraph.For(Catalog).Story;

    private StoryPage Page(IReadOnlyDictionary<uint, QuestEvaluation>? states = null, Func<QuestRecord, bool>? masked = null) =>
        StoryPage.Build(new StoryPageInputs(
            Catalog,
            fixture.Chains,
            states ?? new Dictionary<uint, QuestEvaluation>(),
            masked ?? (static _ => false),
            quest => masked?.Invoke(quest) == true ? SpoilerMask.Placeholder(quest) : quest.Name,
            Expansions.Name,
            StoryRequirements.For(Catalog, null).SideQuests));

    private static Dictionary<uint, QuestEvaluation> Done(IEnumerable<QuestRecord> quests) =>
        quests.ToDictionary(static q => q.RowId, static q => new QuestEvaluation(QuestState.Completed, [], null, null, null));

    [Fact]
    public void Bands_follow_the_main_scenario_categories_with_their_patches()
    {
        var page = Page();

        Assert.Equal(Story.Select(static q => q.Journal.CategoryId).Distinct().Count(), page.Bands.Count);
        Assert.Equal(Story.Count, page.Bands.Sum(static b => b.StoryTotal));
        Assert.Equal(Story.Count, page.StoryLeft);

        var arr = page.Bands[0];
        Assert.True(arr.FirstOfExpansion);
        Assert.Equal("2.0", arr.Patches);
        Assert.Equal("A Realm Reborn", arr.ExpansionName);

        var astral = page.Bands[1];
        Assert.False(astral.FirstOfExpansion);
        Assert.Equal("Seventh Astral Era", astral.Title);
        Assert.Equal("2.1 – 2.5", astral.Patches);

        var heavensward = page.Bands[2];
        Assert.True(heavensward.FirstOfExpansion);
        Assert.Equal("3.0", heavensward.Patches);
    }

    [Fact]
    public void The_Crystal_Tower_opens_after_The_Ultimate_Weapon_at_the_head_of_the_Seventh_Astral_Era()
    {
        var page = Page();
        var astral = page.Bands[1];
        var group = Assert.Single(astral.Groups, static g => g.Lines.Any(static l => l.Chain.Name == "Crystal Tower"));

        Assert.Equal("The Ultimate Weapon", group.OpensAfter?.Name);
        Assert.Equal(Story[StoryPage.GateOf(Catalog, group.Lines[0].Chain.RowIds[0])].RowId, group.OpensAfter!.RowId);
        var tower = group.Lines.Single(static l => l.Chain.Name == "Crystal Tower");
        Assert.True(tower.StoryNeedsIt);
        Assert.Equal("Legacy of Allag", tower.Next?.Name);
        Assert.Equal(tower.Chain.RowIds.Count, tower.Left);
        Assert.Contains(group.Lines, static l => l.Chain.Name == "Hildibrand");

        // Main scenario chains are not optional lines.
        Assert.DoesNotContain(page.Bands.SelectMany(static b => b.Groups).SelectMany(static g => g.Lines), static l => l.Chain.Name == "Dragonsong War");
    }

    [Fact]
    public void A_line_no_story_quest_gates_opens_with_its_expansion()
    {
        var page = Page();
        var reaper = page.Bands.SelectMany(b => b.Groups.Select(g => (Band: b, Group: g))).Single(static p => p.Group.Lines.Any(static l => l.Chain.Name == "Reaper Quests"));

        Assert.Null(reaper.Group.OpensAfter);
        Assert.True(reaper.Band.FirstOfExpansion);
        Assert.Equal(Catalog.ByRowId[reaper.Group.Lines.Single(static l => l.Chain.Name == "Reaper Quests").Chain.RowIds[0]].Expansion, reaper.Band.Expansion);
    }

    [Fact]
    public void A_finished_band_and_a_finished_line_read_done()
    {
        var arr = Story.Where(q => q.Journal.CategoryId == Story[0].Journal.CategoryId).ToList();
        var page = Page(Done(arr));

        Assert.True(page.Bands[0].IsDone);
        Assert.Equal(0, page.Bands[0].StoryLeft);
        Assert.False(page.Bands[1].IsDone);
        Assert.Equal(Story.Count - arr.Count, page.StoryLeft);
        Assert.Equal(page.Bands[1].Next?.RowId, Story[arr.Count].RowId);

        var tower = fixture.Chains.Chains.Single(static c => c.Name == "Crystal Tower");
        var states = Done(arr.Concat(tower.RowIds.Select(id => Catalog.ByRowId[id])));
        var line = Page(states).Bands[1].Groups.SelectMany(static g => g.Lines).Single(static l => l.Chain.Name == "Crystal Tower");
        Assert.True(line.IsDone);
        Assert.Equal(0, line.Left);
    }

    [Fact]
    public void Past_the_story_point_a_band_shows_counts_and_a_group_hides_its_names()
    {
        // The shield masks every story quest after A Realm Reborn 2.0's last one, and the side quests past it.
        var arrCount = Story.Count(q => q.Journal.CategoryId == Story[0].Journal.CategoryId);
        var ahead = Story.Skip(arrCount - 1).Select(static q => q.RowId).ToHashSet();
        var page = Page(masked: q => ahead.Contains(q.RowId));

        Assert.False(page.Bands[0].Past);
        Assert.All(page.Bands.Skip(1), static b => Assert.True(b.Past));
        Assert.True(page.Bands[2].LineCount > 0);

        // The Seventh Astral Era's lines open after The Ultimate Weapon, which the shield masks: no names.
        var astral = page.Bands[1];
        Assert.All(astral.Groups, static g => Assert.True(g.Hidden));
        Assert.All(astral.Groups.SelectMany(static g => g.Lines), static l => Assert.Equal(string.Empty, l.Name));
        Assert.All(astral.Groups.Where(static g => g.OpensAfter is not null), static g => Assert.StartsWith("Main scenario quest (Lv", g.OpensAfterName, StringComparison.Ordinal));
    }
}
