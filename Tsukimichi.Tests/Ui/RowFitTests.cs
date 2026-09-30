using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>Rows that drop parts rather than collide (feature plan v4 L2).</summary>
public class RowFitTests
{
    // A tree row's parts, most important first: count 52, Ready pill 30, mini bar 56, expansion pill 40.
    private static readonly float[] TreeParts = [52f, 30f, 56f, 40f];

    [Fact]
    public void A_wide_row_shows_every_part_and_gives_the_name_the_rest()
    {
        Span<bool> visible = stackalloc bool[4];
        var fit = RowFit.Fit(400f, 120f, 48f, TreeParts, visible);
        Assert.Equal(4, fit.Visible);
        Assert.Equal(178f, fit.PartsWidth, 3);
        Assert.Equal(222f, fit.NameRoom, 3);
        Assert.All(visible.ToArray(), static v => Assert.True(v));
    }

    [Fact]
    public void Parts_go_from_the_least_important_until_the_name_keeps_its_minimum()
    {
        Span<bool> visible = stackalloc bool[4];

        // 178 of parts in 220 leaves 42 for a long name: the expansion pill goes (82 left).
        var fit = RowFit.Fit(220f, 300f, 48f, TreeParts, visible);
        Assert.Equal(3, fit.Visible);
        Assert.Equal(new[] { true, true, true, false }, visible.ToArray());
        Assert.Equal(220f - 138f, fit.NameRoom, 3);

        // 160: with the bar the name has 22; without it 78.
        fit = RowFit.Fit(160f, 300f, 48f, TreeParts, visible);
        Assert.Equal(2, fit.Visible);
        Assert.Equal(new[] { true, true, false, false }, visible.ToArray());
        Assert.Equal(78f, fit.NameRoom, 3);
    }

    [Fact]
    public void What_remains_is_always_a_prefix_of_the_priority_order()
    {
        Span<bool> visible = stackalloc bool[4];
        for (var width = 0f; width <= 400f; width += 3f)
        {
            RowFit.Fit(width, float.PositiveInfinity, 48f, TreeParts, visible);
            var seenHidden = false;
            foreach (var shown in visible)
            {
                Assert.False(shown && seenHidden, $"a less important part shows while a more important one is hidden at {width}");
                seenHidden |= !shown;
            }
        }
    }

    [Fact]
    public void Fewer_parts_show_as_the_row_narrows()
    {
        Span<bool> visible = stackalloc bool[4];
        var last = int.MaxValue;
        for (var width = 400f; width >= 0f; width -= 5f)
        {
            var fit = RowFit.Fit(width, float.PositiveInfinity, 48f, TreeParts, visible);
            Assert.True(fit.Visible <= last);
            Assert.True(fit.NameRoom >= 48f || fit.Visible == 0, $"name room {fit.NameRoom} at {width} with {fit.Visible} parts");
            last = fit.Visible;
        }
    }

    [Fact]
    public void A_short_name_only_needs_its_own_width()
    {
        Span<bool> visible = stackalloc bool[4];

        // "Eden" is 30 wide: 178 + 30 fits in 210 though the minimum is 48.
        var fit = RowFit.Fit(210f, 30f, 48f, TreeParts, visible);
        Assert.Equal(4, fit.Visible);
        Assert.Equal(32f, fit.NameRoom, 3);
    }

    [Fact]
    public void Too_narrow_for_the_name_alone_shows_no_part_and_gives_the_name_everything()
    {
        Span<bool> visible = stackalloc bool[4];
        var fit = RowFit.Fit(40f, 300f, 48f, TreeParts, visible);
        Assert.Equal(0, fit.Visible);
        Assert.Equal(40f, fit.NameRoom);
        Assert.Equal(0f, fit.PartsWidth);
    }

    [Fact]
    public void Unreadable_inputs_are_safe()
    {
        Span<bool> visible = stackalloc bool[3];
        var fit = RowFit.Fit(float.NaN, float.NaN, float.NaN, [10f, float.NaN, -5f], visible);
        Assert.True(fit.NameRoom >= 0f);
        Assert.True(fit.PartsWidth >= 0f);

        var none = RowFit.Fit(100f, 50f, 48f, ReadOnlySpan<float>.Empty, Span<bool>.Empty);
        Assert.Equal(0, none.Visible);
        Assert.Equal(100f, none.NameRoom);
    }

    [Fact]
    public void A_short_visible_span_is_refused()
    {
        Assert.Throws<ArgumentException>(() => RowFit.Fit(100f, 50f, 48f, [1f, 2f], new bool[1]));
    }
}
