using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

public class ConfirmGateTests
{
    private const float Frame = 1f / 60f;

    /// <summary>Feeds <paramref name="frames"/> frames of a plain press; returns the frame index that confirmed, or -1.</summary>
    private static int Hold(ConfirmGate gate, int frames, float frame = Frame)
    {
        for (var i = 0; i < frames; i++)
        {
            if (gate.Update(chordHeld: false, pressed: true, frame))
            {
                return i;
            }
        }

        return -1;
    }

    [Fact]
    public void Idle_gate_reports_no_progress_and_no_confirmation()
    {
        var gate = new ConfirmGate();

        Assert.False(gate.Update(chordHeld: false, pressed: false, Frame));
        Assert.Equal(0f, gate.Progress);
        Assert.False(gate.Confirmed);
        Assert.False(gate.Holding);
        Assert.Equal(ConfirmGate.DefaultHoldSeconds, gate.HoldSeconds);
    }

    [Fact]
    public void Chord_with_a_press_confirms_at_once_and_only_once_per_press()
    {
        var gate = new ConfirmGate();

        Assert.False(gate.Update(chordHeld: true, pressed: false, Frame), "the chord alone is not a click");
        Assert.True(gate.Update(chordHeld: true, pressed: true, Frame));
        Assert.True(gate.Confirmed);
        Assert.Equal(1f, gate.Progress);

        Assert.False(gate.Update(chordHeld: true, pressed: true, Frame), "still held: must not fire again");
        Assert.False(gate.Confirmed);

        gate.Update(chordHeld: true, pressed: false, Frame);
        Assert.True(gate.Update(chordHeld: true, pressed: true, Frame), "a new press fires again");
    }

    [Fact]
    public void Holding_to_the_end_confirms_once_at_six_hundred_milliseconds()
    {
        var gate = new ConfirmGate();

        var fired = Hold(gate, 120);

        // The first frame (the press beginning) adds nothing; 36 more of 1/60 s are exactly 0.6 s, and float
        // accumulation may need one more.
        Assert.InRange(fired, 36, 38);
        Assert.Equal(1f, gate.Progress);
        Assert.False(gate.Holding);
        Assert.False(gate.Update(chordHeld: false, pressed: true, Frame), "latched until release");
    }

    [Fact]
    public void Progress_grows_with_the_hold_and_stays_within_zero_and_one()
    {
        var gate = new ConfirmGate(1f);

        Assert.False(gate.Update(chordHeld: false, pressed: true, 0.05f));
        Assert.Equal(0f, gate.Progress);
        Assert.Equal(-1, Hold(gate, 5, 0.05f));
        Assert.Equal(0.25f, gate.Progress, 3);
        Assert.True(gate.Holding);
        Assert.Equal(0.75f, gate.Remaining, 3);

        Assert.Equal(-1, Hold(gate, 10, 0.05f));
        Assert.Equal(0.75f, gate.Progress, 3);

        Assert.InRange(Hold(gate, 10, 0.05f), 4, 5);
        Assert.Equal(1f, gate.Progress);
        Assert.Equal(0f, gate.Remaining);
    }

    [Fact]
    public void Releasing_before_the_hold_is_done_cancels_and_resets()
    {
        var gate = new ConfirmGate();

        Assert.Equal(-1, Hold(gate, 20));
        Assert.True(gate.Holding);
        Assert.InRange(gate.Progress, 0.5f, 0.6f);

        Assert.False(gate.Update(chordHeld: false, pressed: false, Frame));
        Assert.Equal(0f, gate.Progress);
        Assert.False(gate.Holding);
        Assert.False(gate.Confirmed);

        // The next press starts over rather than resuming.
        Assert.Equal(-1, Hold(gate, 20));
        Assert.InRange(gate.Progress, 0.5f, 0.6f);
    }

