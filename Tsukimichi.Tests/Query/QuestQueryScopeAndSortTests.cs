using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using static Tsukimichi.Tests.Query.QueryTestData;

namespace Tsukimichi.Tests.Query;

public class QuestQueryScopeAndSortTests
{
    private static readonly QuestCatalog Catalog = QuestCatalog.Build(
    [
        Quest(1, "Bravo", section: 1, category: 10, genre: 100, sortKey: 30, level: 5, expansion: 1),
        Quest(2, "alpha", section: 1, category: 10, genre: 101, sortKey: 20, level: 5, expansion: 0),
        Quest(3, "Charlie", section: 1, category: 11, genre: 102, sortKey: 10, level: 1, expansion: 2),
        Quest(4, "delta", section: 2, category: 12, genre: 103, sortKey: 40, level: 9, expansion: 0),
        Quest(5, "Echo", section: 2, category: 12, genre: 103, sortKey: 50, level: 5, expansion: 1),
        Quest(6, "Foxtrot", section: 2, category: 13, genre: 0, sortKey: 60, level: 3, expansion: 3),
    ]);

    private static readonly Dictionary<uint, QuestState> AllStates = States(
        (1, QuestState.Completed),
        (2, QuestState.Ready),
        (3, QuestState.Blocked),
        (4, QuestState.Ready),
        (5, QuestState.Accepted),
        (6, QuestState.Unknown));

    [Fact]
    public void Scope_none_is_the_whole_listed_catalog()
    {
        var result = Run(Catalog, AllStates, scope: QuestScope.None);
        Assert.Equal(new uint[] { 3, 2, 1, 4, 5 }, RowIds(result));
        Assert.Equal(5, result.TotalInScope);
    }

    [Fact]
    public void Scope_section()
    {
        var result = Run(Catalog, AllStates, scope: QuestScope.Section(2));
        Assert.Equal(new uint[] { 4, 5 }, RowIds(result));
        Assert.Equal(2, result.TotalInScope);

        // Unlisted quests never appear under a journal node, whatever the include flag says.
        var withUnlisted = Run(Catalog, AllStates, new FilterSet { IncludeUnlisted = true }, scope: QuestScope.Section(2));
        Assert.Equal(new uint[] { 4, 5 }, RowIds(withUnlisted));
        Assert.Equal(2, withUnlisted.TotalInScope);
    }

    [Fact]
    public void Scope_category()
    {
        var result = Run(Catalog, AllStates, scope: QuestScope.Category(10));
        Assert.Equal(new uint[] { 2, 1 }, RowIds(result));
    }

    [Fact]
    public void Scope_genre()
    {
        var result = Run(Catalog, AllStates, scope: QuestScope.Genre(103));
        Assert.Equal(new uint[] { 4, 5 }, RowIds(result));
    }

    [Fact]
    public void Journal_nodes_holding_only_unlisted_quests_are_empty_scopes_even_when_included()
    {
        var included = new FilterSet { IncludeUnlisted = true };

        var category = Run(Catalog, AllStates, included, scope: QuestScope.Category(13));
        Assert.Empty(category.Rows);
        Assert.Equal(0, category.TotalInScope);
        Assert.True(category.Empty!.ScopeIsEmpty);
        Assert.Empty(category.Empty.Filters);

        var genre = Run(Catalog, AllStates, included, scope: QuestScope.Genre(0));
        Assert.Empty(genre.Rows);
        Assert.True(genre.Empty!.ScopeIsEmpty);
    }

    [Fact]
    public void Scope_none_includes_unlisted_only_when_asked()
    {
        Assert.DoesNotContain(6u, RowIds(Run(Catalog, AllStates, scope: QuestScope.None)));

        var included = Run(Catalog, AllStates, new FilterSet { IncludeUnlisted = true }, scope: QuestScope.None);
        Assert.Contains(6u, RowIds(included));
        Assert.Equal(6, included.TotalInScope);
    }

    [Fact]
    public void Scope_unknown_node_is_empty_scope()
    {
        var result = Run(Catalog, AllStates, scope: QuestScope.Genre(999));
        Assert.Empty(result.Rows);
        Assert.Equal(0, result.TotalInScope);
        Assert.NotNull(result.Empty);
        Assert.True(result.Empty!.ScopeIsEmpty);
        Assert.Empty(result.Empty.Filters);
    }

    [Fact]
    public void Scope_virtual_feature_uses_context_ids_in_journal_order()
    {
        var ctx = QueryContext.Empty with { FeatureQuestIds = new HashSet<uint> { 5, 1, 6, 42 } };
        var result = Run(Catalog, AllStates, scope: QuestScope.VirtualFeature, ctx: ctx);
        Assert.Equal(new uint[] { 1, 5 }, RowIds(result));

        var withUnlisted = Run(Catalog, AllStates, new FilterSet { IncludeUnlisted = true }, scope: QuestScope.VirtualFeature, ctx: ctx);
        Assert.Equal(new uint[] { 1, 5, 6 }, RowIds(withUnlisted));
    }

