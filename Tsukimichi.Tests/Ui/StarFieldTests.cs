using System.Numerics;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

public class StarFieldTests
{
    [Fact]
    public void The_seed_is_expansion_times_7919_plus_the_row_count()
    {
        Assert.Equal(7919 * 3 + 12, StarField.Seed(3, 12));
        Assert.Equal(5, StarField.Seed(0, 5));
    }

    [Fact]
    public void The_same_seed_gives_the_same_sky()
    {
        var first = StarField.Generate(StarField.Seed(2, 9), 30);
        var second = StarField.Generate(StarField.Seed(2, 9), 30);
        var other = StarField.Generate(StarField.Seed(2, 10), 30);

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
    }

    [Fact]
    public void A_longer_field_keeps_every_star_of_a_shorter_one()
    {
        var shorter = StarField.Generate(31, 14);
        var longer = StarField.Generate(31, 240);

        Assert.Equal(shorter, longer.Take(14));
    }

    [Fact]
    public void Stars_cover_the_width_and_stay_inside_vertically()
    {
        foreach (var star in StarField.Generate(12345, 2000))
        {
            Assert.True(star.U is >= 0f and < 1f);
            Assert.InRange(star.V, 0.03f, 0.97f);
        }
    }

    [Fact]
    public void Layers_are_60_32_8_with_their_alphas()
    {
        var stars = StarField.Generate(777, 20_000);

        Assert.InRange(stars.Count(s => s.Layer == StarLayer.Far) / 20_000f, 0.57f, 0.63f);
        Assert.InRange(stars.Count(s => s.Layer == StarLayer.Mid) / 20_000f, 0.29f, 0.35f);
        Assert.InRange(stars.Count(s => s.Layer == StarLayer.Near) / 20_000f, 0.06f, 0.10f);
        Assert.All(stars.Where(s => s.Layer == StarLayer.Far), s => Assert.InRange(s.Alpha, 0.10f, 0.16f));
        Assert.All(stars.Where(s => s.Layer == StarLayer.Mid), s => Assert.InRange(s.Alpha, 0.20f, 0.28f));
        Assert.All(stars.Where(s => s.Layer == StarLayer.Near), s => Assert.InRange(s.Alpha, 0.52f, 0.60f));
    }

    [Fact]
    public void Temperatures_are_mostly_cool_and_far_stars_are_never_gold_or_ember()
    {
        var stars = StarField.Generate(4242, 20_000);
        float Share(StarTemperature t) => stars.Count(s => s.Temperature == t) / 20_000f;

        // Among the stars that may take any colour, the spec's 64 / 24 / 9 / 3; far stars split cool and moon 64 : 24.
        Assert.InRange(Share(StarTemperature.Cool), 0.66f, 0.72f);
        Assert.All(stars.Where(s => s.Layer == StarLayer.Far), s => Assert.True(s.Temperature is StarTemperature.Cool or StarTemperature.Moon));

        var bright = stars.Where(s => s.Layer != StarLayer.Far).ToList();
        Assert.InRange(bright.Count(s => s.Temperature == StarTemperature.Cool) / (float)bright.Count, 0.60f, 0.68f);
        Assert.InRange(bright.Count(s => s.Temperature == StarTemperature.Moon) / (float)bright.Count, 0.20f, 0.28f);
        Assert.InRange(bright.Count(s => s.Temperature == StarTemperature.Gold) / (float)bright.Count, 0.06f, 0.12f);
        Assert.InRange(bright.Count(s => s.Temperature == StarTemperature.Ember) / (float)bright.Count, 0.015f, 0.05f);
    }

    [Fact]
    public void Every_near_star_and_one_mid_star_in_three_twinkle_slowly()
    {
        var stars = StarField.Generate(99, 20_000);

        Assert.All(stars.Where(s => s.Layer == StarLayer.Far), s => Assert.False(s.Twinkles));
        Assert.All(stars.Where(s => s.Layer == StarLayer.Near), s => Assert.True(s.Twinkles));
        var mid = stars.Where(s => s.Layer == StarLayer.Mid).ToList();
        Assert.InRange(mid.Count(s => s.Twinkles) / (float)mid.Count, 0.30f, 0.37f);
        Assert.All(stars.Where(s => s.Twinkles), s =>
        {
            Assert.InRange(s.Period, 7f, 13f);
            Assert.True(s.Phase is >= 0f and < 1f);
        });
    }

