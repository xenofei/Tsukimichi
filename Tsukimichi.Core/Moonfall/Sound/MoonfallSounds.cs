using static Tsukimichi.Core.Moonfall.Sound.MoonfallInstruments;

namespace Tsukimichi.Core.Moonfall.Sound;

/// <summary>Moonfall's sounds (plan v9 G8). Some have variants: a peg hit's step, a power's companion, a cup's value.</summary>
public enum MoonfallSound : byte
{
    None = 0,

    /// <summary>The chime: two notes a fifth apart, a semitone higher for each peg of the shot (variant: the step).</summary>
    PegHit,

    /// <summary>A lit peg clearing at the end of the turn: a soft pop.</summary>
    PegClear,

    /// <summary>The ball off a wall: a barely-there tap.</summary>
    WallTap,

    /// <summary>The ball off the bucket's rim: a small metal tink.</summary>
    RimTap,

    /// <summary>The ball caught in the bucket: a cradle's thunk under a rising arpeggio.</summary>
    BucketCatch,

    /// <summary>A free ball from the shot's score: a bright rising pair and a sparkle.</summary>
    FreeBall,

    /// <summary>The shot: a soft puff and thump.</summary>
    Launch,

    /// <summary>A style shot's sting.</summary>
    StyleShot,

    /// <summary>A power going off: the moon motif's head, tinted for its companion (variant: the <see cref="MoonfallPower"/>).</summary>
    Power,

    /// <summary>The approach to the last orange: a drum roll swelling.</summary>
    FeverRoll,

    /// <summary>The last orange hit: FULL MOON.</summary>
    FeverHit,

    /// <summary>The ball landing in a Fever cup (variant: 0 for 10,000, 1 for 50,000, 2 for 100,000).</summary>
    CupLanding,

    /// <summary>One tick of the score's count-up (variant: 0 counting by 1,000 … 3 by 10, rising as it nears the total).</summary>
    CountTick,

    /// <summary>A button.</summary>
    UiClick,

    /// <summary>Out of balls: a gentle falling phrase.</summary>
    LevelLost,

    /// <summary>The finale music (<see cref="MoonfallFinale"/>).</summary>
    Finale,
}

/// <summary>
/// The sound designs (plan v9 G8), rendered by <see cref="MoonfallSynth"/>. Every sound is the same for the same
/// sound, variant and sample rate. Renders to listen to are in <c>docs/design/v9/sound/</c>.
/// <para>
/// The peg chime follows the measurement [M 10]: two notes a perfect fifth apart, the lower one rising one equal-tempered
/// semitone for each peg the shot has lit, from a base of D4 (inside the measured C♯4–D♯4), and starting low again
/// every shot. D is the finale's key, so the first peg of a shot sounds the key's tonic and fifth, the interval the moon
/// motif opens with. The timbre is struck glass with a slow 5-cent shimmer and a short decay (0.22 s), so a 26-peg
/// shot rings like a run of moonlit glass rather than a stack of beeps, and each chime has died before the next few pile up.
/// </para>
/// <para>
/// Walls and bricks: the footage's audio [M 10] shows nothing but the chime standing out of the music at a wall bounce,
/// so a wall is a barely audible tap (−30 dBFS) and a brick is a peg (it chimes when it lights, and is silent after).
/// The rim gets a small tink, since the lantern cart's rim is metal. Each clearing peg pops softly, as in the footage
/// ("each pop is a short expanding ring with a sound", [M 7]).
/// </para>
/// </summary>
public static class MoonfallSounds
{
    /// <summary>The first peg's lower note: D4.</summary>
    public const int PegBaseMidi = 62;

    /// <summary>The upper note sits a perfect fifth (7 semitones) above.</summary>
    public const int PegFifth = 7;

    /// <summary>Chime steps rendered: three octaves, past the +23 the footage reached; later pegs hold the top one.</summary>
    public const int PegSteps = 36;

    /// <summary>The sample rate the docs' renders and the tests use.</summary>
    public const int ReferenceSampleRate = 48000;

    /// <summary>Every sound but <see cref="MoonfallSound.None"/>.</summary>
    public static ReadOnlySpan<MoonfallSound> All =>
    [
        MoonfallSound.PegHit, MoonfallSound.PegClear, MoonfallSound.WallTap, MoonfallSound.RimTap, MoonfallSound.BucketCatch,
        MoonfallSound.FreeBall, MoonfallSound.Launch, MoonfallSound.StyleShot, MoonfallSound.Power, MoonfallSound.FeverRoll,
        MoonfallSound.FeverHit, MoonfallSound.CupLanding, MoonfallSound.CountTick, MoonfallSound.UiClick, MoonfallSound.LevelLost,
        MoonfallSound.Finale,
    ];

