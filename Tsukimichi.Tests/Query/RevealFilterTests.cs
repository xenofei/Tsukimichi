using System.Collections.Frozen;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using static Tsukimichi.Tests.Query.QueryTestData;

namespace Tsukimichi.Tests.Query;

/// <summary>
/// A jump from the detail pane (or any "Show in Journal") selects its quest in the table (release 1.3 review): the
/// filters a reveal does not clear by themselves (expansion, patch, level range, job category, reward kinds, Repeatable
/// only, Seasonal active only) and the search are cleared where they would hide the quest, and only there.
/// </summary>
public class RevealFilterTests
{
    private static readonly QuestRecord Target = Quest(1, "Coming to Limsa Lominsa", level: 50, expansion: 2, classJobCategory: 7, festival: 9, repeatable: false, rewards: Reward(RewardKind.Emote, "Wave"))
        with { AddedIn = "7.5" };

    private static readonly QuestCatalog Catalog = QuestCatalog.Build([Target, Quest(2, "Something else", level: 10)]);

    [Fact]
    public void Every_filter_that_hides_the_quest_is_cleared_and_it_is_listed()
    {
        var filters = new FilterSet
        {
            Expansions = [0, 1],
            AddedIn = "6.0",
            LevelMin = 1,
            LevelMax = 20,
            ClassJobCategoryId = 3,
            RewardKinds = new() { [RewardKind.Emote] = TriState.Hidden },
            RepeatableOnly = true,
            SeasonalActiveOnly = true,
        };
        Assert.Empty(RowIds(Run(Catalog, States(Catalog, QuestState.Blocked), filters)));

        Assert.True(QuestQuery.ClearFiltersHiding(Target, filters, FrozenSet<ushort>.Empty));
        Assert.Contains(1u, RowIds(Run(Catalog, States(Catalog, QuestState.Blocked), filters)));
        Assert.False(filters.IsActive());
    }

    [Fact]
    public void A_filter_the_quest_passes_stays_on()
    {
        var filters = new FilterSet
        {
            Expansions = [2],
            AddedIn = "7.5",
            LevelMin = 40,
            LevelMax = 60,
            ClassJobCategoryId = 7,
            RewardKinds = new() { [RewardKind.Emote] = TriState.Only },
            SeasonalActiveOnly = true,
        };
        var before = filters.Clone();

        Assert.False(QuestQuery.ClearFiltersHiding(Target, filters, new HashSet<ushort> { 9 }));
        Assert.Equal(before, filters);

        // Only the one that hides it goes.
        filters.RepeatableOnly = true;
        Assert.True(QuestQuery.ClearFiltersHiding(Target, filters, new HashSet<ushort> { 9 }));
        Assert.False(filters.RepeatableOnly);
        Assert.Equal(before, filters);
    }

    [Fact]
    public void A_search_that_does_not_match_hides_the_quest()
    {
        var ctx = QueryContext.Empty with { SearchIndex = SearchIndex.For(Catalog) };

        Assert.False(QuestQuery.SearchHides(Target, string.Empty, ctx));
        Assert.False(QuestQuery.SearchHides(Target, "limsa", ctx));
        Assert.True(QuestQuery.SearchHides(Target, "something", ctx));

        // Its journal text matched: listed, so not hidden.
        Assert.False(QuestQuery.SearchHides(Target, "something", ctx with { JournalHits = new HashSet<uint> { 1 } }));

        // No index to ask: a search is taken to hide it.
        Assert.True(QuestQuery.SearchHides(Target, "limsa", null));
    }
}