    [Fact]
    public void Cancel_drops_a_press_in_progress_without_confirming()
    {
        var gate = new ConfirmGate();
        Hold(gate, 30);

        gate.Cancel();

        Assert.Equal(0f, gate.Progress);
        Assert.False(gate.Confirmed);
        Assert.False(gate.Holding);
        Assert.False(gate.Update(chordHeld: false, pressed: true, Frame), "a single frame after cancel is far from the hold");
    }

    [Fact]
    public void Bad_frame_times_do_not_advance_or_break_the_hold()
    {
        var gate = new ConfirmGate();

        Assert.False(gate.Update(chordHeld: false, pressed: true, float.NaN));
        Assert.False(gate.Update(chordHeld: false, pressed: true, -1f));
        Assert.False(gate.Update(chordHeld: false, pressed: true, float.PositiveInfinity));
        Assert.Equal(0f, gate.Progress);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-0.5f)]
    [InlineData(float.NaN)]
    public void Hold_duration_must_be_positive(float seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConfirmGate(seconds));
    }

    [Fact]
    public void Countdown_tenths_follow_the_hold_and_vanish_when_idle_or_fired()
    {
        var gate = new ConfirmGate();
        Assert.Equal(6, gate.HoldTenths);
        Assert.Equal(0, gate.RemainingTenths);

        gate.Update(chordHeld: false, pressed: true, 0.05f);
        Assert.Equal(0, gate.RemainingTenths); // the press only began

        gate.Update(chordHeld: false, pressed: true, 0.05f);
        Assert.Equal(6, gate.RemainingTenths); // 0.55 s left rounds up

        Hold(gate, 9, 0.05f);
        Assert.Equal(1, gate.RemainingTenths); // 0.1 s left

        gate.Update(chordHeld: false, pressed: true, 0.04f);
        Assert.Equal(1, gate.RemainingTenths); // 0.01 s left never shows as zero

        Assert.InRange(Hold(gate, 2, 0.05f), 0, 1);
        Assert.True(gate.Confirmed);
        Assert.Equal(0, gate.RemainingTenths);
    }

    [Fact]
    public void Countdown_tenths_scale_with_a_custom_hold()
    {
        var gate = new ConfirmGate(1.2f);
        gate.Update(chordHeld: false, pressed: true, 0.01f);
        gate.Update(chordHeld: false, pressed: true, 0.01f);

        Assert.Equal(12, gate.HoldTenths);
        Assert.Equal(12, gate.RemainingTenths);
    }

    // ---- 1.11.0: the safety table (feature plan v6 S2) ----

    [Fact]
    public void The_hold_length_follows_settings_within_its_range()
    {
        var gate = new ConfirmGate();

        gate.SetHoldSeconds(1.5f);
        Assert.Equal(1.5f, gate.HoldSeconds, 4);
        Assert.InRange(Hold(gate, 200), 90, 92); // the press beginning, then 90 frames of 1/60 s

        gate.Update(chordHeld: false, pressed: false, Frame);
        gate.SetHoldSeconds(0.01f);
        Assert.Equal(SafetyRules.MinHoldSeconds, gate.HoldSeconds, 4);

        gate.SetHoldSeconds(60f);
        Assert.Equal(SafetyRules.MaxHoldSeconds, gate.HoldSeconds, 4);
        Assert.Equal(SafetyRules.MaxHoldTenths, gate.HoldTenths);
    }

    [Fact]
    public void Changing_the_hold_length_starts_a_press_over()
    {
        var gate = new ConfirmGate();
        Hold(gate, 30);
        Assert.True(gate.Holding);

        gate.SetHoldSeconds(1f);

        Assert.False(gate.Holding);
        Assert.Equal(0f, gate.Progress);
    }

    [Fact]
    public void Setting_the_same_hold_length_leaves_a_press_alone()
    {
        var gate = new ConfirmGate();
        Hold(gate, 30);

        gate.SetHoldSeconds(ConfirmGate.DefaultHoldSeconds);

        Assert.True(gate.Holding);
    }

    [Fact]
    public void Ctrl_works_as_the_chord_like_shift()
    {
        // The chord argument is "Ctrl or Shift": the caller passes either key.
        var gate = new ConfirmGate();
        var ctrlHeld = true;
        Assert.True(gate.Update(chordHeld: ctrlHeld, pressed: true, Frame));
    }

    [Fact]
    public void A_press_let_go_early_is_reported_as_abandoned_once()
    {
        var gate = new ConfirmGate();
        Hold(gate, 10);

        Assert.False(gate.Update(chordHeld: false, pressed: false, Frame));
        Assert.True(gate.Abandoned);
        gate.Update(chordHeld: false, pressed: false, Frame);
        Assert.False(gate.Abandoned, "only on the frame of the release");
    }

    [Fact]
    public void A_confirmed_press_is_not_abandoned()
    {
        var gate = new ConfirmGate();
        gate.Update(chordHeld: true, pressed: true, Frame);

        gate.Update(chordHeld: false, pressed: false, Frame);

        Assert.False(gate.Abandoned);
    }

    [Fact]
    public void Two_click_mode_confirms_on_the_second_click()
    {
        var gate = new ConfirmGate();

        Assert.False(gate.ClickTwice(chordHeld: false, now: 1.0));
        Assert.True(gate.AwaitingSecond(1.5));
        Assert.True(gate.ClickTwice(chordHeld: false, now: 2.0));
        Assert.True(gate.Confirmed);
        Assert.False(gate.AwaitingSecond(2.1));
    }

    [Fact]
    public void Two_click_mode_ignores_a_double_click_and_takes_the_chord()
    {
        var gate = new ConfirmGate();

        Assert.False(gate.ClickTwice(chordHeld: false, now: 1.0));
        Assert.False(gate.ClickTwice(chordHeld: false, now: 1.05));
        Assert.True(gate.ClickTwice(chordHeld: true, now: 1.06), "Ctrl or Shift still confirms at once");
    }

    // ---- 1.11.0 review: only time held counts ----

    [Fact]
    public void The_frame_the_press_begins_adds_nothing()
    {
        // A one-second hitch just before the click: none of that time was held.
        var gate = new ConfirmGate();

        Assert.False(gate.Update(chordHeld: false, pressed: true, 1f));
        Assert.Equal(0f, gate.Elapsed);
        Assert.False(gate.Confirmed);
    }

    [Fact]
    public void A_single_long_frame_cannot_finish_the_hold()
    {
        var gate = new ConfirmGate();
        gate.Update(chordHeld: false, pressed: true, Frame);

        Assert.False(gate.Update(chordHeld: false, pressed: true, 5f));
        Assert.Equal(ConfirmGate.MaxFrameSeconds, gate.Elapsed, 4);
    }

    [Theory]
    [InlineData(0.1f)]
    [InlineData(0.25f)]
    [InlineData(1f)]
    public void A_low_frame_rate_never_finishes_the_hold_early(float frame)
    {
        var gate = new ConfirmGate();

        var fired = Hold(gate, 200, frame);

        // Frames after the first are the time the press had lasted when it fired; never less than the hold.
        Assert.True(fired > 0, "it still finishes, only later");
        Assert.True(fired * frame >= ConfirmGate.DefaultHoldSeconds, $"fired after {fired * frame} s at {frame} s a frame");
    }

    [Fact]
    public void A_new_press_after_a_release_skips_its_first_frame_again()
    {
        var gate = new ConfirmGate();
        Hold(gate, 10);
        gate.Update(chordHeld: false, pressed: false, Frame);

        gate.Update(chordHeld: false, pressed: true, 0.5f);

        Assert.Equal(0f, gate.Elapsed);
    }

    [Fact]
    public void Cancel_forgets_a_first_click()
    {
        var gate = new ConfirmGate();
        gate.ClickTwice(chordHeld: false, now: 1.0);

        gate.Cancel();

        Assert.False(gate.AwaitingSecond(1.5));
        Assert.False(gate.ClickTwice(chordHeld: false, now: 2.0));
    }
}
