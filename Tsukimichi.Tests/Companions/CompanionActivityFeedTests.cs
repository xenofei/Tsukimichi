using Tsukimichi.Core.Companions;

namespace Tsukimichi.Tests.Companions;

public class CompanionActivityFeedTests
{
    [Fact]
    public void Nothing_running_and_no_note_says_nothing()
    {
        var feed = new CompanionActivityFeed();
        Assert.Null(feed.Live);
        Assert.Null(feed.TextAt(0.0));
    }

    [Fact]
    public void Travel_comes_before_Questionable_AutoDuty_and_Artisan()
    {
        var feed = new CompanionActivityFeed();
        feed.Report(StopTarget.Artisan, "Artisan", canStop: true);
        feed.Report(StopTarget.AutoDuty, "AutoDuty", canStop: true);
        feed.Report(StopTarget.Questionable, "Questionable", canStop: true);
        Assert.Equal(StopTarget.Questionable, feed.Live?.Kind);

        feed.Report(StopTarget.Travel, "Going to giver", canStop: true);
        Assert.Equal(StopTarget.Travel, feed.Live?.Kind);
        Assert.Equal("Going to giver", feed.TextAt(0.0));

        feed.Report(StopTarget.Travel, null, canStop: false);
        feed.Report(StopTarget.Questionable, null, canStop: false);
        Assert.Equal(StopTarget.AutoDuty, feed.Live?.Kind);
    }

    [Fact]
    public void An_empty_line_reads_as_not_running()
    {
        var feed = new CompanionActivityFeed();
        feed.Report(StopTarget.Questionable, string.Empty, canStop: true);
        Assert.Null(feed.Live);
    }

    [Fact]
    public void A_note_takes_the_text_for_its_time_and_the_live_hand_off_keeps_its_stop()
    {
        var feed = new CompanionActivityFeed();
        feed.Report(StopTarget.AutoDuty, "AutoDuty is running.", canStop: true);
        feed.Note("AutoDuty started.", 10.0);

        Assert.Equal("AutoDuty started.", feed.TextAt(10.0));
        Assert.Equal("AutoDuty started.", feed.TextAt(10.0 + CompanionActivityFeed.NoteSeconds - 0.01));
        Assert.True(feed.Live?.CanStop);
        Assert.Equal("AutoDuty is running.", feed.TextAt(10.0 + CompanionActivityFeed.NoteSeconds));
    }

    [Fact]
    public void A_newer_note_replaces_the_older_one_and_starts_its_own_time()
    {
        var feed = new CompanionActivityFeed();
        feed.Note("Copied.", 0.0);
        feed.Note("Sent to Artisan.", 5.0);
        Assert.Equal("Sent to Artisan.", feed.TextAt(CompanionActivityFeed.NoteSeconds + 1.0));

        feed.ClearNote();
        Assert.Null(feed.TextAt(5.0));
    }
}
