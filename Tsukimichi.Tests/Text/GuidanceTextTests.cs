using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Text;

namespace Tsukimichi.Tests.Text;

/// <summary>
/// The plain chat lines of <c>/tsuki msq</c>, <c>next</c> and <c>go</c> (plan v7, 1.21.0 P8): the spec's lines word for
/// word, coordinates read "X 27, Y 34.8", a distance only in the same zone with one of eight compass words, no glyphs,
/// symbols, brackets or percent signs, masked names in words, and at most one "Say what's next" line every 10 s.
/// </summary>
public sealed class GuidanceTextTests
{
    private static readonly char[] Forbidden = ['·', '›', '‹', '☾', '→', '←', '(', ')', '[', ']', '<', '>', '%', '/', '|', '•', '★', '—'];

    private static void AssertPlain(GuidanceLine line)
    {
        Assert.True(GuidanceText.IsSpeakable(line.Text), $"not speakable: {line.Text}");
        Assert.True(line.Text.IndexOfAny(Forbidden) < 0, $"a symbol in: {line.Text}");
        Assert.DoesNotContain("  ", line.Text, StringComparison.Ordinal);
        Assert.EndsWith(".", line.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(27f, "27")]
    [InlineData(27.04f, "27")]
    [InlineData(34.8f, "34.8")]
    [InlineData(34.75f, "34.8")]
    [InlineData(7.8f, "7.8")]
    [InlineData(10.97f, "11")]
    public void Coordinates_have_one_decimal_and_drop_a_trailing_zero(float value, string expected)
    {
        Assert.Equal(expected, GuidanceText.Number(value));
    }

    [Fact]
    public void Coordinates_read_x_then_y()
    {
        Assert.Equal("X 27, Y 34.8", GuidanceText.Coordinates(27.0f, 34.8f));
    }

    [Theory]
    [InlineData(0f, -10f, CompassPoint.North)]
    [InlineData(10f, -10f, CompassPoint.NorthEast)]
    [InlineData(10f, 0f, CompassPoint.East)]
    [InlineData(10f, 10f, CompassPoint.SouthEast)]
    [InlineData(0f, 10f, CompassPoint.South)]
    [InlineData(-10f, 10f, CompassPoint.SouthWest)]
    [InlineData(-10f, 0f, CompassPoint.West)]
    [InlineData(-10f, -10f, CompassPoint.NorthWest)]
    [InlineData(1f, -10f, CompassPoint.North)]
    public void The_compass_follows_the_map(float dx, float dz, CompassPoint expected)
    {
        // North is towards negative z, east towards positive x, as on the game's map.
        Assert.Equal(expected, GuidanceText.Compass(100f, 100f, 100f + dx, 100f + dz));
    }

    [Fact]
    public void Every_compass_word_is_a_plain_word()
    {
        var words = Enum.GetValues<CompassPoint>().Select(GuidanceText.CompassWord).ToList();
        Assert.Equal(8, words.Distinct().Count());
        Assert.All(words, w => Assert.True(GuidanceText.IsSpeakable(w)));
        Assert.Equal("south-west", GuidanceText.CompassWord(CompassPoint.SouthWest));
    }

    [Theory]
    [InlineData(3f, 10)]
    [InlineData(14f, 10)]
    [InlineData(55f, 60)]
    [InlineData(63f, 60)]
    [InlineData(68f, 70)]
    [InlineData(1234f, 1230)]
    public void Distances_round_to_tens_of_yalms(float yalms, int expected)
    {
        Assert.Equal(expected, GuidanceText.RoundYalms(yalms));
    }

    [Fact]
    public void Msq_says_what_is_left_never_a_percentage()
    {
        var line = GuidanceText.Msq("Dawntrail", "The Long Road to Xak Tural", 70448, 48, 91, 95, 100);
        Assert.Equal("Main scenario: Dawntrail, at The Long Road to Xak Tural. 48 quests left in this expansion, and 91 to the latest story, levels 95 to 100.", line.Text);
        Assert.Equal("The Long Road to Xak Tural", line.QuestName);
        Assert.Equal(70448u, line.QuestRowId);
        AssertPlain(line);

        var one = GuidanceText.Msq("Winter's Prelude", "Last Quest", 1, 1, 1, 100, 100);
        Assert.Equal("Main scenario: Winter's Prelude, at Last Quest. 1 quest left to the latest story, level 100.", one.Text);
        AssertPlain(one);
    }

    [Fact]
    public void Msq_caught_up()
    {
        var line = GuidanceText.MsqCaughtUp();
        Assert.Equal("Main scenario: caught up. The story continues in a later patch.", line.Text);
        Assert.Equal(0u, line.QuestRowId);
        AssertPlain(line);
    }

    [Fact]
    public void Next_in_the_journal_elsewhere_names_the_zone_and_where_you_are()
    {
        var line = GuidanceText.NextInJournal("The Long Road to Xak Tural", 70448, 3, "Speak with Erenville.", new GuidancePlace("Shaaloani", 27.0f, 34.8f), "Tuliyollal");
        Assert.Equal("Next: The Long Road to Xak Tural, step 3. Speak with Erenville. Shaaloani, X 27, Y 34.8. You are in Tuliyollal.", line.Text);
        Assert.Equal("Next: ", line.Before);
        AssertPlain(line);
    }

    [Fact]
    public void Next_ready_in_the_same_zone_gives_the_distance_and_direction()
    {
        var line = GuidanceText.NextReady("Caught in the Act", 1, "Elaisse", new GuidancePlace("The Pillars", 7.8f, 10.8f, SameZone: true, Yalms: 58f, Direction: CompassPoint.SouthWest), "The Pillars");
        Assert.Equal("Next: Caught in the Act. Talk to Elaisse in The Pillars, X 7.8, Y 10.8, about 60 yalms south-west of you.", line.Text);
        AssertPlain(line);
    }

    [Fact]
    public void A_distance_is_given_only_in_the_same_zone()
    {
        var elsewhere = GuidanceText.NextReady("Q", 1, "Krile", new GuidancePlace("Old Sharlayan", 10f, 11f, SameZone: false, Yalms: 900f, Direction: CompassPoint.North), "Tuliyollal");
        Assert.DoesNotContain("yalms", elsewhere.Text, StringComparison.Ordinal);
        Assert.Contains("You are in Tuliyollal.", elsewhere.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Nothing_ready_counts_other_jobs_in_words()
    {
        Assert.Equal("Next: nothing is Ready on this job. 4 quests are Ready on another job.", GuidanceText.NextNothing(4).Text);
        Assert.Equal("Next: nothing is Ready on this job. 1 quest is Ready on another job.", GuidanceText.NextNothing(1).Text);
        Assert.Equal("Next: nothing is Ready on this job.", GuidanceText.NextNothing(0).Text);
        AssertPlain(GuidanceText.NextNothing(4));
    }

    [Fact]
    public void A_masked_quest_is_words_never_a_name_or_a_placeholder()
    {
        var line = GuidanceText.NextMasked(mainScenario: true, 83, zoneHidden: true);
        Assert.Equal("Next: a main scenario quest at level 83, in a zone ahead of your story.", line.Text);
        Assert.Equal(0u, line.QuestRowId);
        AssertPlain(line);
        Assert.Equal("Next: a quest at level 50.", GuidanceText.NextMasked(false, 50, false).Text);

        // A hidden zone in a step line is words too.
        var step = GuidanceText.NextInJournal("Q", 1, 2, "Speak with someone.", new GuidancePlace(null, 1f, 2f), "Tuliyollal");
        Assert.Contains("It is in a zone ahead of your story.", step.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("X 1", step.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Go_says_where_and_how()
    {
        var line = GuidanceText.Go("The Long Road to Xak Tural", 70448, 3, "Erenville", new GuidancePlace("Shaaloani", 27f, 34.8f), GuidanceText.GoAction.TeleportFirst);
        Assert.Equal("Going to step 3 of The Long Road to Xak Tural: Erenville, Shaaloani, X 27, Y 34.8. Teleporting first.", line.Text);
        Assert.Equal("The Long Road to Xak Tural", line.QuestName);
        AssertPlain(line);

        var giver = GuidanceText.Go("Caught in the Act", 1, 0, "Elaisse", new GuidancePlace("The Pillars", 7.8f, 10.8f), GuidanceText.GoAction.Flag);
        Assert.Equal("Going to the giver of Caught in the Act: Elaisse, The Pillars, X 7.8, Y 10.8. Flagged on the map.", giver.Text);
    }

    [Fact]
    public void Step_done_in_the_same_zone_omits_the_zone()
    {
        var line = GuidanceText.StepDone(4, "Speak with Erenville again.", new GuidancePlace("Shaaloani", 26.7f, 31.0f, SameZone: true, Yalms: 72f, Direction: CompassPoint.North), "Shaaloani");
        Assert.Equal("Step done. Next: step 4. Speak with Erenville again. X 26.7, Y 31, about 70 yalms north of you.", line.Text);
        AssertPlain(line);
        Assert.StartsWith("Quest done. Next: ", GuidanceText.QuestDone(GuidanceText.NextNothing(0)).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_step_without_a_place_says_so()
    {
        var line = GuidanceText.NextInJournal("Morbid Motivation", 66677, 1, "Obtain a sack of adventurer's effects.", null, "Mor Dhona");
        Assert.EndsWith("This step has no place on the map.", line.Text, StringComparison.Ordinal);
        AssertPlain(line);
    }

    [Theory]
    [InlineData("Talk to «Krile» (again) → now", "Talk to Krile again now")]
    [InlineData("Obtain hunks of nanka flesh ☾.", "Obtain hunks of nanka flesh.")]
    [InlineData("  50% done · nearly ", "50 done nearly")]
    [InlineData("Area ahead (Lv 61)", "Area ahead Lv 61")]
    [InlineData(null, "")]
    public void Game_text_keeps_only_what_reads_aloud(string? text, string expected)
    {
        Assert.Equal(expected, GuidanceText.Speakable(text));
        Assert.True(GuidanceText.IsSpeakable(GuidanceText.Speakable(text)));
    }

    [Fact]
    public void Symbols_in_the_game_text_never_reach_the_line()
    {
        var line = GuidanceText.NextInJournal("The Hand (Part 1)", 1, 2, "Deliver «materials» → Rowena.", new GuidancePlace("Mor Dhona", 22f, 6.7f), "Mor Dhona");
        AssertPlain(line);
        Assert.Equal("The Hand Part 1", line.QuestName);
    }

    [Fact]
    public void The_throttle_lets_one_line_through_every_ten_seconds()
    {
        var throttle = new GuidanceThrottle();
        Assert.True(throttle.TryTake(1_000));
        Assert.False(throttle.TryTake(2_000));
        Assert.False(throttle.TryTake(10_999));
        Assert.True(throttle.TryTake(11_000));
        Assert.False(throttle.TryTake(11_001));
        Assert.True(throttle.TryTake(30_000));
    }

    [Fact]
    public void A_step_moving_on_is_a_step_done()
    {
        AcceptedQuest[] before = [new(10, 2), new(11, 1)];
        AcceptedQuest[] after = [new(10, 3), new(11, 1)];
        Assert.Equal(new StepFinish(10, false), StepProgress.Finished(before, after, _ => false));
    }

    [Fact]
    public void A_quest_turned_in_is_a_quest_done_and_wins_over_a_step()
    {
        AcceptedQuest[] before = [new(10, 2), new(11, 255)];
        AcceptedQuest[] after = [new(10, 3)];
        Assert.Equal(new StepFinish(11, true), StepProgress.Finished(before, after, id => id == 11));
    }

    [Fact]
    public void Abandoning_or_taking_a_quest_is_nothing_done()
    {
        AcceptedQuest[] before = [new(10, 2)];
        Assert.Null(StepProgress.Finished(before, [], _ => false));
        Assert.Null(StepProgress.Finished(before, [new(10, 2), new(12, 1)], _ => false));
        Assert.Null(StepProgress.Finished([], [new(12, 1)], _ => false));
    }
}
