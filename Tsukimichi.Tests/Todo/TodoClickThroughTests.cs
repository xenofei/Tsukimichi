using System.Numerics;
using Tsukimichi.Core.Todo;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Todo;

/// <summary>A nearly invisible Todo overlay takes the pointer only over its rows and captions, or with a modifier held.</summary>
public class TodoClickThroughTests
{
    [Theory]
    [InlineData(0f, true)]
    [InlineData(0.05f, true)]
    [InlineData(0.1f, false)]
    [InlineData(0.85f, false)]
    [InlineData(float.NaN, false)]
    public void Under_a_tenth_the_panel_counts_as_nearly_invisible(float opacity, bool expected)
    {
        Assert.Equal(expected, TodoClickThrough.NearlyInvisible(opacity));
    }

    [Fact]
    public void A_nearly_invisible_panel_passes_clicks_off_its_targets()
    {
        Assert.True(TodoClickThrough.PassesClicks(0f, pointerOnTarget: false, modifierHeld: false));
    }

    [Fact]
    public void A_target_under_the_pointer_or_a_held_modifier_takes_the_click()
    {
        Assert.False(TodoClickThrough.PassesClicks(0f, pointerOnTarget: true, modifierHeld: false));
        Assert.False(TodoClickThrough.PassesClicks(0f, pointerOnTarget: false, modifierHeld: true));
    }

    [Fact]
    public void A_visible_panel_takes_clicks_as_before()
    {
        Assert.False(TodoClickThrough.PassesClicks(0.5f, pointerOnTarget: false, modifierHeld: false));
    }

    [Fact]
    public void Hits_finds_the_pointer_inside_a_target_edges_included()
    {
        ScreenRect[] targets =
        [
            new(new Vector2(10f, 10f), new Vector2(200f, 30f)),
            new(new Vector2(10f, 40f), new Vector2(200f, 60f)),
        ];

        Assert.True(TodoClickThrough.Hits(targets, new Vector2(50f, 20f)));
        Assert.True(TodoClickThrough.Hits(targets, new Vector2(200f, 60f)));
        Assert.False(TodoClickThrough.Hits(targets, new Vector2(50f, 35f)));
        Assert.False(TodoClickThrough.Hits([], new Vector2(50f, 20f)));
    }

    [Fact]
    public void An_empty_target_is_never_hit()
    {
        ScreenRect[] targets = [new(new Vector2(10f, 10f), new Vector2(10f, 30f))];

        Assert.False(TodoClickThrough.Hits(targets, new Vector2(10f, 20f)));
    }
}
