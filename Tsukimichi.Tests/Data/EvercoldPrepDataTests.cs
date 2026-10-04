using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using Tsukimichi.GameData;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// Before Evercold (feature plan v7, 1.20.0, N7) over real data: the end of the 7.x main scenario is read from the
/// Quest sheet (the last story quest nothing follows), never a hard-coded id, and today's data is Dawntrail's, so the
/// card is live until the curated early access day. The frozen catalog runs everywhere; the live sheets check the same
/// where the game is.
/// </summary>
public sealed class EvercoldPrepDataTests(FixtureCatalog fixture, GameDataFixture game, ITestOutputHelper output)
    : IClassFixture<FixtureCatalog>, IClassFixture<GameDataFixture>
{
    private const byte Dawntrail = 5;

    /// <summary>The fixture as the plugin builds it, dated with the shipped <c>quest_patches.json</c>.</summary>
    private CatalogBundle Dated() =>
        CatalogFixtureFile.Read(FixtureCatalog.CatalogPath(), JournalFiling.Refiled, fixture.Curated, QuestPatches.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), QuestPatches.FileName))).Bundle;

    private void AssertEndOfSevenX(QuestCatalog catalog)
    {
        var end = EvercoldPrep.StoryEnd(catalog);
        Assert.NotNull(end);
        output.WriteLine($"{end.RowId} {end.Name} (expansion {end.Expansion}, patch {end.AddedIn}, Lv {end.Level})");

        // A main scenario quest of Dawntrail that no story quest follows, and the last of them in journal order.
        var graph = MsqGraph.For(catalog);
        Assert.Contains(end, graph.Story);
        Assert.Equal(Dawntrail, end.Expansion);
        Assert.Empty(graph.Successors(end.RowId));
        var after = graph.Story.SkipWhile(q => q.RowId != end.RowId).Skip(1).ToList();
        Assert.All(after, q => Assert.NotEmpty(graph.Successors(q.RowId)));

        // Today's data is 7.x: the card is live until the curated day.
        var launch = EvercoldPrep.Launch(fixture.Curated);
        Assert.Equal(Dawntrail, EvercoldPrep.LatestExpansion(catalog));
        Assert.False(EvercoldPrep.IsRetired(catalog, launch, launch.EarlyAccessUtc.AddDays(-30)));
        Assert.True(EvercoldPrep.IsRetired(catalog, launch, launch.EarlyAccessUtc));
    }

    [Fact]
    public void The_end_of_the_7x_story_comes_from_the_frozen_catalog()
    {
        var catalog = Dated().Catalog;
        AssertEndOfSevenX(catalog);

        // Dated, it is a quest of the newest 7.x patch series: the line reads "through Patch 7.5".
        var end = EvercoldPrep.StoryEnd(catalog);
        Assert.NotNull(end);
        Assert.StartsWith("7.", end.AddedIn, StringComparison.Ordinal);
        Assert.Equal(PatchIndex.For(catalog).NewestSeries, PatchVersion.Series(end.AddedIn));
    }

    [GameDataFact]
    public void The_end_of_the_7x_story_comes_from_the_live_quest_sheet()
    {
        AssertEndOfSevenX(game.Bundle.Catalog);
        Assert.Equal(EvercoldPrep.StoryEnd(fixture.Bundle.Catalog)?.RowId, EvercoldPrep.StoryEnd(game.Bundle.Catalog)?.RowId);
    }
}