    [Fact]
    public void The_twinkle_peaks_at_30_percent_and_dips_at_70()
    {
        Assert.Equal(0f, StarField.TwinkleCurve(0f), 4);
        Assert.Equal(1f, StarField.TwinkleCurve(0.3f), 4);
        Assert.Equal(0f, StarField.TwinkleCurve(0.5f), 4);
        Assert.Equal(-1f, StarField.TwinkleCurve(0.7f), 4);
        Assert.Equal(0f, StarField.TwinkleCurve(0.99999f), 3);
        for (var f = 0f; f < 1f; f += 0.01f)
        {
            Assert.InRange(StarField.TwinkleCurve(f), -1f, 1f);
        }
    }

    [Fact]
    public void A_near_star_breathes_by_28_percent_and_holds_still_without_twinkle()
    {
        var near = new Star(0.5f, 0.5f, StarLayer.Near, StarTemperature.Cool, 0.56f, 10f, 0f);
        var mid = near with { Layer = StarLayer.Mid, Alpha = 0.24f };
        var far = near with { Layer = StarLayer.Far, Alpha = 0.12f, Period = 0f };

        Assert.Equal(0.56f * 1.28f, StarField.Alpha(near, 3.0, twinkle: true), 4);
        Assert.Equal(0.56f * 0.72f, StarField.Alpha(near, 7.0, twinkle: true), 4);
        Assert.Equal(0.24f * 1.18f, StarField.Alpha(mid, 3.0, twinkle: true), 4);
        Assert.Equal(0.56f, StarField.Alpha(near, 3.0, twinkle: false));
        Assert.Equal(0.12f, StarField.Alpha(far, 3.0, twinkle: true));
    }

    [Theory]
    [InlineData(5f, 100f, 5f)]
    [InlineData(-5f, 100f, 95f)]
    [InlineData(250f, 100f, 50f)]
    [InlineData(100f, 100f, 0f)]
    [InlineData(3f, 0f, 0f)]
    public void Positions_wrap_on_the_tile(float x, float period, float expected)
    {
        Assert.Equal(expected, StarField.Wrap(x, period), 3);
    }

    [Fact]
    public void The_drift_moves_every_star_left_by_the_same_amount()
    {
        var star = new Star(0.25f, 0.5f, StarLayer.Mid, StarTemperature.Cool, 0.2f, 0f, 0f);

        Assert.Equal(50f, StarField.Position(star, 200f, 0f));
        Assert.Equal(44f, StarField.Position(star, 200f, 6f));
        Assert.Equal(190f, StarField.Position(star, 200f, 60f));
    }

    [Fact]
    public void Stars_fade_over_10_px_at_the_left_and_right_edges()
    {
        Assert.Equal(0f, StarField.EdgeFade(-1f, 0f, 100f, 10f));
        Assert.Equal(0f, StarField.EdgeFade(0f, 0f, 100f, 10f));
        Assert.Equal(0.5f, StarField.EdgeFade(5f, 0f, 100f, 10f));
        Assert.Equal(1f, StarField.EdgeFade(50f, 0f, 100f, 10f));
        Assert.Equal(0.5f, StarField.EdgeFade(95f, 0f, 100f, 10f));
        Assert.Equal(1f, StarField.EdgeFade(1f, 0f, 100f, 0f));
    }

