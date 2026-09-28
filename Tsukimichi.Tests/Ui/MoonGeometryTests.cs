using System.Numerics;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

public class MoonGeometryTests
{
    private static readonly Vector2 Center = new(100f, 80f);
    private static readonly float[] Radii = [6f, 8f, 12f, 20f, 32f, 48f];

    public static TheoryData<MoonPhase> Phases => new()
    {
        MoonPhase.Full, MoonPhase.WaxingGibbous, MoonPhase.FirstQuarter, MoonPhase.WaningGibbous, MoonPhase.New,
    };

    public static TheoryData<float> Fractions
    {
        get
        {
            var data = new TheoryData<float>();
            for (var f = 0f; f <= 1.0001f; f += 0.05f) data.Add(MathF.Round(f, 2));
            return data;
        }
    }

    // ------------------------------------------------------------------ state phases

    [Theory]
    [MemberData(nameof(Phases))]
    public void Lit_polygon_is_convex_and_inside_the_disc_at_every_radius(MoonPhase phase)
    {
        foreach (var radius in Radii)
        {
            var poly = MoonGeometry.LitPolygon(Center, radius, phase);
            if (phase == MoonPhase.New)
            {
                Assert.Empty(poly);
                continue;
            }

            Assert.True(poly.Length >= 3, $"{phase} r={radius}: only {poly.Length} points");
            AssertConvexClockwise(poly, radius, $"{phase} r={radius}");
            AssertInsideDisc(poly, radius, $"{phase} r={radius}");
        }
    }

    [Fact]
    public void Full_moon_area_is_the_whole_disc()
    {
        Assert.InRange(Fraction(MoonPhase.Full, 20f), 0.95f, 1.05f);
    }

    [Fact]
    public void Quarter_moon_area_is_half_the_disc()
    {
        Assert.InRange(Fraction(MoonPhase.FirstQuarter, 20f), 0.45f, 0.55f);
    }

    [Fact]
    public void New_moon_has_no_lit_area()
    {
        Assert.Equal(0f, Fraction(MoonPhase.New, 20f));
    }

    [Fact]
    public void Gibbous_moons_match_the_approved_two_disc_construction()
    {
        // Spec: "disc lit ~75%": the lit width at the equator is 75% of the diameter. The lit region is the lens of two
        // equal discs offset by GibbousOffset·r (assets/icons/render_icons.py), whose area is 2·acos(u) − 2·u·sqrt(1 − u²)
        // with u = offset / 2, i.e. about 0.685 of the disc. Both the analytic value and the coarse ~three-quarters band hold.
        var u = MoonGeometry.GibbousOffset / 2f;
        var expected = (2f * MathF.Acos(u) - 2f * u * MathF.Sqrt(1f - u * u)) / MathF.PI;

        var waxing = Fraction(MoonPhase.WaxingGibbous, 24f);
        var waning = Fraction(MoonPhase.WaningGibbous, 24f);

        Assert.InRange(waxing, expected - 0.02f, expected + 0.02f);
        Assert.InRange(waxing, 0.65f, 0.75f);
        Assert.InRange(waning, waxing - 0.005f, waxing + 0.005f);
    }

    [Fact]
    public void Waxing_is_lit_on_the_right_and_waning_on_the_left()
    {
        var waxing = MoonGeometry.LitPolygon(Center, 20f, MoonPhase.WaxingGibbous);
        var waning = MoonGeometry.LitPolygon(Center, 20f, MoonPhase.WaningGibbous);
        var quarter = MoonGeometry.LitPolygon(Center, 20f, MoonPhase.FirstQuarter);

        Assert.True(Centroid(waxing).X > Center.X + 2f);
        Assert.True(Centroid(waning).X < Center.X - 2f);
        Assert.True(Centroid(quarter).X > Center.X + 2f);
        Assert.All(quarter, p => Assert.True(p.X >= Center.X - 1e-3f));
    }

