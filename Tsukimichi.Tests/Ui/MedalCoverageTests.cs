using System.Numerics;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// Shapes thinner than a pixel in a mesh built for one size (the round 5 implementation review, required fix 1): they
/// collapse to their centreline with their alpha scaled by their thickness and feather a pixel either side, so a fine
/// line carries its own coverage instead of drawing a pixel wide at full strength. Meshes for any size keep ImGui's
/// half-pixel scheme. Built at <see cref="MeshBuilder.BoxUnits"/> px, so one unit is one pixel.
/// </summary>
public sealed class MedalCoverageTests
{
    private static readonly Vector4 White = Vector4.One;

    private static MeshBuilder OnePixelPerUnit() => new(1f, MeshBuilder.BoxUnits);

    private static List<Vector2> Line(float x0, float x1, float y, int n = 4) =>
        [.. Enumerable.Range(0, n + 1).Select(i => new Vector2(x0 + (x1 - x0) * i / n, y))];

    private static float Alpha(uint color) => (color >> 24) / 255f;

    /// <summary>Where each vertex lands when the mesh is drawn at its own size: position × size / 128 + its AA offset.</summary>
    private static Vector2[] Drawn(MeshPart part, float k = 1f) =>
        [.. part.Positions.Select((p, i) => p * k + part.Offsets[i])];

    [Fact]
    public void A_line_a_third_of_a_pixel_wide_draws_on_its_centreline_at_that_alpha()
    {
        var part = OnePixelPerUnit().Strip(Line(10f, 30f, 20f), Line(10f, 30f, 20.3f), MeshBuilder.Solid(White)).Build().Parts.Single();
        var drawn = Drawn(part);
        var opaque = Enumerable.Range(0, drawn.Length).Where(i => part.Colors[i] >> 24 != 0).ToArray();
        Assert.NotEmpty(opaque);
        foreach (var i in opaque)
        {
            Assert.Equal(20.15f, drawn[i].Y, 3);
            Assert.Equal(0.3f, Alpha(part.Colors[i]), 2);
        }

        // Each transparent fringe vertex is a pixel from the centreline vertex it feathers.
        foreach (var i in Enumerable.Range(0, drawn.Length).Except(opaque))
        {
            var nearest = opaque.Min(j => Vector2.Distance(drawn[i], drawn[j]));
            Assert.Equal(1f, nearest, 3);
        }
    }

    [Theory]
    [InlineData(0.30f, 0.00f)]
    [InlineData(0.30f, 0.25f)]
    [InlineData(0.30f, 0.50f)]
    [InlineData(0.16f, 0.37f)]
    [InlineData(0.70f, 0.10f)]
    public void A_fine_line_adds_its_thickness_in_coverage_wherever_it_falls(float thickness, float shift)
    {
        // A horizontal hairline sampled at pixel centres down one column, as the GPU does: the tent a pixel either side
        // of its centreline sums to its thickness at any sub-pixel position.
        var y = 20f + shift;
        var part = OnePixelPerUnit().Strip(Line(0f, 40f, y, 8), Line(0f, 40f, y + thickness, 8), MeshBuilder.Solid(White)).Build().Parts.Single();
        var coverage = Enumerable.Range(15, 12).Sum(row => Coverage(part, new Vector2(21.37f, row + 0.5f)));
        Assert.Equal(thickness, coverage, 2);
    }

    [Fact]
    public void A_thin_polygon_and_a_thin_ring_fade_by_their_thickness_too()
    {
        var polygon = OnePixelPerUnit().Polygon([new(10f, 10f), new(40f, 10f), new(40f, 10.4f), new(10f, 10.4f)], MeshBuilder.Solid(White)).Build().Parts.Single();
        Assert.All(polygon.Colors.Where(static c => c >> 24 != 0), c => Assert.Equal(0.4f, Alpha(c), 2));

        var ring = OnePixelPerUnit().Annulus(new Vector2(64f), 30f, 30.5f, MeshBuilder.Solid(White)).Build().Parts.Single();
        Assert.All(ring.Colors.Where(static c => c >> 24 != 0), c => Assert.Equal(0.5f, Alpha(c), 2));
    }

    [Fact]
    public void A_tapered_glint_fades_to_nothing_at_its_points()
    {
        // TaperArc's shape: 0 → 0.8 px → 0, so the ends carry no alpha and the middle 0.8.
        const int n = 8;
        var top = new List<Vector2>();
        var bottom = new List<Vector2>();
        for (var i = 0; i <= n; i++)
        {
            var half = 0.4f * MathF.Sin(MathF.PI * i / n);
            top.Add(new Vector2(10f + i, 20f - half));
            bottom.Add(new Vector2(10f + i, 20f + half));
        }

        var part = OnePixelPerUnit().Strip(top, bottom, MeshBuilder.Solid(White)).Build().Parts.Single();
        Assert.Equal(0f, Alpha(part.Colors[0]));
        Assert.Equal(0.8f, Alpha(part.Colors[2 * (n / 2)]), 2);
    }

    [Fact]
    public void Shapes_a_pixel_wide_or_more_keep_the_half_pixel_scheme()
    {
        var part = OnePixelPerUnit().Strip(Line(10f, 30f, 20f), Line(10f, 30f, 23f), MeshBuilder.Solid(White)).Build().Parts.Single();
        var opaque = Enumerable.Range(0, part.Colors.Length).Where(i => part.Colors[i] >> 24 != 0).ToArray();
        Assert.All(opaque, i => Assert.Equal(255u, part.Colors[i] >> 24));
        Assert.Contains(opaque, i => MathF.Abs(part.Offsets[i].Y) is > 0.49f and < 0.51f && part.Offsets[i].X == 0f);
    }

