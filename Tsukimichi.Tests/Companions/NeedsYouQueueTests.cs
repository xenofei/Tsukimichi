using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Companions;

/// <summary>
/// The "Needs you" panel's queue (plan v7, 1.18.0, A5; spec-1.18): one alert at a time with "+N more", a kind already
/// waiting replaced in place, dismiss and a cleared cause fade it and bring the next, it rises 4 px over Rise once (no
/// pulse), and an alert without words is refused.
/// </summary>
public class NeedsYouQueueTests
{
    private static NeedsYouAlert Alert(NeedsYouKind kind, string title = "Title", string line = "Line") => new(kind, title, line);

    [Fact]
    public void One_alert_at_a_time_and_the_rest_wait_behind_more()
    {
        var queue = new NeedsYouQueue();
        queue.Raise(Alert(NeedsYouKind.Death), 0);
        queue.Raise(Alert(NeedsYouKind.DutyPop), 1);
        queue.Raise(Alert(NeedsYouKind.Tell), 2);
        Assert.Equal(NeedsYouKind.Death, queue.Current!.Kind);
        Assert.Equal(2, queue.More);

        queue.Dismiss(3);
        Assert.True(queue.Leaving);
        Assert.True(queue.Tick(3 + StopDock.LeaveSeconds, false));
        Assert.Equal(NeedsYouKind.DutyPop, queue.Current!.Kind);
        Assert.Equal(1, queue.More);
    }

    [Fact]
    public void A_second_alert_of_a_kind_replaces_the_first_in_place()
    {
        var queue = new NeedsYouQueue();
        queue.Raise(Alert(NeedsYouKind.Tell, line: "first"), 0);
        queue.Raise(Alert(NeedsYouKind.Death), 1);
        queue.Raise(Alert(NeedsYouKind.Tell, line: "second"), 2);
        Assert.Equal(1, queue.More);
        Assert.Equal("second", queue.Current!.Line);
    }

    [Fact]
    public void A_cleared_cause_takes_its_alert_away()
    {
        var queue = new NeedsYouQueue();
        queue.Raise(Alert(NeedsYouKind.Tell), 0);
        queue.Raise(Alert(NeedsYouKind.DutyPop), 0);
        queue.Clear(NeedsYouKind.DutyPop, 1);
        Assert.False(queue.Has(NeedsYouKind.DutyPop));
        Assert.Equal(0, queue.More);

        queue.Raise(Alert(NeedsYouKind.Death), 2);
        queue.Clear(NeedsYouKind.Tell, 3);
        Assert.True(queue.Leaving);
        queue.Tick(3.5, false);
        Assert.Equal(NeedsYouKind.Death, queue.Current!.Kind);
        Assert.False(queue.Leaving);
    }

    [Fact]
    public void It_rises_four_pixels_and_fades_in_once_with_no_pulse()
    {
        var queue = new NeedsYouQueue();
        queue.Raise(Alert(NeedsYouKind.Stuck), 10);
        Assert.Equal(MotionTokens.RiseLogical, queue.Rise(10, false));
        Assert.Equal(0f, queue.Alpha(10, false));
        Assert.Equal(0f, queue.Rise(10 + MotionTokens.Rise + 0.001, false));
        Assert.Equal(1f, queue.Alpha(10 + MotionTokens.Rise + 0.001, false));

        // Steady from then on: no pulse, no blink.
        for (var t = 11.0; t < 40.0; t += 0.37)
        {
            Assert.Equal(1f, queue.Alpha(t, false));
            Assert.Equal(0f, queue.Rise(t, false));
        }

        Assert.False(queue.Interactive(10.05, false));
        Assert.True(queue.Interactive(11, false));
    }

    [Fact]
    public void Under_reduce_motion_it_appears_and_goes_at_once()
    {
        var queue = new NeedsYouQueue();
        queue.Raise(Alert(NeedsYouKind.Stuck), 10);
        Assert.Equal(1f, queue.Alpha(10, true));
        Assert.Equal(0f, queue.Rise(10, true));
        queue.Dismiss(11);
        Assert.Equal(0f, queue.Alpha(11, true));
        Assert.False(queue.Tick(11, true));
        Assert.Null(queue.Current);
    }

    [Fact]
    public void An_alert_without_words_is_refused()
    {
        var queue = new NeedsYouQueue();
        Assert.Throws<ArgumentException>(() => queue.Raise(Alert(NeedsYouKind.Death, title: ""), 0));
        Assert.Null(queue.Current);
    }

    [Fact]
    public void Every_kind_has_a_title_key_and_the_eyebrow_has_one()
    {
        foreach (var kind in new[] { NeedsYouKind.Death, NeedsYouKind.Stuck, NeedsYouKind.DutyPop, NeedsYouKind.Tell })
        {
            Assert.Equal("NeedsYouTitle" + kind, NeedsYouAlert.TitleKey(kind));
        }

        Assert.Equal("NeedsYouEyebrow", NeedsYouAlert.EyebrowKey);
    }
}