    [Fact]
    public void Scope_virtual_unlisted_shows_unlisted_regardless_of_include_flag()
    {
        var result = Run(Catalog, AllStates, scope: QuestScope.VirtualUnlisted);
        Assert.Equal(new uint[] { 6 }, RowIds(result));
        Assert.Equal(1, result.TotalInScope);
    }

    [Fact]
    public void Scope_factories_carry_kind_and_id()
    {
        Assert.Equal(ScopeKind.None, QuestScope.None.Kind);
        Assert.Equal(ScopeKind.Section, QuestScope.Section(1).Kind);
        Assert.Equal(1u, QuestScope.Section(1).Id);
        Assert.Equal(ScopeKind.Category, QuestScope.Category(2).Kind);
        Assert.Equal(ScopeKind.Genre, QuestScope.Genre(3).Kind);
        Assert.Equal(ScopeKind.VirtualFeature, QuestScope.VirtualFeature.Kind);
        Assert.Equal(ScopeKind.VirtualUnlisted, QuestScope.VirtualUnlisted.Kind);
        Assert.Equal(QuestScope.Genre(3), new QuestScope(ScopeKind.Genre, 3));
    }

    [Fact]
    public void Default_sort_is_journal_order()
    {
        var result = Run(Catalog, AllStates, new FilterSet { IncludeUnlisted = true });
        Assert.Equal(new uint[] { 3, 2, 1, 4, 5, 6 }, RowIds(result));
    }

    [Fact]
    public void Journal_descending_reverses()
    {
        var result = Run(Catalog, AllStates, new FilterSet { IncludeUnlisted = true }, sort: new SortSpec(SortColumn.Journal, true));
        Assert.Equal(new uint[] { 6, 5, 4, 1, 2, 3 }, RowIds(result));
    }

    [Fact]
    public void Name_sort_ignores_case_both_directions()
    {
        var asc = Run(Catalog, AllStates, sort: new SortSpec(SortColumn.Name, false));
        Assert.Equal(new[] { "alpha", "Bravo", "Charlie", "delta", "Echo" }, Names(asc));

        var desc = Run(Catalog, AllStates, sort: new SortSpec(SortColumn.Name, true));
        Assert.Equal(new[] { "Echo", "delta", "Charlie", "Bravo", "alpha" }, Names(desc));
    }

    [Fact]
    public void Level_sort_is_stable_on_ties()
    {
        // Levels in journal order: 3->1, 2->5, 1->5, 4->9, 5->5; ties keep journal order.
        var asc = Run(Catalog, AllStates, sort: new SortSpec(SortColumn.Level, false));
        Assert.Equal(new uint[] { 3, 2, 1, 5, 4 }, RowIds(asc));

        var desc = Run(Catalog, AllStates, sort: new SortSpec(SortColumn.Level, true));
        Assert.Equal(new uint[] { 4, 2, 1, 5, 3 }, RowIds(desc));
    }

    [Fact]
    public void State_sort_follows_enum_order_and_is_stable()
    {
        var asc = Run(Catalog, AllStates, sort: new SortSpec(SortColumn.State, false));
        // Ready: 2, 4 (journal order), Accepted: 5, Blocked: 3, Completed: 1
        Assert.Equal(new uint[] { 2, 4, 5, 3, 1 }, RowIds(asc));

        var desc = Run(Catalog, AllStates, sort: new SortSpec(SortColumn.State, true));
        Assert.Equal(new uint[] { 1, 3, 5, 2, 4 }, RowIds(desc));
    }

    [Fact]
    public void Expansion_sort_is_stable()
    {
        var asc = Run(Catalog, AllStates, sort: new SortSpec(SortColumn.Expansion, false));
        // expansion 0: 2, 4; 1: 1, 5; 2: 3
        Assert.Equal(new uint[] { 2, 4, 1, 5, 3 }, RowIds(asc));

        var desc = Run(Catalog, AllStates, sort: new SortSpec(SortColumn.Expansion, true));
        Assert.Equal(new uint[] { 3, 1, 5, 2, 4 }, RowIds(desc));
    }

    [Fact]
    public void Sort_with_many_equal_keys_keeps_journal_order()
    {
        var quests = Enumerable.Range(1, 500).Select(i => Quest((uint)i, $"Quest {i}", sortKey: 1000 - i, level: 10)).ToArray();
        var catalog = QuestCatalog.Build(quests);
        var expected = catalog.All.Select(q => q.RowId).ToArray();

        var asc = Run(catalog, States(catalog, QuestState.Ready), sort: new SortSpec(SortColumn.Level, false));
        Assert.Equal(expected, RowIds(asc));

        var desc = Run(catalog, States(catalog, QuestState.Ready), sort: new SortSpec(SortColumn.Level, true));
        Assert.Equal(expected, RowIds(desc));
    }
}
