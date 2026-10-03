using System.Numerics;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>Halo gauge geometry (T10; docs/review/panel/qa-data-engineer.md §2.2, core radius rule from feature plan v3).</summary>
public class GaugeGeometryTests
{
    private const float TwoPi = 2f * MathF.PI;
    private static readonly Vector2 Center = new(50f, 40f);

    [Theory]
    [InlineData(6f)]
    [InlineData(7f)]
    [InlineData(8f)]
    [InlineData(12f)]
    [InlineData(18f)]
    [InlineData(23f)]
    public void Arc_angles_are_strictly_increasing_with_floor(float radius)
    {
        float[] fractions = [0f, 0.03f, 0.1f, 0.5f, 0.97f, 1f];
        var previous = -1f;
        foreach (var f in fractions)
        {
            var sweep = GaugeGeometry.Sweep(f, radius);
            Assert.True(sweep > previous, $"R {radius}: sweep({f}) = {sweep} not above {previous}");
            previous = sweep;
        }

        Assert.Equal(0f, GaugeGeometry.Sweep(0f, radius));
        Assert.Equal(TwoPi, GaugeGeometry.Sweep(1f, radius));
    }

    [Fact]
    public void Floor_epsilon_matches_spec()
    {
        Assert.Equal(0.0853f, GaugeGeometry.FloorEpsilon(7f, 1.5f), 4);
        Assert.Equal(0.06f, GaugeGeometry.FloorEpsilon(23f, 2.5f), 4);

        // Proposal §3.2 table with the gauge's own stroke: 0.060 at 64 px, 0.061 at 24 px, 0.087 at 16 px.
        Assert.Equal(0.060f, GaugeGeometry.FloorEpsilon(32f), 3);
        Assert.Equal(0.061f, GaugeGeometry.FloorEpsilon(12f), 3);
        Assert.Equal(0.087f, GaugeGeometry.FloorEpsilon(8f), 3);
    }

    [Theory]
    [InlineData(8f)]
    [InlineData(12f)]
    [InlineData(32f)]
    public void Fraction_below_floor_renders_floor_not_zero(float radius)
    {
        var eps = GaugeGeometry.FloorEpsilon(radius);
        var sweep = GaugeGeometry.Sweep(0.01f, radius);
        Assert.InRange(sweep, eps * TwoPi, (eps + 0.01f) * TwoPi);
        Assert.Equal(0f, GaugeGeometry.Sweep(0f, radius));

        // The pip reaches past the round cap by the allowance.
        Assert.True(GaugeGeometry.ArcLength(1f / 612f, radius) >= GaugeGeometry.Stroke(radius) + GaugeGeometry.CapAllowancePx - 1e-3f);
    }

    [Fact]
    public void Arc_starts_at_twelve_oclock_clockwise()
    {
        Assert.Equal(-MathF.PI / 2f, GaugeGeometry.StartAngle);
        Assert.Equal(0f, GaugeGeometry.AngleAt(0.25f), 5);                    // a quarter turn ends at 3 o'clock
        Assert.Equal(MathF.PI / 2f, GaugeGeometry.EndAngle(0.5f, 12f), 5);    // the floor is symmetric: one half is 6 o'clock

        var (start, _) = GaugeGeometry.Caps(Center, 12f, 0.25f);
        Assert.Equal(Center.X, start.X, 4);
        Assert.True(start.Y < Center.Y);                                       // y down: 12 o'clock is above
        var quarter = GaugeGeometry.TrackPoint(Center, 12f, GaugeGeometry.AngleAt(0.25f));
        Assert.True(quarter.X > Center.X + 9f);                                // clockwise: a quarter turn lands on the right
    }

    [Theory]
    [InlineData(8f, 0.03f)]
    [InlineData(12f, 0.5f)]
    [InlineData(18f, 0.97f)]
    [InlineData(32f, 1f)]
    public void Cap_positions_lie_on_track_radius(float radius, float fraction)
    {
        var (start, end) = GaugeGeometry.Caps(Center, radius, fraction);
        Assert.Equal(0.80f * radius, Vector2.Distance(start, Center), 4);
        Assert.Equal(0.80f * radius, Vector2.Distance(end, Center), 4);
    }

