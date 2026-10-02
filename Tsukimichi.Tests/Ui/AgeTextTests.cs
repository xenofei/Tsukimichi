using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>The one "how long ago" rule (1.8.0, R7 F6): hours below a day, whole days from one day on.</summary>
public sealed class AgeTextTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(0, AgeUnit.JustNow, 0)]
    [InlineData(59, AgeUnit.JustNow, 0)]
    [InlineData(60, AgeUnit.Minutes, 1)]
    [InlineData(59 * 60 + 59, AgeUnit.Minutes, 59)]
    [InlineData(3600, AgeUnit.Hours, 1)]
    [InlineData(23 * 3600 + 3599, AgeUnit.Hours, 23)]
    [InlineData(24 * 3600, AgeUnit.Days, 1)]
    [InlineData(47 * 3600, AgeUnit.Days, 1)]
    [InlineData(49 * 3600, AgeUnit.Days, 2)]
    public void Ages_read_in_one_unit(int secondsAgo, AgeUnit unit, int value)
    {
        Assert.Equal((unit, value), AgeText.Of(Now.AddSeconds(-secondsAgo), Now));
    }

    [Fact]
    public void A_time_slightly_ahead_reads_just_now()
    {
        Assert.Equal((AgeUnit.JustNow, 0), AgeText.Of(Now.AddSeconds(30), Now));
    }
}
