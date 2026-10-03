using System.Numerics;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// Decoration Plain's flat glyphs (<see cref="MedalTokens.Plain"/>, docs/design/flair-v13/spec.md §1.1): their own
/// brightness ladder on the standard palette, measured as the round-5 metric does, at a true 12 px in greyscale on
/// Plain's pane (salience = the mean of |L − background| over the 12 × 12 box): Ready reads at least 1.3× every other
/// state, Completed at most 0.8× Ready. The crescents are the shipped one, lit on the right; Done's half moon is lit on
/// the left. Quiet's light rim keeps the medal's face and swaps the gilt for a silver hairline.
/// </summary>
public class PlainMedalTests
{
    private const int Px = 12;
    private const int Samples = 4;

    private static readonly Vector3 Ground = Rgb(MedalTokens.PlainGround);

    [Fact]
    public void Ready_reads_first_and_completed_recedes_at_12_px()
    {
        var salience = Enum.GetValues<QuestState>().ToDictionary(static s => s, static s => Salience(s));
        var ready = salience[QuestState.Ready];

        // The design measured Ready at 87.6 and Completed at 45.6 on this pane: the metric is the same one.
        Assert.InRange(ready, 65f, 115f);
        Assert.InRange(salience[QuestState.Completed], 25f, 65f);
        foreach (var (state, value) in salience)
        {
            if (state != QuestState.Ready)
            {
                Assert.True(ready >= 1.3f * value, $"Ready {ready:0.0} is not 1.3× {state} {value:0.0}");
            }
        }

        Assert.True(salience[QuestState.Completed] <= 0.8f * ready, $"Completed {salience[QuestState.Completed]:0.0} is over 0.8× Ready {ready:0.0}");
    }

    [Fact]
    public void Not_checked_is_the_quietest_and_in_journal_outranks_completed()
    {
        var salience = Enum.GetValues<QuestState>().ToDictionary(static s => s, static s => Salience(s));
        Assert.Equal(QuestState.Unknown, salience.MinBy(static p => p.Value).Key);
        Assert.True(salience[QuestState.Accepted] > salience[QuestState.Completed]);
    }

    [Fact]
    public void The_ladder_is_its_own_not_the_high_contrast_one()
    {
        Assert.False(MedalTokens.Plain.Flat);
        Assert.True(MedalTokens.Plain.IsPlain);
        Assert.Same(MedalTokens.Plain, MedalTokens.For(GlyphPalette.Standard, MedalFinish.Plain));
        Assert.Same(MedalTokens.LightRim, MedalTokens.For(GlyphPalette.Standard, MedalFinish.LightRim));
        Assert.Same(MedalTokens.Standard, MedalTokens.For(GlyphPalette.Standard, MedalFinish.Gilt));

        // The high-contrast palette keeps its own ladder at every level.
        Assert.Same(MedalTokens.HighContrastDark, MedalTokens.For(GlyphPalette.HighContrastDark, MedalFinish.Plain));
        Assert.Same(MedalTokens.HighContrastLight, MedalTokens.For(GlyphPalette.HighContrastLight, MedalFinish.LightRim));
    }

    [Theory]
    [InlineData(QuestState.Ready)]
    [InlineData(QuestState.ReadyOnOtherJob)]
    [InlineData(QuestState.Accepted)]
    public void The_crescents_are_lit_on_the_right_with_the_limb_toward_the_lower_right(QuestState state)
    {
        // The shipped crescent (MedalArt.PhaseOutline of the scene moon): its lit area sits right of the moon's centre
        // and below it, the limb facing about 28 degrees under the horizontal.
        var radius = state == QuestState.Ready ? MedalArt.PlainReadyMoonRadius : MedalArt.PlainMoonRadius;
        var outline = MedalArt.PhaseOutline(MedalArt.PlainMoon, radius, MedalArt.SceneMoonTerminator, MedalArt.SceneMoonTilt);
        var centroid = Centroid(outline);
        Assert.True(centroid.X > MedalArt.PlainMoon.X + (radius * 0.3f), $"{state}: lit centroid {centroid} is not right of the moon");
        Assert.True(centroid.Y > MedalArt.PlainMoon.Y, $"{state}: lit centroid {centroid} is not toward the lower right");
        var angle = MathF.Atan2(centroid.Y - MedalArt.PlainMoon.Y, centroid.X - MedalArt.PlainMoon.X) * 180f / MathF.PI;
        Assert.InRange(angle, 18f, 38f);

        // And the glyph draws that emblem: an emblem-coloured vertex sits right of the moon's centre.
        var emblem = MeshBuilder.Pack(MedalTokens.PlainEmblem(state) with { W = 1f });
        var lit = MedalArt.Medal(state, MedalTokens.Plain, Px).Parts.SelectMany(static p => p.Positions.Zip(p.Colors)).Where(v => (v.Second | 0xFF000000u) == emblem && v.Second >> 24 > 0).ToArray();
        Assert.NotEmpty(lit);
        Assert.True(lit.Average(static v => v.First.X) > MedalArt.PlainMoon.X);
    }

