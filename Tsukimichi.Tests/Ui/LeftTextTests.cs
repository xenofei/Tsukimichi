using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>"Show what is left, not tallies" (feature plan v6): counts say what is still to do; the tally is for tooltips.</summary>
public sealed class LeftTextTests
{
    [Theory]
    [InlineData(3, 7, 4, "4 left", "4 left")]
    [InlineData(7, 7, 0, "", "all done")]
    [InlineData(9, 7, 0, "", "all done")]
    [InlineData(0, 120, 120, "120 left", "120 left")]
    [InlineData(-2, 3, 3, "3 left", "3 left")]
    public void What_is_left_and_a_quiet_finish(int done, int total, int count, string left, string leftOrDone)
    {
        Assert.Equal(count, LeftText.Count(done, total));
        Assert.Equal(left, LeftText.Left(done, total));
        Assert.Equal(leftOrDone, LeftText.LeftOrDone(done, total));
    }

    [Fact]
    public void The_tally_stays_for_tooltips()
    {
        Assert.Equal("4 of 7 done", LeftText.Tally(4, 7));
    }
}
