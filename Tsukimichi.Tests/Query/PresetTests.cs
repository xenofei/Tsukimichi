using System.Text.Json;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using static Tsukimichi.Tests.Query.QueryTestData;

namespace Tsukimichi.Tests.Query;

/// <summary>The one-click presets: Feature quests (with the available-first sort), Around my level and Stalled.</summary>
public class PresetTests
{
    private static readonly DateTime Now = new(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

    // Journal order 1..6 (row ids double as sort keys); section 2 keeps them out of the main scenario.
    private static readonly QuestCatalog Catalog = QuestCatalog.Build(
    [
        Quest(1, "Alpha", section: 2, level: 10),
        Quest(2, "Bravo", section: 2, level: 15),
        Quest(3, "Charlie", section: 2, level: 20),
        Quest(4, "Delta", section: 2, level: 25),
        Quest(5, "Echo", section: 2, level: 26),
        Quest(6, "Foxtrot", section: 2, level: 50),
    ]);

    private static QueryContext Features(params uint[] rowIds) => QueryContext.Empty with { FeatureQuestIds = new HashSet<uint>(rowIds) };

    private static FilterSet With(Preset preset) => new() { Preset = preset };

    [Fact]
    public void Filter_set_carries_the_preset_through_clone_equality_reset_and_json()
    {
        var filters = With(Preset.Stalled);
        Assert.True(filters.IsActive());

        var clone = filters.Clone();
        Assert.Equal(filters, clone);
        Assert.Equal(filters.GetHashCode(), clone.GetHashCode());

        clone.Preset = Preset.LevelBand;
        Assert.NotEqual(filters, clone);

        var back = JsonSerializer.Deserialize<FilterSet>(JsonSerializer.Serialize(filters));
        Assert.Equal(filters, back);

        filters.Reset();
        Assert.Equal(Preset.None, filters.Preset);
        Assert.False(filters.IsActive());
    }

    [Fact]
    public void No_preset_changes_nothing()
    {
        var result = Run(Catalog, States(Catalog, QuestState.Blocked), With(Preset.None), ctx: Features(1));
        Assert.Equal(new uint[] { 1, 2, 3, 4, 5, 6 }, RowIds(result));
    }

    // ---- Feature quests ----

    [Fact]
    public void Feature_preset_keeps_only_the_feature_set_in_journal_order()
    {
        var result = Run(Catalog, States(Catalog, QuestState.Blocked), With(Preset.FeatureQuests), ctx: Features(5, 2, 99));
        Assert.Equal(new uint[] { 2, 5 }, RowIds(result));
    }

    [Fact]
    public void Feature_preset_with_available_first_sort_leads_with_ready_other_job_and_accepted()
    {
        var states = States((1, QuestState.Completed), (2, QuestState.Accepted), (3, QuestState.Blocked), (4, QuestState.Ready), (5, QuestState.ReadyOnOtherJob), (6, QuestState.Foreclosed));
        var sort = SortSpec.Default with { AvailableFirst = true };

        var result = Run(Catalog, states, With(Preset.FeatureQuests), sort: sort, ctx: Features(1, 2, 3, 4, 5, 6));

        // Available rows keep journal order among themselves, and so do the rest.
        Assert.Equal(new uint[] { 2, 4, 5, 1, 3, 6 }, RowIds(result));
    }

    [Fact]
    public void Available_first_applies_after_a_column_sort_and_before_pinned_first()
    {
        var states = States((1, QuestState.Blocked), (2, QuestState.Ready), (3, QuestState.Blocked), (4, QuestState.Ready), (5, QuestState.Blocked), (6, QuestState.Ready));
        var sort = new SortSpec(SortColumn.Level, Descending: true, PinnedFirst: true, AvailableFirst: true);
        var ctx = QueryContext.Empty with { Pinned = new HashSet<uint> { 3, 6 } };

        var result = Run(Catalog, states, sort: sort, ctx: ctx);

        // Level descending: 6 5 4 3 2 1. Available first: 6 4 2 | 5 3 1. Pinned first: 6 3 | 4 2 5 1.
        Assert.Equal(new uint[] { 6, 3, 4, 2, 5, 1 }, RowIds(result));
    }

    [Fact]
    public void Available_first_off_leaves_the_order_alone()
    {
        var states = States((1, QuestState.Blocked), (2, QuestState.Ready), (3, QuestState.Blocked), (4, QuestState.Ready), (5, QuestState.Blocked), (6, QuestState.Ready));
        Assert.Equal(new uint[] { 1, 2, 3, 4, 5, 6 }, RowIds(Run(Catalog, states)));
    }

    [Fact]
    public void Feature_preset_composes_with_scope_and_other_filters()
    {
        var states = States((1, QuestState.Ready), (2, QuestState.Completed), (3, QuestState.Ready), (4, QuestState.Ready), (5, QuestState.Ready), (6, QuestState.Ready));
        var filters = With(Preset.FeatureQuests);
        filters.HideCompleted = true;
        filters.LevelMax = 25;

        var result = Run(Catalog, states, filters, QuestScope.Section(2), ctx: Features(1, 2, 3, 6));

        Assert.Equal(new uint[] { 1, 3 }, RowIds(result));
    }

    [Fact]
    public void Empty_feature_set_blames_the_preset_by_name()
    {
        var result = Run(Catalog, States(Catalog, QuestState.Ready), With(Preset.FeatureQuests), ctx: Features());

        Assert.Empty(result.Rows);
        Assert.NotNull(result.Empty);
        Assert.Equal([FilterNames.FeatureQuests], result.Empty.Filters);
        Assert.Equal(6, result.TotalInScope);
    }

    [Fact]
    public void Preset_is_named_first_among_several_blamed_filters()
    {
        // Feature set holds only the level-10 quest; the level range starts at 15: dropping either filter alone restores rows.
        var result = Run(Catalog, States(Catalog, QuestState.Ready), new FilterSet { Preset = Preset.FeatureQuests, LevelMin = 15 }, ctx: Features(1));

        Assert.NotNull(result.Empty);
        Assert.Equal([FilterNames.FeatureQuests, FilterNames.LevelRange], result.Empty.Filters);
    }

    // ---- Around my level ----

    [Fact]
    public void Level_band_keeps_quests_within_five_levels_inclusive()
    {
        var ctx = QueryContext.Empty with { CurrentLevel = 20 };
        var result = Run(Catalog, States(Catalog, QuestState.Ready), With(Preset.LevelBand), ctx: ctx);

        // 15, 20, 25 are in; 10 and 26 are out.
        Assert.Equal(new uint[] { 2, 3, 4 }, RowIds(result));
    }

    [Fact]
    public void Level_band_near_level_one_does_not_wrap()
    {
        var ctx = QueryContext.Empty with { CurrentLevel = 3 };
        var result = Run(Catalog, States(Catalog, QuestState.Ready), With(Preset.LevelBand), ctx: ctx);
        Assert.Empty(result.Rows);
        Assert.Equal([FilterNames.LevelBand], result.Empty!.Filters);
    }

    [Fact]
    public void Level_band_without_a_known_level_keeps_nothing()
    {
        var result = Run(Catalog, States(Catalog, QuestState.Ready), With(Preset.LevelBand), ctx: QueryContext.Empty);
        Assert.Empty(result.Rows);
        Assert.Equal([FilterNames.LevelBand], result.Empty!.Filters);
    }

    [Fact]
    public void Level_band_radius_is_five()
    {
        Assert.Equal(5, QuestQuery.LevelBandRadius);
    }

    [Fact]
    public void Level_band_uses_the_displayed_level()
    {
        var catalog = QuestCatalog.Build(
        [
            Quest(1, "Raw ten", section: 2, level: 10),
            Quest(2, "Shown as fifteen", section: 2, level: 10) with { LevelOffset = 5 },
            Quest(3, "Shown as twenty-six", section: 2, level: 25) with { LevelOffset = 1 },
        ]);
        var ctx = QueryContext.Empty with { CurrentLevel = 20 };

        var result = Run(catalog, States(catalog, QuestState.Ready), With(Preset.LevelBand), ctx: ctx);

        Assert.Equal(new uint[] { 2 }, RowIds(result));
    }

    // ---- Stalled ----

    private static QueryContext StalledContext(int days, params (uint RowId, DateTime SinceUtc)[] entries)
    {
        var since = new Dictionary<ushort, DateTime>();
        foreach (var (rowId, sinceUtc) in entries)
        {
            since[QuestRecord.ToQuestId(rowId)] = sinceUtc;
        }

        return QueryContext.Empty with { AcceptedSince = since, NowUtc = Now, StalledDays = days };
    }

    [Fact]
    public void Stalled_keeps_accepted_quests_at_least_the_configured_days_old()
    {
        var states = States((1, QuestState.Accepted), (2, QuestState.Accepted), (3, QuestState.Accepted), (4, QuestState.Ready), (5, QuestState.Accepted), (6, QuestState.Completed));
        var ctx = StalledContext(
            7,
            (1, Now.AddDays(-8)),          // stalled
            (2, Now.AddDays(-7)),          // exactly seven days: stalled
            (3, Now.AddDays(-6.99)),       // fresh
            (4, Now.AddDays(-30)),         // not accepted any more
            (6, Now.AddDays(-30)));        // completed

        var result = Run(Catalog, states, With(Preset.Stalled), ctx: ctx);

        // 5 is accepted with no recorded time: unknown, never stalled.
        Assert.Equal(new uint[] { 1, 2 }, RowIds(result));
    }

    [Fact]
    public void Stalled_honours_the_configured_day_count()
    {
        var states = States((1, QuestState.Accepted), (2, QuestState.Accepted));
        var ctx = StalledContext(3, (1, Now.AddDays(-4)), (2, Now.AddDays(-2)));

        Assert.Equal(new uint[] { 1 }, RowIds(Run(Catalog, states, With(Preset.Stalled), ctx: ctx)));
        Assert.Equal(new uint[] { 1, 2 }, RowIds(Run(Catalog, states, With(Preset.Stalled), ctx: ctx with { StalledDays = 0 })));
    }

    [Fact]
    public void Stalled_without_a_sidecar_keeps_nothing_and_names_the_preset()
    {
        var states = States((1, QuestState.Accepted));
        var result = Run(Catalog, states, With(Preset.Stalled), ctx: QueryContext.Empty with { NowUtc = Now });

        Assert.Empty(result.Rows);
        Assert.Equal([FilterNames.Stalled], result.Empty!.Filters);
    }

    [Fact]
    public void Stalled_rows_keep_journal_order_which_groups_by_category()
    {
        var catalog = QuestCatalog.Build(
        [
            Quest(1, "Late", section: 2, category: 11, sortKey: 30),
            Quest(2, "Early", section: 2, category: 10, sortKey: 10),
            Quest(3, "Middle", section: 2, category: 10, sortKey: 20),
        ]);
        var states = States(catalog, QuestState.Accepted);
        var ctx = StalledContext(1, (1, Now.AddDays(-2)), (2, Now.AddDays(-9)), (3, Now.AddDays(-5)));

        Assert.Equal(new uint[] { 2, 3, 1 }, RowIds(Run(catalog, states, With(Preset.Stalled), ctx: ctx)));
    }

    [Fact]
    public void Default_context_has_seven_stalled_days_and_no_level()
    {
        Assert.Equal(7, QueryContext.DefaultStalledDays);
        Assert.Equal(7, QueryContext.Empty.StalledDays);
        Assert.Equal(0, QueryContext.Empty.CurrentLevel);
        Assert.Null(QueryContext.Empty.AcceptedSince);
    }
}