    [Fact]
    public void Quest_states_map_to_the_spec_phases()
    {
        Assert.Equal(MoonPhase.Full, MoonGeometry.PhaseOf(QuestState.Completed));
        Assert.Equal(MoonPhase.WaxingGibbous, MoonGeometry.PhaseOf(QuestState.Accepted));
        Assert.Equal(MoonPhase.FirstQuarter, MoonGeometry.PhaseOf(QuestState.Ready));
        Assert.Equal(MoonPhase.FirstQuarter, MoonGeometry.PhaseOf(QuestState.ReadyOnOtherJob));
        Assert.Equal(MoonPhase.WaningGibbous, MoonGeometry.PhaseOf(QuestState.DoneThisCycle));
        Assert.Equal(MoonPhase.New, MoonGeometry.PhaseOf(QuestState.Blocked));
        Assert.Equal(MoonPhase.New, MoonGeometry.PhaseOf(QuestState.Foreclosed));
        Assert.Equal(MoonPhase.New, MoonGeometry.PhaseOf(QuestState.Unknown));
    }

    // ------------------------------------------------------------------ filling moon

    [Theory]
    [InlineData(0.00f, 0.00f)]
    [InlineData(0.25f, 0.25f)]
    [InlineData(0.50f, 0.50f)]
    [InlineData(0.75f, 0.75f)]
    [InlineData(1.00f, 1.00f)]
    public void Exact_terminator_lit_area_tracks_the_width(float litWidth, float expected)
    {
        var layers = MoonGeometry.Terminator(Center, 20f, litWidth);
        Assert.InRange(layers.LitAreaFraction, expected - 0.05f, expected + 0.05f);
    }

    [Theory]
    [InlineData(0.00f)]
    [InlineData(0.25f)]
    [InlineData(0.50f)]
    [InlineData(0.75f)]
    [InlineData(1.00f)]
    public void Filling_moon_lit_area_tracks_the_floored_fraction(float fraction)
    {
        // The floor moves 0.25 to 0.30 and 0.75 to 0.70 at r = 20 (floor 0.10); 0, 0.5 and 1 are fixed points.
        var expected = MoonGeometry.FlooredFraction(fraction, 20f);
        var layers = MoonGeometry.Filling(Center, 20f, fraction);
        Assert.InRange(layers.LitAreaFraction, expected - 0.05f, expected + 0.05f);
    }

    [Fact]
    public void Filling_moon_endpoints_need_no_overlay()
    {
        var empty = MoonGeometry.Filling(Center, 20f, 0f);
        Assert.False(empty.BaseLit);
        Assert.False(empty.HasOverlay);

        var full = MoonGeometry.Filling(Center, 20f, 1f);
        Assert.True(full.BaseLit);
        Assert.False(full.HasOverlay);

        var crescent = MoonGeometry.Filling(Center, 20f, 0.25f);
        Assert.True(crescent.BaseLit);      // lit disc with the shadow lens on top
        Assert.False(crescent.OverlayLit);

        var gibbous = MoonGeometry.Filling(Center, 20f, 0.75f);
        Assert.False(gibbous.BaseLit);      // dark disc with the lit lens on top
        Assert.True(gibbous.OverlayLit);
    }

    [Theory]
    [MemberData(nameof(Fractions))]
    public void Filling_moon_overlay_is_convex_and_inside_the_disc_at_every_radius(float fraction)
    {
        foreach (var radius in Radii)
        {
            var layers = MoonGeometry.Filling(Center, radius, fraction);
            if (!layers.HasOverlay) continue;
            AssertConvexClockwise(layers.Overlay, radius, $"f={fraction} r={radius}");
            AssertInsideDisc(layers.Overlay, radius, $"f={fraction} r={radius}");
        }
    }

    [Fact]
    public void Filling_moon_lit_area_is_monotonic_in_the_fraction()
    {
        foreach (var radius in Radii)
        {
            var previous = -1f;
            for (var f = 0f; f <= 1.0001f; f += 0.02f)
            {
                var current = MoonGeometry.Filling(Center, radius, f).LitAreaFraction;
                Assert.True(current >= previous - 1e-3f, $"r={radius}: lit fraction fell from {previous} to {current} at f={f}");
                previous = current;
            }
        }
    }

    [Fact]
    public void Filling_moon_fills_from_the_right()
    {
        var crescent = MoonGeometry.Filling(Center, 20f, 0.25f);
        Assert.True(Centroid(crescent.Overlay).X < Center.X);   // the shadow sits on the left

        var gibbous = MoonGeometry.Filling(Center, 20f, 0.75f);
        Assert.True(Centroid(gibbous.Overlay).X > Center.X);    // the lit lens sits on the right
    }

