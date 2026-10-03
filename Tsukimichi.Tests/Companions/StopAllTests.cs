using Tsukimichi.Core.Companions;

namespace Tsukimichi.Tests.Companions;

public class StopAllTests
{
    [Fact]
    public void Everything_running_stops_on_the_first_press_when_nothing_asks()
    {
        var running = StopTarget.Travel | StopTarget.Questionable | StopTarget.Artisan;
        var decision = StopAll.Decide(running, StopTarget.None, confirmed: false);
        Assert.Equal(running, decision.StopNow);
        Assert.Equal(StopTarget.None, decision.Ask);
    }

    [Fact]
    public void A_stop_that_asks_waits_while_the_rest_stop()
    {
        var running = StopTarget.Travel | StopTarget.Questionable | StopTarget.AutoDuty;
        var decision = StopAll.Decide(running, StopTarget.Questionable | StopTarget.AutoDuty, confirmed: false);
        Assert.Equal(StopTarget.Travel, decision.StopNow);
        Assert.Equal(StopTarget.Questionable | StopTarget.AutoDuty, decision.Ask);
    }

    [Fact]
    public void A_confirming_press_stops_what_was_asked_about()
    {
        var running = StopTarget.Questionable | StopTarget.AutoDuty;
        var decision = StopAll.Decide(running, running, confirmed: true);
        Assert.Equal(running, decision.StopNow);
        Assert.Equal(StopTarget.None, decision.Ask);
    }

    [Fact]
    public void Nothing_that_is_not_running_is_asked_about()
    {
        var decision = StopAll.Decide(StopTarget.None, StopTarget.Questionable, confirmed: false);
        Assert.Equal(StopTarget.None, decision.StopNow);
        Assert.Equal(StopTarget.None, decision.Ask);
    }

    [Fact]
    public void Targets_come_out_in_stop_order()
    {
        var all = StopTarget.Artisan | StopTarget.Travel | StopTarget.AutoDuty | StopTarget.Lifestream | StopTarget.Questionable;
        Assert.Equal(StopAll.Order, StopAll.Each(all));
        Assert.Equal([StopTarget.Travel, StopTarget.Artisan], StopAll.Each(StopTarget.Artisan | StopTarget.Travel));
        Assert.Empty(StopAll.Each(StopTarget.None));
    }

    [Fact]
    public void A_second_press_within_the_window_confirms_once()
    {
        var confirm = new StopConfirm();
        Assert.False(confirm.Answer(0));

        confirm.Ask(1_000);
        Assert.True(confirm.Answer(1_000 + StopConfirm.WindowMs));
        Assert.False(confirm.Answer(1_000 + StopConfirm.WindowMs));
    }

    [Fact]
    public void A_late_second_press_asks_again()
    {
        var confirm = new StopConfirm();
        confirm.Ask(1_000);
        Assert.False(confirm.Answer(1_001 + StopConfirm.WindowMs));
        Assert.Equal(10, StopConfirm.WindowSeconds);
    }
}

public class HandOffClaimTests
{
    [Fact]
    public void Nothing_is_ours_until_a_hand_off_is_claimed()
    {
        var claim = new HandOffClaim();
        Assert.False(claim.Claimed);
        Assert.False(claim.Observe(busy: true, now: 0));
        Assert.False(claim.Claimed);
    }

    [Fact]
    public void A_claimed_run_is_ours_while_busy_and_ends_when_it_goes_idle()
    {
        var claim = new HandOffClaim();
        claim.Claim(0);
        Assert.False(claim.Observe(busy: false, now: 100));
        Assert.True(claim.Claimed);
        Assert.True(claim.Observe(busy: true, now: 200));
        Assert.True(claim.Observe(busy: true, now: 60_000));
        Assert.False(claim.Observe(busy: false, now: 60_100));
        Assert.False(claim.Claimed);

        // A run the player starts later in the plugin's own window is not Tsukimichi's.
        Assert.False(claim.Observe(busy: true, now: 70_000));
    }

    [Fact]
    public void A_hand_off_that_never_starts_lapses_after_the_grace()
    {
        var claim = new HandOffClaim();
        claim.Claim(0);
        Assert.False(claim.Observe(busy: false, now: HandOffClaim.GraceMs));
        Assert.True(claim.Claimed);
        Assert.False(claim.Observe(busy: false, now: HandOffClaim.GraceMs + 1));
        Assert.False(claim.Claimed);
    }

    [Fact]
    public void Release_and_a_new_claim_start_over()
    {
        var claim = new HandOffClaim();
        claim.Claim(0);
        Assert.True(claim.Observe(busy: true, now: 10));
        claim.Release();
        Assert.False(claim.Claimed);

        claim.Claim(1_000);
        Assert.False(claim.Observe(busy: false, now: 1_100));
        Assert.True(claim.Claimed);
    }
}
