using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>A name, its marks and a tail on one line, ending in an ellipsis instead of being cut mid-letter (R3 #5).</summary>
public class LineFitTests
{
    [Fact]
    public void Everything_shows_when_the_line_has_room()
    {
        Span<bool> shown = stackalloc bool[2];
        var fit = LineFit.Fit(400f, 120f, 48f, [30f, 20f], shown, tailGap: 8f);

        Assert.False(fit.NameCut);
        Assert.Equal(120f, fit.NameDrawn);
        Assert.Equal(2, fit.Visible);
        Assert.True(shown[0] && shown[1]);
        Assert.Equal(400f - 120f - 50f - 8f, fit.TailRoom);
    }

    [Fact]
    public void A_long_name_ends_in_an_ellipsis_and_leaves_no_tail()
    {
        Span<bool> shown = stackalloc bool[1];
        var fit = LineFit.Fit(200f, 500f, 48f, [30f], shown, tailGap: 8f);

        Assert.True(fit.NameCut);
        Assert.Equal(170f, fit.NameRoom);
        Assert.Equal(170f, fit.NameDrawn);
        Assert.Equal(1, fit.Visible);
        Assert.Equal(0f, fit.TailRoom);
    }

    [Fact]
    public void The_least_important_part_goes_before_the_name_drops_under_its_minimum()
    {
        Span<bool> shown = stackalloc bool[2];
        var fit = LineFit.Fit(100f, 300f, 48f, [30f, 40f], shown);

        Assert.Equal(1, fit.Visible);
        Assert.True(shown[0]);
        Assert.False(shown[1]);
        Assert.Equal(70f, fit.NameRoom);
        Assert.True(fit.NameCut);
    }

    [Fact]
    public void A_tail_with_less_than_its_minimum_is_left_out()
    {
        Span<bool> shown = stackalloc bool[0];
        Assert.Equal(0f, LineFit.Fit(130f, 100f, 48f, [], shown, tailGap: 8f, tailMin: 30f).TailRoom);
        Assert.Equal(42f, LineFit.Fit(150f, 100f, 48f, [], shown, tailGap: 8f, tailMin: 30f).TailRoom);
    }

    [Fact]
    public void A_name_within_half_a_pixel_of_its_room_is_not_cut()
    {
        Span<bool> shown = stackalloc bool[0];
        Assert.False(LineFit.Fit(100f, 100.4f, 48f, [], shown).NameCut);
        Assert.True(LineFit.Fit(100f, 101f, 48f, [], shown).NameCut);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(-20f)]
    public void Nonsense_widths_fit_nothing_rather_than_throw(float available)
    {
        Span<bool> shown = stackalloc bool[1];
        var fit = LineFit.Fit(available, 80f, 48f, [10f], shown, tailGap: 4f);

        Assert.Equal(0f, fit.NameRoom);
        Assert.Equal(0f, fit.TailRoom);
        Assert.True(fit.NameCut);
    }

    [Theory]
    [InlineData(100f, 100f, false)]
    [InlineData(100.5f, 100f, false)]
    [InlineData(100.6f, 100f, true)]
    [InlineData(10f, -5f, true)]
    public void Needs_ellipsis_allows_half_a_pixel(float width, float room, bool expected)
    {
        Assert.Equal(expected, LineFit.NeedsEllipsis(width, room));
    }
}