    [Fact]
    public void Filling_moon_clamps_out_of_range_fractions()
    {
        Assert.Equal(0f, MoonGeometry.Filling(Center, 20f, -0.5f).LitAreaFraction);
        Assert.Equal(1f, MoonGeometry.Filling(Center, 20f, 1.5f).LitAreaFraction);
        Assert.Equal(0f, MoonGeometry.Filling(Center, 20f, float.NaN).LitAreaFraction);
    }

    // ------------------------------------------------------------------ terminator floor

    [Fact]
    public void Terminator_floor_is_the_larger_of_the_fraction_and_the_pixel_floor()
    {
        Assert.Equal(0.10f, MoonGeometry.TerminatorFloor(20f), 4);          // 1.5 / 40 = 0.0375 < 0.10
        Assert.Equal(1.5f / 14f, MoonGeometry.TerminatorFloor(7f), 4);      // 0.107 > 0.10
        Assert.Equal(0.25f, MoonGeometry.TerminatorFloor(3f), 4);           // 1.5 / 6
        Assert.Equal(0.5f, MoonGeometry.TerminatorFloor(1f), 4);            // capped at the half
        Assert.Equal(0.5f, MoonGeometry.TerminatorFloor(0f), 4);
    }

    [Fact]
    public void Floored_fraction_keeps_the_endpoints_exact_and_lifts_everything_between()
    {
        Assert.Equal(0f, MoonGeometry.FlooredFraction(0f, 7f));
        Assert.Equal(1f, MoonGeometry.FlooredFraction(1f, 7f));
        Assert.Equal(0f, MoonGeometry.FlooredFraction(-0.2f, 7f));
        Assert.Equal(1f, MoonGeometry.FlooredFraction(1.2f, 7f));
        Assert.Equal(0f, MoonGeometry.FlooredFraction(float.NaN, 7f));
        Assert.Equal(0.5f, MoonGeometry.FlooredFraction(0.5f, 7f), 5);

        var floor = MoonGeometry.TerminatorFloor(7f);
        Assert.True(MoonGeometry.FlooredFraction(0.001f, 7f) >= floor);
        Assert.True(MoonGeometry.FlooredFraction(0.999f, 7f) <= 1f - floor);
    }

    [Fact]
    public void Three_percent_at_radius_seven_draws_a_visible_crescent()
    {
        // 17 / 612 on a 14 px tree moon: the plan's failing case. The lit sliver must be at least 1.5 px wide.
        var layers = MoonGeometry.Filling(Center, 7f, 17f / 612f);
        Assert.True(layers.BaseLit);
        Assert.True(layers.HasOverlay);
        Assert.True(layers.LitEquatorWidth >= MoonGeometry.MinLitWidthPx - 1e-3f, $"lit width {layers.LitEquatorWidth} px");
        Assert.True(layers.LitAreaFraction > 0f);
    }

    [Fact]
    public void Near_one_the_dark_side_keeps_the_same_floor()
    {
        var layers = MoonGeometry.Filling(Center, 7f, 0.97f);
        Assert.False(layers.BaseLit);
        Assert.True(layers.HasOverlay);
        Assert.True(layers.DarkEquatorWidth >= MoonGeometry.MinLitWidthPx - 1e-3f, $"dark width {layers.DarkEquatorWidth} px");
        Assert.True(layers.LitAreaFraction < 1f);
    }

    [Fact]
    public void Lit_width_is_floored_at_every_radius_and_exact_at_the_ends()
    {
        foreach (var radius in Radii)
        {
            var pixelFloor = MathF.Min(radius, MathF.Max(MoonGeometry.MinLitWidthPx, MoonGeometry.MinLitFraction * 2f * radius));
            foreach (var f in new[] { 0.001f, 0.03f, 0.10f, 0.90f, 0.97f, 0.999f })
            {
                var layers = MoonGeometry.Filling(Center, radius, f);
                Assert.True(layers.LitEquatorWidth >= pixelFloor - 1e-3f, $"r={radius} f={f}: lit {layers.LitEquatorWidth}");
                Assert.True(layers.DarkEquatorWidth >= pixelFloor - 1e-3f, $"r={radius} f={f}: dark {layers.DarkEquatorWidth}");
            }

            Assert.Equal(0f, MoonGeometry.Filling(Center, radius, 0f).LitEquatorWidth);
            Assert.Equal(2f * radius, MoonGeometry.Filling(Center, radius, 1f).LitEquatorWidth);
        }
    }