    [Fact]
    public void Placed_stars_stay_4_px_inside_the_sky_and_never_outside_it()
    {
        var stars = StarField.Generate(31, 240);
        var into = new PlacedStar[512];
        var view = new SkyView(new Vector2(0, 0), new Vector2(300, 900), new Vector2(8, 500), new Vector2(292, 892), 4f, 37.3f, 10f, 12.5, true, true);

        var n = StarField.Place(stars, view, into);

        Assert.InRange(n, 10, 240);
        for (var i = 0; i < n; i++)
        {
            Assert.InRange(into[i].Position.X, 12f, 288f);
            Assert.InRange(into[i].Position.Y, 504f, 888f);
            Assert.InRange(into[i].Alpha, 0.0001f, 0.60f * 1.28f);
        }
    }

    [Fact]
    public void A_sky_that_grows_uncovers_stars_where_they_were()
    {
        var stars = StarField.Generate(31, 240);
        var small = new PlacedStar[512];
        var large = new PlacedStar[512];
        var canvasMin = new Vector2(0, 0);
        var canvasMax = new Vector2(300, 900);

        var a = StarField.Place(stars, new SkyView(canvasMin, canvasMax, new Vector2(0, 600), canvasMax, 4f, 0f, 0f, 0, false, false), small);
        var b = StarField.Place(stars, new SkyView(canvasMin, canvasMax, new Vector2(0, 300), canvasMax, 4f, 0f, 0f, 0, false, false), large);

        Assert.True(b > a);
        var seen = large.Take(b).Select(s => s.Position).ToHashSet();
        Assert.All(small.Take(a), s => Assert.Contains(s.Position, seen));
    }

    [Fact]
    public void Still_stars_sit_on_whole_pixels()
    {
        var into = new PlacedStar[64];
        var view = new SkyView(new Vector2(0.3f, 0.7f), new Vector2(200.3f, 100.7f), new Vector2(0.3f, 0.7f), new Vector2(200.3f, 100.7f), 4f, 0f, 0f, 0, false, false);

        var n = StarField.Place(StarField.Generate(5, 40), view, into);

        Assert.True(n > 0);
        Assert.All(into.Take(n), s => Assert.Equal(s.Position, new Vector2(MathF.Round(s.Position.X), MathF.Round(s.Position.Y))));
    }

    [Fact]
    public void Placing_a_frame_allocates_nothing()
    {
        var field = new SkyField(31, 240);
        var into = new PlacedStar[512];
        var clock = new SkyClock();
        var view = new SkyView(new Vector2(0, 0), new Vector2(300, 900), new Vector2(8, 300), new Vector2(292, 892), 4f, 0f, 10f, 0, true, true);
        // Warm up exactly what is measured: a first call's one-time setup (static state, tiered JIT) would otherwise land
        // inside the measured frames whenever no earlier test happened to run it.
        for (var i = 0; i < 100; i++)
        {
            clock.Advance(1 / 60.0, animates: true, focused: true, drifts: true);
            clock.TakeAmbient(i);
            StarField.Place(field.For(300, 900), view with { Offset = clock.Offset(6f), Time = clock.Time }, into);
            MotionTokens.MeteorAlpha(i / 100f);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 200; i++)
        {
            clock.Advance(1 / 60.0, animates: true, focused: true, drifts: true);
            clock.TakeAmbient(i);
            StarField.Place(field.For(300, 900), view with { Offset = clock.Offset(6f), Time = clock.Time }, into);
            MotionTokens.MeteorAlpha(i / 200f);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated == 0, $"{allocated} bytes allocated over 200 sky frames");
    }

    [Fact]
    public void A_cached_field_shows_more_of_the_same_stars_as_it_grows()
    {
        var field = new SkyField(7, 64);

        var small = field.For(70, 200).ToArray();
        var large = field.For(70, 900).ToArray();

        Assert.Equal(10, small.Length);
        Assert.Equal(45, large.Length);
        Assert.Equal(small, large.Take(small.Length));
        Assert.Equal(64, field.For(1000, 1000).Length);
    }

    [Fact]
    public void Milky_way_stars_are_far_and_hug_its_axis()
    {
        var stars = StarField.Band(11, 2000);

        Assert.All(stars, s =>
        {
            Assert.Equal(StarLayer.Far, s.Layer);
            Assert.True(s.U is >= 0f and < 1f);
            Assert.InRange(s.V, -1f, 1f);
        });
        Assert.True(stars.Count(s => MathF.Abs(s.V) < 0.5f) > stars.Length * 0.6f);
    }

