using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Text;
using static Tsukimichi.Tests.Query.QueryTestData;

namespace Tsukimichi.Tests.Query;

/// <summary>
/// Journal text search (P9) in the query: a quest whose journal words match passes the search box even when its name
/// does not, and only the quests the viewed character completed ever match that way.
/// </summary>
public sealed class JournalSearchTests
{
    private static readonly QuestCatalog Catalog = QuestCatalog.Build(
    [
        Quest(65575, "Coming to the City"),
        Quest(65576, "A Knight's Errand"),
        Quest(65577, "The Unplayed One"),
    ]);

    private static readonly IReadOnlyDictionary<uint, QuestState> States = QueryTestData.States(
        (65575, QuestState.Completed),
        (65576, QuestState.Completed),
        (65577, QuestState.Ready));

    [Fact]
    public void Journal_words_of_completed_quests_widen_the_name_search()
    {
        var index = Index();
        var evaluations = Evaluations(States);
        var hits = index.Match("dragon", id => evaluations.TryGetValue(id, out var e) && JournalVisibility.IsCompleted(e.State));
        var ctx = QueryContext.Empty with { JournalHits = hits };

        // The unplayed quest's journal names the dragon too, but it is not completed, so only the knight's errand shows.
        Assert.Equal(new uint[] { 65576 }, RowIds(Run(Catalog, evaluations, search: "dragon", ctx: ctx)));

        // Without journal hits the search is by name only and finds nothing.
        Assert.Empty(RowIds(Run(Catalog, evaluations, search: "dragon")));

        // A name match still shows alongside.
        var both = index.Match("city", id => evaluations.TryGetValue(id, out var e) && JournalVisibility.IsCompleted(e.State));
        Assert.Equal(new uint[] { 65575 }, RowIds(Run(Catalog, evaluations, search: "city", ctx: QueryContext.Empty with { JournalHits = both })));
    }

    private static JournalTextIndex Index()
    {
        // Made-up words: tests never carry the game's text.
        var builder = new JournalTextIndex.Builder();
        builder.Add(65575, JournalTokenizer.Words("You arrive in the city."));
        builder.Add(65576, JournalTokenizer.Words("A dragon circles the keep."));
        builder.Add(65577, JournalTokenizer.Words("The dragon returns."));
        return builder.Build("test", "en");
    }
}
