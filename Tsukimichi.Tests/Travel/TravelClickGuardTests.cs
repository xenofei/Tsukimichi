using Tsukimichi.Core.Travel;

namespace Tsukimichi.Tests.Travel;

/// <summary>A double click on a travel control starts once and is not undone by its own second click (<see cref="TravelClickGuard"/>).</summary>
public class TravelClickGuardTests
{
    [Fact]
    public void Nothing_is_held_before_a_start()
    {
        var guard = new TravelClickGuard();

        Assert.False(guard.Holding(0));
        Assert.False(guard.Holding(1_000_000));
    }

    [Fact]
    public void A_click_right_after_a_start_is_held()
    {
        var guard = new TravelClickGuard();
        guard.Started(10_000);

        Assert.True(guard.Holding(10_000));
        Assert.True(guard.Holding(10_000 + TravelClickGuard.WindowMs - 1));
    }

    [Fact]
    public void A_click_after_the_window_goes_through()
    {
        var guard = new TravelClickGuard();
        guard.Started(10_000);

        Assert.False(guard.Holding(10_000 + TravelClickGuard.WindowMs));
    }

    [Fact]
    public void A_clock_that_went_backwards_does_not_hold()
    {
        var guard = new TravelClickGuard();
        guard.Started(10_000);

        Assert.False(guard.Holding(9_000));
    }
}