    [Fact]
    public void Done_is_a_waning_half_lit_on_the_left_with_its_arc_on_the_dark_right()
    {
        var half = MedalArt.PhaseOutline(MedalArt.Center, MedalArt.PlainDoneMoonRadius, 0f, 0f, litLeft: true);
        Assert.True(Centroid(half).X < MedalArt.Center.X - 5f);
        Assert.True(MedalArt.PlainDoneArcCenter.X > MedalArt.Center.X);

        var gilt = MeshBuilder.Pack(MedalTokens.PlainGilt with { W = 1f });
        var arc = MedalArt.Medal(QuestState.DoneThisCycle, MedalTokens.Plain, Px).Parts.SelectMany(static p => p.Positions.Zip(p.Colors)).Where(v => (v.Second | 0xFF000000u) == gilt && v.Second >> 24 > 0).ToArray();
        Assert.NotEmpty(arc);

        // On the dark right: only the arc's round caps reach back to the centre line, by less than a pixel.
        Assert.All(arc, v => Assert.True(v.First.X > MedalArt.Center.X - MedalArt.PlainPixel, $"gilt at {v.First}"));
        Assert.True(arc.Average(static v => v.First.X) > MedalArt.Center.X + 20f);
    }

    [Fact]
    public void Locked_out_is_a_red_disc_with_two_cracks_and_not_checked_the_moon_question_mark()
    {
        Assert.Equal(GlyphTokens.Medallion.DalamudShade, MedalTokens.PlainDisc(QuestState.Foreclosed));
        Assert.Equal(GlyphTokens.Mist, MedalTokens.PlainEmblem(QuestState.Unknown));
        Assert.Equal(MedalArt.PlainCrackA[0], MedalArt.PlainCrackB[0]);

        // The cracks meet off-centre at the upper left and stay inside the disc.
        Assert.True(MedalArt.PlainCrackA[0].X < MedalArt.Center.X && MedalArt.PlainCrackA[0].Y < MedalArt.Center.Y);
        Assert.All(MedalArt.PlainCrackA.Concat(MedalArt.PlainCrackB), p => Assert.True(Vector2.Distance(p, MedalArt.Center) < MedalArt.PlainDiscRadius));
    }

    [Fact]
    public void Every_plain_glyph_builds_flat_inside_its_box()
    {
        foreach (var state in Enum.GetValues<QuestState>())
        {
            foreach (var size in new[] { 0f, 12f, 16f })
            {
                var mesh = MedalArt.Medal(state, MedalTokens.Plain, size);
                Assert.NotEmpty(mesh.Parts);
                foreach (var part in mesh.Parts)
                {
                    Assert.Equal(0, part.Indices.Length % 3);
                    Assert.All(part.Indices, i => Assert.True(i < part.Positions.Length));
                    Assert.All(part.Positions, p => Assert.True(Vector2.Distance(p, MedalArt.Center) <= MedalArt.KeylineRadius + 1f, $"{state}: {p}"));
                }

                Assert.True(mesh.Count(12f).Vertices <= 1500, $"{state}: {mesh.Count(12f).Vertices} vertices");
            }
        }
    }

    [Fact]
    public void Quiets_light_rim_keeps_the_face_and_drops_the_gilt()
    {
        var gilt = new[]
        {
            GlyphTokens.Medallion.GiltHigh, GlyphTokens.Medallion.GiltMid, GlyphTokens.Medallion.GiltShade, GlyphTokens.Medallion.GiltDeep,
        }.Select(static c => MeshBuilder.Pack(c with { W = 1f })).ToHashSet();
        var rim = MeshBuilder.Pack(MedalTokens.LightRimInk with { W = 1f });
        foreach (var state in new[] { QuestState.Ready, QuestState.Blocked, QuestState.Unknown })
        {
            var mesh = MedalArt.Medal(state, MedalTokens.LightRim, 16f);
            var colors = mesh.Parts.SelectMany(static p => p.Colors).Where(static c => c >> 24 != 0).Select(static c => c | 0xFF000000u).ToHashSet();
            Assert.Contains(rim, colors);
            Assert.DoesNotContain(colors, gilt.Contains);

            // Nothing past the hairline: the medal reads as laid on the pane.
            var reach = mesh.Parts.SelectMany(static p => p.Positions).Max(static p => Vector2.Distance(p, MedalArt.Center));
            Assert.True(reach <= MedalTokens.LightRimRadius + 6f, $"{state}: {reach}");
        }
    }

