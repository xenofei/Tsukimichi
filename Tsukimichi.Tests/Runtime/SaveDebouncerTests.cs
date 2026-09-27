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

    [Fact]
    public void Failed_save_keeps_the_change_pending_and_waits_a_full_interval()
    {
        var d = new SaveDebouncer(TimeSpan.FromSeconds(10));
        d.MarkDirty();

        d.MarkFailed(T0);

        Assert.True(d.Pending);
        Assert.Equal(1, d.Failures);
        Assert.Null(d.LastSavedUtc);
        Assert.Equal(T0, d.LastAttemptUtc);
        Assert.False(d.ShouldSave(T0.AddSeconds(1)));
        Assert.False(d.ShouldSave(T0.AddSeconds(9.9)));
        Assert.True(d.ShouldSave(T0.AddSeconds(10)));
    }

    [Fact]
    public void Repeated_failures_count_until_a_save_succeeds()
    {
        var d = new SaveDebouncer(TimeSpan.FromSeconds(10));
        d.MarkDirty();
        d.MarkFailed(T0);
        d.MarkFailed(T0.AddSeconds(10));
        Assert.Equal(2, d.Failures);
        Assert.True(d.ShouldSave(T0.AddSeconds(20)));

        d.MarkSaved(T0.AddSeconds(20));

        Assert.Equal(0, d.Failures);
        Assert.False(d.Pending);
        Assert.Equal(T0.AddSeconds(20), d.LastSavedUtc);
        Assert.Equal(T0.AddSeconds(20), d.LastAttemptUtc);
    }

    [Fact]
    public void Failure_after_a_successful_save_still_spaces_the_retry()
    {
        var d = new SaveDebouncer(TimeSpan.FromSeconds(10));
        d.MarkDirty();
        d.MarkSaved(T0);
        d.MarkDirty();
        Assert.True(d.ShouldSave(T0.AddSeconds(10)));

        d.MarkFailed(T0.AddSeconds(10));

        Assert.Equal(T0, d.LastSavedUtc);
        Assert.False(d.ShouldSave(T0.AddSeconds(15)));
        Assert.True(d.ShouldSave(T0.AddSeconds(20)));
    }
}
