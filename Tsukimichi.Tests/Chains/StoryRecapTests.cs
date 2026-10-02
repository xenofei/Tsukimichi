using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.Tests.Query;

namespace Tsukimichi.Tests.Chains;

/// <summary>
/// The story recap, "Previously…" (feature plan v5 collector extras, R9 F5): which quests a recap reads (only completed
/// ones, in story order, the last N of the main scenario or every completed one of a chain) and how the page is
/// joined for the clipboard. The text itself is the game's and is never in the repository.
/// </summary>
public sealed class StoryRecapTests
{
    // A linear main scenario of six quests (section 0) and a linear side chain of four (genre 200).
    private static readonly QuestRecord[] Msq =
    [
        Linked(66001, "Msq 1", 0, 50, 0),
        Linked(66002, "Msq 2", 0, 50, 66001),
        Linked(66003, "Msq 3", 0, 50, 66002),
        Linked(66004, "Msq 4", 0, 50, 66003),
        Linked(66005, "Msq 5", 0, 50, 66004),
        Linked(66006, "Msq 6", 0, 50, 66005),
    ];

    private static readonly QuestRecord[] Side =
    [
        Linked(67001, "Side 1", 2, 200, 0),
        Linked(67002, "Side 2", 2, 200, 67001),
        Linked(67003, "Side 3", 2, 200, 67002),
        Linked(67004, "Side 4", 2, 200, 67003),
    ];

    private static readonly QuestCatalog Catalog = QuestCatalog.Build([.. Msq, .. Side]);

    [Fact]
    public void The_last_completed_quests_come_in_story_order()
    {
        var done = new HashSet<uint> { 66001, 66002, 66003, 66004 };
        var picked = StoryRecap.MainScenario(Catalog, done.Contains, 3);

        Assert.Equal([66002u, 66003u, 66004u], picked.Select(q => q.RowId));
    }

    [Fact]
    public void Only_completed_quests_are_read_wherever_they_sit()
    {
        // A gap (66003 not done: another path, or the journal's order) is skipped, never filled with an open quest.
        var done = new HashSet<uint> { 66001, 66002, 66004, 66005 };
        var picked = StoryRecap.MainScenario(Catalog, done.Contains, 10);

        Assert.Equal([66001u, 66002u, 66004u, 66005u], picked.Select(q => q.RowId));
        Assert.All(picked, q => Assert.Contains(q.RowId, done));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void A_length_of_none_reads_nothing(int count)
    {
        Assert.Empty(StoryRecap.MainScenario(Catalog, static _ => true, count));
    }

    [Fact]
    public void Nothing_done_reads_nothing()
    {
        Assert.Empty(StoryRecap.MainScenario(Catalog, static _ => false, 10));
    }

    [Fact]
    public void A_chain_recap_reads_every_completed_quest_of_the_chain_in_play_order()
    {
        var chains = ChainCatalog.Build(Catalog, CuratedData.Empty);
        var chain = chains.ForQuest(67003);
        Assert.NotNull(chain);

        var done = new HashSet<uint> { 67003, 67001, 67002, 66001 };
        var picked = StoryRecap.Chain(chain, Catalog, done.Contains);

        Assert.Equal([67001u, 67002u, 67003u], picked.Select(q => q.RowId));
    }

    [Fact]
    public void A_chain_is_started_once_any_of_its_quests_is_done()
    {
        var chain = ChainCatalog.Build(Catalog, CuratedData.Empty).ForQuest(67001);
        Assert.NotNull(chain);

        // The main scenario's quests are not this story's.
        Assert.False(StoryRecap.HasStarted(chain, new HashSet<uint> { 66001, 66002 }.Contains));
        Assert.True(StoryRecap.HasStarted(chain, new HashSet<uint> { 67003 }.Contains));
        Assert.False(StoryRecap.HasStarted(chain, static _ => false));
    }

    [Theory]
    [InlineData(1, 3)]
    [InlineData(10, 10)]
    [InlineData(99, 40)]
    public void The_length_setting_is_clamped(int setting, int expected)
    {
        Assert.Equal(expected, StoryRecap.ClampLength(setting));
    }

    [Fact]
    public void The_page_joins_titles_and_entries_as_paragraphs()
    {
        var text = StoryRecap.Compose(
            "Previously in the main scenario",
            [
                new RecapChapter(66001, "Msq 1", ["First entry.", "  Second entry.  ", ""]),
                new RecapChapter(66002, "Msq 2", []),
                new RecapChapter(66003, "Msq 3", ["Last entry."]),
            ]);

        Assert.Equal(
            "Previously in the main scenario\n\nMsq 1\n\nFirst entry.\n\nSecond entry.\n\nMsq 2\n\nMsq 3\n\nLast entry.",
            text);
    }

    private static QuestRecord Linked(uint rowId, string name, uint section, uint genre, uint previous) =>
        QueryTestData.Quest(rowId, name, section: section, category: section * 10, genre: genre) with
        {
            PreviousQuests = previous == 0 ? Prereq.None : new Prereq([previous], JoinKind.All),
        };
}
