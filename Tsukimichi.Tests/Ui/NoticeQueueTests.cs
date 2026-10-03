using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The notice dock's queue (feature plan v6 U2, decision 6): one notice at a time with a pager; one-time prompts close
/// themselves after about 15 s on screen, paused while hovered; prompts that need action stay. The clock counts only the
/// frames the dock is drawn, a step at most <see cref="NoticeQueue.MaxStepSeconds"/>.
/// </summary>
public class NoticeQueueTests
{
    private const double Frame = 1.0 / 60.0;

    /// <summary>Ticks once a frame from <paramref name="from"/> up to <paramref name="to"/>; the last tick's answer.</summary>
    private static bool Run(NoticeQueue queue, double from, double to, bool paused = false)
    {
        for (var now = from; now < to; now += Frame)
        {
            queue.Tick(now, paused);
        }

        return queue.Tick(to, paused);
    }

    [Fact]
    public void An_empty_queue_shows_nothing()
    {
        var queue = new NoticeQueue();
        Assert.Null(queue.Current);
        Assert.Equal(0, queue.Count);
        Assert.Equal(0, queue.Position);
        Assert.False(queue.Tick(1.0, paused: false));
    }

    [Fact]
    public void A_one_time_prompt_closes_itself_after_its_time_on_screen()
    {
        var queue = new NoticeQueue();
        queue.Set(NoticeKind.PinPrompt, true);

        Assert.True(Run(queue, 0.0, NoticeQueue.OneTimeSeconds - 0.5));
        Assert.False(Run(queue, NoticeQueue.OneTimeSeconds - 0.5, NoticeQueue.OneTimeSeconds + 0.5));
        Assert.Null(queue.Current);
        Assert.True(queue.Closed(NoticeKind.PinPrompt));
    }

    [Fact]
    public void Hovering_stops_the_clock()
    {
        var queue = new NoticeQueue();
        queue.Set(NoticeKind.Context, true);
        Run(queue, 0.0, 10.0);

        // The pointer rests on the dock for a long while: no time is used.
        Assert.True(Run(queue, 10.0, 100.0, paused: true));
        Assert.Equal(NoticeQueue.OneTimeSeconds - 10.0, queue.Remaining, 3);

        // Then the rest of its time without the pointer ends it.
        Assert.True(Run(queue, 100.0, 104.0));
        Assert.False(Run(queue, 104.0, 106.0));
    }

    [Fact]
    public void A_long_gap_between_ticks_does_not_use_up_a_prompt()
    {
        var queue = new NoticeQueue();
        queue.Set(NoticeKind.PinPrompt, true);
        Run(queue, 0.0, 5.0);

        // The game hitches, or the dock goes undrawn for minutes: the gap counts as one capped step.
        Assert.True(queue.Tick(600.0, paused: false));
        Assert.Equal(NoticeQueue.OneTimeSeconds - 5.0 - NoticeQueue.MaxStepSeconds, queue.Remaining, 3);
    }

    [Fact]
    public void A_suspended_clock_starts_afresh_when_the_window_reopens()
    {
        var queue = new NoticeQueue();
        queue.Set(NoticeKind.Context, true);
        Run(queue, 0.0, 5.0);

        // The window closes for ten minutes, then opens again: the prompt still has all its time left.
        queue.Suspend();
        Assert.True(queue.Tick(605.0, paused: false));
        Assert.Equal(NoticeQueue.OneTimeSeconds - 5.0, queue.Remaining, 3);
        Assert.Equal(NoticeKind.Context, queue.Current);
    }

    [Theory]
    [InlineData(NoticeKind.RebuildFailed)]
    [InlineData(NoticeKind.Freshness)]
    public void A_notice_that_needs_action_stays(NoticeKind kind)
    {
        var queue = new NoticeQueue();
        queue.Set(kind, true);
        queue.Tick(0.0, paused: false);

        Assert.True(NoticeQueue.Stays(kind));
        Assert.True(Run(queue, 0.0, 60.0));
        Assert.Equal(kind, queue.Current);
        Assert.True(double.IsPositiveInfinity(queue.Remaining));

        // It goes when its cause does.
        queue.Set(kind, false);
        Assert.Null(queue.Current);
    }

