using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using static Tsukimichi.Tests.Query.QueryTestData;

namespace Tsukimichi.Tests.Query;

public class PinnedFirstTests
{
    // Journal order: 3, 2, 1, 4, 5 (by sort key); names alpha(2) Bravo(1) Charlie(3) delta(4) Echo(5).
    private static readonly QuestCatalog Catalog = QuestCatalog.Build(
    [
        Quest(1, "Bravo", sortKey: 30, level: 5),
        Quest(2, "alpha", sortKey: 20, level: 5),
        Quest(3, "Charlie", sortKey: 10, level: 1),
        Quest(4, "delta", sortKey: 40, level: 9),
        Quest(5, "Echo", sortKey: 50, level: 5),
    ]);

    private static readonly Dictionary<uint, QuestState> AllReady = States(Catalog, QuestState.Ready);

    private static QueryContext Pins(params uint[] rowIds) => QueryContext.Empty with { Pinned = new HashSet<uint>(rowIds) };

    [Fact]
    public void Default_sort_has_pinned_first_on()
    {
        Assert.True(SortSpec.Default.PinnedFirst);
        Assert.True(new SortSpec(SortColumn.Name, true).PinnedFirst);
        Assert.False((SortSpec.Default with { PinnedFirst = false }).PinnedFirst);
    }

    [Fact]
    public void Pinned_rows_lead_in_journal_order()
    {
        var result = Run(Catalog, AllReady, ctx: Pins(5, 1));
        Assert.Equal(new uint[] { 1, 5, 3, 2, 4 }, RowIds(result));
    }

    [Fact]
    public void Pinned_rows_lead_after_a_column_sort_and_keep_the_sorted_order_within_each_group()
    {
        var asc = Run(Catalog, AllReady, sort: new SortSpec(SortColumn.Name, false), ctx: Pins(5, 1));
        Assert.Equal(new[] { "Bravo", "Echo", "alpha", "Charlie", "delta" }, Names(asc));

        var desc = Run(Catalog, AllReady, sort: new SortSpec(SortColumn.Name, true), ctx: Pins(5, 1));
        Assert.Equal(new[] { "Echo", "Bravo", "delta", "Charlie", "alpha" }, Names(desc));
    }

    [Fact]
    public void Journal_descending_partitions_the_reversed_order()
    {
        var result = Run(Catalog, AllReady, sort: new SortSpec(SortColumn.Journal, true), ctx: Pins(1, 5));
        Assert.Equal(new uint[] { 5, 1, 4, 2, 3 }, RowIds(result));
    }

    [Fact]
    public void Pinned_first_off_leaves_the_sort_alone()
    {
        var result = Run(Catalog, AllReady, sort: SortSpec.Default with { PinnedFirst = false }, ctx: Pins(5, 1));
        Assert.Equal(new uint[] { 3, 2, 1, 4, 5 }, RowIds(result));
    }

    [Fact]
    public void No_pins_or_all_pinned_changes_nothing()
    {
        Assert.Equal(new uint[] { 3, 2, 1, 4, 5 }, RowIds(Run(Catalog, AllReady)));
        Assert.Equal(new uint[] { 3, 2, 1, 4, 5 }, RowIds(Run(Catalog, AllReady, ctx: Pins(1, 2, 3, 4, 5))));

        // A pin for a quest outside the result set is ignored.
        Assert.Equal(new uint[] { 3, 2, 1, 4, 5 }, RowIds(Run(Catalog, AllReady, ctx: Pins(99))));
    }

    [Fact]
    public void Pinned_first_composes_with_the_pinned_only_filter()
    {
        var result = Run(Catalog, AllReady, new FilterSet { PinnedOnly = true }, ctx: Pins(5, 1));
        Assert.Equal(new uint[] { 1, 5 }, RowIds(result));
    }
}
