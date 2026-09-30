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

        // 36 frames of 1/60 s is exactly 0.6 s; float accumulation may need one more.
        Assert.InRange(fired, 35, 37);
        Assert.Equal(1f, gate.Progress);
        Assert.False(gate.Holding);
        Assert.False(gate.Update(chordHeld: false, pressed: true, Frame), "latched until release");
    }

    [Fact]
    public void Progress_grows_with_the_hold_and_stays_within_zero_and_one()
    {
        var gate = new ConfirmGate(1f);

        Assert.False(gate.Update(chordHeld: false, pressed: true, 0.25f));
        Assert.Equal(0.25f, gate.Progress, 3);
        Assert.True(gate.Holding);
        Assert.Equal(0.75f, gate.Remaining, 3);

        Assert.False(gate.Update(chordHeld: false, pressed: true, 0.5f));
        Assert.Equal(0.75f, gate.Progress, 3);

        Assert.True(gate.Update(chordHeld: false, pressed: true, 5f));
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
        Assert.Equal(6, gate.RemainingTenths); // 0.55 s left rounds up

        gate.Update(chordHeld: false, pressed: true, 0.5f);
        Assert.Equal(1, gate.RemainingTenths); // 0.05 s left never shows as zero

        gate.Update(chordHeld: false, pressed: true, 0.1f);
        Assert.True(gate.Confirmed);
        Assert.Equal(0, gate.RemainingTenths);
    }

    [Fact]
    public void Countdown_tenths_scale_with_a_custom_hold()
    {
        var gate = new ConfirmGate(1.2f);
        gate.Update(chordHeld: false, pressed: true, 0.01f);

        Assert.Equal(12, gate.HoldTenths);
        Assert.Equal(12, gate.RemainingTenths);
    }
}
