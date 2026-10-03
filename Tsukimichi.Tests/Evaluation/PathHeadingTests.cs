using System.Globalization;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Tests.Evaluation;

/// <summary>
/// The Path card's header (feature plan v6 U5, owner point 8): what is left before the quest and which quest to do
/// next, never "831 steps · 830 done"; nothing when the quest is the next one or done; the totals only on hover.
/// </summary>
public sealed class PathHeadingTests
{
    private static PathStep[] Path(params QuestState[] states) =>
        states.Select((state, i) => new PathStep(100u + (uint)i, state, states.Length - 1 - i)).ToArray();

    [Fact]
    public void The_whole_main_scenario_done_before_the_quest_says_nothing()
    {
        // 830 earlier quests done and the target Ready: the old "831 steps · 830 done".
        var states = Enumerable.Repeat(QuestState.Completed, 830).Append(QuestState.Ready).ToArray();

        var heading = PathHeading.For(Path(states), statesKnown: true);

        Assert.Same(PathHeading.None, heading);
        Assert.Equal(string.Empty, heading.Caption);
        Assert.Equal(string.Empty, heading.Tooltip);
        Assert.False(heading.HasNext);
    }

    [Fact]
    public void A_done_target_says_nothing_even_with_earlier_quests_open()
    {
        var heading = PathHeading.For(Path(QuestState.Ready, QuestState.Completed), statesKnown: true);

        Assert.Same(PathHeading.None, heading);
    }

    [Fact]
    public void Quests_left_before_the_target_are_counted_and_the_first_is_next()
    {
        var heading = PathHeading.For(
            Path(QuestState.Completed, QuestState.Completed, QuestState.Ready, QuestState.Blocked, QuestState.Blocked, QuestState.Blocked),
            statesKnown: true);

        Assert.Equal(new PathHeading(3, 2, 5, 2), heading);
        Assert.Equal("3 quests before this one", heading.Caption);
        Assert.Equal("2 of 5 earlier quests done", heading.Tooltip);
        Assert.True(heading.HasNext);
    }

    [Fact]
    public void One_quest_before_reads_in_the_singular()
    {
        var heading = PathHeading.For(Path(QuestState.Completed, QuestState.Accepted, QuestState.Blocked), statesKnown: true);

        Assert.Equal("1 quest before this one", heading.Caption);
        Assert.Equal(1, heading.NextIndex);
    }

    [Fact]
    public void A_skipped_quest_early_on_is_the_one_to_do_next()
    {
        var heading = PathHeading.For(Path(QuestState.Completed, QuestState.Ready, QuestState.Completed, QuestState.Blocked), statesKnown: true);

        Assert.Equal(1, heading.NextIndex);
        Assert.Equal(1, heading.QuestsBefore);
    }

    [Fact]
    public void Large_counts_are_grouped()
    {
        var states = Enumerable.Repeat(QuestState.Blocked, 1200).Append(QuestState.Blocked).ToArray();

        var heading = PathHeading.For(Path(states), statesKnown: true);

        var grouped = 1200.ToString("N0", CultureInfo.CurrentCulture);
        Assert.Equal($"{grouped} quests before this one", heading.Caption);
        Assert.Equal($"0 of {grouped} earlier quests done", heading.Tooltip);
        Assert.Equal(0, heading.NextIndex);
    }

    [Fact]
    public void Without_a_character_or_earlier_quests_there_is_no_heading()
    {
        Assert.Same(PathHeading.None, PathHeading.For(Path(QuestState.Blocked, QuestState.Blocked), statesKnown: false));
        Assert.Same(PathHeading.None, PathHeading.For(Path(QuestState.Ready), statesKnown: true));
        Assert.Same(PathHeading.None, PathHeading.For([], statesKnown: true));
    }
}