    [Fact]
    public void Core_radius_is_zero_below_R12_and_0_61_R_from_R12()
    {
        Assert.Equal(0f, GaugeGeometry.CoreRadius(11f));
        Assert.Equal(0f, GaugeGeometry.CoreRadius(8f));
        Assert.Equal(0.61f * 12f, GaugeGeometry.CoreRadius(12f), 1);
        Assert.Equal(0.61f * 32f, GaugeGeometry.CoreRadius(32f), 1);
        foreach (var radius in new[] { 12f, 16f, 24f, 32f })
        {
            var core = GaugeGeometry.CoreRadius(radius);
            Assert.True(core > 0f && core < GaugeGeometry.TrackRadius(radius) - GaugeGeometry.Stroke(radius) * 0.5f);
        }
    }

    [Fact]
    public void Stroke_is_at_least_two_px_and_0_18_R()
    {
        Assert.Equal(2f, GaugeGeometry.Stroke(8f));
        Assert.Equal(2.16f, GaugeGeometry.Stroke(12f), 4);
        Assert.Equal(5.76f, GaugeGeometry.Stroke(32f), 4);
    }

    [Fact]
    public void Out_of_range_and_nan_fractions_clamp()
    {
        Assert.Equal(0f, GaugeGeometry.Sweep(-0.1f, 12f));
        Assert.Equal(TwoPi, GaugeGeometry.Sweep(1.2f, 12f));
        Assert.Equal(0f, GaugeGeometry.Sweep(float.NaN, 12f));
        Assert.Equal(0f, GaugeGeometry.CoreLitWidth(float.NaN, 7.3f));
    }

    [Fact]
    public void Near_complete_is_distinguishable_from_complete()
    {
        const float radius = 8f;
        var gap = (GaugeGeometry.Sweep(1f, radius) - GaugeGeometry.Sweep(0.97f, radius)) * GaugeGeometry.TrackRadius(radius);
        Assert.True(gap - GaugeGeometry.Stroke(radius) >= 1f, $"gap between the caps {gap - GaugeGeometry.Stroke(radius)} px");
        Assert.Equal(TwoPi, GaugeGeometry.Sweep(1f, radius));                  // 1 closes the ring
    }

    [Fact]
    public void Monotone_over_fine_grid()
    {
        var previous = -1f;
        for (var i = 0; i <= 1000; i++)
        {
            var sweep = GaugeGeometry.Sweep(i / 1000f, 12f);
            Assert.True(sweep >= previous);
            previous = sweep;
        }
    }

    [Theory]
    [InlineData(4f, HaloMode.NumberOnly)]
    [InlineData(7.9f, HaloMode.NumberOnly)]
    [InlineData(8f, HaloMode.Ring)]
    [InlineData(11.9f, HaloMode.Ring)]
    [InlineData(12f, HaloMode.Core)]
    [InlineData(32f, HaloMode.Core)]
    [InlineData(float.NaN, HaloMode.NumberOnly)]
    public void Mode_follows_the_size_rules(float radius, HaloMode expected)
    {
        Assert.Equal(expected, GaugeGeometry.ModeFor(radius));
    }

    [Fact]
    public void Core_moon_has_its_own_floor()
    {
        var core = GaugeGeometry.CoreRadius(12f);                               // 7.3 px
        Assert.Equal(1.5f / (2f * core), GaugeGeometry.CoreFloor(core), 4);   // just above 0.10 at 7.3 px
        Assert.Equal(0.10f, GaugeGeometry.CoreFloor(GaugeGeometry.CoreRadius(32f)), 4);
        Assert.Equal(0.5f, GaugeGeometry.CoreFloor(1f), 4);
        Assert.True(GaugeGeometry.CoreLitWidth(1f / 612f, core) >= 0.10f);
        Assert.True(GaugeGeometry.CoreLitWidth(611f / 612f, core) <= 0.90f + 1e-4f);
        Assert.Equal(0f, GaugeGeometry.CoreLitWidth(0f, core));
        Assert.Equal(1f, GaugeGeometry.CoreLitWidth(1f, core));
    }

    [Theory]
    [InlineData(0.1f)]
    [InlineData(0.5f)]
    [InlineData(0.9f)]
    public void A_filling_moon_is_lit_toward_its_lit_limb(float width)
    {
        // It waxes from the right: the shading peaks right of centre (nudged up for the key light), never on the dark side.
        var highlight = GaugeGeometry.MoonHighlight(width);
        Assert.Equal(new Vector2(0.45f, -0.25f), highlight);
        Assert.True(highlight.X > 0f && highlight.Y < 0f);
    }

    [Fact]
    public void Only_a_full_moon_takes_the_upper_left_key_light()
    {
        Assert.Equal(new Vector2(-0.32f, -0.34f), GaugeGeometry.MoonHighlight(1f));
        Assert.Equal(GaugeGeometry.MoonHighlight(1f), GaugeGeometry.MoonHighlight(1.5f));
    }
}
