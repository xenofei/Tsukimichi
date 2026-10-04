using System.Numerics;
using Tsukimichi.Core.Umbra;

namespace Tsukimichi.Tests.Umbra;

/// <summary>
/// Keeping clear of Umbra's toolbar (plan v8 M3; spec-1.22 M3 "Keeping clear", umbra-clearance-1.22.png): top edges at the
/// bar's height + 8 under a top bar, bottom edges at its top − 8 over a bottom one, nothing for a floating or
/// auto-hidden bar, the assumed 32 px top bar when Umbra's settings can't be read, and the player's own place given back
/// when the bar leaves.
/// </summary>
public sealed class UmbraClearanceTests
{
    private const float ViewportTop = 0f;
    private const float ViewportHeight = 1080f;

    private static readonly UmbraToolbar Top = new(Enabled: true, TopAligned: true, AutoHide: false, Stretched: true, Height: 32, YOffset: 0, UiScalePercent: 100);

    [Fact]
    public void Without_Umbra_nothing_keeps_clear()
    {
        var clearance = UmbraClearance.For(umbraLoaded: false, Top, assumedHeight: 32, uiScale: 1f);
        Assert.Equal(UmbraClearance.None, clearance);
        Assert.False(clearance.Any);
        Assert.Equal(new Vector2(10, 5), clearance.Apply(new Vector2(10, 5), new Vector2(200, 100), ViewportTop, ViewportHeight));
    }

    [Fact]
    public void A_top_bar_keeps_top_edges_at_its_height_plus_eight()
    {
        var clearance = UmbraClearance.For(true, Top, 32, 1f);
        Assert.Equal(40f, clearance.Top);
        Assert.Equal(0f, clearance.Bottom);
        Assert.Equal(ClearanceSource.Read, clearance.Source);

        // The moon icon reads the bar without the gap (it adds its own 8 px).
        Assert.Equal(32f, clearance.Bar);
        Assert.True(clearance.TopAligned);

        Assert.Equal(new Vector2(10, 40), clearance.Apply(new Vector2(10, 5), new Vector2(200, 100), ViewportTop, ViewportHeight));
        // Already clear: unchanged.
        Assert.Equal(new Vector2(10, 300), clearance.Apply(new Vector2(10, 300), new Vector2(200, 100), ViewportTop, ViewportHeight));
    }

    [Fact]
    public void The_bar_scales_with_Umbras_UI_scale_and_the_gap_with_Tsukimichis()
    {
        var clearance = UmbraClearance.For(true, Top with { UiScalePercent = 150, YOffset = 4 }, 32, uiScale: 2f);
        // (32 + 4) × 1.5 = 54, + 8 × 2 = 70.
        Assert.Equal(70f, clearance.Top);
        Assert.Equal(54f, clearance.Bar);
    }

    [Fact]
    public void A_bottom_bar_keeps_bottom_edges_above_it()
    {
        var clearance = UmbraClearance.For(true, Top with { TopAligned = false }, 32, 1f);
        Assert.Equal(0f, clearance.Top);
        Assert.Equal(40f, clearance.Bottom);
        Assert.False(clearance.TopAligned);

        // A 100 px panel whose foot sits at 1070 moves up so its foot is at 1080 − 40.
        Assert.Equal(new Vector2(10, 940), clearance.Apply(new Vector2(10, 970), new Vector2(200, 100), ViewportTop, ViewportHeight));
        Assert.Equal(new Vector2(10, 500), clearance.Apply(new Vector2(10, 500), new Vector2(200, 100), ViewportTop, ViewportHeight));
    }

    [Fact]
    public void A_floating_auto_hidden_or_hidden_bar_moves_nothing()
    {
        foreach (var bar in new[] { Top with { Stretched = false }, Top with { AutoHide = true }, Top with { Enabled = false }, Top with { Height = 0 } })
        {
            var clearance = UmbraClearance.For(true, bar, 32, 1f);
            Assert.False(clearance.Any);
            Assert.Equal(ClearanceSource.Read, clearance.Source);
        }
    }

    [Fact]
    public void Unread_settings_assume_a_top_bar_of_the_set_height()
    {
        var assumed = UmbraClearance.For(true, toolbar: null, assumedHeight: 32, uiScale: 1f);
        Assert.Equal(40f, assumed.Top);
        Assert.Equal(ClearanceSource.Assumed, assumed.Source);

        Assert.Equal(56f, UmbraClearance.For(true, null, 48, 1f).Top);
        Assert.False(UmbraClearance.For(true, null, 0, 1f).Any);
    }

    [Fact]
    public void A_surface_taller_than_the_room_keeps_its_top_clear()
    {
        var clearance = new UmbraClearance(40f, 40f, ClearanceSource.Read, true);
        Assert.Equal(new Vector2(0, 40), clearance.Apply(new Vector2(0, 0), new Vector2(100, 1060), ViewportTop, ViewportHeight));
    }