    [Theory]
    [InlineData(324f, 92f, 21)]
    [InlineData(324f, 20f, 6)]
    [InlineData(324f, 1000f, 48)]
    [InlineData(0f, 100f, 6)]
    [InlineData(float.NaN, 100f, 6)]
    public void Density_is_one_star_per_1400_px_squared_between_6_and_48(float width, float height, int expected)
    {
        Assert.Equal(expected, StarField.CountFor(width, height));
    }

    [Theory]
    [InlineData(48, 1f, 48)]
    [InlineData(48, 0.5f, 24)]
    [InlineData(13, 0.5f, 7)]
    [InlineData(6, 0.5f, 4)]
    [InlineData(3, 0.5f, 3)]
    [InlineData(10, 0f, 4)]
    [InlineData(10, 2f, 10)]
    [InlineData(10, float.NaN, 10)]
    [InlineData(0, 0.5f, 0)]
    [InlineData(-3, 0.5f, 0)]
    public void A_palette_thins_its_sky_to_a_share_of_the_stars_never_under_four(int count, float density, int expected)
    {
        // spec-1.17 decision 6: Kugane Lacquer's sky is half as dense (the mock keeps at least 4 a sky).
        Assert.Equal(expected, StarField.Thinned(count, density));
    }

    [Fact]
    public void A_thinned_sky_is_the_same_sky_with_fewer_stars()
    {
        // The share shown is a prefix of the field, so no star moves or changes when the palette thins the sky.
        var field = new SkyField(0x4B55, StarField.MaxStars);
        var full = field.For(324f, 600f).ToArray();
        var thinned = full.AsSpan(0, StarField.Thinned(full.Length, 0.5f)).ToArray();
        Assert.Equal(full.Length / 2, thinned.Length);
        Assert.Equal(full.Take(thinned.Length), thinned);
    }

    [Fact]
    public void A_path_gets_one_band_per_expansion_header_seeded_by_its_row_count()
    {
        var rows = new[]
        {
            new PathRow(PathRowKind.Band) { Expansion = 0 },
            new PathRow(PathRowKind.FoldedRun) { Expansion = 0, Count = 5 },
            new PathRow(PathRowKind.Step) { Expansion = 0, RowId = 1 },
            new PathRow(PathRowKind.Band) { Expansion = 2 },
            new PathRow(PathRowKind.Step) { Expansion = 2, RowId = 2, IsTarget = true },
        };

        var bands = StarField.ForPath(rows);

        Assert.Equal(2, bands.Count);
        Assert.Equal((0, 0, 3), (bands[0].Expansion, bands[0].FirstRow, bands[0].RowCount));
        Assert.Equal((2, 3, 2), (bands[1].Expansion, bands[1].FirstRow, bands[1].RowCount));
        Assert.Equal(StarField.Seed(0, 3), bands[0].Seed);
        Assert.Equal(StarField.Seed(2, 2), bands[1].Seed);

        // 20 + 24 + 24 = 68 logical px at 324 wide: 16 stars; the second band, 20 + 32 = 52 px: 12.
        Assert.Equal(16, bands[0].Stars.Count);
        Assert.Equal(12, bands[1].Stars.Count);
        Assert.Equal(StarField.Generate(bands[0].Seed, 16), bands[0].Stars);
    }

    [Fact]
    public void A_path_of_one_still_gets_a_sky_and_no_rows_get_none()
    {
        var single = StarField.ForPath([new PathRow(PathRowKind.Step) { Expansion = 4, RowId = 9, IsTarget = true, State = QuestState.Ready }]);

        var band = Assert.Single(single);
        Assert.Equal(4, band.Expansion);
        // One 32 px target row at 324 wide: 7 stars.
        Assert.Equal(7, band.Stars.Count);
        Assert.Empty(StarField.ForPath([]));
    }
}
