using System.Numerics;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

public class BesidePlacementTests
{
    private static readonly ScreenRect Screen = new(Vector2.Zero, new Vector2(1920, 1080));
    private static readonly Vector2 Panel = new(300, 120);
    private const float Gap = 6f;

    [Fact]
    public void Goes_right_of_the_window_aligned_with_its_top()
    {
        var window = ScreenRect.FromSize(new Vector2(400, 200), new Vector2(700, 500));

        Assert.True(BesidePlacement.TryPlace(window, Panel, Screen, Gap, out var pos, out var side));
        Assert.Equal(CardSide.Right, side);
        Assert.Equal(new Vector2(1106, 200), pos);
    }

    [Fact]
    public void Goes_left_when_the_right_has_no_room()
    {
        var window = ScreenRect.FromSize(new Vector2(1100, 200), new Vector2(700, 500));

        Assert.True(BesidePlacement.TryPlace(window, Panel, Screen, Gap, out var pos, out var side));
        Assert.Equal(CardSide.Left, side);
        Assert.Equal(new Vector2(1100 - Gap - Panel.X, 200), pos);
    }

    [Fact]
    public void Goes_below_a_window_as_wide_as_the_screen()
    {
        var window = ScreenRect.FromSize(new Vector2(0, 100), new Vector2(1920, 600));

        Assert.True(BesidePlacement.TryPlace(window, Panel, Screen, Gap, out var pos, out var side));
        Assert.Equal(CardSide.Below, side);
        Assert.Equal(new Vector2(0, 706), pos);
    }

    [Fact]
    public void Goes_above_when_only_the_top_has_room()
    {
        var window = ScreenRect.FromSize(new Vector2(0, 300), new Vector2(1920, 780));

        Assert.True(BesidePlacement.TryPlace(window, Panel, Screen, Gap, out var pos, out var side));
        Assert.Equal(CardSide.Above, side);
        Assert.Equal(new Vector2(0, 300 - Gap - Panel.Y), pos);
    }

    [Fact]
    public void Slides_up_to_stay_on_screen_beside_a_window_near_the_bottom()
    {
        var window = ScreenRect.FromSize(new Vector2(400, 1000), new Vector2(700, 80));

        Assert.True(BesidePlacement.TryPlace(window, Panel, Screen, Gap, out var pos, out var side));
        Assert.Equal(CardSide.Right, side);
        Assert.Equal(1080 - Panel.Y, pos.Y);
    }

    [Fact]
    public void Declines_when_no_side_has_room()
    {
        var window = ScreenRect.FromSize(new Vector2(10, 10), new Vector2(1900, 1060));

        Assert.False(BesidePlacement.TryPlace(window, Panel, Screen, Gap, out _, out _));
    }

    [Fact]
    public void Declines_an_empty_or_oversized_panel()
    {
        var window = ScreenRect.FromSize(new Vector2(400, 200), new Vector2(700, 500));

        Assert.False(BesidePlacement.TryPlace(window, Vector2.Zero, Screen, Gap, out _, out _));
        Assert.False(BesidePlacement.TryPlace(window, new Vector2(2000, 100), Screen, Gap, out _, out _));
    }

    [Fact]
    public void Never_covers_the_window_and_stays_on_screen()
    {
        for (var x = -200; x <= 1900; x += 150)
        {
            for (var y = -100; y <= 1060; y += 120)
            {
                foreach (var windowSize in new[] { new Vector2(700, 500), new Vector2(1500, 900), new Vector2(200, 100) })
                {
                    var window = ScreenRect.FromSize(new Vector2(x, y), windowSize);
                    if (!BesidePlacement.TryPlace(window, Panel, Screen, Gap, out var pos, out _))
                    {
                        continue;
                    }

                    var panel = ScreenRect.FromSize(pos, Panel);
                    Assert.True(ScreenRect.Intersect(panel, window).IsEmpty, $"panel {panel} covers window {window}");
                    Assert.True(panel.Min.X >= 0 && panel.Min.Y >= 0 && panel.Max.X <= 1920 && panel.Max.Y <= 1080, $"panel {panel} leaves the screen");
                }
            }
        }
    }
}