    /// <summary>How many variants <paramref name="sound"/> has (1 for most).</summary>
    public static int Variants(MoonfallSound sound) => sound switch
    {
        MoonfallSound.None => 0,
        MoonfallSound.PegHit => PegSteps,
        MoonfallSound.Power => MoonfallPowers.Count + 1,
        MoonfallSound.CupLanding => 3,
        MoonfallSound.CountTick => 4,
        _ => 1,
    };

    /// <summary>Each sound's length in seconds (the finale's from its score).</summary>
    public static double Seconds(MoonfallSound sound) => sound switch
    {
        MoonfallSound.PegHit => 0.85,
        MoonfallSound.PegClear => 0.12,
        MoonfallSound.WallTap => 0.08,
        MoonfallSound.RimTap => 0.3,
        MoonfallSound.BucketCatch => 1.2,
        MoonfallSound.FreeBall => 1.0,
        MoonfallSound.Launch => 0.3,
        MoonfallSound.StyleShot => 1.3,
        MoonfallSound.Power => 1.5,
        MoonfallSound.FeverRoll => 3.0,
        MoonfallSound.FeverHit => 3.0,
        MoonfallSound.CupLanding => 1.8,
        MoonfallSound.CountTick => 0.06,
        MoonfallSound.UiClick => 0.06,
        MoonfallSound.LevelLost => 1.5,
        MoonfallSound.Finale => MoonfallFinale.Seconds,
        _ => throw new ArgumentOutOfRangeException(nameof(sound)),
    };

    /// <summary>
    /// Each sound's peak in dBFS: the mix between them. The big moments (FULL MOON, the finale) are loudest; the chime
    /// sits well under them because a long shot rings dozens; the taps, pops and ticks that come in runs are quiet.
    /// </summary>
    public static double PeakDbfs(MoonfallSound sound, int variant = 0) => sound switch
    {
        // Higher chimes sound louder at the same level, so each step is a little lower.
        MoonfallSound.PegHit => -9 - (0.08 * Math.Clamp(variant, 0, PegSteps - 1)),
        MoonfallSound.PegClear => -22,
        MoonfallSound.WallTap => -30,
        MoonfallSound.RimTap => -22,
        MoonfallSound.BucketCatch => -7,
        MoonfallSound.FreeBall => -8,
        MoonfallSound.Launch => -14,
        MoonfallSound.StyleShot => -6,
        MoonfallSound.Power => -6,
        MoonfallSound.FeverRoll => -6,
        MoonfallSound.FeverHit => -2,
        MoonfallSound.CupLanding => -5,
        MoonfallSound.CountTick => -24,
        MoonfallSound.UiClick => -20,
        MoonfallSound.LevelLost => -12,
        MoonfallSound.Finale => -1.5,
        _ => throw new ArgumentOutOfRangeException(nameof(sound)),
    };

    /// <summary>The chime's lower note for the shot's <paramref name="step"/>th peg (from 0), in hertz.</summary>
    public static double PegLowHz(int step) => MoonfallSynth.Hz(PegBaseMidi + Math.Clamp(step, 0, PegSteps - 1));

    /// <summary>The chime's upper note: a perfect fifth (equal-tempered) above <see cref="PegLowHz"/>.</summary>
    public static double PegHighHz(int step) => MoonfallSynth.Hz(PegBaseMidi + PegFifth + Math.Clamp(step, 0, PegSteps - 1));

