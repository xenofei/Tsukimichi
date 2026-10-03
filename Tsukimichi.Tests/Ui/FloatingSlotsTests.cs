using System.Numerics;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The one slot manager for floating layers (feature plan v6 U2): the Undo toast, the notice dock and hints each have a
/// home along the body's bottom edge, and none ever covers the status bar, the selected row or another layer.
/// </summary>
public class FloatingSlotsTests
{
    private const float Margin = 10f;

    // A body 1000 × 600 at (100, 50); the status bar starts at its bottom, y = 650.
    private static readonly ScreenRect Body = new(new Vector2(100f, 50f), new Vector2(1100f, 650f));
    private static readonly Vector2 Toast = new(300f, 40f);
    private static readonly Vector2 Dock = new(360f, 90f);

    private static ScreenRect Place(FloatingLayer layer, Vector2 size, ScreenRect keepClear = default, params ScreenRect[] taken) =>
        FloatingSlots.Place(layer, size, in Body, in keepClear, taken, Margin);

    [Fact]
    public void Each_layer_has_its_home_on_the_bottom_edge()
    {
        var undo = Place(FloatingLayer.Undo, Toast);
        Assert.Equal(Body.Center.X, undo.Center.X, 0);
        Assert.Equal(Body.Max.Y - Margin, undo.Max.Y);

        var dock = Place(FloatingLayer.Dock, Dock);
        Assert.Equal(Body.Max.X - Margin, dock.Max.X);
        Assert.Equal(Body.Max.Y - Margin, dock.Max.Y);

        var hint = Place(FloatingLayer.Hint, new Vector2(200f, 30f));
        Assert.Equal(Body.Min.X + Margin, hint.Min.X);
        Assert.Equal(Body.Max.Y - Margin, hint.Max.Y);
    }

    [Fact]
    public void No_layer_covers_the_status_bar()
    {
        foreach (var layer in Enum.GetValues<FloatingLayer>())
        {
            foreach (var height in new[] { 20f, 200f, 590f, 900f })
            {
                var rect = Place(layer, new Vector2(300f, height));
                Assert.True(rect.Max.Y <= Body.Max.Y || rect.Min.Y == Body.Min.Y, $"{layer} {height}: {rect}");
                Assert.True(rect.Min.Y >= Body.Min.Y);
            }
        }
    }

    [Fact]
    public void A_layer_leaves_the_selected_row_clear()
    {
        // The selected row sits right where the dock's home is: the dock goes to the top edge instead.
        var row = new ScreenRect(new Vector2(100f, 590f), new Vector2(1100f, 618f));
        var dock = Place(FloatingLayer.Dock, Dock, row);
        Assert.False(Overlaps(dock, row));
        Assert.Equal(Body.Min.Y + Margin, dock.Min.Y);
    }

    [Fact]
    public void The_selected_row_is_kept_clear_wherever_it_is()
    {
        for (var y = Body.Min.Y; y < Body.Max.Y - 28f; y += 7f)
        {
            var row = new ScreenRect(new Vector2(100f, y), new Vector2(1100f, y + 28f));
            foreach (var layer in Enum.GetValues<FloatingLayer>())
            {
                var rect = Place(layer, Dock, row);
                Assert.False(Overlaps(rect, row), $"{layer} over the row at {y}: {rect}");
            }
        }
    }

    [Fact]
    public void Layers_never_cover_each_other()
    {
        // A narrow body: the toast and the dock both want the bottom right corner; the later one stacks above.
        var narrow = new ScreenRect(new Vector2(0f, 0f), new Vector2(380f, 600f));
        var undo = FloatingSlots.Place(FloatingLayer.Undo, Toast, in narrow, default, [], Margin);
        var dock = FloatingSlots.Place(FloatingLayer.Dock, Dock, in narrow, default, [undo], Margin);
        var hint = FloatingSlots.Place(FloatingLayer.Hint, new Vector2(200f, 30f), in narrow, default, [undo, dock], Margin);

        Assert.False(Overlaps(undo, dock));
        Assert.False(Overlaps(undo, hint));
        Assert.False(Overlaps(dock, hint));
        Assert.True(dock.Max.Y <= undo.Min.Y);
    }

    [Fact]
    public void A_layer_wider_than_the_body_is_kept_inside_it()
    {
        var rect = Place(FloatingLayer.Dock, new Vector2(5000f, 60f));
        Assert.True(rect.Min.X >= Body.Min.X);
        Assert.True(rect.Max.X <= Body.Max.X);
    }

    private static bool Overlaps(ScreenRect a, ScreenRect b) =>
        a.Min.X < b.Max.X && b.Min.X < a.Max.X && a.Min.Y < b.Max.Y && b.Min.Y < a.Max.Y;
}
