using System.Numerics;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

public class MoonDetailTests
{
    [Theory]
    [InlineData(0.10f)]
    [InlineData(0.30f)]
    [InlineData(0.50f)]
    [InlineData(0.60f)]
    [InlineData(0.80f)]
    [InlineData(0.95f)]
    public void Terminator_crosses_the_equator_at_one_minus_twice_the_width_and_meets_both_poles(float width)
    {
        var region = LitRegion.ForWidth(width);
        Assert.Equal(1f - 2f * width, region.BoundaryX(0f), 4);
        Assert.Equal(0f, region.BoundaryX(-1f), 3);
        Assert.Equal(0f, region.BoundaryX(1f), 3);
    }

    [Fact]
    public void Accepted_gibbous_terminator_is_the_circle_at_2_4_r_with_radius_2_6_r()
    {
        var region = LitRegion.ForWidth(0.60f);
        // Any point of the circle centre (2.4, 0) radius 2.6 inside the disc lies on the boundary.
        foreach (var y in new[] { -0.9f, -0.5f, 0f, 0.3f, 0.7f })
        {
            var x = region.BoundaryX(y);
            Assert.Equal(2.6f, MathF.Sqrt((x - 2.4f) * (x - 2.4f) + y * y), 3);
        }
    }

    [Fact]
    public void Waning_lens_is_lit_on_the_left_with_cusps_at_the_rim()
    {
        var region = LitRegion.WaningLens;
        Assert.True(region.LitOnLeft);
        Assert.Equal(0.5f, region.BoundaryX(0f), 4);
        Assert.True(region.IsLit(new Vector2(-0.8f, 0f)));
        Assert.False(region.IsLit(new Vector2(0.7f, 0f)));
        Assert.Equal(0.968f, region.BoundaryHalfHeight, 3);
        Assert.Equal(-0.25f, region.BoundaryX(region.BoundaryHalfHeight), 3);
    }

    [Theory]
    [InlineData(0.20f)]
    [InlineData(0.50f)]
    [InlineData(0.60f)]
    [InlineData(0.85f)]
    public void IsLit_agrees_with_the_drawn_layers_away_from_the_boundary(float width)
    {
        const float radius = 40f;
        var center = new Vector2(0f, 0f);
        var layers = MoonGeometry.Terminator(center, radius, width);
        var region = LitRegion.ForWidth(width);
        for (var y = -0.9f; y <= 0.9f; y += 0.1f)
        {
            for (var x = -0.9f; x <= 0.9f; x += 0.1f)
            {
                var p = new Vector2(x, y);
                if (p.LengthSquared() > 0.95f * 0.95f || MathF.Abs(region.Depth(p)) < 0.03f) continue;
                var inOverlay = Inside(layers.Overlay, p * radius);
                var litByLayers = layers.BaseLit ? !inOverlay : inOverlay;
                Assert.True(litByLayers == region.IsLit(p), $"w {width} at ({x:0.0}, {y:0.0})");
            }
        }
    }

    [Fact]
    public void Full_and_empty_regions_have_no_terminator()
    {
        Assert.Equal(LitShape.Full, LitRegion.ForWidth(1f).Shape);
        Assert.Equal(LitShape.None, LitRegion.ForWidth(0f).Shape);
        Assert.Equal(LitShape.None, LitRegion.ForWidth(float.NaN).Shape);
        Assert.False(LitRegion.Full.HasTerminator);
        Assert.Equal(0f, MoonDetail.BandWidthAt(LitRegion.Full, 0f));
        Assert.True(LitRegion.Full.IsLit(new Vector2(-0.9f, 0.1f)));
        Assert.False(LitRegion.None.IsLit(Vector2.Zero));
    }

    [Fact]
    public void Visibility_skips_dark_centres_and_halves_straddlers()
    {
        var ready = LitRegion.ForWidth(0.5f);
        Assert.Equal(1f, MoonDetail.Visibility(ready, new Vector2(0.30f, 0.06f), 0.24f));   // M2, wholly lit
        Assert.Equal(0f, MoonDetail.Visibility(ready, new Vector2(-0.30f, -0.24f), 0.32f)); // M1, dark side
        Assert.Equal(0.5f, MoonDetail.Visibility(ready, new Vector2(0.10f, 0.60f), 0.2f));  // C3 region, straddles
    }

    [Fact]
    public void Clamp_moves_dark_points_onto_the_boundary_inside_the_disc()
    {
        var accepted = LitRegion.ForWidth(0.60f);
        var dark = new Vector2(-0.7f, 0.2f);
        var clamped = accepted.Clamp(dark);
        Assert.Equal(accepted.BoundaryX(0.2f), clamped.X, 4);
        Assert.Equal(0.2f, clamped.Y, 4);
        Assert.Equal(new Vector2(0.5f, 0.1f), accepted.Clamp(new Vector2(0.5f, 0.1f)));

        var waning = LitRegion.WaningLens;
        var nearCusp = waning.Clamp(new Vector2(0.1f, 0.99f));
        Assert.True(nearCusp.Length() <= 1.0001f);
    }

    [Fact]
    public void Band_is_its_full_width_at_the_equator_and_vanishes_at_the_poles()
    {
        var ready = LitRegion.ForWidth(0.5f);
        Assert.Equal(MoonDetail.BandWidth, MoonDetail.BandWidthAt(ready, 0f), 4);
        Assert.Equal(0f, MoonDetail.BandWidthAt(ready, 1f), 4);
        Assert.Equal(0f, MoonDetail.BandWidthAt(ready, -1f), 4);

        // A thin crescent never gets a band wider than itself.
        var sliver = LitRegion.ForWidth(0.03f);
        Assert.True(MoonDetail.BandWidthAt(sliver, 0f) <= 0.06f + 1e-4f);
    }

    [Fact]
    public void Vignette_rises_quadratically_from_0_72_r_to_the_rim()
    {
        Assert.Equal(0f, MoonDetail.VignetteRamp(0.5f));
        Assert.Equal(0f, MoonDetail.VignetteRamp(0.72f));
        Assert.Equal(0.25f, MoonDetail.VignetteRamp(0.86f), 4);
        Assert.Equal(1f, MoonDetail.VignetteRamp(1f));
    }

    [Fact]
    public void Detail_map_fits_the_primitive_budget_and_the_disc()
    {
        // Maria one mesh each; craters a floor and a highlight wall each plus one shadow wall; the band; the vignette.
        var primitives = MoonDetail.Maria.Length
                         + MoonDetail.Craters.Length * 2
                         + MoonDetail.Craters.Count(c => c.HasShadowArc)
                         + 2;
        Assert.Equal(MoonDetail.PrimitiveBudget, primitives);

        foreach (var mare in MoonDetail.Maria)
            Assert.True(mare.Center.Length() + MathF.Max(mare.Radii.X, mare.Radii.Y) < 1f);
        foreach (var crater in MoonDetail.Craters)
            Assert.True(crater.Center.Length() + crater.Radius < 1f);
    }

    private static bool Inside(Vector2[] polygon, Vector2 p)
    {
        if (polygon.Length < 3) return false;
        var positive = false;
        var negative = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            var a = polygon[j];
            var b = polygon[i];
            var cross = (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
            if (cross > 0f) positive = true;
            else if (cross < 0f) negative = true;
        }

        return !(positive && negative);
    }
}
