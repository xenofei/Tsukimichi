using System.Text.Json;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using static Tsukimichi.Tests.Query.QueryTestData;

namespace Tsukimichi.Tests.Query;

/// <summary>The "Added in" filter and the Unlocks quick view's "New in 7.5x" group (P8).</summary>
public class AddedInFilterTests
{
    private static QuestRecord Dated(uint rowId, string patch, string? name = null) => Quest(rowId, name ?? $"Quest {rowId}") with { AddedIn = patch };

    private static readonly QuestCatalog Catalog = QuestCatalog.Build(
    [
        Dated(1, "2.0"),
        Dated(2, "7.4"),
        Dated(3, "7.5"),
        Dated(4, "7.51"),
        Dated(5, "7.55"),
        Dated(6, "7.55"),
        Dated(7, ""),
        Dated(8, "7.55") with { IsRetired = true },
    ]);

    private static readonly Dictionary<uint, QuestState> AllReady = States(Catalog, QuestState.Ready);

    [Fact]
    public void Added_in_is_off_by_default_and_counts_as_active_and_in_the_badge_when_set()
    {
        Assert.Equal(string.Empty, new FilterSet().AddedIn);
        Assert.False(new FilterSet().AddedInEngaged());
        Assert.True(new FilterSet { AddedIn = "7.5" }.IsActive());
        Assert.Equal(1, FilterBadge.Count(new FilterSet { AddedIn = "7.5" }));
        Assert.Equal(0, FilterBadge.Count(new FilterSet()));
    }

    [Fact]
    public void Added_in_survives_clone_equality_reset_and_json()
    {
        var filters = new FilterSet { AddedIn = "7.5" };

        var clone = filters.Clone();
        Assert.Equal(filters, clone);
        Assert.Equal(filters.GetHashCode(), clone.GetHashCode());
        Assert.NotEqual(filters, new FilterSet());
        Assert.NotEqual(filters, new FilterSet { AddedIn = "7.4" });

        var back = JsonSerializer.Deserialize<FilterSet>(JsonSerializer.Serialize(filters));
        Assert.Equal("7.5", back!.AddedIn);

        // A config saved before 0.9.0 has no AddedIn: the default stands.
        var old = JsonSerializer.Deserialize<FilterSet>("""{"HideCompleted":true}""");
        Assert.Equal(string.Empty, old!.AddedIn);

        filters.Reset();
        Assert.Equal(string.Empty, filters.AddedIn);
        Assert.False(filters.IsActive());
    }

    [Fact]
    public void A_null_value_reads_as_not_engaged()
    {
        var filters = new FilterSet { AddedIn = null! };

        Assert.False(filters.AddedInEngaged());
        Assert.False(filters.IsActive());
        Assert.Equal(new FilterSet(), filters);
        Assert.Equal(new uint[] { 1, 2, 3, 4, 5, 6, 7 }, RowIds(Run(Catalog, AllReady, filters)));
    }

    [Fact]
    public void Added_in_keeps_the_whole_series_and_nothing_unknown()
    {
        var result = Run(Catalog, AllReady, new FilterSet { AddedIn = "7.5" });

        Assert.Equal(new uint[] { 3, 4, 5, 6 }, RowIds(result));
    }

    [Fact]
    public void Added_in_an_old_series_keeps_only_it()
    {
        Assert.Equal(new uint[] { 1 }, RowIds(Run(Catalog, AllReady, new FilterSet { AddedIn = "2.0" })));
        Assert.Equal(new uint[] { 2 }, RowIds(Run(Catalog, AllReady, new FilterSet { AddedIn = "7.4" })));
    }

    [Fact]
    public void A_hand_edited_value_is_normalized()
    {
        Assert.Equal(new uint[] { 3, 4, 5, 6 }, RowIds(Run(Catalog, AllReady, new FilterSet { AddedIn = "7.50" })));
    }

    [Fact]
    public void Added_in_that_hides_everything_names_itself()
    {
        var result = Run(Catalog, AllReady, new FilterSet { AddedIn = "6.0" });

        Assert.Empty(result.Rows);
        Assert.Equal([FilterNames.AddedIn], result.Empty!.Filters);
    }

