using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Ui.Themes;

namespace Tsukimichi.Tests.Ui;

/// <summary>The moon icon's theme particles (spec-1.22 H2): their limits, their timing, and none where they must not draw.</summary>
public class IconParticlesTests
{
    private const double Step = 0.05;
    private const double Span = 60.0;

    public static TheoryData<ThemeId> Themes => [.. Enum.GetValues<ThemeId>()];

    /// <summary>Every frame of a minute, at 20 frames a second.</summary>
    private static IEnumerable<(double T, IconParticle[] Particles)> Frames(ThemeId theme)
    {
        var buffer = new IconParticle[IconParticles.Max];
        for (var t = 0.0; t <= Span; t += Step)
        {
            var n = IconParticles.At(theme, Flair.Full, false, t, buffer);
            yield return (t, buffer[..n]);
        }
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void At_most_three_at_once_never_above_55_alpha_never_past_14_radii(ThemeId theme)
    {
        foreach (var (t, particles) in Frames(theme))
        {
            Assert.InRange(particles.Length, 0, IconParticles.Max);
            foreach (var p in particles)
            {
                Assert.InRange(p.Alpha, 0f, IconParticles.MaxAlpha);
                var reach = MathF.Sqrt((p.X * p.X) + (p.Y * p.Y));
                Assert.True(reach <= IconParticles.MaxReach, $"{theme} at {t:0.00} s: {p.Kind} at {reach:0.000} radii");
                Assert.True(float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Size), $"{theme} at {t:0.00} s");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void Each_theme_draws_something_in_its_minute(ThemeId theme)
    {
        Assert.Contains(Frames(theme), frame => frame.Particles.Any(static p => p.Alpha > 0.1f));
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void Reduce_motion_Quiet_and_Plain_draw_none(ThemeId theme)
    {
        var buffer = new IconParticle[IconParticles.Max];
        for (var t = 0.0; t < 20.0; t += 0.37)
        {
            Assert.Equal(0, IconParticles.At(theme, Flair.Full, reduceMotion: true, t, buffer));
            Assert.Equal(0, IconParticles.At(theme, Flair.Quiet, reduceMotion: false, t, buffer));
            Assert.Equal(0, IconParticles.At(theme, Flair.Plain, reduceMotion: false, t, buffer));
        }

        Assert.False(IconParticles.Enabled(Flair.Full, true));
        Assert.False(IconParticles.Enabled(Flair.Quiet, false));
        Assert.False(IconParticles.Enabled(Flair.Plain, false));
        Assert.True(IconParticles.Enabled(Flair.Full, false));
    }

    [Fact]
    public void A_short_buffer_or_a_bad_clock_writes_nothing_past_it()
    {
        var one = new IconParticle[1];
        Assert.Equal(1, IconParticles.At(ThemeId.Medallion, Flair.Full, false, 1.0, one));
        Assert.Equal(0, IconParticles.At(ThemeId.Medallion, Flair.Full, false, double.NaN, one));
        Assert.Equal(0, IconParticles.At(ThemeId.Medallion, Flair.Full, false, 1.0, Span<IconParticle>.Empty));
    }

    [Fact]
    public void Aether_shards_ring_the_foot_and_never_cross_the_face()
    {
        foreach (var (t, particles) in Frames(ThemeId.AetherCrystal))
        {
            Assert.InRange(particles.Length, 0, 2);
            foreach (var p in particles)
            {
                Assert.Equal(IconParticleKind.Shard, p.Kind);

                // The shard's whole body stays outside the face (the well), and so off the crescent.
                var reach = MathF.Sqrt((p.X * p.X) + (p.Y * p.Y));
                Assert.True(reach - p.Size >= IconParticles.FaceRadius, $"shard over the face at {t:0.00} s ({reach:0.000} radii)");

                // Never above the icon's middle: the orbit rings its foot.
                Assert.True(p.Y > 0f, $"shard above the middle at {t:0.00} s");
            }
        }
    }

    [Fact]
    public void Aether_shards_flash_briefly_as_their_face_meets_the_light()
    {
        var flashing = Frames(ThemeId.AetherCrystal).Count(frame => frame.Particles.Any(static p => p.Flash > 0.5f));
        var frames = Frames(ThemeId.AetherCrystal).Count();
        Assert.InRange(flashing, 1, frames / 3);
    }

    [Fact]
    public void Medallion_motes_live_36_s_staggered_12_s_and_rise()
    {
        var buffer = new IconParticle[IconParticles.Max];
        Assert.Equal(3, IconParticles.At(ThemeId.Medallion, Flair.Full, false, 0.3, buffer));
        Assert.All(buffer, static p => Assert.Equal(IconParticleKind.Mote, p.Kind));

        // The first mote: born at 0, fully in by 0.6 s, gone by 3.6 s, rising (Y falls) as it lives.
        var early = Mote(0.7);
        var late = Mote(2.0);
        Assert.True(late.Y < early.Y);
        Assert.Equal(IconParticles.MaxAlpha, early.Alpha, 3);
        Assert.True(Mote(3.59).Alpha < 0.02f);
        Assert.True(Mote(0.05).Alpha < 0.1f);

        static IconParticle Mote(double t)
        {
            var into = new IconParticle[IconParticles.Max];
            IconParticles.At(ThemeId.Medallion, Flair.Full, false, t, into);
            return into[0];
        }
    }

    [Fact]
    public void Classic_stars_stay_put_and_breathe_between_27_and_49()
    {
        var first = new IconParticle[IconParticles.Max];
        Assert.Equal(3, IconParticles.At(ThemeId.Classic, Flair.Full, false, 0.0, first));
        foreach (var (_, particles) in Frames(ThemeId.Classic))
        {
            Assert.Equal(3, particles.Length);
            for (var i = 0; i < 3; i++)
            {
                Assert.Equal(first[i].X, particles[i].X);
                Assert.Equal(first[i].Y, particles[i].Y);
                Assert.InRange(particles[i].Alpha, 0.27f, 0.49f);
            }
        }
    }

    [Fact]
    public void Ishgard_glint_runs_the_upper_left_quarter_every_6_s_for_12_s()
    {
        var buffer = new IconParticle[IconParticles.Max];
        Assert.Equal(1, IconParticles.At(ThemeId.IshgardGlass, Flair.Full, false, 0.6, buffer));
        Assert.Equal(IconParticleKind.Glint, buffer[0].Kind);
        Assert.True(buffer[0].X < 0f && buffer[0].Y < 0f, "the glint's head is on the lit upper-left rim");
        Assert.Equal(0, IconParticles.At(ThemeId.IshgardGlass, Flair.Full, false, 3.0, buffer));
        Assert.Equal(1, IconParticles.At(ThemeId.IshgardGlass, Flair.Full, false, 6.6, buffer));
        foreach (var (_, particles) in Frames(ThemeId.IshgardGlass))
        {
            foreach (var p in particles)
            {
                // Between 195° and 255°, the upper-left quarter (screen axes, +Y down).
                var degrees = p.Angle * 180f / MathF.PI;
                Assert.InRange(degrees, 194.9f, 255.1f);
            }
        }
    }

    [Fact]
    public void Orrery_bead_passes_behind_on_its_far_half_on_a_12_s_orbit()
    {
        var behind = Frames(ThemeId.Orrery).Count(frame => frame.Particles is [{ Behind: true }]);
        var front = Frames(ThemeId.Orrery).Count(frame => frame.Particles is [{ Behind: false }]);
        Assert.InRange(behind, front * 0.8, front * 1.25);
        Assert.True(IconParticles.HasOrbit(ThemeId.Orrery));
        Assert.False(IconParticles.HasOrbit(ThemeId.Medallion));

        var a = new IconParticle[1];
        var b = new IconParticle[1];
        IconParticles.At(ThemeId.Orrery, Flair.Full, false, 1.0, a);
        IconParticles.At(ThemeId.Orrery, Flair.Full, false, 13.0, b);
        Assert.Equal(a[0].X, b[0].X, 3);
        Assert.Equal(a[0].Y, b[0].Y, 3);
    }

    [Fact]
    public void Sumi_flecks_drift_down_on_the_right_side()
    {
        foreach (var (_, particles) in Frames(ThemeId.Sumi))
        {
            Assert.InRange(particles.Length, 0, 2);
            Assert.All(particles, static p => Assert.True(p.X > 0f, "a fleck on the left side"));
        }

        var early = new IconParticle[2];
        var later = new IconParticle[2];
        IconParticles.At(ThemeId.Sumi, Flair.Full, false, 0.5, early);
        IconParticles.At(ThemeId.Sumi, Flair.Full, false, 1.5, later);
        Assert.True(later[0].Y > early[0].Y);
    }

    [Fact]
    public void Drawing_particles_allocates_nothing()
    {
        var buffer = new IconParticle[IconParticles.Max];
        var themes = Enum.GetValues<ThemeId>();
        foreach (var theme in themes)
        {
            IconParticles.At(theme, Flair.Full, false, 1.0, buffer);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            foreach (var theme in themes)
            {
                IconParticles.At(theme, Flair.Full, false, i * 0.016, buffer);
            }
        }

        // 6000 frames' worth: nothing per frame (a few bytes of slack for the runtime itself).
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 1024, $"{allocated} bytes");
    }
}
