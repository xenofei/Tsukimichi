using Tsukimichi.Core.Query;

namespace Tsukimichi.Tests.Query;

/// <summary>
/// <see cref="NewGamePlusChapters"/> (1.19.0, C4): a quest's place in its chapter, walking the chapter's parts along the
/// sheet's links and counting a start city's own steps once, and the guess from a capture's replayed quests.
/// </summary>
public sealed class NewGamePlusChaptersTests
{
    // Chapter 1, split by start city: parts 1 and 2 (two cities) both lead into part 3. Part 1 holds a shared quest,
    // then two city variants side by side (masks 1 and 2); part 3 two shared quests. Chapter 2 is one part.
    private static readonly NewGamePlusChapters Chapters = new(
        [
            new NewGamePlusPart(1, 1, 3, [new(100, 0), new(101, 1), new(102, 2)]),
            new NewGamePlusPart(2, 1, 3, [new(110, 0), new(111, 0)]),
            new NewGamePlusPart(3, 1, 0, [new(120, 0), new(121, 0)]),
            new NewGamePlusPart(4, 2, 0, [new(200, 0), new(201, 0), new(202, 0)]),
            new NewGamePlusPart(5, 9, 0, [new(900, 0)]),
        ],
        new Dictionary<uint, string> { [1] = "A Realm Reborn - Part 1", [2] = "Heavensward - Part 1" });

    [Fact]
    public void A_quest_reads_its_place_in_its_chapter_from_the_first_part_to_the_last()
    {
        Assert.Equal(new NewGamePlusPosition(2, "Heavensward - Part 1", 2, 3), Chapters.Position(201));

        // Part 3 follows part 1 (the lowest part that links to it): 100, the city's own 101, then 120 and 121.
        Assert.Equal(new NewGamePlusPosition(1, "A Realm Reborn - Part 1", 4, 4), Chapters.Position(121));
    }

    [Fact]
    public void A_city_variant_counts_its_own_city_only()
    {
        // 102 is the second city's version of 101: the count is the same four steps, 102 the second.
        Assert.Equal(new NewGamePlusPosition(1, "A Realm Reborn - Part 1", 2, 4), Chapters.Position(102));
        Assert.Equal(new NewGamePlusPosition(1, "A Realm Reborn - Part 1", 2, 4), Chapters.Position(101));
    }

    [Fact]
    public void The_HUD_hint_picks_the_part_a_shared_quest_is_played_from()
    {
        // Part 2 is the other city's way into part 3: 110, 111, then the shared 120 and 121.
        Assert.Equal(new NewGamePlusPosition(1, "A Realm Reborn - Part 1", 2, 4), Chapters.Position(111, hint: 2));

        // A shared quest is counted along the first city's way unless the HUD says otherwise.
        Assert.Equal(3, Chapters.Position(120, hint: 1)?.Index);
    }

    [Fact]
    public void A_quest_no_chapter_lists_or_an_unnamed_chapter_has_no_position()
    {
        Assert.Null(Chapters.Position(999));
        Assert.Null(Chapters.Position(900));
        Assert.Null(NewGamePlusChapters.Empty.Position(100));
        Assert.True(Chapters.Lists(900));
        Assert.Equal(string.Empty, Chapters.ChapterName(9));
    }

    [Fact]
    public void The_guess_takes_the_chapter_most_replayed_quests_sit_in_and_the_earliest_of_them()
    {
        Assert.Equal(new NewGamePlusPosition(2, "Heavensward - Part 1", 2, 3), Chapters.Guess([202, 201, 121]));
        Assert.Null(Chapters.Guess([999]));
        Assert.Null(Chapters.Guess([]));
    }

    [Fact]
    public void A_link_loop_ends_the_walk()
    {
        var looped = new NewGamePlusChapters(
            [new NewGamePlusPart(1, 1, 2, [new(10, 0)]), new NewGamePlusPart(2, 1, 1, [new(11, 0)])],
            new Dictionary<uint, string> { [1] = "Loop" });

        Assert.Equal(2, looped.Position(11)?.Count);
    }
}