    [Fact]
    public void Patch_index_lists_series_newest_first_and_the_newest_series_over_live_quests()
    {
        var index = PatchIndex.For(Catalog);

        Assert.Equal("7.55", index.Newest);
        Assert.Equal("7.5", index.NewestSeries);
        Assert.Equal(4, index.NewestSeriesCount);
        Assert.Equal(6, index.Known);
        Assert.Equal([new PatchSeries("7.5", 4), new PatchSeries("7.4", 1), new PatchSeries("2.0", 1)], index.Series);
        Assert.Same(index, PatchIndex.For(Catalog));

        // New means the whole newest series (7.5, 7.51, 7.55), the unit of the Added in filter.
        Assert.True(index.IsNew(Catalog.GetByRowId(3)!));
        Assert.True(index.IsNew(Catalog.GetByRowId(4)!));
        Assert.True(index.IsNew(Catalog.GetByRowId(5)!));
        Assert.False(index.IsNew(Catalog.GetByRowId(2)!));
        Assert.False(index.IsNew(Catalog.GetByRowId(7)!));
        Assert.False(index.IsNew(Catalog.GetByRowId(8)!));
    }

    [Fact]
    public void Without_patch_data_nothing_is_new()
    {
        var catalog = QuestCatalog.Build([Quest(1, "A"), Quest(2, "B")]);
        var sort = SortSpec.Default with { NewThisPatchFirst = true };

        var result = Run(catalog, States(catalog, QuestState.Ready), new FilterSet { Preset = Preset.FeatureQuests }, sort: sort, ctx: QueryContext.Empty with { FeatureQuestIds = new HashSet<uint> { 2 } });

        Assert.Equal(string.Empty, PatchIndex.For(catalog).NewestSeries);
        Assert.Equal(0, PatchIndex.For(catalog).NewestSeriesCount);
        Assert.Equal(new uint[] { 2 }, RowIds(result));
        Assert.Equal(0, result.NewThisPatch);
    }

    [Fact]
    public void Unlocks_view_leads_with_its_unlocks_from_the_newest_series_and_adds_nothing()
    {
        // Unlocks 1, 2, 4, 5: 4 (7.51) and 5 (7.55) are in the newest series; 3 and 6 are too but are not unlocks.
        var ctx = QueryContext.Empty with { FeatureQuestIds = new HashSet<uint> { 1, 2, 4, 5 } };
        var sort = SortSpec.Default with { NewThisPatchFirst = true };

        var result = Run(Catalog, AllReady, new FilterSet { Preset = Preset.FeatureQuests }, sort: sort, ctx: ctx);

        Assert.Equal(new uint[] { 4, 5, 1, 2 }, RowIds(result));
        Assert.Equal(2, result.NewThisPatch);
    }

    [Fact]
    public void The_new_group_keeps_pinned_and_available_first_inside_it()
    {
        var ctx = QueryContext.Empty with { FeatureQuestIds = new HashSet<uint> { 1, 4, 5, 6 }, Pinned = new HashSet<uint> { 6 } };
        var states = States((1, QuestState.Ready), (4, QuestState.Blocked), (5, QuestState.Ready), (6, QuestState.Blocked));
        var sort = SortSpec.Default with { NewThisPatchFirst = true, AvailableFirst = true };

        var result = Run(Catalog, states, new FilterSet { Preset = Preset.FeatureQuests }, sort: sort, ctx: ctx);

        Assert.Equal(new uint[] { 6, 5, 4, 1 }, RowIds(result));
        Assert.Equal(3, result.NewThisPatch);
    }

    [Fact]
    public void Other_views_have_no_group()
    {
        var result = Run(Catalog, AllReady, new FilterSet(), sort: SortSpec.Default);
        Assert.Equal(0, result.NewThisPatch);
        Assert.Equal(new uint[] { 1, 2, 3, 4, 5, 6, 7 }, RowIds(result));

        // The Unlocks preset without the sort flag keeps table order and holds only unlocks.
        var ctx = QueryContext.Empty with { FeatureQuestIds = new HashSet<uint> { 1, 5 } };
        var unsorted = Run(Catalog, AllReady, new FilterSet { Preset = Preset.FeatureQuests }, ctx: ctx);
        Assert.Equal(new uint[] { 1, 5 }, RowIds(unsorted));
        Assert.Equal(0, unsorted.NewThisPatch);
    }

    [Fact]
    public void Hidden_new_quests_do_not_count_in_the_group()
    {
        var ctx = QueryContext.Empty with { FeatureQuestIds = new HashSet<uint> { 1, 5, 6 } };
        var states = States((1, QuestState.Ready), (5, QuestState.Completed), (6, QuestState.Ready));
        var sort = SortSpec.Default with { NewThisPatchFirst = true };

        var result = Run(Catalog, states, new FilterSet { Preset = Preset.FeatureQuests, HideCompleted = true }, sort: sort, ctx: ctx);

        Assert.Equal(new uint[] { 6, 1 }, RowIds(result));
        Assert.Equal(1, result.NewThisPatch);
    }
}