    // ------------------------------------------------------------------ the round-5 metric

    /// <summary>The mean |L − background| (0–255 greyscale) of <paramref name="state"/>'s Plain glyph at 12 px on Plain's pane.</summary>
    private static float Salience(QuestState state)
    {
        var mesh = MedalArt.Medal(state, MedalTokens.Plain, Px);
        var k = Px / 128f;
        var background = Luma(Ground);
        var total = 0f;
        for (var y = 0; y < Px; y++)
        {
            for (var x = 0; x < Px; x++)
            {
                var pixel = Vector3.Zero;
                for (var sy = 0; sy < Samples; sy++)
                {
                    for (var sx = 0; sx < Samples; sx++)
                    {
                        var p = new Vector2(x + ((sx + 0.5f) / Samples), y + ((sy + 0.5f) / Samples));
                        pixel += Shade(mesh, k, p);
                    }
                }

                total += MathF.Abs(Luma(pixel / (Samples * Samples)) - background);
            }
        }

        return total / (Px * Px);
    }

    /// <summary>The colour at <paramref name="p"/> (device px) with every triangle composited over the ground in draw order.</summary>
    private static Vector3 Shade(MedalMesh mesh, float k, Vector2 p)
    {
        var color = Ground;
        foreach (var part in mesh.Parts)
        {
            if (Px < part.MinSizePx)
            {
                continue;
            }

            for (var t = 0; t + 2 < part.Indices.Length; t += 3)
            {
                var (a, b, c) = (part.Indices[t], part.Indices[t + 1], part.Indices[t + 2]);
                var p0 = (part.Positions[a] * k) + part.Offsets[a];
                var p1 = (part.Positions[b] * k) + part.Offsets[b];
                var p2 = (part.Positions[c] * k) + part.Offsets[c];
                var area = ((p1.X - p0.X) * (p2.Y - p0.Y)) - ((p2.X - p0.X) * (p1.Y - p0.Y));
                if (MathF.Abs(area) < 1e-9f)
                {
                    continue;
                }

                var w0 = (((p1.X - p.X) * (p2.Y - p.Y)) - ((p2.X - p.X) * (p1.Y - p.Y))) / area;
                var w1 = (((p2.X - p.X) * (p0.Y - p.Y)) - ((p0.X - p.X) * (p2.Y - p.Y))) / area;
                var w2 = 1f - w0 - w1;
                if (w0 < -1e-6f || w1 < -1e-6f || w2 < -1e-6f)
                {
                    continue;
                }

                var v = (w0 * Unpack(part.Colors[a])) + (w1 * Unpack(part.Colors[b])) + (w2 * Unpack(part.Colors[c]));
                var alpha = Math.Clamp(v.W, 0f, 1f);
                color = Vector3.Lerp(color, new Vector3(v.X, v.Y, v.Z), alpha);
            }
        }

        return color;
    }

    private static Vector4 Unpack(uint c) => new((c & 0xFF) / 255f, ((c >> 8) & 0xFF) / 255f, ((c >> 16) & 0xFF) / 255f, (c >> 24) / 255f);

    private static Vector3 Rgb(Vector4 c) => new(c.X, c.Y, c.Z);

    private static float Luma(Vector3 c) => 255f * ((0.299f * c.X) + (0.587f * c.Y) + (0.114f * c.Z));

    private static Vector2 Centroid(IReadOnlyList<Vector2> loop)
    {
        // The polygon's area centroid.
        var area = 0f;
        var cx = 0f;
        var cy = 0f;
        for (var i = 0; i < loop.Count; i++)
        {
            var a = loop[i];
            var b = loop[(i + 1) % loop.Count];
            var cross = (a.X * b.Y) - (b.X * a.Y);
            area += cross;
            cx += (a.X + b.X) * cross;
            cy += (a.Y + b.Y) * cross;
        }

        area *= 0.5f;
        return new Vector2(cx / (6f * area), cy / (6f * area));
    }
}
