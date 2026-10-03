using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The Completed moon's vector face (plan v7 V1; docs/design/v7/ui/spec.md section 4 and R3.4): the seas, lobes and
/// hearts are the atlas's crater-less source (completed-v7-small.svg), the seas are one union at one opacity (no darker
/// spots where two overlap), no crater is drawn, and Completed still recedes behind Ready at row size.
/// </summary>
public sealed class CompletedMoonTests(ITestOutputHelper output)
{
    private static string SmallSource() =>
        File.ReadAllText(Path.Combine(OrnamentLayoutTests.RepoRoot(), "docs", "design", "v7", "ui", "completed-moon", "completed-v7-small.svg"));

    private static readonly Regex EllipseTag = new(
        "<ellipse cx=\"([0-9.]+)\" cy=\"([0-9.]+)\" rx=\"([0-9.]+)\" ry=\"([0-9.]+)\"(?: transform=\"rotate\\((-?[0-9.]+)[^\"]*\")? fill-opacity=\"([0-9.]+)\"/>",
        RegexOptions.CultureInvariant);

    private static List<(Vector2 C, Vector2 R, float Degrees, float Opacity)> Ellipses(string svg, string groupStart)
    {
        var start = svg.IndexOf(groupStart, StringComparison.Ordinal);
        Assert.True(start >= 0, $"the source has {groupStart}");
        var end = svg.IndexOf("</g>", start, StringComparison.Ordinal);
        static float F(Group g) => g.Success ? float.Parse(g.Value, CultureInfo.InvariantCulture) : 0f;
        return [.. EllipseTag.Matches(svg[start..end]).Select(m => (
            new Vector2(F(m.Groups[1]), F(m.Groups[2])), new Vector2(F(m.Groups[3]), F(m.Groups[4])), F(m.Groups[5]), F(m.Groups[6])))];
    }

    [Fact]
    public void The_seas_and_hearts_are_the_atlas_small_source()
    {
        var svg = SmallSource();
        Assert.Contains("<g fill=\"#56658C\" opacity=\".46\">", svg, StringComparison.Ordinal);
        Assert.Contains("fill=\"#3F4B70\"", svg, StringComparison.Ordinal);
        Assert.Equal(0x56658Cu, GlyphTokens.MedallionDetail.MariaBasaltHex);
        Assert.Equal(0x3F4B70u, GlyphTokens.MedallionDetail.MareHeartHex);
        Assert.Equal(0.46f, MedalArt.FullSeaOpacity);

        var seas = Ellipses(svg, "<g fill=\"#56658C\" opacity=\".46\">");
        Assert.Equal(17, seas.Count);
        Assert.Equal(seas.Count, MedalArt.FullSeaShapes.Count);
        for (var i = 0; i < seas.Count; i++)
        {
            var (c, r, degrees) = MedalArt.FullSeaShapes[i];
            Assert.True(Vector2.Distance(c, seas[i].C) < 0.01f && Vector2.Distance(r, seas[i].R) < 0.01f && MathF.Abs(degrees - seas[i].Degrees) < 0.01f, $"sea {i}: {c} {r} {degrees}° against {seas[i]}");
            Assert.Equal(1f, seas[i].Opacity); // opaque inside the group: the union
        }

        var hearts = Ellipses(svg, "fill=\"#3F4B70\">");
        Assert.Equal(3, hearts.Count);
        Assert.Equal(hearts.Count, MedalArt.FullSeaHeartShapes.Count);
        for (var i = 0; i < hearts.Count; i++)
        {
            var (c, r, degrees, opacity) = MedalArt.FullSeaHeartShapes[i];
            Assert.True(Vector2.Distance(c, hearts[i].C) < 0.01f && Vector2.Distance(r, hearts[i].R) < 0.01f && MathF.Abs(degrees - hearts[i].Degrees) < 0.01f, $"heart {i}");
            Assert.Equal(hearts[i].Opacity, opacity, 3);
        }
    }

    [Fact]
    public void The_seas_are_one_union_and_never_darken_where_they_overlap()
    {
        // Sampled over the whole face every half unit: the seas alone never pass one sea's opacity (stacked ellipses
        // would reach 1 - (1 - .46)^2 = .71 where two overlap), and they do reach it inside.
        var deepest = 0f;
        var strongest = 0f;
        for (var y = 25f; y <= 95f; y += 0.5f)
        {
            for (var x = 25f; x <= 95f; x += 0.5f)
            {
                var p = new Vector2(x, y);
                if (Vector2.Distance(p, MedalArt.CompletedMoon) > MedalArt.CompletedMoonRadius)
                {
                    continue;
                }

                var cover = MedalArt.FullSeaCover(p);
                Assert.InRange(cover, 0f, 1f);
                deepest = MathF.Max(deepest, cover);
                strongest = MathF.Max(strongest, MedalArt.FullFaceDetail(p).W);
            }
        }

        Assert.True(deepest > 0.97f, $"the seas reach {deepest:0.00} of their opacity");
        Assert.True(strongest < 0.62f, $"the face detail reaches alpha {strongest:0.00} (seas .46 under hearts of at most .22)");

        // Every sea's centre is covered once: Imbrium's centre and the overlap with its north-west lobe draw the same.
        foreach (var (c, _, _) in MedalArt.FullSeaShapes)
        {
            Assert.True(MedalArt.FullSeaCover(c) > 0.9f, $"sea at {c}");
        }
    }

    [Theory]
    [InlineData(77.0f, 40.0f)]
    [InlineData(42.0f, 80.5f)]
    [InlineData(58.0f, 32.6f)]
    public void No_crater_is_drawn_where_the_atlas_has_one(float x, float y)
    {
        // The three rim-lit craters of completed-v7.svg sit in the highlands, off every sea: the vector face (which
        // draws at 48 px and under, as the crater-less source does) shows bare highland there, never a dark mark.
        var p = new Vector2(x, y);
        Assert.True(MedalArt.FullSeaCover(p) < 0.05f, $"sea cover {MedalArt.FullSeaCover(p):0.000} at {p}");
        var detail = MedalArt.FullFaceDetail(p);
        Assert.True(detail.W < 0.06f || MedalRaster.Luma(MedalRaster.Rgb(detail)) > MedalRaster.Luma(MedalRaster.Rgb(GlyphTokens.MedallionDetail.FullMoonMid)), $"a dark mark at {p}: {detail}");
    }

    [Fact]
    public void Completed_recedes_behind_ready_at_row_size()
    {
        // The round-5 ladder (metrics.py): Completed about 0.72x Ready, never over 0.8x, on the table's Night ground.
        var night = ColorMath.FromHex(GlyphTokens.NightHex);
        foreach (var px in new[] { 16, 20, 24, 28, 31 })
        {
            var ready = MedalRaster.Salience(QuestState.Ready, MedalTokens.Standard, px, night);
            var completed = MedalRaster.Salience(QuestState.Completed, MedalTokens.Standard, px, night);
            output.WriteLine($"{px} px: Ready {ready:0.0}, Completed {completed:0.0}, ratio {completed / ready:0.000}");
            Assert.True(completed <= 0.8f * ready, $"{px} px: Completed {completed:0.0} is over 0.8x Ready {ready:0.0}");
        }
    }
}
