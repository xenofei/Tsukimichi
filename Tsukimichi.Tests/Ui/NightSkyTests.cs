using System.Numerics;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>The Full sky's clocks, its hosts and its constellations (docs/design/v7/ui/spec.md §3 and Revision 3).</summary>
public class NightSkyTests
{
    [Fact]
    public void Each_expansion_has_its_own_constellation_of_5_to_8_stars_in_the_unit_box()
    {
        Assert.Equal(6, Constellations.All.Count);
        Assert.Equal(["The Chocobo", "The Wyrm", "The Lotus", "The Tower", "The Crescent", "The Plume"], Constellations.All.Select(c => c.Name));
        for (byte e = 0; e < 6; e++)
        {
            var c = Constellations.For(e);
            Assert.NotNull(c);
            Assert.InRange(c.Points.Length, 5, 8);
            Assert.All(c.Points, p =>
            {
                Assert.InRange(p.X, 0f, 1f);
                Assert.InRange(p.Y, 0f, 1f);
            });
            Assert.All(c.Edges, edge =>
            {
                Assert.InRange(edge.From, 0, c.Points.Length - 1);
                Assert.InRange(edge.To, 0, c.Points.Length - 1);
                Assert.NotEqual(edge.From, edge.To);
            });
        }

        Assert.Null(Constellations.For(6));
        Assert.Null(Constellations.For(byte.MaxValue));
    }

    [Fact]
    public void Every_constellation_is_one_figure()
    {
        foreach (var c in Constellations.All)
        {
            var reached = new HashSet<int> { 0 };
            var grew = true;
            while (grew)
            {
                grew = false;
                foreach (var (from, to) in c.Edges)
                {
                    if (reached.Contains(from) ^ reached.Contains(to))
                    {
                        reached.Add(from);
                        reached.Add(to);
                        grew = true;
                    }
                }
            }

            Assert.True(reached.Count == c.Points.Length, $"{c.Name} has a star no link reaches");
        }
    }

    [Fact]
    public void The_tower_is_a_tall_narrow_spire()
    {
        var tower = Constellations.Tower;
        var width = tower.Points.Max(p => p.X) - tower.Points.Min(p => p.X);
        var height = tower.Points.Max(p => p.Y) - tower.Points.Min(p => p.Y);

        Assert.True(height > 3f * width);
        Assert.Equal(tower.Points.Min(p => p.Y), tower.Points[Constellations.Lead].Y);
    }

    [Fact]
    public void The_sky_clock_runs_only_while_animating_and_focused()
    {
        var clock = new SkyClock();

        clock.Advance(0.05, animates: true, focused: false, drifts: true);
        clock.Advance(0.05, animates: false, focused: true, drifts: true);
        Assert.Equal(0, clock.Time);
        Assert.Equal(0, clock.DriftTime);

        clock.Advance(0.05, animates: true, focused: true, drifts: false);
        Assert.Equal(0.05, clock.Time, 6);
        Assert.Equal(0, clock.DriftTime);

        clock.Advance(0.05, animates: true, focused: true, drifts: true);
        Assert.Equal(0.10, clock.Time, 6);
        Assert.Equal(0.05, clock.DriftTime, 6);
    }

    [Fact]
    public void A_hitch_is_not_caught_up_on()
    {
        var clock = new SkyClock();

        clock.Advance(30, animates: true, focused: true, drifts: true);
        clock.Advance(double.NaN, animates: true, focused: true, drifts: true);
        clock.Advance(-1, animates: true, focused: true, drifts: true);

        Assert.Equal(SkyClock.MaxStepSeconds, clock.DriftTime, 6);
    }

    [Fact]
    public void The_sky_drifts_6_px_a_minute()
    {
        var clock = new SkyClock();
        for (var i = 0; i < 3600; i++)
        {
            clock.Advance(1 / 60.0, animates: true, focused: true, drifts: true);
        }

        Assert.Equal(6f, clock.Offset(MotionTokens.SkyDriftPxPerMinute), 2);
    }

    [Fact]
    public void An_ambient_meteor_falls_every_3_to_6_minutes_of_focused_time()
    {
        var clock = new SkyClock(seed: 5);
        var times = new List<double>();
        for (var frame = 0; frame < 60 * 60 * 40; frame++)
        {
            clock.Advance(1 / 60.0, animates: true, focused: true, drifts: true);
            if (clock.TakeAmbient(now: 1e6 + frame))
            {
                times.Add(clock.DriftTime);
            }
        }

        Assert.InRange(times.Count, 6, 14);
        Assert.InRange(times[0], 180, 360.1);
        for (var i = 1; i < times.Count; i++)
        {
            Assert.InRange(times[i] - times[i - 1], 180, 360.1);
        }
    }

