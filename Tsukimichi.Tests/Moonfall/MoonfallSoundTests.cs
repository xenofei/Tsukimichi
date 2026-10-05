using Tsukimichi.Core.Moonfall;
using Tsukimichi.Core.Moonfall.Sound;
using Tsukimichi.Tests.Localization;
using static Tsukimichi.Tests.Moonfall.MoonfallTestKit;

namespace Tsukimichi.Tests.Moonfall;

/// <summary>Moonfall's sound (plan v9 G8): the synthesiser, the measured chime, levels, lengths and the event table.</summary>
public sealed class MoonfallSoundTests
{
    private const int Rate = MoonfallSounds.ReferenceSampleRate;

    /// <summary>Every sound and variant, rendered once for the whole class (the finale takes a moment).</summary>
    private static readonly Lazy<MoonfallSoundBank> Bank = new(() => MoonfallSoundBank.Render(Rate)!);

    public static TheoryData<MoonfallSound> Sounds()
    {
        var data = new TheoryData<MoonfallSound>();
        foreach (var sound in MoonfallSounds.All)
        {
            data.Add(sound);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Sounds))]
    public void The_same_sound_renders_the_same_samples(MoonfallSound sound)
    {
        var variant = MoonfallSounds.Variants(sound) - 1;
        var a = MoonfallSounds.Render(sound, variant, Rate);
        var b = MoonfallSounds.Render(sound, variant, Rate);
        Assert.Equal(a.Channels, b.Channels);
        Assert.True(a.Samples.SequenceEqual(b.Samples));
    }

    [Theory]
    [MemberData(nameof(Sounds))]
    public void No_sound_clips_and_none_is_silent(MoonfallSound sound)
    {
        for (var v = 0; v < MoonfallSounds.Variants(sound); v++)
        {
            var buffer = Bank.Value.Get(sound, v)!;
            var peak = buffer.PeakDbfs();
            Assert.True(peak <= -1.0, $"{sound} {v} peaks at {peak:F2} dBFS");
            Assert.True(peak > -40, $"{sound} {v} is nearly silent ({peak:F2} dBFS)");
            Assert.True(Math.Abs(peak - MoonfallSounds.PeakDbfs(sound, v)) < 0.05, $"{sound} {v} peaks at {peak:F2}, not at its design level");
            foreach (var s in buffer.Samples)
            {
                Assert.True(float.IsFinite(s));
            }
        }
    }

    [Theory]
    [MemberData(nameof(Sounds))]
    public void Each_sound_is_its_designed_length(MoonfallSound sound)
    {
        var buffer = Bank.Value.Get(sound)!;
        Assert.Equal(MoonfallSounds.Seconds(sound), buffer.Seconds, 3);
        var (min, max) = sound switch
        {
            MoonfallSound.Finale => (20.0, 30.0),
            MoonfallSound.FeverRoll or MoonfallSound.FeverHit => (2.0, 3.0),
            MoonfallSound.PegHit => (0.5, 1.0),
            MoonfallSound.CountTick or MoonfallSound.UiClick or MoonfallSound.WallTap or MoonfallSound.PegClear => (0.03, 0.15),
            _ => (0.2, 2.0),
        };
        Assert.InRange(buffer.Seconds, min, max);

        // Every sound ends in silence, so a voice that stops at its end never clicks.
        var samples = buffer.Samples;
        Assert.True(Math.Abs(samples[^1]) < 1e-4);
    }

    [Fact]
    public void Only_the_finale_is_stereo()
    {
        foreach (var sound in MoonfallSounds.All)
        {
            Assert.Equal(sound == MoonfallSound.Finale ? 2 : 1, Bank.Value.Get(sound)!.Channels);
        }
    }

    [Fact]
    public void The_chime_is_two_notes_a_fifth_apart_climbing_a_semitone_a_peg()
    {
        // [M 10]: the two notes stand in a perfect fifth (measured 1.49–1.50), and the lower climbs one equal-tempered
        // semitone per peg, from ≈ C♯4–D♯4 (272–318 Hz).
        Assert.InRange(MoonfallSounds.PegLowHz(0), 272, 318);
        for (var step = 0; step < MoonfallSounds.PegSteps; step++)
        {
            Assert.Equal(Math.Pow(2, 7 / 12.0), MoonfallSounds.PegHighHz(step) / MoonfallSounds.PegLowHz(step), 9);
            Assert.InRange(MoonfallSounds.PegHighHz(step) / MoonfallSounds.PegLowHz(step), 1.49, 1.50);
            if (step > 0)
            {
                Assert.Equal(Math.Pow(2, 1 / 12.0), MoonfallSounds.PegLowHz(step) / MoonfallSounds.PegLowHz(step - 1), 9);
            }
        }

        // Later pegs hold the top step.
        Assert.Equal(MoonfallSounds.PegLowHz(MoonfallSounds.PegSteps - 1), MoonfallSounds.PegLowHz(99));
    }

    [Fact]
    public void The_rendered_chime_sounds_those_two_notes()
    {
        // On a semitone grid from an octave under the base to five octaves over it, the two loudest notes of each
        // rendered chime are its own two, and each is sharper in tune than a quarter-tone either side of it.
        for (var step = 0; step < 26; step++)
        {
            var samples = Bank.Value.Get(MoonfallSound.PegHit, step)!.Samples[..8192].ToArray();
            var grid = Enumerable.Range(-12, 72).Select(n => (Note: n, Energy: Goertzel(samples, MoonfallSynth.Hz(MoonfallSounds.PegBaseMidi + n)))).OrderByDescending(x => x.Energy).ToArray();
            Assert.Equal(new[] { step, step + MoonfallSounds.PegFifth }, grid.Take(2).Select(x => x.Note).Order().ToArray());
            foreach (var hz in (double[])[MoonfallSounds.PegLowHz(step), MoonfallSounds.PegHighHz(step)])
            {
                var quarter = Math.Pow(2, 0.5 / 12);
                Assert.True(Goertzel(samples, hz) > 2 * Goertzel(samples, hz * quarter));
                Assert.True(Goertzel(samples, hz) > 2 * Goertzel(samples, hz / quarter));
            }
        }
    }

    [Fact]
    public void A_shots_chimes_climb_from_the_bottom_and_start_again_each_shot()
    {
        // A real game: the cue table reads the engine's own peg count, so each shot's first peg is step 0.
        var game = new MoonfallGame(Grid(60), 1, 7);
        var cues = new MoonfallSoundCues();
        var shots = new List<List<int>>();
        for (var shot = 0; shot < 5; shot++)
        {
            Assert.True(game.Shoot((shot * 9.0) - 18));
            var steps = new List<int>();
            foreach (var (_, e) in RunUntil(game, static g => g.Phase is MoonfallPhase.Aiming or MoonfallPhase.Won or MoonfallPhase.Lost))
            {
                var cue = cues.For(e);
                if (cue.Sound == MoonfallSound.PegHit)
                {
                    steps.Add(cue.Variant);
                }
            }

            shots.Add(steps);
        }

        // A shot that hits nothing has no chimes; every shot that hits climbs from step 0.
        Assert.True(shots.Count(steps => steps.Count > 1) >= 2);
        Assert.All(shots, steps => Assert.Equal(Enumerable.Range(0, steps.Count), steps));
    }

    [Fact]
    public void The_table_maps_the_events_it_should()
    {
        var cues = new MoonfallSoundCues();
        MoonfallCue For(MoonfallEventKind kind, long value = 0, int count = 0, double x = 400) => cues.For(new MoonfallEvent(kind, 0, value, count, x, 300));

        Assert.Equal(new MoonfallCue(MoonfallSound.PegHit, 0, 0f), For(MoonfallEventKind.PegHit, 10, 1));
        Assert.Equal(4, For(MoonfallEventKind.PegHit, 10, 5).Variant);
        Assert.Equal(MoonfallSounds.PegSteps - 1, For(MoonfallEventKind.PegHit, 10, 500).Variant);
        Assert.True(For(MoonfallEventKind.PegHit, 10, 1, x: 0).Pan < 0);
        Assert.True(For(MoonfallEventKind.PegHit, 10, 1, x: 800).Pan > 0);
        Assert.InRange(For(MoonfallEventKind.PegHit, 10, 1, x: double.NaN).Pan, 0f, 0f);

        // The bucket's free ball is part of its catch's sound; a score's or the drum's free ball has its own.
        Assert.Equal(MoonfallSound.BucketCatch, For(MoonfallEventKind.BucketCatch).Sound);
        Assert.Equal(MoonfallSound.None, For(MoonfallEventKind.FreeBall).Sound);
        Assert.Equal(MoonfallSound.FreeBall, For(MoonfallEventKind.FreeBall, 25_000).Sound);
        Assert.Equal(MoonfallSound.FreeBall, For(MoonfallEventKind.FreeBall).Sound);

        // Full Moon: the roll on the approach, stopped by a miss or by the hit, which starts the finale.
        Assert.Equal(MoonfallSound.FeverRoll, For(MoonfallEventKind.FeverApproach).Sound);
        Assert.Equal(new MoonfallCue(MoonfallSound.None, 0, 0, MoonfallSound.FeverRoll), For(MoonfallEventKind.FeverApproachEnded));
        var hit = For(MoonfallEventKind.FeverHit);
        Assert.Equal((MoonfallSound.FeverHit, MoonfallSound.FeverRoll, true), (hit.Sound, hit.Stops, hit.StartsFinale));
        Assert.Equal(new[] { 0, 1, 2, 1, 0 }, MoonfallRules.FeverBucketValues.ToArray().Select(v => For(MoonfallEventKind.FeverLanded, v).Variant));
        Assert.Equal(2, For(MoonfallEventKind.FeverLanded, MoonfallRules.PerfectFeverBucketValue).Variant);

        // Each companion's tint of the power motif.
        Assert.Equal(new MoonfallCue(MoonfallSound.Power, (int)MoonfallPower.Gate, 0f), For(MoonfallEventKind.PowerTriggered, (long)MoonfallPower.Gate));

        // The ball leaving the board and the next ball are silent; walls barely tap.
        Assert.Equal(MoonfallSound.None, For(MoonfallEventKind.BallLost).Sound);
        Assert.Equal(MoonfallSound.None, For(MoonfallEventKind.NextBall).Sound);
        Assert.Equal(MoonfallSound.WallTap, For(MoonfallEventKind.WallBounce).Sound);
        Assert.Equal(MoonfallSound.StyleShot, For(MoonfallEventKind.StyleShot).Sound);
        Assert.Equal(MoonfallSound.LevelLost, For(MoonfallEventKind.LevelLost).Sound);
    }

    [Fact]
    public void Every_companion_has_its_own_tint_of_the_motif()
    {
        var tints = Enumerable.Range(1, MoonfallPowers.Count).Select(p => MoonfallPowerTint.For((MoonfallPower)p)).ToArray();
        Assert.Equal(tints.Length, tints.Distinct().Count());
        var renders = Enumerable.Range(1, MoonfallPowers.Count).Select(p => Bank.Value.Get(MoonfallSound.Power, p)!.Samples.ToArray()).ToArray();
        for (var a = 0; a < renders.Length; a++)
        {
            for (var b = a + 1; b < renders.Length; b++)
            {
                Assert.False(renders[a].SequenceEqual(renders[b]));
            }
        }
    }

    [Fact]
    public void Reading_cues_allocates_nothing()
    {
        var events = Enum.GetValues<MoonfallEventKind>().Select(k => new MoonfallEvent(k, 1, 25_000, 3, 200, 300)).ToArray();
        var cues = new MoonfallSoundCues();
        var ticker = new MoonfallCountTicker();
        var sum = 0;
        void Run()
        {
            for (var i = 0; i < 100; i++)
            {
                foreach (var e in events)
                {
                    sum += cues.For(e).Variant;
                }

                sum += ticker.Next(i * 100, 10_000, 0.016, out var v) ? v : 0;
            }
        }

        Run();
        var before = GC.GetAllocatedBytesForCurrentThread();
        Run();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(sum > 0);
    }

    [Fact]
    public void The_count_up_ticks_at_most_twenty_a_second_and_once_more_as_it_lands()
    {
        var ticker = new MoonfallCountTicker();
        Assert.False(ticker.Next(0, 0, 0.016, out _));

        // The counter climbs every frame at 60 fps for a second: 20 ticks, not 60.
        var ticks = 0;
        var variants = new List<int>();
        for (var frame = 1; frame <= 60; frame++)
        {
            if (ticker.Next(frame * 300, 30_000, 1 / 60.0, out var v))
            {
                ticks++;
                variants.Add(v);
            }
        }

        Assert.InRange(ticks, 19, 21);
        Assert.Equal(0, variants[0]);

        // Landing ticks at once, at the top pitch, even inside the interval.
        Assert.True(ticker.Next(30_000, 30_000, 0.001, out var landed));
        Assert.Equal(3, landed);

        // Nothing while it holds still; a lower score (a new level) starts afresh without a tick.
        Assert.False(ticker.Next(30_000, 30_000, 1, out _));
        Assert.False(ticker.Next(0, 500, 1, out _));
        Assert.True(ticker.Next(100, 500, 0.06, out var near));
        Assert.Equal(2, near);
    }

    [Fact]
    public void Voicing_covers_the_chord_in_range_and_moves_little()
    {
        int[] d = [2, 6, 9];
        int[] g = [7, 11, 2];
        int[] a7 = [9, 1, 4, 7];
        var first = MoonfallFinale.Voice(d, null, 55, 72, 4);
        var second = MoonfallFinale.Voice(g, first, 55, 72, 4);
        var third = MoonfallFinale.Voice(a7, second, 55, 72, 4);
        foreach (var (voicing, chord) in new[] { (first, d), (second, g), (third, a7) })
        {
            Assert.Equal(voicing.Order(), voicing);
            Assert.All(voicing, n => Assert.InRange(n, 55, 72));
            Assert.All(voicing, n => Assert.Contains(n % 12, chord));
            Assert.Contains(chord[0], voicing.Select(n => n % 12));
            Assert.Contains(chord[1], voicing.Select(n => n % 12));
            Assert.Single(voicing, n => n % 12 == chord[1]);
        }

        // Voice leading: D to G moves the four voices at most two semitones each.
        Assert.All(first.Zip(second), p => Assert.InRange(Math.Abs(p.First - p.Second), 0, 2));
        Assert.Equal(first, MoonfallFinale.Voice(d, null, 55, 72, 4));
    }

    [Fact]
    public void The_finale_runs_twenty_bars_of_three_four_with_a_short_tail()
    {
        Assert.Equal(20 * 3 * 60 / 132.0, MoonfallFinale.Bars * MoonfallFinale.Bar, 9);
        Assert.InRange(MoonfallFinale.Seconds, 20, 30);

        // It is loudest at the climax (bars 15 to 18), not in its quiet opening.
        var finale = Bank.Value.Get(MoonfallSound.Finale)!;
        double Rms(double from, double to)
        {
            var samples = finale.Samples[((int)(from * Rate) * 2)..((int)(to * Rate) * 2)];
            var sum = 0.0;
            foreach (var s in samples)
            {
                sum += s * s;
            }

            return Math.Sqrt(sum / samples.Length);
        }

        Assert.True(Rms(14 * MoonfallFinale.Bar, 17 * MoonfallFinale.Bar) > 2 * Rms(0, MoonfallFinale.Bar));
    }

    [Fact]
    public void The_bank_clamps_variants_and_has_no_none()
    {
        Assert.Null(Bank.Value.Get(MoonfallSound.None));
        Assert.Same(Bank.Value.Get(MoonfallSound.PegHit, MoonfallSounds.PegSteps - 1), Bank.Value.Get(MoonfallSound.PegHit, 1000));
        Assert.Same(Bank.Value.Get(MoonfallSound.Launch), Bank.Value.Get(MoonfallSound.Launch, -3));
        Assert.Null(MoonfallSoundBank.Render(Rate, new CancellationToken(canceled: true)));
    }

    [Fact]
    public void Other_sample_rates_render_the_same_lengths()
    {
        var at44 = MoonfallSounds.Render(MoonfallSound.PegHit, 3, 44100);
        Assert.Equal(44100, at44.SampleRate);
        Assert.Equal(MoonfallSounds.Seconds(MoonfallSound.PegHit), at44.Seconds, 3);
        Assert.True(at44.PeakDbfs() <= -1.0);
    }

    [Fact]
    public void Sound_never_feeds_the_engine()
    {
        // Determinism: the engine's files never name the sound code, and the sound code only reads events.
        var moonfall = Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi.Core", "Moonfall");
        var engine = Directory.GetFiles(moonfall, "*.cs", SearchOption.TopDirectoryOnly);
        Assert.NotEmpty(engine);
        Assert.All(engine, file => Assert.DoesNotContain("Moonfall.Sound", File.ReadAllText(file), StringComparison.Ordinal));
        Assert.All(engine, file => Assert.DoesNotContain("MoonfallSound", File.ReadAllText(file), StringComparison.Ordinal));
        foreach (var file in Directory.GetFiles(Path.Combine(moonfall, "Sound"), "*.cs"))
        {
            Assert.DoesNotContain("MoonfallGame", File.ReadAllText(file), StringComparison.Ordinal);
        }
    }

    /// <summary>The energy of <paramref name="samples"/> at <paramref name="hz"/> (Goertzel, Hann-windowed).</summary>
    private static double Goertzel(ReadOnlySpan<float> samples, double hz)
    {
        var w = 2 * Math.PI * hz / Rate;
        var coefficient = 2 * Math.Cos(w);
        double s1 = 0, s2 = 0;
        for (var i = 0; i < samples.Length; i++)
        {
            var window = 0.5 - (0.5 * Math.Cos(2 * Math.PI * i / (samples.Length - 1)));
            var s0 = (samples[i] * window) + (coefficient * s1) - s2;
            s2 = s1;
            s1 = s0;
        }

        return (s1 * s1) + (s2 * s2) - (coefficient * s1 * s2);
    }
}