    [Fact]
    public void Lit_width_is_monotonic_in_the_fraction()
    {
        foreach (var radius in Radii)
        {
            var previous = -1f;
            for (var f = 0f; f <= 1.0001f; f += 0.01f)
            {
                var width = MoonGeometry.Filling(Center, radius, f).LitEquatorWidth;
                Assert.True(width >= previous - 1e-3f, $"r={radius}: lit width fell from {previous} to {width} at f={f}");
                previous = width;
            }
        }
    }

    [Fact]
    public void Equator_crossings_of_a_square_are_its_sides()
    {
        var square = new[] { new Vector2(0, 0), new Vector2(2, 0), new Vector2(2, 2), new Vector2(0, 2) };
        Assert.True(MoonGeometry.EquatorCrossings(square, 1f, out var min, out var max));
        Assert.Equal(0f, min, 4);
        Assert.Equal(2f, max, 4);
        Assert.False(MoonGeometry.EquatorCrossings(square, 3f, out _, out _));
    }

    // ------------------------------------------------------------------ building blocks

    [Fact]
    public void Segment_count_grows_with_radius_in_multiples_of_four()
    {
        var previous = 0;
        foreach (var radius in new[] { 1f, 6f, 10f, 20f, 40f, 48f, 200f, 5000f })
        {
            var n = MoonGeometry.SegmentsFor(radius);
            Assert.True(n >= MoonGeometry.MinSegments && n <= MoonGeometry.MaxSegments);
            Assert.Equal(0, n % 4);
            Assert.True(n >= previous);
            previous = n;
        }
    }

    [Fact]
    public void Lens_handles_disjoint_and_nested_discs()
    {
        var points = new List<Vector2>();

        MoonGeometry.LensPolygon(Center, 10f, Center + new Vector2(50f, 0f), 10f, 16, points);
        Assert.Empty(points);

        MoonGeometry.LensPolygon(Center, 10f, Center, 100f, 16, points);
        Assert.Equal(16, points.Count);
        Assert.InRange(MoonGeometry.PolygonArea(points.ToArray()) / (MathF.PI * 100f), 0.95f, 1f);

        MoonGeometry.LensPolygon(Center, 100f, Center + new Vector2(5f, 0f), 10f, 16, points);
        Assert.True(points.Count >= 12);
        Assert.InRange(MoonGeometry.PolygonArea(points.ToArray()) / (MathF.PI * 100f), 0.95f, 1f);
    }

    [Fact]
    public void Polygon_area_matches_the_shoelace_of_a_square()
    {
        var square = new[] { new Vector2(0, 0), new Vector2(2, 0), new Vector2(2, 2), new Vector2(0, 2) };
        Assert.Equal(4f, MoonGeometry.PolygonArea(square), 4);
    }

    // ------------------------------------------------------------------ helpers

    private static float Fraction(MoonPhase phase, float radius)
    {
        var poly = MoonGeometry.LitPolygon(Center, radius, phase);
        return MoonGeometry.PolygonArea(poly) / (MathF.PI * radius * radius);
    }

    private static Vector2 Centroid(Vector2[] poly)
    {
        var sum = Vector2.Zero;
        foreach (var p in poly) sum += p;
        return sum / poly.Length;
    }

    /// <summary>Every turn has the same sign (convex) and that sign is positive: increasing angle, clockwise on a y-down screen.</summary>
    private static void AssertConvexClockwise(Vector2[] poly, float radius, string label)
    {
        var tolerance = -1e-4f * radius * radius;
        var positiveTurns = 0;
        for (var i = 0; i < poly.Length; i++)
        {
            var p0 = poly[i];
            var p1 = poly[(i + 1) % poly.Length];
            var p2 = poly[(i + 2) % poly.Length];
            var e1 = p1 - p0;
            var e2 = p2 - p1;
            var cross = e1.X * e2.Y - e1.Y * e2.X;
            Assert.True(cross >= tolerance, $"{label}: concave turn {cross} at vertex {i + 1}");
            if (cross > -tolerance) positiveTurns++;
        }

        Assert.True(positiveTurns >= 3, $"{label}: degenerate polygon");
    }

    private static void AssertInsideDisc(Vector2[] poly, float radius, string label)
    {
        var limit = radius + 1e-3f * radius;
        foreach (var p in poly)
        {
            var distance = Vector2.Distance(p, Center);
            Assert.True(distance <= limit, $"{label}: point {p} is {distance - radius} outside the disc");
        }
    }
}
