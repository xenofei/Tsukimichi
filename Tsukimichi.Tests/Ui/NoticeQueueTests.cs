using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The notice dock's queue (feature plan v6 U2, decision 6): one notice at a time with a pager; one-time prompts close
/// themselves after about 15 s on screen, paused while hovered; prompts that need action stay.
/// </summary>
public class NoticeQueueTests
{
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
        queue.Tick(0.0, paused: false);

        Assert.True(queue.Tick(NoticeQueue.OneTimeSeconds - 0.5, paused: false));
        Assert.False(queue.Tick(NoticeQueue.OneTimeSeconds + 0.5, paused: false));
        Assert.Null(queue.Current);
        Assert.True(queue.Closed(NoticeKind.PinPrompt));
    }

    [Fact]
    public void Hovering_stops_the_clock()
    {
        var queue = new NoticeQueue();
        queue.Set(NoticeKind.Context, true);
        queue.Tick(0.0, paused: false);
        queue.Tick(10.0, paused: false);

        // The pointer rests on the dock for a long while: no time is used.
        Assert.True(queue.Tick(100.0, paused: true));
        Assert.Equal(NoticeQueue.OneTimeSeconds - 10.0, queue.Remaining, 6);

        // Then the rest of its time without the pointer ends it.
        Assert.True(queue.Tick(104.0, paused: false));
        Assert.False(queue.Tick(106.0, paused: false));
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
        Assert.True(queue.Tick(10_000.0, paused: false));
        Assert.Equal(kind, queue.Current);
        Assert.True(double.IsPositiveInfinity(queue.Remaining));

        // It goes when its cause does.
        queue.Set(kind, false);
        Assert.Null(queue.Current);
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
        queue.Tick(0.0, paused: false);
        queue.Tick(NoticeQueue.OneTimeSeconds + 1.0, paused: false);

        // The context line ran out; the pin prompt waited behind it and now has its full time.
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
