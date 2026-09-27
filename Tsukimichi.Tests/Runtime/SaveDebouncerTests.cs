using Tsukimichi.Core.Runtime;

namespace Tsukimichi.Tests.Runtime;

public sealed class SaveDebouncerTests
{
    private static readonly DateTime T0 = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Nothing_pending_means_no_save()
    {
        var d = new SaveDebouncer(TimeSpan.FromSeconds(10));

        Assert.False(d.Pending);
        Assert.False(d.ShouldSave(T0));
    }

    [Fact]
    public void First_change_saves_immediately()
    {
        var d = new SaveDebouncer(TimeSpan.FromSeconds(10));

        d.MarkDirty();

        Assert.True(d.ShouldSave(T0));
    }

    [Fact]
    public void Changes_within_the_interval_wait_until_it_elapses()
    {
        var d = new SaveDebouncer(TimeSpan.FromSeconds(10));
        d.MarkDirty();
        d.MarkSaved(T0);

        d.MarkDirty();

        Assert.True(d.Pending);
        Assert.False(d.ShouldSave(T0.AddSeconds(9.9)));
        Assert.True(d.ShouldSave(T0.AddSeconds(10)));
    }

    [Fact]
    public void Saving_clears_pending_until_the_next_change()
    {
        var d = new SaveDebouncer(TimeSpan.FromSeconds(10));
        d.MarkDirty();

        d.MarkSaved(T0);

        Assert.False(d.Pending);
        Assert.Equal(T0, d.LastSavedUtc);
        Assert.False(d.ShouldSave(T0.AddMinutes(5)));
    }

    [Fact]
    public void Zero_interval_saves_on_every_change()
    {
        var d = new SaveDebouncer(TimeSpan.Zero);
        d.MarkDirty();
        d.MarkSaved(T0);
        d.MarkDirty();

        Assert.True(d.ShouldSave(T0));
    }

    [Fact]
    public void Negative_interval_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SaveDebouncer(TimeSpan.FromSeconds(-1)));
    }
}
