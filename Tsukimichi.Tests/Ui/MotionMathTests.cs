using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

public class MotionMathTests
{
    private const float Frame = 1f / 60f;

    [Fact]
    public void Approach_moves_part_of_the_way_each_frame()
    {
        var v = MotionMath.Approach(0f, 1f, MotionMath.HoverRate, Frame);

        Assert.InRange(v, 0.2f, 0.3f); // 1 − e^(−0.3) ≈ 0.26
    }

    [Fact]
    public void Approach_is_frame_rate_independent()
    {
        var at60 = 0f;
        for (var i = 0; i < 12; i++)
        {
            at60 = MotionMath.Approach(at60, 1f, MotionMath.SelectRate, 1f / 60f);
        }

        var at30 = 0f;
        for (var i = 0; i < 6; i++)
        {
            at30 = MotionMath.Approach(at30, 1f, MotionMath.SelectRate, 1f / 30f);
        }

        Assert.Equal(at60, at30, 4);
    }

    [Fact]
    public void Hover_settles_within_about_a_quarter_second()
    {
        var v = 0f;
        var frames = 0;
        while (v < 1f && frames < 600)
        {
            v = MotionMath.Approach(v, 1f, MotionMath.HoverRate, Frame);
            frames++;
        }

        // Snaps to the target instead of creeping forever; ~120 ms to 95 %, a few frames more to within 0.001.
        Assert.Equal(1f, v);
        Assert.InRange(frames, 5, 30);
    }

    [Theory]
    [InlineData(0f, 0.016f)]
    [InlineData(-5f, 0.016f)]
    [InlineData(18f, 0f)]
    [InlineData(18f, -1f)]
    [InlineData(float.NaN, 0.016f)]
    [InlineData(18f, float.PositiveInfinity)]
    public void Approach_with_a_bad_rate_or_time_stays_put(float rate, float dt)
    {
        Assert.Equal(0.4f, MotionMath.Approach(0.4f, 1f, rate, dt));
    }

    [Fact]
    public void Approach_from_a_non_finite_value_jumps_to_the_target()
    {
        Assert.Equal(0.7f, MotionMath.Approach(float.NaN, 0.7f, MotionMath.HoverRate, Frame));
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(1f, 1f)]
    [InlineData(-1f, 0f)]
    [InlineData(2f, 1f)]
    [InlineData(0.5f, 0.875f)]
    public void Ease_out_cubic_is_clamped_and_front_loaded(float t, float expected)
    {
        Assert.Equal(expected, MotionMath.EaseOutCubic(t), 5);
    }

    [Fact]
    public void Pulse_progress_runs_from_zero_to_one_then_ends()
    {
        Assert.Equal(-1f, MotionMath.PulseProgress(10d, 9.9d, 0.45f));
        Assert.Equal(0f, MotionMath.PulseProgress(10d, 10d, 0.45f));
        Assert.Equal(0.5f, MotionMath.PulseProgress(10d, 10.225d, 0.45f), 4);
        Assert.Equal(-1f, MotionMath.PulseProgress(10d, 10.45d, 0.45f));
        Assert.Equal(-1f, MotionMath.PulseProgress(10d, 10.1d, 0f));
    }

    [Fact]
    public void A_new_key_starts_at_its_target()
    {
        var store = new MotionStore();

        Assert.Equal(1f, store.Lerp(7, 1f, MotionMath.HoverRate, 1d, Frame, animate: true));
    }

    [Fact]
    public void An_existing_key_eases_towards_a_new_target()
    {
        var store = new MotionStore();
        store.Lerp(7, 0f, MotionMath.HoverRate, 1d, Frame, animate: true);

        var v = store.Lerp(7, 1f, MotionMath.HoverRate, 1d + Frame, Frame, animate: true);

        Assert.InRange(v, 0.01f, 0.99f);
    }

    [Fact]
    public void Without_animation_the_value_jumps_so_scrolling_leaves_no_wake()
    {
        var store = new MotionStore();
        store.Lerp(7, 0f, MotionMath.HoverRate, 1d, Frame, animate: true);

        Assert.Equal(1f, store.Lerp(7, 1f, MotionMath.HoverRate, 1d + Frame, Frame, animate: false));
    }

    [Fact]
    public void Keys_untouched_for_two_seconds_are_pruned()
    {
        var store = new MotionStore();
        store.Lerp(1, 1f, MotionMath.HoverRate, 10d, Frame, animate: true);
        store.Lerp(2, 1f, MotionMath.HoverRate, 11.5d, Frame, animate: true);

        var removed = store.Prune(12.2d);

        Assert.Equal(1, removed);
        Assert.False(store.Contains(1));
        Assert.True(store.Contains(2));
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public void A_triggered_pulse_reports_progress_and_is_forgotten_when_done()
    {
        var store = new MotionStore();
        store.Trigger(3, 5d, animate: true);

        Assert.Equal(0.5f, store.Pulse(3, 1f, 5.5d, animate: true), 4);
        Assert.Equal(-1f, store.Pulse(3, 1f, 6.1d, animate: true));
        Assert.Equal(-1f, store.Pulse(3, 1f, 5.5d, animate: true));
    }

    [Fact]
    public void Pulses_never_play_under_reduce_motion()
    {
        var store = new MotionStore();
        store.Trigger(3, 5d, animate: false);

        Assert.Equal(-1f, store.Pulse(3, 1f, 5.2d, animate: true));

        store.Trigger(4, 5d, animate: true);
        Assert.Equal(-1f, store.Pulse(4, 1f, 5.2d, animate: false));
    }

    [Fact]
    public void An_unknown_key_has_no_pulse()
    {
        Assert.Equal(-1f, new MotionStore().Pulse(99, 1f, 1d, animate: true));
    }
}
