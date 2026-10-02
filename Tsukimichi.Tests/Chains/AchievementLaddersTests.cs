using Tsukimichi.Core.Chains;

namespace Tsukimichi.Tests.Chains;

/// <summary>
/// The achievements that need several quests (feature plan v5 collector extras, R5 F6): progress from the completion
/// bits, the quests still to do in the ladder's order, and the earned flag (the game's when it has one, else every
/// quest done).
/// </summary>
public sealed class AchievementLaddersTests
{
    private static readonly AchievementLadder TalesOfWar = new(314, "Tales of War", [65801, 66103, 65855, 65975, 65612]);
    private static readonly AchievementLadder WarStillWageth = new(1165, "The War Still Wageth On", [67651, 67652]);

    [Fact]
    public void Progress_counts_the_done_quests_and_lists_the_rest_in_ladder_order()
    {
        var done = new HashSet<uint> { 66103, 65975 };
        var progress = AchievementLadders.Progress(TalesOfWar, done.Contains);

        Assert.Equal(2, progress.Done);
        Assert.Equal(5, progress.Total);
        Assert.Equal([65801u, 65855u, 65612u], progress.Remaining);
        Assert.False(progress.Earned);
        Assert.False(progress.FromGame);
        Assert.False(progress.IsComplete);
        Assert.Equal(0.4f, progress.Fraction, 3);
    }

    [Fact]
    public void Every_quest_done_reads_earned_without_the_game_list()
    {
        var progress = AchievementLadders.Progress(WarStillWageth, static _ => true);

        Assert.True(progress.IsComplete);
        Assert.True(progress.Earned);
        Assert.Empty(progress.Remaining);
        Assert.False(progress.FromGame);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_game_flag_decides_earned_when_it_is_known(bool flag)
    {
        // Half the quests done, yet the game's list is the word on the achievement (an old quest line the game
        // credited, or a list loaded before the last turn-in was saved).
        var progress = AchievementLadders.Progress(WarStillWageth, static id => id == 67651, flag);

        Assert.Equal(1, progress.Done);
        Assert.Equal(flag, progress.Earned);
        Assert.True(progress.FromGame);
    }

    [Fact]
    public void Build_drops_single_quest_sets_dedupes_and_indexes_by_quest()
    {
        var ladders = AchievementLadders.Build(
        [
            TalesOfWar,
            new AchievementLadder(1, "One quest", [65000]),
            new AchievementLadder(2, "Listed twice", [67651, 67651, 70000]),
        ]);

        Assert.Equal(2, ladders.Count);
        Assert.Equal([65801u, 66103u, 65855u, 65975u, 65612u], ladders.All[0].RowIds);
        Assert.Equal([67651u, 70000u], ladders.All[1].RowIds);
        Assert.Single(ladders.ForQuest(65855));
        Assert.Equal("Listed twice", Assert.Single(ladders.ForQuest(67651)).Name);
        Assert.Empty(ladders.ForQuest(65000));
        Assert.Same(AchievementLadders.Empty, AchievementLadders.Build([]));
    }
}
