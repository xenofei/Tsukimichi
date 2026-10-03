using Tsukimichi.Core.Companions;

namespace Tsukimichi.Tests.Companions;

/// <summary>The companion settings are read again only for a reader, after a file settles, and on the backstop.</summary>
public class SetupReadScheduleTests
{
    private const long Interval = 30_000;
    private const long Settle = 300;
    private const long ReadAt = 1_000_000;

    [Fact]
    public void Nothing_is_read_while_nobody_asks()
    {
        Assert.False(SetupReadSchedule.IsDue(wanted: false, listMoved: true, stale: true, ReadAt + Interval * 10, 0, ReadAt, Interval, Settle));
    }

    [Fact]
    public void A_changed_plugin_list_is_read_at_once()
    {
        Assert.True(SetupReadSchedule.IsDue(wanted: true, listMoved: true, stale: false, ReadAt + 1, 0, ReadAt, Interval, Settle));
    }

    [Fact]
    public void A_changed_file_waits_until_it_has_been_quiet()
    {
        var changedAt = ReadAt + 5_000;
        Assert.False(SetupReadSchedule.IsDue(wanted: true, listMoved: false, stale: true, changedAt + Settle - 1, changedAt, ReadAt, Interval, Settle));
        Assert.True(SetupReadSchedule.IsDue(wanted: true, listMoved: false, stale: true, changedAt + Settle, changedAt, ReadAt, Interval, Settle));
    }

    [Fact]
    public void An_invalidation_waits_for_nothing()
    {
        Assert.True(SetupReadSchedule.IsDue(wanted: true, listMoved: false, stale: true, ReadAt + 1, 0, ReadAt, Interval, Settle));
    }

    [Fact]
    public void Without_a_change_the_backstop_reads_again_after_the_interval()
    {
        Assert.False(SetupReadSchedule.IsDue(wanted: true, listMoved: false, stale: false, ReadAt + Interval - 1, 0, ReadAt, Interval, Settle));
        Assert.True(SetupReadSchedule.IsDue(wanted: true, listMoved: false, stale: false, ReadAt + Interval, 0, ReadAt, Interval, Settle));
    }
}
