using System.Text.Json;
using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using static Tsukimichi.Tests.Query.QueryTestData;

namespace Tsukimichi.Tests.Query;

/// <summary>The Story sidequests quick view: membership, reading order under the journal sort, and its name.</summary>
public class StorySidequestsPresetTests
{
    private const uint Side = StorySidequests.SidequestSectionId;

    // Genre 100 (journal first): lone story 1, then a story whose play order is 3 before 2; 4 has no artwork.
    // Genre 101: story 5. Genre 102 holds 6, an unlock quest with artwork.
    private static readonly QuestCatalog Catalog = QuestCatalog.Build(
    [
        Story(Quest(1, "Lone", section: Side, genre: 100, level: 10)),
        Story(Quest(2, "Second", section: Side, genre: 100, level: 20), previous: 3),
        Story(Quest(3, "First", section: Side, genre: 100, level: 30)),
        Quest(4, "No art", section: Side, genre: 100, level: 40),
        Story(Quest(5, "Other zone", section: Side, genre: 101, level: 5)),
        Story(Quest(6, "Unlock", section: Side, genre: 102, level: 50)),
    ]);

    private static readonly StorySidequests Stories = StorySidequests.Build(Catalog, new HashSet<uint> { 6 });

    private static QueryContext Context => QueryContext.Empty with { Stories = Stories };

    private static QuestRecord Story(QuestRecord quest, uint previous = 0) => quest with
    {
        Icon = 100000 + quest.RowId,
        PreviousQuests = previous == 0 ? Prereq.None : new Prereq([previous], JoinKind.All),
    };

    private static FilterSet Filters() => new() { Preset = Preset.StorySidequests };

    [Fact]
    public void Keeps_story_sidequests_in_reading_order()
    {
        var result = Run(Catalog, States(Catalog, QuestState.Ready), Filters(), ctx: Context);

        // Zone by zone in journal order; inside a story, play order (3 before 2).
        Assert.Equal(new uint[] { 1, 3, 2, 5 }, RowIds(result));
    }

    [Fact]
    public void Descending_journal_sort_reverses_the_reading_order_and_other_columns_sort_as_usual()
    {
        var descending = Run(Catalog, States(Catalog, QuestState.Ready), Filters(), sort: SortSpec.Default with { Descending = true }, ctx: Context);
        Assert.Equal(new uint[] { 5, 2, 3, 1 }, RowIds(descending));

        var byLevel = Run(Catalog, States(Catalog, QuestState.Ready), Filters(), sort: SortSpec.Default with { Column = SortColumn.Level }, ctx: Context);
        Assert.Equal(new uint[] { 5, 1, 2, 3 }, RowIds(byLevel));
    }

    [Fact]
    public void Composes_with_other_filters()
    {
        var states = States((1, QuestState.Completed), (2, QuestState.Ready), (3, QuestState.Completed), (4, QuestState.Ready), (5, QuestState.Ready), (6, QuestState.Ready));
        var filters = Filters();
        filters.HideCompleted = true;

        Assert.Equal(new uint[] { 2, 5 }, RowIds(Run(Catalog, states, filters, ctx: Context)));
    }

    [Fact]
    public void Without_stories_nothing_passes_and_the_preset_is_named()
    {
        var result = Run(Catalog, States(Catalog, QuestState.Ready), Filters(), ctx: QueryContext.Empty);

        Assert.Empty(result.Rows);
        Assert.Equal([FilterNames.StorySidequests], result.Empty!.Filters);
        Assert.Equal("Story sidequests", FilterNames.PresetName(Preset.StorySidequests));
    }

    [Fact]
    public void Filter_set_round_trips_the_preset()
    {
        var filters = Filters();
        Assert.True(filters.IsActive());
        Assert.Equal(filters, JsonSerializer.Deserialize<FilterSet>(JsonSerializer.Serialize(filters)));
        Assert.Equal(filters, filters.Clone());
    }
}