    /// <summary>Renders <paramref name="sound"/>'s <paramref name="variant"/> at <paramref name="sampleRate"/>.</summary>
    public static MoonfallSoundBuffer Render(MoonfallSound sound, int variant, int sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(variant);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(variant, Math.Max(1, Variants(sound)));
        if (sound == MoonfallSound.Finale)
        {
            return MoonfallFinale.Render(sampleRate);
        }

        var s = new MoonfallSynth(sampleRate, Seconds(sound));
        switch (sound)
        {
            case MoonfallSound.PegHit:
                PegHit(s, variant);
                break;
            case MoonfallSound.PegClear:
                s.Thump(0, 760, 430, 0.03, 0.022, 1.0);
                s.Noise(0, 0.01, 0.3, MoonfallEnvelope.Struck(0.001, 0.006), 1500, 5000, 3000, 11);
                break;
            case MoonfallSound.WallTap:
                s.Thump(0, 1300, 1100, 0.01, 0.008, 1.0);
                s.Noise(0, 0.01, 0.4, MoonfallEnvelope.Struck(0.0008, 0.004), 1500, 5000, 5000, 12);
                break;
            case MoonfallSound.RimTap:
                s.Note(0, MoonfallSynth.Hz(91), 0.3, 1.0, [new(1, 1), new(2.76, 0.5, 0.6), new(5.4, 0.25, 0.4)], MoonfallEnvelope.Struck(0.001, 0.05));
                break;
            case MoonfallSound.BucketCatch:
                BucketCatch(s);
                break;
            case MoonfallSound.FreeBall:
                FreeBall(s);
                break;
            case MoonfallSound.Launch:
                s.Noise(0, 0.25, 1.0, new MoonfallEnvelope(0.006, 0.06, 0, 0.04), 120, 2600, 380, 13);
                s.Thump(0.005, 150, 72, 0.05, 0.05, 0.8);
                break;
            case MoonfallSound.StyleShot:
                StyleShot(s);
                break;
            case MoonfallSound.Power:
                Power(s, MoonfallPowerTint.For((MoonfallPower)variant));
                break;
            case MoonfallSound.FeverRoll:
                FeverRoll(s);
                break;
            case MoonfallSound.FeverHit:
                FeverHit(s);
                break;
            case MoonfallSound.CupLanding:
                CupLanding(s, variant);
                break;
            case MoonfallSound.CountTick:
                // A6, B6, C♯7, D7: the ticks climb to the tonic as the count reaches its total.
                s.Note(0, MoonfallSynth.Hz(new[] { 93, 95, 97, 98 }[variant]), 0.04, 1.0, [new(1, 1), new(2, 0.15, 0.5)], MoonfallEnvelope.Struck(0.001, 0.009));
                break;
            case MoonfallSound.UiClick:
                s.Thump(0, 1500, 1150, 0.006, 0.009, 1.0);
                s.Noise(0, 0.006, 0.35, MoonfallEnvelope.Struck(0.0008, 0.003), 2000, 7000, 7000, 14);
                break;
            case MoonfallSound.LevelLost:
                // A4, F♯4, D4, unhurried: the moon setting, not a buzzer.
                Flute(s, 0.0, 69, 0.22, 0.8, -0.1);
                Flute(s, 0.3, 66, 0.22, 0.75, 0);
                Flute(s, 0.6, 62, 0.45, 0.7, 0.1);
                Harp(s, 0.6, 50, 0.4, 0, 0.6);
                s.Reverb(0.3, 0.78);
                break;
        }

        return s.Finish(PeakDbfs(sound, variant), stereo: false);
    }

    private static void PegHit(MoonfallSynth s, int step)
    {
        // The two notes struck together; the upper a little softer and shorter, so the lower note carries the climb.
        Glass(s, 0, PegLowHz(step), 1.0, 0.22);
        Glass(s, 0, PegHighHz(step), 0.6, 0.18);
        s.Reverb(0.12, 0.7, 0.45);
    }

    private static void BucketCatch(MoonfallSynth s)
    {
        // The cradle's wooden thunk, then D5 F♯5 A5 D6 rising as the ball settles.
        s.Thump(0, 190, 105, 0.03, 0.045, 0.9);
        int[] notes = [74, 78, 81, 86];
        for (var k = 0; k < notes.Length; k++)
        {
            Glass(s, 0.03 + (k * 0.045), MoonfallSynth.Hz(notes[k]), 0.55 + (k * 0.05), 0.32, (k - 1.5) * 0.15);
        }

        s.Reverb(0.2, 0.75);
    }

    private static void FreeBall(MoonfallSynth s)
    {
        // A5 then D6, a rising fourth that lands on the tonic, with a few high twinkles above.
        Celesta(s, 0, 81, 0.8, -0.1, 0.4);
        Celesta(s, 0.09, 86, 1.0, 0.1, 0.5);
        var random = new MoonfallRandom(21);
        int[] twinkles = [98, 102, 105, 110];
        for (var k = 0; k < 6; k++)
        {
            var note = twinkles[random.Next(twinkles.Length)];
            Glass(s, 0.15 + (k * 0.06), MoonfallSynth.Hz(note), 0.18, 0.12, (random.Next(9) - 4) * 0.1);
        }

        s.Reverb(0.22, 0.78);
    }

