using System.Diagnostics;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using static Tsukimichi.Tests.Query.QueryTestData;

namespace Tsukimichi.Tests.Query;

public class QueryPerfTests
{
    private static QuestCatalog Synthetic(int count)
    {
        var quests = new QuestRecord[count];
        for (var i = 0; i < count; i++)
        {
            var rowId = (uint)(65536 + i);
            var section = (uint)(1 + i % 5);
            var category = (uint)(10 + i % 23);
            var genre = (uint)(i % 40 == 0 ? 0 : 100 + i % 150);
            quests[i] = Quest(
                rowId,
                $"Quest number {i} of the {(i % 3 == 0 ? "moon" : "path")}",
                section, category, genre,
                sortKey: count - i,
                level: (byte)(1 + i % 100),
                expansion: (byte)(i % 6),
                classJobCategory: (uint)(i % 7),
                repeatable: i % 11 == 0,
                festival: (ushort)(i % 13 == 0 ? 7 : 0),
                internalId: $"Syn{i:D5}_{i % 1000:D5}",
                rewards:
                [
                    Reward(RewardKind.Item, $"Reward item {i}", (uint)i),
                    Reward((RewardKind)(i % 23), $"Kind reward {i % 23}", (uint)i),
                ]);
        }

        return QuestCatalog.Build(quests);
    }

    [Fact]
    [Trait("Category", "Perf")]
    public void Six_thousand_quests_query_under_250ms()
    {
        var catalog = Synthetic(6000);
        var states = new Dictionary<uint, QuestState>(catalog.Count);
        var i = 0;
        foreach (var quest in catalog.All)
        {
            states[quest.RowId] = (QuestState)(i++ % 8);
        }

        var index = SearchIndex.For(catalog);
        var filters = new FilterSet { HideCompleted = true, LevelMin = 5, LevelMax = 90 };
        filters.Expansions.Add(0);
        filters.Expansions.Add(1);
        filters.Expansions.Add(2);
        filters.RewardKinds[RewardKind.Mount] = TriState.Hidden;
        var ctx = QueryContext.Empty with { SearchIndex = index, Pinned = new HashSet<uint> { 65540 } };
        var sort = new SortSpec(SortColumn.Name, false);

        // Warm up the JIT once, then measure a filtered, searched, name-sorted query.
        QuestQuery.Apply(catalog, states, filters, QuestScope.None, sort, "moon", ctx);

        var watch = Stopwatch.StartNew();
        var result = QuestQuery.Apply(catalog, states, filters, QuestScope.None, sort, "moon 1", ctx);
        watch.Stop();

        Assert.NotEmpty(result.Rows);
        Assert.True(watch.ElapsedMilliseconds < 250, $"query took {watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    [Trait("Category", "Perf")]
    public void Empty_reason_on_six_thousand_quests_is_cheap()
    {
        var catalog = Synthetic(6000);
        var states = States(catalog, QuestState.Completed);
        var ctx = QueryContext.Empty with { SearchIndex = SearchIndex.For(catalog) };
        var filters = new FilterSet { HideCompleted = true, AvailableOnly = true, RepeatableOnly = true };

        QuestQuery.Apply(catalog, states, filters, QuestScope.None, SortSpec.Default, "", ctx);

        var watch = Stopwatch.StartNew();
        var result = QuestQuery.Apply(catalog, states, filters, QuestScope.None, SortSpec.Default, "", ctx);
        watch.Stop();

        Assert.Empty(result.Rows);
        Assert.NotNull(result.Empty);
        Assert.True(watch.ElapsedMilliseconds < 250, $"query took {watch.ElapsedMilliseconds} ms");
    }
}