    [Fact]
    public void A_mesh_for_any_size_draws_a_fine_line_at_full_strength()
    {
        // The hero mesh (no size) cannot know a line is thin; it keeps the old scheme, and the atlas covers hero sizes.
        var part = new MeshBuilder().Strip(Line(10f, 30f, 20f), Line(10f, 30f, 20.3f), MeshBuilder.Solid(White)).Build().Parts.Single();
        Assert.All(part.Colors.Where(static c => c >> 24 != 0), static c => Assert.Equal(255u, c >> 24));
    }

    [Fact]
    public void Thickness_is_the_inward_ray_to_the_far_side()
    {
        Vector2[] box = [new(0f, 0f), new(10f, 0f), new(10f, 2f), new(0f, 2f)];
        Assert.Equal(2f, MeshBuilder.Thickness(box, 0, new Vector2(0f, 1f)), 4);
        Assert.Equal(10f, MeshBuilder.Thickness(box, 0, new Vector2(1f, 0f)), 4);
        Assert.True(float.IsPositiveInfinity(MeshBuilder.Thickness(box, 0, new Vector2(-1f, 0f))));
    }

    // ------------------------------------------------------------------ the medals

    [Theory]
    [MemberData(nameof(MedalTests.AllStates), MemberType = typeof(MedalTests))]
    public void Row_medals_are_built_per_whole_pixel_size(int stateValue)
    {
        var state = (QuestState)stateValue;
        var tokens = MedalTokens.Standard;
        Assert.Same(MedalArt.Medal(state, tokens, 20f), MedalArt.Medal(state, tokens, 20.4f));
        Assert.NotSame(MedalArt.Medal(state, tokens, 20f), MedalArt.Medal(state, tokens, 21f));
        Assert.NotSame(MedalArt.Medal(state, tokens, 20f), MedalArt.Medal(state, tokens));
        Assert.Same(MedalArt.Medal(state, tokens), MedalArt.Medal(state, tokens, MedalLayout.RowTierMaxPx));
        Assert.Same(MedalArt.Medal(state, tokens, MedalArt.RowMinPx), MedalArt.Medal(state, tokens, 3f));

        foreach (var size in new[] { 16f, 20f, 28f, 31f })
        {
            var mesh = MedalArt.Medal(state, tokens, size);
            Assert.True(mesh.Count(size).Vertices <= 4000, $"{state}: {mesh.Count(size).Vertices} vertices at {size} px");
            foreach (var part in mesh.Parts)
            {
                Assert.All(part.Indices, i => Assert.True(i < part.Positions.Length));
                Assert.All(part.Offsets, o => Assert.True(float.IsFinite(o.X) && float.IsFinite(o.Y) && o.Length() <= 2f, $"{state} at {size} px: AA offset {o}"));
            }
        }
    }

    [Fact]
    public void Ready_horizon_at_row_size_is_a_faint_line_not_a_bar()
    {
        // The horizon strip is 1 unit tall: at 20 px a sixth of a pixel, so no vertex on it draws brighter than its
        // peak alpha (0.7) times that.
        var mesh = MedalArt.Medal(QuestState.Ready, MedalTokens.Standard, 20f);
        const float k = 20f / 128f;
        var moonstone = MeshBuilder.Pack(GlyphTokens.Medallion.MoonstoneHigh) & 0x00FFFFFFu;
        var horizon = mesh.Parts.SelectMany(static p => p.Positions.Select((q, i) => (Position: q, Color: p.Colors[i])))
            .Where(v => (v.Color & 0x00FFFFFFu) == moonstone && MathF.Abs(v.Position.Y - MedalArt.Horizon) <= 0.5f && v.Position.X is > 20f and < 108f)
            .ToArray();
        Assert.NotEmpty(horizon);
        Assert.All(horizon, v => Assert.True(Alpha(v.Color) <= 0.7f * k + 0.01f, $"horizon vertex at {v.Position} alpha {Alpha(v.Color)}"));
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>The alpha a mesh lays down at <paramref name="p"/>, its triangles interpolated and composited "over".</summary>
    private static float Coverage(MeshPart part, Vector2 p)
    {
        var drawn = Drawn(part);
        var covered = 0f;
        for (var t = 0; t + 2 < part.Indices.Length; t += 3)
        {
            var (a, b, c) = (part.Indices[t], part.Indices[t + 1], part.Indices[t + 2]);
            var (p0, p1, p2) = (drawn[a], drawn[b], drawn[c]);
            var area = (p1.X - p0.X) * (p2.Y - p0.Y) - (p2.X - p0.X) * (p1.Y - p0.Y);
            if (MathF.Abs(area) < 1e-9f)
            {
                continue;
            }

            var w0 = ((p1.X - p.X) * (p2.Y - p.Y) - (p2.X - p.X) * (p1.Y - p.Y)) / area;
            var w1 = ((p2.X - p.X) * (p0.Y - p.Y) - (p0.X - p.X) * (p2.Y - p.Y)) / area;
            var w2 = 1f - w0 - w1;
            if (w0 < -1e-6f || w1 < -1e-6f || w2 < -1e-6f)
            {
                continue;
            }

            var alpha = w0 * Alpha(part.Colors[a]) + w1 * Alpha(part.Colors[b]) + w2 * Alpha(part.Colors[c]);
            covered += alpha * (1f - covered);
        }

        return covered;
    }
}