    private static void StyleShot(MoonfallSynth s)
    {
        // A quick D major run up to a held D6 and A6 (the chime's fifth, an octave up), brass under celesta.
        int[] run = [74, 78, 81];
        for (var k = 0; k < run.Length; k++)
        {
            Celesta(s, k * 0.05, run[k], 0.7, -0.2 + (k * 0.1), 0.25);
        }

        Brass(s, 0.15, 74, 0.4, 0.55, 0.15);
        Brass(s, 0.15, 81, 0.4, 0.45, 0.25);
        Celesta(s, 0.15, 86, 0.9, -0.1, 0.6);
        Celesta(s, 0.15, 93, 0.6, 0.15, 0.5);
        s.Reverb(0.25, 0.8);
    }

    /// <summary>The moon motif's head (root, fifth up, third: D5 A5 F♯5 untinted) and a soft bloom on the root.</summary>
    private static void Power(MoonfallSynth s, in MoonfallPowerTint tint)
    {
        var root = 74 + tint.Transpose;
        int[] motif = [0, 7, tint.Minor ? 3 : 4];
        for (var k = 0; k < motif.Length; k++)
        {
            var at = k * tint.Spacing;
            var hold = k == motif.Length - 1 ? 0.45 : tint.Spacing;
            Voice(s, tint.Timbre, at, root + motif[k], hold, 0.9, (k - 1) * 0.15);
            if (tint.Canon != 0)
            {
                Voice(s, tint.Timbre, at + 0.07, root + motif[k] + tint.Canon, hold, 0.5, (1 - k) * 0.15);
            }
        }

        var bloom = 3 * tint.Spacing;
        Celesta(s, bloom, root + 12, 0.35, 0.2, 0.5);
        Celesta(s, bloom, root + 19, 0.2, -0.2, 0.45);
        if (tint.Drum)
        {
            Timpani(s, 0, root - 36 + 12, 0.9);
        }

        if (tint.Crackle)
        {
            for (var k = 0; k < 7; k++)
            {
                s.Noise(0.02 + (k * 0.05), 0.01, 0.25, MoonfallEnvelope.Struck(0.0005, 0.004), 2500, 9000, 9000, 31 + (ulong)k, (k % 3) - 1);
            }
        }

        s.Reverb(tint.Reverb, 0.8);
    }

    private static void Voice(MoonfallSynth s, MoonfallTimbre timbre, double at, int midi, double hold, double level, double pan)
    {
        switch (timbre)
        {
            case MoonfallTimbre.Flute:
                Flute(s, at, midi, hold, level, pan);
                break;
            case MoonfallTimbre.Brass:
                Brass(s, at, midi, hold, level * 0.8, pan);
                break;
            case MoonfallTimbre.Harp:
                Harp(s, at, midi, level, pan, 0.5);
                break;
            case MoonfallTimbre.Strings:
                Strings(s, at, midi, hold + 0.1, level, pan, 0.06);
                break;
            default:
                Celesta(s, at, midi, level, pan, 0.55);
                break;
        }
    }

    private static void FeverRoll(MoonfallSynth s)
    {
        // Snare strokes speeding from 14 to 24 a second and swelling from a whisper, over a soft timpani roll on A2 (the
        // key's dominant, which FULL MOON resolves to D).
        var t = 0.0;
        var stroke = 0;
        while (t < 2.95)
        {
            var progress = t / 3.0;
            var level = 0.12 + (0.88 * progress * progress);
            Snare(s, t, level * (stroke % 2 == 0 ? 1.0 : 0.85), 100 + (ulong)stroke, stroke % 2 == 0 ? -0.1 : 0.1);
            if (stroke % 2 == 0)
            {
                Timpani(s, t, 45, level * 0.5, 0.05, 0.3);
            }

            t += 1 / (14 + (10 * progress));
            stroke++;
        }

        s.Reverb(0.15, 0.7);
    }

    private static void FeverHit(MoonfallSynth s)
    {
        // A bass drum and timpani on D, a cymbal, a D major brass chord, and the chime's D and A high in bells.
        s.Thump(0, 95, 46, 0.05, 0.28, 1.0);
        Timpani(s, 0, 38, 0.9, 0, 0.7);
        Cymbal(s, 0, 0.5, 0.9, 41, 0.1);
        int[] chord = [62, 66, 69, 74];
        for (var k = 0; k < chord.Length; k++)
        {
            Brass(s, 0.01, chord[k], 1.6, 0.42, (k - 1.5) * 0.2);
        }

        Strings(s, 0.02, 50, 1.8, 0.4, 0, 0.03);
        Celesta(s, 0.0, 86, 0.6, -0.25, 0.9);
        Celesta(s, 0.0, 93, 0.5, 0.25, 0.9);
        Glass(s, 0.12, MoonfallSynth.Hz(98), 0.3, 0.6, 0);
        s.Reverb(0.3, 0.84);
    }