    [Fact]
    public void Completion_meteors_come_at_most_once_in_30_seconds_and_keep_ambient_ones_away()
    {
        var clock = new SkyClock();

        Assert.True(clock.TakeCompletion(100));
        Assert.False(clock.TakeCompletion(110));
        Assert.False(clock.TakeCompletion(129.9));
        Assert.True(clock.TakeCompletion(130));

        while (clock.DriftTime < clock.NextAmbient)
        {
            clock.Advance(0.1, animates: true, focused: true, drifts: true);
        }

        var due = clock.NextAmbient;
        Assert.False(clock.TakeAmbient(now: 140));
        Assert.True(clock.NextAmbient > due);
    }

    [Fact]
    public void The_meteor_head_peaks_at_80_and_the_ambient_at_45()
    {
        Assert.InRange(MotionTokens.Meteor, 0.2f, 1f);
        Assert.Equal(0f, MotionTokens.MeteorAlpha(0f));
        Assert.Equal(MotionTokens.MeteorPeak, MotionTokens.MeteorAlpha(MotionTokens.MeteorFadeIn), 4);
        Assert.Equal(MotionTokens.AmbientMeteorPeak, MotionTokens.MeteorAlpha(MotionTokens.MeteorFadeIn, MotionTokens.AmbientMeteorPeak), 4);
        for (var p = 0f; p < 1f; p += 0.01f)
        {
            Assert.InRange(MotionTokens.MeteorAlpha(p), 0f, MotionTokens.MeteorPeak);
        }

        Assert.True(MotionTokens.MeteorAlpha(0.5f) < MotionTokens.MeteorAlpha(0.2f));
        Assert.Equal(0f, MotionTokens.MeteorAlpha(1f));
        Assert.Equal(0f, MotionTokens.MeteorAlpha(float.NaN));
        Assert.Equal(0.80f, MotionTokens.MeteorPeak);
        Assert.Equal(0.45f, MotionTokens.MeteorTailFrom);
    }

    [Fact]
    public void The_meteor_starts_20_percent_in_and_travels_64_by_34()
    {
        var travel = new Vector2(64, 34);

        Assert.Equal(new Vector2(20, 120), SkyRects.MeteorHead(new Vector2(0, 100), new Vector2(100, 200), travel, 0f));
        var end = SkyRects.MeteorHead(new Vector2(0, 100), new Vector2(100, 200), travel, 1f);
        Assert.Equal(84f, end.X, 3);
        Assert.Equal(154f, end.Y, 3);
        Assert.InRange(MathF.Atan2(34, 64) * 180f / MathF.PI, 27.5f, 28.5f);
    }

    [Fact]
    public void The_constellation_goes_to_the_tree_first_then_a_path_band_that_holds_the_box()
    {
        var box = Constellations.ClearBoxLogical;
        SkyRect[] rects =
        [
            new(SkySite.Rail, 0, new Vector2(0, 300), new Vector2(70, 800)),
            new(SkySite.Path, 0, new Vector2(400, 0), new Vector2(500, 60)),
            new(SkySite.Path, 1, new Vector2(400, 100), new Vector2(560, 240)),
            new(SkySite.Tree, 0, new Vector2(80, 500), new Vector2(370, 580)),
        ];

        // The tree's sky is too short (80 px), so the second Path band takes it.
        Assert.Equal(2, SkyRects.ConstellationHost(rects, box, 4f));

        rects[3] = rects[3] with { Max = new Vector2(370, 700) };
        Assert.Equal(3, SkyRects.ConstellationHost(rects, box, 4f));

        Assert.Equal(-1, SkyRects.ConstellationHost(rects.AsSpan(0, 2), box, 4f));
    }

    [Fact]
    public void The_milky_way_needs_one_sky_200_px_tall_and_the_meteor_takes_the_largest()
    {
        SkyRect[] rects =
        [
            new(SkySite.Rail, 0, new Vector2(0, 300), new Vector2(70, 480)),
            new(SkySite.Tree, 0, new Vector2(80, 500), new Vector2(370, 720)),
            new(SkySite.Title, 0, new Vector2(500, 10), new Vector2(900, 40)),
        ];

        Assert.Equal(1, SkyRects.BandHost(rects, 200f));
        Assert.Equal(-1, SkyRects.BandHost(rects.AsSpan(0, 1), 200f));
        Assert.Equal(1, SkyRects.Largest(rects));
        Assert.Equal(-1, SkyRects.Largest([]));
    }

    [Fact]
    public void Sky_rects_are_read_a_frame_later()
    {
        var sky = new SkyRects();
        sky.Add(SkySite.Tree, 0, new Vector2(0, 0), new Vector2(10, 10));
        sky.Add(SkySite.Rail, 0, new Vector2(5, 5), new Vector2(5, 9));

        Assert.Equal(0, sky.Previous.Length);
        sky.Swap();
        Assert.Equal(1, sky.Previous.Length);
        Assert.Equal(SkySite.Tree, sky.Previous[0].Site);
        sky.Swap();
        Assert.Equal(0, sky.Previous.Length);
    }

    [Fact]
    public void The_constellation_box_sits_at_the_skys_lower_right()
    {
        var corner = SkyRects.ConstellationBox(new Vector2(0, 0), new Vector2(300, 400), new Vector2(120, 100), 4f);

        Assert.Equal(new Vector2(176, 296), corner);
    }
}