    [Fact]
    public void A_notice_that_stays_folds_to_a_chip_until_its_cause_goes()
    {
        var queue = new NoticeQueue();
        queue.Set(NoticeKind.RebuildFailed, true);
        queue.SetCollapsed(NoticeKind.RebuildFailed, true);

        // Folded, it is still the notice on screen and still stays.
        Assert.True(queue.Collapsed(NoticeKind.RebuildFailed));
        Assert.Equal(NoticeKind.RebuildFailed, queue.Current);
        Assert.True(Run(queue, 0.0, 60.0));

        // Once the rebuild works the notice goes; a new failure shows in full.
        queue.Set(NoticeKind.RebuildFailed, false);
        queue.Set(NoticeKind.RebuildFailed, true);
        Assert.False(queue.Collapsed(NoticeKind.RebuildFailed));

        // A one-time prompt does not fold.
        queue.Set(NoticeKind.PinPrompt, true);
        queue.SetCollapsed(NoticeKind.PinPrompt, true);
        Assert.False(queue.Collapsed(NoticeKind.PinPrompt));
    }

    [Fact]
    public void A_closed_prompt_stays_closed_for_the_session()
    {
        var queue = new NoticeQueue();
        queue.Set(NoticeKind.WhatsNew, true);
        queue.Close(NoticeKind.WhatsNew);
        Assert.Null(queue.Current);

        // Deselecting and selecting again (the card's notice comes and goes) does not bring it back.
        queue.Set(NoticeKind.WhatsNew, false);
        queue.Set(NoticeKind.WhatsNew, true);
        Assert.Null(queue.Current);
    }

    [Fact]
    public void One_notice_shows_at_a_time_and_the_pager_reaches_the_others()
    {
        var queue = new NoticeQueue();
        queue.Set(NoticeKind.PinPrompt, true);
        queue.Set(NoticeKind.WelcomeBack, true);
        queue.Set(NoticeKind.Context, true);

        // The most important first; a lower-ranked arrival waits behind the pager.
        Assert.Equal(3, queue.Count);
        Assert.Equal(NoticeKind.Context, queue.Current);
        Assert.Equal(1, queue.Position);

        queue.Step(1);
        Assert.Equal(NoticeKind.PinPrompt, queue.Current);
        Assert.Equal(2, queue.Position);
        queue.Step(1);
        Assert.Equal(NoticeKind.WelcomeBack, queue.Current);
        queue.Step(1);
        Assert.Equal(NoticeKind.Context, queue.Current);
        queue.Step(-1);
        Assert.Equal(NoticeKind.WelcomeBack, queue.Current);
    }

    [Fact]
    public void Only_the_notice_on_screen_uses_time()
    {
        var queue = new NoticeQueue();
        queue.Set(NoticeKind.Context, true);
        queue.Set(NoticeKind.PinPrompt, true);

        // Tick until the context line runs out; the pin prompt waited behind it and now has its full time.
        var now = 0.0;
        while (queue.Current == NoticeKind.Context && now < NoticeQueue.OneTimeSeconds + 1.0)
        {
            queue.Tick(now, paused: false);
            now += Frame;
        }

        Assert.Equal(NoticeKind.PinPrompt, queue.Current);
        Assert.Equal(NoticeQueue.OneTimeSeconds, queue.Remaining, 6);
    }

    [Fact]
    public void A_failure_takes_the_dock_from_a_prompt()
    {
        var queue = new NoticeQueue();
        queue.Set(NoticeKind.PinPrompt, true);
        queue.Set(NoticeKind.RebuildFailed, true);
        Assert.Equal(NoticeKind.RebuildFailed, queue.Current);

        // Once it is fixed the prompt is back.
        queue.Set(NoticeKind.RebuildFailed, false);
        Assert.Equal(NoticeKind.PinPrompt, queue.Current);
    }
}