    [Fact]
    public void The_players_place_comes_back_when_the_bar_leaves()
    {
        var bar = UmbraClearance.For(true, Top, 32, 1f);
        var size = new Vector2(200, 100);
        var place = ClearedPlace.Untouched;

        // The bar appears: the overlay at y 10 moves to 40, and y 10 is remembered.
        var (setTo, next) = place.Step(new Vector2(20, 10), size, ViewportTop, ViewportHeight, bar, dragging: false);
        Assert.Equal(new Vector2(20, 40), setTo);
        Assert.Equal(new ClearedPlace(new Vector2(20, 10), new Vector2(20, 40)), next);

        // While the bar stays, nothing moves.
        (setTo, next) = next.Step(new Vector2(20, 40), size, ViewportTop, ViewportHeight, bar, dragging: false);
        Assert.Null(setTo);

        // The bar leaves: back to y 10, nothing remembered.
        (setTo, next) = next.Step(new Vector2(20, 40), size, ViewportTop, ViewportHeight, UmbraClearance.None, dragging: false);
        Assert.Equal(new Vector2(20, 10), setTo);
        Assert.Equal(ClearedPlace.Untouched, next);
    }

    [Fact]
    public void Moving_the_surface_makes_its_place_the_players()
    {
        var bar = UmbraClearance.For(true, Top, 32, 1f);
        var size = new Vector2(200, 100);
        var (_, moved) = ClearedPlace.Untouched.Step(new Vector2(20, 10), size, ViewportTop, ViewportHeight, bar, dragging: false);

        // A drag, even into the bar's band: nothing moves while it lasts.
        var (setTo, next) = moved.Step(new Vector2(300, 12), size, ViewportTop, ViewportHeight, bar, dragging: true);
        Assert.Null(setTo);
        Assert.Equal(moved, next);

        // A click that moved nothing keeps the remembered place.
        (setTo, next) = moved.Step(new Vector2(20, 40), size, ViewportTop, ViewportHeight, bar, dragging: true);
        Assert.Null(setTo);
        Assert.Equal(moved, next);

        // Once dropped somewhere else, the new place is the player's: the bar leaving moves nothing.
        (setTo, next) = moved.Step(new Vector2(300, 600), size, ViewportTop, ViewportHeight, UmbraClearance.None, dragging: false);
        Assert.Null(setTo);
        Assert.Equal(ClearedPlace.Untouched, next);

        // Dropped inside the band: kept clear, and that drop is the place that comes back.
        (setTo, next) = moved.Step(new Vector2(300, 12), size, ViewportTop, ViewportHeight, bar, dragging: false);
        Assert.Equal(new Vector2(300, 40), setTo);
        Assert.Equal(new Vector2(300, 12), next.Original);
    }

    [Theory]
    [InlineData(1.1f)]
    [InlineData(1.2f)]
    [InlineData(1.35f)]
    [InlineData(0.95f)]
    public void At_a_fractional_UI_scale_the_cleared_place_is_whole_pixels_and_the_players_place_is_kept(float uiScale)
    {
        // ImGui keeps window positions in whole pixels: a fractional place (32 + 8 × 1.2 = 41.6) comes back as 41 and
        // must not read as the player moving the overlay.
        var size = new Vector2(200, 100);
        foreach (var bar in new[] { UmbraClearance.For(true, Top, 32, uiScale), UmbraClearance.For(true, Top with { TopAligned = false }, 32, uiScale) })
        {
            var start = bar.TopAligned ? new Vector2(20, 10) : new Vector2(20, 975);
            var (setTo, next) = ClearedPlace.Untouched.Step(start, size, ViewportTop, ViewportHeight, bar, dragging: false);
            Assert.NotNull(setTo);
            var placed = setTo.Value;
            Assert.Equal(MathF.Round(placed.Y), placed.Y);
            Assert.Equal(placed, next.Moved);

            // Clear of the band: the top edge at or below it, the bottom edge at or above it.
            if (bar.TopAligned)
            {
                Assert.True(placed.Y >= bar.Top);
            }
            else
            {
                Assert.True(placed.Y + size.Y <= ViewportHeight - bar.Bottom);
            }

            // Next frame ImGui reports the window where it was put, truncated to whole pixels: nothing moves, nothing is forgotten.
            var landed = new Vector2(MathF.Truncate(placed.X), MathF.Truncate(placed.Y));
            (setTo, next) = next.Step(landed, size, ViewportTop, ViewportHeight, bar, dragging: false);
            Assert.Null(setTo);
            Assert.Equal(start, next.Original);

            // The bar leaves: the player's own place comes back.
            (setTo, _) = next.Step(landed, size, ViewportTop, ViewportHeight, UmbraClearance.None, dragging: false);
            Assert.Equal(start, setTo);
        }
    }

    [Fact]
    public void Unread_settings_cost_no_allocation_per_frame()
    {
        UmbraClearance.For(true, null, 32, 1.2f);
        UmbraClearance.For(true, null, 48, 1.2f);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++)
        {
            UmbraClearance.For(true, null, 32, 1.2f);
            UmbraClearance.For(true, null, 48, 1.2f);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void A_bar_that_shrinks_gives_back_what_it_can()
    {
        var tall = UmbraClearance.For(true, Top with { Height = 64 }, 32, 1f);
        var size = new Vector2(200, 100);
        var (setTo, next) = ClearedPlace.Untouched.Step(new Vector2(20, 10), size, ViewportTop, ViewportHeight, tall, false);
        Assert.Equal(new Vector2(20, 72), setTo);

        var shorter = UmbraClearance.For(true, Top, 32, 1f);
        (setTo, next) = next.Step(new Vector2(20, 72), size, ViewportTop, ViewportHeight, shorter, false);
        Assert.Equal(new Vector2(20, 40), setTo);
        Assert.Equal(new Vector2(20, 10), next.Original);
    }
}
