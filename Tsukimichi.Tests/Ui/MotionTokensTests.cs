using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The motion foundation (feature plan v6 U8a): one token table at 120–220 ms that the eased rates follow, the waxing
/// moon's one-shot, the Motion line in Settings with its override, and the completions that start a wax.
/// </summary>
public sealed class MotionTokensTests
{
    [Fact]
    public void Every_interaction_token_is_between_120_and_220_ms()
    {
        Assert.Equal(0.12f, MotionTokens.Shortest);
        Assert.Equal(0.22f, MotionTokens.Longest);
        foreach (var seconds in MotionTokens.Interaction)
        {
            Assert.InRange(seconds, MotionTokens.Shortest, MotionTokens.Longest);
        }
    }

    [Fact]
    public void The_eased_rates_settle_in_their_token_s_time()
    {
        Assert.Equal(MotionTokens.HoverIn, MotionTokens.SecondsFor(MotionMath.HoverRate), 3);
        Assert.Equal(MotionTokens.HoverOut, MotionTokens.SecondsFor(MotionMath.HoverOutRate), 3);
        Assert.Equal(MotionTokens.Select, MotionTokens.SecondsFor(MotionMath.SelectRate), 3);
        Assert.Equal(MotionTokens.Chevron, MotionTokens.SecondsFor(MotionMath.ChevronRate), 3);

        // 90 % of the way after the token's time, frame by frame at 60 fps.
        var v = 0f;
        for (var t = 0f; t < MotionTokens.Select - 0.0001f; t += 1f / 60f)
        {
            v = MotionMath.Approach(v, 1f, MotionMath.SelectRate, 1f / 60f);
        }

        Assert.InRange(v, 0.85f, 0.95f);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    public void A_rate_needs_a_time(float seconds)
    {
        Assert.Equal(0f, MotionTokens.RateFor(seconds));
        Assert.Equal(0f, MotionTokens.SecondsFor(seconds));
    }

    [Fact]
    public void The_wax_runs_from_the_half_moon_to_full_and_then_stops()
    {
        Assert.Equal(MotionTokens.WaxFrom, MotionTokens.WaxFraction(0f));
        Assert.InRange(MotionTokens.WaxFraction(0.5f), 0.9f, 0.95f);
        Assert.True(MotionTokens.WaxFraction(0.99f) > 0.99f);
        Assert.Equal(-1f, MotionTokens.WaxFraction(1f));
        Assert.Equal(-1f, MotionTokens.WaxFraction(-1f));
        Assert.Equal(-1f, MotionTokens.WaxFraction(float.NaN));
        Assert.InRange(MotionTokens.Wax, 0.2f, 1f);
    }

    [Fact]
    public void Windows_turning_motion_off_is_said_and_offers_the_override()
    {
        var status = MotionStatus.Of(reduceMotion: true, chosen: false, windowsAnimationsOff: true);

        Assert.False(status.On);
        Assert.Equal("Motion is off, because Windows' \"Show animations\" is off.", status.Text);
        Assert.Equal(MotionFix.AnimateAnyway, status.Fix);
        Assert.Equal("Animate Tsukimichi anyway", status.FixLabel);
    }

    [Fact]
    public void The_override_is_said_and_can_be_undone()
    {
        var status = MotionStatus.Of(reduceMotion: false, chosen: true, windowsAnimationsOff: true);

        Assert.True(status.On);
        Assert.Equal("Motion is on for Tsukimichi, though Windows' \"Show animations\" is off.", status.Text);
        Assert.Equal(MotionFix.FollowWindows, status.Fix);
        Assert.Equal("Follow Windows again", status.FixLabel);
    }

    [Theory]
    [InlineData(false, false, false, true, "Motion is on, following Windows' \"Show animations\".", MotionFix.None)]
    [InlineData(false, false, null, true, "Motion is on.", MotionFix.None)]
    [InlineData(true, false, null, false, "Motion is off.", MotionFix.None)]
    [InlineData(true, true, true, false, "Motion is off: Reduce motion is on.", MotionFix.None)]
    [InlineData(true, true, false, false, "Motion is off: Reduce motion is on.", MotionFix.FollowWindows)]
    [InlineData(false, true, false, true, "Motion is on.", MotionFix.None)]
    [InlineData(false, true, null, true, "Motion is on.", MotionFix.None)]
    public void The_Motion_line_says_why(bool reduce, bool chosen, bool? windowsOff, bool on, string text, MotionFix fix)
    {
        var status = MotionStatus.Of(reduce, chosen, windowsOff);

        Assert.Equal(on, status.On);
        Assert.Equal(text, status.Text);
        Assert.Equal(fix, status.Fix);
        Assert.Equal(fix == MotionFix.None, status.FixLabel.Length == 0);
    }

    private static readonly DateTime T0 = new(2026, 10, 2, 20, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void The_first_look_replays_nothing_then_new_completions_wax_once()
    {
        var cues = new CompletionCues();
        var into = new List<uint>();
        var events = new List<QuestEvent> { new(QuestEventKind.Completed, 1, T0) };

        Assert.Equal(0, cues.Take(7, events, shown: true, into));
        Assert.Empty(into);

        events.InsertRange(0, [new(QuestEventKind.Completed, 2, T0.AddSeconds(5)), new(QuestEventKind.Accepted, 3, T0.AddSeconds(5))]);
        Assert.Equal(1, cues.Take(7, events, shown: true, into));
        Assert.Equal([2u], into);

        into.Clear();
        Assert.Equal(0, cues.Take(7, events, shown: true, into));
        Assert.Empty(into);
    }

    [Fact]
    public void Another_character_on_screen_or_a_new_character_plays_nothing()
    {
        var cues = new CompletionCues();
        var into = new List<uint>();
        var events = new List<QuestEvent>();
        cues.Take(7, events, shown: true, into);

        events.Insert(0, new(QuestEventKind.Completed, 2, T0));
        Assert.Equal(0, cues.Take(7, events, shown: false, into));

        // Noted while hidden: showing the character again does not play it late.
        Assert.Equal(0, cues.Take(7, events, shown: true, into));

        // Logging in elsewhere starts over without replaying that character's list.
        events.Insert(0, new(QuestEventKind.Completed, 4, T0.AddSeconds(1)));
        Assert.Equal(0, cues.Take(8, events, shown: true, into));
        Assert.Empty(into);
    }

    [Fact]
    public void Events_that_fell_off_the_list_are_found_by_time()
    {
        var cues = new CompletionCues();
        var into = new List<uint>();
        var events = new List<QuestEvent> { new(QuestEventKind.Completed, 1, T0) };
        cues.Take(7, events, shown: true, into);

        // The noted newest event is gone (the list is capped); older ones must not play.
        var later = new List<QuestEvent> { new(QuestEventKind.Completed, 5, T0.AddSeconds(3)), new(QuestEventKind.Completed, 0, T0.AddSeconds(-3)) };
        Assert.Equal(1, cues.Take(7, later, shown: true, into));
        Assert.Equal([5u], into);
    }
}
