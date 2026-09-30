using Lumina.Data;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using Tsukimichi.GameData;
using static Tsukimichi.Tests.Query.QueryTestData;

namespace Tsukimichi.Tests.Data;

/// <summary>
/// The shipped <c>quest_patches.json</c> (P8) over the frozen catalog: the file itself, the mapping into
/// <see cref="QuestRecord.AddedIn"/>, and the Unlocks quick view's "New in 7.5x" group on real data.
/// </summary>
public class QuestPatchesFixtureTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>
{
    private static readonly Lazy<QuestPatches> Shipped = new(() => QuestPatches.Load(ShippedPath()));

    private static string ShippedPath() => Path.Combine(FixtureCatalog.ShippedDataDir(), QuestPatches.FileName);

    /// <summary>The fixture as the plugin builds it: refiled with the curated overlay and dated with the shipped patches.</summary>
    private CatalogBundle Dated() =>
        CatalogFixtureFile.Read(FixtureCatalog.CatalogPath(), JournalFiling.Refiled, fixture.Curated, Shipped.Value).Bundle;

    [Fact]
    [Trait("Category", "Curated")]
    public void Shipped_file_parses_cleanly_and_holds_only_patch_numbers()
    {
        var patches = Shipped.Value;

        Assert.Empty(patches.Warnings);
        Assert.NotEmpty(patches.ByRowId);
        Assert.All(patches.ByRowId.Values, p => Assert.True(p.Length == 0 || PatchVersion.IsPatch(p), $"\"{p}\" is not a patch number"));
        Assert.NotEmpty(patches.History);

        // The text is canonical: a regeneration that changes nothing leaves the file untouched.
        Assert.Equal(File.ReadAllText(ShippedPath()).ReplaceLineEndings("\n"), patches.ToJson());
    }

    [Fact]
    [Trait("Category", "Curated")]
    public void Shipped_file_lists_every_quest_of_the_fixture()
    {
        // Every catalog id is listed (known or not), so DataGen's diff after the next game patch sees exactly the new ones.
        var patches = Shipped.Value;
        var missing = fixture.LegacyBundle.Catalog.All.Where(q => !patches.Lists(q.RowId)).Select(q => $"{q.RowId} {q.Name}").ToList();

        Assert.True(missing.Count == 0, $"quest_patches.json does not list {missing.Count} fixture quests (run tools/regen.ps1 -Patch <x.y>): {string.Join(", ", missing.Take(10))}");
        Assert.Equal(fixture.GameVersion, patches.GameVersion);
    }

    [Fact]
    [Trait("Category", "Curated")]
    public void Shipped_file_is_no_older_than_its_expansions()
    {
        // The one fact the game data states: a quest cannot predate its expansion (Quest.Expansion + 2 is its first major).
        var patches = Shipped.Value;
        var wrong = fixture.LegacyBundle.Catalog.All
            .Where(q => patches.For(q.RowId) is { Length: > 0 } p && int.Parse(p[..p.IndexOf('.', StringComparison.Ordinal)], System.Globalization.CultureInfo.InvariantCulture) < q.Expansion + 2)
            .Select(q => $"{q.RowId} {q.Name} ({patches.For(q.RowId)}, expansion {q.Expansion})")
            .ToList();

        Assert.True(wrong.Count == 0, string.Join(", ", wrong.Take(10)));
    }

    [Fact]
    public void Mapping_sets_added_in_from_the_file()
    {
        var catalog = Dated().Catalog;

        Assert.Equal("2.0", catalog.GetByRowId(65537)!.AddedIn); // A Good Adventurer Is Hard to Find
        Assert.Equal("7.5", catalog.GetByRowId(70979)!.AddedIn); // You Otter Be There
        Assert.Equal("7.55", catalog.GetByRowId(70998)!.AddedIn); // Touring Tural
        Assert.All(catalog.All, q => Assert.Equal(Shipped.Value.For(q.RowId), q.AddedIn));
    }

    [Fact]
    public void The_fixture_without_the_file_leaves_every_patch_unknown()
    {
        Assert.All(fixture.Bundle.Catalog.All, q => Assert.Equal(string.Empty, q.AddedIn));
    }

    [Fact]
    public void Filter_keeps_one_series_on_the_fixture()
    {
        var catalog = Dated().Catalog;

        var result = Run(catalog, States(catalog, QuestState.Ready), new FilterSet { AddedIn = "7.5" });

        Assert.NotEmpty(result.Rows);
        Assert.All(result.Rows, r => Assert.Equal("7.5", PatchVersion.Series(r.Quest.AddedIn)));
        Assert.Equal(catalog.All.Count(q => !q.IsRemoved && PatchVersion.Series(q.AddedIn) == "7.5"), result.Rows.Length);
    }

    [Fact]
    public void Unlocks_view_leads_with_its_unlocks_from_the_newest_series_on_the_fixture()
    {
        var bundle = Dated();
        var catalog = bundle.Catalog;
        var index = PatchIndex.For(catalog);
        var unlocks = fixture.Curated.FeatureQuests;
        var ctx = QueryContext.Empty with { FeatureQuestIds = unlocks };
        var sort = SortSpec.Default with { AvailableFirst = true, NewThisPatchFirst = true };

        var result = Run(catalog, States(catalog, QuestState.Ready), new FilterSet { Preset = Preset.FeatureQuests }, sort: sort, ctx: ctx);

        Assert.Equal(PatchVersion.Series(Shipped.Value.Newest), index.NewestSeries);
        Assert.True(PatchVersion.Compare(index.NewestSeries, "7.5") >= 0, $"newest series {index.NewestSeries}");

        // Only unlocks: the group is the view's own quests from the newest series, and the view adds nothing.
        Assert.All(result.Rows, r => Assert.Contains(r.Quest.RowId, unlocks));
        var expectedNew = unlocks.Count(id => catalog.GetByRowId(id) is { } q && index.IsNew(q));
        Assert.True(expectedNew > 0, $"no unlock quest in {index.NewestSeries}x on the fixture");
        Assert.Equal(expectedNew, result.NewThisPatch);
        Assert.All(result.Rows.Take(result.NewThisPatch), r => Assert.True(PatchVersion.InSeries(r.Quest.AddedIn, index.NewestSeries)));
        Assert.All(result.Rows.Skip(result.NewThisPatch), r => Assert.False(index.IsNew(r.Quest)));
        Assert.Equal(unlocks.Count(id => catalog.GetByRowId(id) is { IsRemoved: false }), result.Rows.Length);
    }

    [GameDataFact]
    public void Live_mapping_takes_the_file()
    {
        var path = Environment.GetEnvironmentVariable(GameDataFactAttribute.EnvVar)!;
        using var game = new Lumina.GameData(path, new Lumina.LuminaOptions { DefaultExcelLanguage = Language.English, PanicOnSheetChecksumMismatch = false });

        var bundle = CatalogMapper.Map(game.Excel, Language.English, curated: fixture.Curated, patches: Shipped.Value);

        Assert.Equal("7.5", bundle.Catalog.GetByRowId(70979)!.AddedIn);
        Assert.Equal(bundle.Catalog.Count, bundle.Catalog.All.Count(q => Shipped.Value.Lists(q.RowId)));
    }
}