    private static void CupLanding(MoonfallSynth s, int variant)
    {
        // Bells peal up D major; the 100,000 cup rings four and has a warm brass chord under it.
        int[][] peals = [[86, 93], [86, 90, 93], [86, 90, 93, 98]];
        var peal = peals[variant];
        for (var k = 0; k < peal.Length; k++)
        {
            Glass(s, k * 0.07, MoonfallSynth.Hz(peal[k]), 0.7, 0.45, (k - ((peal.Length - 1) * 0.5)) * 0.2);
            Celesta(s, k * 0.07, peal[k] - 12, 0.35, 0, 0.5);
        }

        if (variant == 2)
        {
            Brass(s, 0.05, 50, 0.9, 0.3, -0.1);
            Brass(s, 0.05, 57, 0.9, 0.25, 0.1);
        }

        s.Reverb(0.25, 0.8);
    }
}

/// <summary>The voice a power's motif is played on.</summary>
public enum MoonfallTimbre : byte
{
    Celesta,
    Flute,
    Brass,
    Harp,
    Strings,
}

/// <summary>
/// How a companion colours the shared power motif (plan v9 G8): its key, voice, mode, pace and a touch of its own.
/// </summary>
/// <param name="Transpose">Semitones from D5.</param>
/// <param name="Timbre">The voice.</param>
/// <param name="Minor">A minor third instead of the major.</param>
/// <param name="Canon">A second voice this many semitones away, a beat behind (0 for none).</param>
/// <param name="Spacing">Seconds between the motif's notes.</param>
/// <param name="Reverb">How much hall.</param>
/// <param name="Drum">A timpani stroke under it.</param>
/// <param name="Crackle">A spark of noise over it.</param>
public readonly record struct MoonfallPowerTint(int Transpose, MoonfallTimbre Timbre, bool Minor, int Canon, double Spacing, double Reverb, bool Drum, bool Crackle)
{
    /// <summary>The tint for <paramref name="power"/>'s companion (decision 22's cast).</summary>
    public static MoonfallPowerTint For(MoonfallPower power) => power switch
    {
        // Minfilia: guidance, crystal-bright with an octave of light above.
        MoonfallPower.SuperGuide => new(0, MoonfallTimbre.Celesta, false, 12, 0.11, 0.32, false, false),

        // Alphinaud and Alisaie: the twins, the motif in canon an octave apart.
        MoonfallPower.Multiball => new(0, MoonfallTimbre.Celesta, false, -12, 0.1, 0.25, false, false),

        // Cid: the engineer's brass.
        MoonfallPower.Wings => new(-5, MoonfallTimbre.Brass, false, 0, 0.11, 0.2, false, false),

        // Raubahn: low brass and a drum.
        MoonfallPower.Burst => new(-7, MoonfallTimbre.Brass, false, 0, 0.12, 0.22, true, false),

        // Merlwyb: the sea, a harp rolling over a lower octave.
        MoonfallPower.Flippers => new(2, MoonfallTimbre.Harp, false, -12, 0.1, 0.25, false, false),

        // Urianger: the astrologian's mystery, minor, on flute in a long hall.
        MoonfallPower.Gate => new(0, MoonfallTimbre.Flute, true, 0, 0.13, 0.45, false, false),

        // Kan-E-Senna: the Twelveswood, a soft flute.
        MoonfallPower.Bloom => new(5, MoonfallTimbre.Flute, false, 0, 0.12, 0.3, false, false),

        // Tataru: quick and playful, plucked.
        MoonfallPower.Draw => new(7, MoonfallTimbre.Harp, false, 0, 0.08, 0.2, false, false),

        // Y'shtola: warm brass with sparks.
        MoonfallPower.Fireball => new(3, MoonfallTimbre.Brass, false, 0, 0.1, 0.22, false, true),

        // Louisoix: slow and reverent, strings.
        MoonfallPower.Path => new(-2, MoonfallTimbre.Strings, false, 0, 0.18, 0.45, false, false),

        // The moogle courier: high, fast and crackling.
        MoonfallPower.Bolt => new(12, MoonfallTimbre.Celesta, false, 0, 0.06, 0.2, false, true),

        _ => new(0, MoonfallTimbre.Celesta, false, 0, 0.11, 0.25, false, false),
    };
}
