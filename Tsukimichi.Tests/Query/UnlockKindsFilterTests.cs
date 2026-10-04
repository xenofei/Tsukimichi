using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Unlocks;
using Tsukimichi.Tests.Unlocks;

namespace Tsukimichi.Tests.Query;

/// <summary>
/// The Unlocks filter and the search by what quests open (plan v7, 1.19.0 K3): a kind chip keeps the quests that open
/// one of the kinds on, the empty-result guard names it, a reveal clears it only when it hides the quest, and the
/// filter set's equality, copy, reset and badge all know it; an unlock name finds its quest in the table.
/// </summary>
public class UnlockKindsFilterTests
{
    private static readonly QuestCatalog Catalog = UnlockFindTests.Quests();
    private static readonly QuestUnlocks Unlocks = UnlockFindTests.Build();

    private static QueryContext Context(QuestUnlocks? unlocks) => QueryContext.Empty with { Unlocks = unlocks };

    private static uint[] Rows(FilterSet filters, string search = "", QuestUnlocks? unlocks = null) =>
        QuestQuery.Apply(Catalog, new Dictionary<uint, QuestState>(), filters, QuestScope.None, SortSpec.Default, search, Context(unlocks ?? Unlocks))
            .Rows.Select(r => r.Quest.RowId).ToArray();

    [Fact]
    public void A_kind_on_keeps_the_quests_that_open_it_and_two_keep_either()
    {
        Assert.Equal(
            [UnlockFindTests.JewelOfThavnair, UnlockFindTests.SecondCurrent],
            Rows(new FilterSet { UnlockKinds = [UnlockFindKind.Flying] }));
        Assert.Equal(
            [UnlockFindTests.JewelOfThavnair, UnlockFindTests.SecondCurrent, UnlockFindTests.CamelQuest],
            Rows(new FilterSet { UnlockKinds = [UnlockFindKind.Flying, UnlockFindKind.Mount] }));
        Assert.Equal(6, Rows(new FilterSet()).Length);
    }

    [Fact]
    public void Nothing_left_names_the_filter_and_without_the_index_it_keeps_nothing()
    {
        var filters = new FilterSet { UnlockKinds = [UnlockFindKind.Job] };
        var result = QuestQuery.Apply(Catalog, new Dictionary<uint, QuestState>(), filters, QuestScope.None, SortSpec.Default, string.Empty, Context(Unlocks));
        Assert.Empty(result.Rows);
        Assert.Equal([FilterNames.UnlockKinds], result.Empty!.Filters.ToArray());

        Assert.Empty(QuestQuery.Apply(Catalog, new Dictionary<uint, QuestState>(), new FilterSet { UnlockKinds = [UnlockFindKind.Area] }, QuestScope.None, SortSpec.Default, string.Empty, Context(null)).Rows);
    }

    [Fact]
    public void An_unlock_name_finds_its_quest_in_the_table()
    {
        Assert.Equal([UnlockFindTests.BoundForKugane, UnlockFindTests.OtherWayToKugane], Rows(new FilterSet(), "kugane"));

        // Terms may mix the quest's own name and what it opens.
        Assert.Equal([UnlockFindTests.JewelOfThavnair], Rows(new FilterSet(), "jewel flying"));

        // Without the index, only names and rewards match.
        Assert.Empty(Rows(new FilterSet(), "kugane", QuestUnlocks.Empty));
        Assert.False(QuestQuery.SearchHides(Catalog.GetByRowId(UnlockFindTests.BoundForKugane)!, "kugane", Context(Unlocks) with { SearchIndex = SearchIndex.For(Catalog) }));
    }

    [Fact]
    public void A_reveal_clears_the_filter_only_when_it_hides_the_quest()
    {
        var filters = new FilterSet { UnlockKinds = [UnlockFindKind.Flying] };
        Assert.False(QuestQuery.ClearFiltersHiding(Catalog.GetByRowId(UnlockFindTests.JewelOfThavnair)!, filters, null, null, Unlocks));
        Assert.True(filters.UnlockKindsEngaged());

        Assert.True(QuestQuery.ClearFiltersHiding(Catalog.GetByRowId(UnlockFindTests.Plain)!, filters, null, null, Unlocks));
        Assert.False(filters.UnlockKindsEngaged());
    }

    [Fact]
    public void The_filter_set_copies_compares_resets_and_counts_the_kinds()
    {
        var filters = new FilterSet { UnlockKinds = [UnlockFindKind.Mount, UnlockFindKind.Flying] };
        var copy = filters.Clone();
        Assert.Equal(filters, copy);
        Assert.Equal(filters.GetHashCode(), new FilterSet { UnlockKinds = [UnlockFindKind.Flying, UnlockFindKind.Mount] }.GetHashCode());
        Assert.NotEqual(filters, new FilterSet());
        copy.UnlockKinds.Remove(UnlockFindKind.Mount);
        Assert.Equal(2, filters.UnlockKinds.Count);

        Assert.True(filters.IsActive());
        Assert.Equal(1, FilterBadge.Count(filters));
        Assert.True(FilterSummary.IsSet(filters, FilterGroup.Unlocks));
        Assert.True(FilterSummary.CanReset(filters, null));

        filters.Reset();
        Assert.Empty(filters.UnlockKinds);
        Assert.Equal(new FilterSet(), filters);
        Assert.False(FilterSummary.IsSet(filters, FilterGroup.Unlocks));
    }
}
