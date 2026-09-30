using System.Text.Json;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using static Tsukimichi.Tests.Query.QueryTestData;

namespace Tsukimichi.Tests.Query;

/// <summary>The Abandoned filter chip (P10): FilterSet plumbing and the query over the viewed character's ledger.</summary>
public class AbandonedFilterTests
{
    private static readonly QuestCatalog Catalog = QuestCatalog.Build(
    [
        Quest(1, "Kept"),
        Quest(2, "Dropped"),
        Quest(3, "Also dropped"),
    ]);

    private static readonly Dictionary<uint, QuestState> AllStates = States(Catalog, QuestState.Ready);

    [Fact]
    public void Abandoned_only_is_off_by_default_and_counts_as_active_when_on()
    {
        Assert.False(new FilterSet().AbandonedOnly);
        Assert.True(new FilterSet { AbandonedOnly = true }.IsActive());
    }

    [Fact]
    public void Abandoned_only_survives_clone_equality_reset_and_json()
    {
        var filters = new FilterSet { AbandonedOnly = true };

        var clone = filters.Clone();
        Assert.Equal(filters, clone);
        Assert.Equal(filters.GetHashCode(), clone.GetHashCode());
        Assert.NotEqual(filters, new FilterSet());

        var back = JsonSerializer.Deserialize<FilterSet>(JsonSerializer.Serialize(filters));
        Assert.True(back!.AbandonedOnly);

        filters.Reset();
        Assert.False(filters.AbandonedOnly);
        Assert.False(filters.IsActive());
    }

    [Fact]
    public void Abandoned_only_keeps_the_ledger_quests()
    {
        var ctx = QueryContext.Empty with { Abandoned = new HashSet<ushort> { 2, 3 } };

        var result = Run(Catalog, AllStates, new FilterSet { AbandonedOnly = true }, ctx: ctx);

        Assert.Equal(new uint[] { 2, 3 }, RowIds(result));
    }

    [Fact]
    public void Abandoned_only_without_a_ledger_keeps_nothing_and_names_itself()
    {
        var result = Run(Catalog, AllStates, new FilterSet { AbandonedOnly = true });

        Assert.Empty(result.Rows);
        Assert.Equal([FilterNames.Abandoned], result.Empty!.Filters);
    }

    [Fact]
    public void Abandoned_only_combines_with_the_other_filters()
    {
        var states = States((1, QuestState.Ready), (2, QuestState.Completed), (3, QuestState.Ready));
        var ctx = QueryContext.Empty with { Abandoned = new HashSet<ushort> { 2, 3 } };

        var result = Run(Catalog, states, new FilterSet { AbandonedOnly = true, HideCompleted = true }, ctx: ctx);

        Assert.Equal(new uint[] { 3 }, RowIds(result));
    }
}
