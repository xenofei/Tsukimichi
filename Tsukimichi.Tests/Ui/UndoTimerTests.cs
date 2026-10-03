using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>The floating Undo's clock (feature plan v6 S2): eight seconds, paused while hovered, restarted by a newer change.</summary>
public class UndoTimerTests
{
    [Fact]
    public void A_new_timer_shows_nothing()
    {
        var timer = new UndoTimer();

        Assert.False(timer.Showing);
        Assert.False(timer.Tick(1.0, paused: false));
        Assert.Equal(0.0, timer.Remaining);
    }

    [Fact]
    public void The_toast_stays_for_the_undo_time_then_goes()
    {
        var timer = new UndoTimer();
        timer.Start(10.0);

        Assert.True(timer.Tick(10.0 + SafetyRules.UndoSeconds - 0.1, paused: false));
        Assert.False(timer.Tick(10.0 + SafetyRules.UndoSeconds + 0.1, paused: false));
        Assert.False(timer.Showing);
    }

    [Fact]
    public void Hovering_stops_the_clock()
    {
        var timer = new UndoTimer();
        timer.Start(0.0, seconds: 2.0);

        Assert.True(timer.Tick(1.0, paused: false));
        // Hovered for a long while: no time is used.
        Assert.True(timer.Tick(30.0, paused: true));
        Assert.Equal(1.0, timer.Remaining, 6);
        // Then a second and a half without the pointer ends it.
        Assert.False(timer.Tick(31.5, paused: false));
    }

    [Fact]
    public void A_newer_change_starts_the_clock_over()
    {
        var timer = new UndoTimer();
        timer.Start(0.0, seconds: 2.0);
        timer.Tick(1.9, paused: false);

        timer.Start(1.9, seconds: 2.0);

        Assert.Equal(1.9, timer.StartedAt);
        Assert.True(timer.Tick(3.5, paused: false));
        Assert.False(timer.Tick(4.0, paused: false));
    }

    [Fact]
    public void Stop_takes_it_down_at_once()
    {
        var timer = new UndoTimer();
        timer.Start(0.0);
        timer.Stop();

        Assert.False(timer.Showing);
        Assert.False(timer.Tick(0.1, paused: false));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    public void A_bad_length_falls_back_to_the_undo_time(double seconds)
    {
        var timer = new UndoTimer();
        timer.Start(0.0, seconds);

        Assert.Equal(SafetyRules.UndoSeconds, timer.Remaining);
    }

    [Fact]
    public void A_clock_that_went_backwards_uses_no_time()
    {
        var timer = new UndoTimer();
        timer.Start(10.0, seconds: 2.0);

        Assert.True(timer.Tick(5.0, paused: false));
        Assert.Equal(2.0, timer.Remaining, 6);
    }
}
