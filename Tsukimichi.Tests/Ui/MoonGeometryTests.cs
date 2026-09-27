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
    public void Filling_moon_lit_area_tracks_the_fraction(float fraction, float expected)
    {
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
