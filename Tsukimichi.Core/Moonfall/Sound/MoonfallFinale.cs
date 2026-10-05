using static Tsukimichi.Core.Moonfall.Sound.MoonfallInstruments;

namespace Tsukimichi.Core.Moonfall.Sound;

/// <summary>
/// Moonfall's finale (plan v9 G8, decision 3: an original piece, composed as code): about 29 seconds of joyful,
/// orchestral-feeling music for FULL MOON and the LEVEL CLEAR tally, on a moon motif of Tsukimichi's own.
/// <para>
/// <b>The moon motif:</b> D5 A5 F♯5 | B5 A5, a rising fifth (the peg chime's interval, so the music grows out of the
/// sound the player has heard all level), a fall to the third, then a reach up to the sixth: the moon rising, catching,
/// and rising further. It moves by leaps in three-four, a lilting waltz at 132 beats a minute; "Ode to Joy" moves by
/// steps and repeated notes in four-four, and none of Peggle's music was heard or used.
/// </para>
/// <para>
/// <b>The form</b> (20 bars of 3/4 in D major): two bars of a harp rising through D major over a timpani swell; the motif
/// on flute and celesta (bars 3–6); its answer on horns and strings in G, through C and a ii–V back to G (7–10); a rising
/// sequence Em, F♯m, G, A7 with a snare-free timpani roll, a harp run and a cymbal swell (11–14); the motif again in
/// full, an octave up, on strings, flute, horns and celesta (15–18); and a held D major chord with the chime's fifth
/// echoed high on celesta (19–20), ringing out in the hall.
/// </para>
/// <para>
/// <b>Procedural voicing:</b> the string pad, the horn chords and the harp's waltz chords are not written out. For each
/// chord <see cref="Voice"/> picks the notes in a range that cover the root and third (and seventh), avoid a doubled
/// third and tight clusters, and move least from the previous chord, so the inner voices lead smoothly on their own.
/// </para>
/// </summary>
public static class MoonfallFinale
{
    /// <summary>Beats per minute.</summary>
    public const double Tempo = 132;

    /// <summary>Bars of 3/4.</summary>
    public const int Bars = 20;

    /// <summary>Seconds of hall after the last bar.</summary>
    public const double TailSeconds = 1.5;

    public static double Beat => 60 / Tempo;

    public static double Bar => 3 * Beat;

    /// <summary>The whole piece, about 28.8 s.</summary>
    public static double Seconds => (Bars * Bar) + TailSeconds;

    // Pitch classes, root first, then the third, the fifth and any seventh.
    private static readonly int[] D = [2, 6, 9];
    private static readonly int[] G = [7, 11, 2];
    private static readonly int[] Em = [4, 7, 11];
    private static readonly int[] A = [9, 1, 4];
    private static readonly int[] C = [0, 4, 7];
    private static readonly int[] Am = [9, 0, 4];
    private static readonly int[] D7 = [2, 6, 9, 0];
    private static readonly int[] FsM = [6, 9, 1];
    private static readonly int[] A7 = [9, 1, 4, 7];
    private static readonly int[] Em7 = [4, 7, 11, 2];

    /// <summary>Each bar's chord, and the chord from its third beat where it changes (null when it does not).</summary>
    private static readonly (int[] First, int[]? Third)[] Harmony =
    [
        (D, null), (D, null), (D, null), (G, null), (Em, A), (D, null), (G, null), (C, null), (Am, D7), (G, null),
        (Em, null), (FsM, null), (G, null), (A7, null), (D, null), (G, null), (Em7, A7), (D, null), (D, null), (D, null),
    ];

    /// <summary>The tune: bar, beat, length in beats, MIDI note.</summary>
    private static readonly (int Bar, double Beat, double Beats, int Midi)[] Tune =
    [
        // The motif (flute and celesta).
        (2, 0, 1, 74), (2, 1, 1, 81), (2, 2, 1, 78),
        (3, 0, 2, 83), (3, 2, 1, 81),
        (4, 0, 1, 79), (4, 1, 1, 76), (4, 2, 1, 81),
        (5, 0, 3, 78),

        // The answer, in G (horns and strings).
        (6, 0, 1, 79), (6, 1, 1, 86), (6, 2, 1, 83),
        (7, 0, 2, 88), (7, 2, 1, 86),
        (8, 0, 1, 84), (8, 1, 1, 81), (8, 2, 1, 86),
        (9, 0, 3, 83),

        // The rise (strings and flute).
        (10, 0, 1, 76), (10, 1, 1, 79), (10, 2, 1, 83),
        (11, 0, 1, 78), (11, 1, 1, 81), (11, 2, 1, 85),
        (12, 0, 1, 79), (12, 1, 1, 83), (12, 2, 1, 86),
        (13, 0, 1.5, 88), (13, 1.5, 0.5, 86), (13, 2, 1, 85),

        // The motif in full, an octave up, landing on the tonic.
        (14, 0, 1, 86), (14, 1, 1, 93), (14, 2, 1, 90),
        (15, 0, 2, 95), (15, 2, 1, 93),
        (16, 0, 1, 91), (16, 1, 1, 88), (16, 2, 1, 93),
        (17, 0, 5, 86),
    ];

    /// <summary>Renders the finale in stereo at <paramref name="sampleRate"/>, its peak at −1.5 dBFS.</summary>
    public static MoonfallSoundBuffer Render(int sampleRate)
    {
        var s = new MoonfallSynth(sampleRate, Seconds);
        Intro(s);
        Melody(s);
        Accompaniment(s);
        Percussion(s);
        Ending(s);

        // The arc: a soft opening, the motif sung out, a gathering rise, the climax fullest, and the held chord.
        s.Dynamics(
        [
            (At(0), -5), (At(2), -1.5), (At(6), -1), (At(10), -1.5), (At(13), 0), (At(14), 1), (At(17, 2), 1), (At(19), 0),
        ]);
        s.Reverb(0.28, 0.84, 0.3);
        return s.Finish(MoonfallSounds.PeakDbfs(MoonfallSound.Finale), stereo: true);
    }

    /// <summary>
    /// The notes for a chord of <paramref name="chord"/>'s pitch classes, <paramref name="count"/> of them in
    /// [<paramref name="low"/>, <paramref name="high"/>], chosen to cover the chord and move least from
    /// <paramref name="previous"/> (or to sit mid-range when there is none). Ascending.
    /// </summary>
    internal static int[] Voice(int[] chord, int[]? previous, int low, int high, int count)
    {
        var candidates = new List<int>();
        for (var midi = low; midi <= high; midi++)
        {
            if (Array.IndexOf(chord, midi % 12) >= 0)
            {
                candidates.Add(midi);
            }
        }

        var best = new int[count];
        var bestScore = double.MaxValue;
        var current = new int[count];

        void Search(int from, int depth)
        {
            if (depth == count)
            {
                var score = Score(current, chord, previous, (low + high) * 0.5);
                if (score < bestScore)
                {
                    bestScore = score;
                    Array.Copy(current, best, count);
                }

                return;
            }

            for (var i = from; i < candidates.Count; i++)
            {
                current[depth] = candidates[i];
                Search(i + 1, depth + 1);
            }
        }

        Search(0, 0);
        return best;
    }

    private static double Score(int[] notes, int[] chord, int[]? previous, double centre)
    {
        var score = 0.0;
        for (var k = 0; k < notes.Length; k++)
        {
            score += previous is { } p && p.Length == notes.Length ? Math.Abs(notes[k] - p[k]) : Math.Abs(notes[k] - centre) * 0.5;
        }

        int Count(int pc) => notes.Count(n => n % 12 == pc);
        score += Count(chord[0]) == 0 ? 6 : 0;
        score += Count(chord[1]) == 0 ? 8 : 0;
        score += Count(chord[1]) > 1 ? 4 : 0;
        score += Count(chord[2]) == 0 ? 1 : 0;
        score += chord.Length > 3 && Count(chord[3]) == 0 ? 3 : 0;
        for (var k = 1; k < notes.Length; k++)
        {
            score += notes[k] - notes[k - 1] < 3 ? 2 : 0;
        }

        return score;
    }

    private static double At(int bar, double beat = 0) => (bar * Bar) + (beat * Beat);

    /// <summary>A chord's root in the bass, D2 to C♯3.</summary>
    private static int Bass(int[] chord) => chord[0] + 36 < 38 ? chord[0] + 48 : chord[0] + 36;

    private static void Intro(MoonfallSynth s)
    {
        // The harp rises through D major, an octave every three beats, sweeping left to right.
        int[] rise = [50, 54, 57, 62, 66, 69, 74, 78, 81, 86, 90, 93];
        for (var k = 0; k < rise.Length; k++)
        {
            Harp(s, k * 0.5 * Beat, rise[k], 0.42, -0.5 + (k / 11.0), 0.9);
        }

        // A string chord swelling in under it.
        foreach (var note in Voice(D, null, 55, 72, 4))
        {
            Strings(s, 0, note, (2 * Bar) - 0.1, 0.1, (note - 63) * 0.04, 1.6);
        }

        Celesta(s, At(1, 2), 93, 0.3, 0.3, 0.8);
    }

    private static void Melody(MoonfallSynth s)
    {
        foreach (var (bar, beat, beats, midi) in Tune)
        {
            var at = At(bar, beat);
            var length = beats * Beat;
            if (bar < 6)
            {
                Flute(s, at, midi, length * 0.92, 0.55, -0.1);
                Celesta(s, at, midi, 0.28, 0.3, 0.5);
            }
            else if (bar < 10)
            {
                Brass(s, at, midi - 12, length * 0.92, 0.5, 0.25);
                Strings(s, at, midi, length * 0.98, 0.32, -0.3, 0.06);
            }
            else if (bar < 14)
            {
                Strings(s, at, midi, length * 0.98, 0.5, -0.3, 0.06);
                Flute(s, at, midi, length * 0.92, 0.32, -0.05);
            }
            else
            {
                Strings(s, at, midi, length * 0.98, 0.55, -0.3, 0.05);
                Flute(s, at, midi, length * 0.92, 0.38, -0.05);
                Brass(s, at, midi - 12, length * 0.92, 0.48, 0.25);
                Celesta(s, at, midi, 0.24, 0.3, 0.45);
            }
        }
    }

    private static void Accompaniment(MoonfallSynth s)
    {
        int[]? pad = null;
        int[]? harp = null;
        int[]? horns = null;
        for (var bar = 2; bar < 17; bar++)
        {
            var (first, third) = Harmony[bar];
            var climax = bar >= 14;
            Segment(s, bar, 0, third is null ? 3 : 2, first, climax, ref pad, ref harp, ref horns);
            if (third is not null)
            {
                Segment(s, bar, 2, 1, third, climax, ref pad, ref harp, ref horns);
            }
        }
    }

    /// <summary>One chord's share of a bar: the pad held, the bass on its first beat, and the waltz's chords on beats 2 and 3.</summary>
    private static void Segment(MoonfallSynth s, int bar, int beat, int beats, int[] chord, bool climax, ref int[]? pad, ref int[]? harp, ref int[]? horns)
    {
        var at = At(bar, beat);
        var length = beats * Beat;
        pad = Voice(chord, pad, 55, 72, 4);
        for (var k = 0; k < pad.Length; k++)
        {
            Strings(s, at, pad[k], length - 0.02, climax ? 0.15 : 0.11, -0.4 + (k * 0.27), 0.08);
        }

        var bass = Bass(chord);
        if (climax)
        {
            Strings(s, at, bass, length - 0.03, 0.42, 0, 0.04);
            horns = Voice(chord, horns, 50, 64, 3);
            foreach (var note in horns)
            {
                Brass(s, at, note, length * 0.95, 0.13, 0.3);
            }
        }
        else
        {
            Pizzicato(s, at, bass, 0.6, 0);
        }

        // Oom-pah-pah: the chord on the beats after the bass.
        harp = Voice(chord, harp, 62, 76, 3);
        for (var b = beat == 0 ? 1 : 0; b < beats; b++)
        {
            foreach (var note in harp)
            {
                if (climax)
                {
                    Pizzicato(s, At(bar, beat + b), note, 0.15, -0.35);
                }
                else
                {
                    Harp(s, At(bar, beat + b), note, 0.17, -0.35, 0.35);
                }
            }
        }
    }

    private static void Percussion(MoonfallSynth s)
    {
        // The opening swell on D, landing on the motif's first beat.
        Roll(s, 0, 2 * Bar, 38, 0.05, 0.45);
        Timpani(s, At(2), 38, 0.7);
        Timpani(s, At(6), 43, 0.55);
        Timpani(s, At(10), 40, 0.55);

        // The rise: a roll on A, the harp running up A mixolydian, a cymbal swelling into the climax.
        Roll(s, At(13), Bar, 45, 0.1, 0.6);
        int[] run = [69, 71, 73, 74, 76, 78, 79, 81, 83, 85, 86, 88];
        for (var k = 0; k < run.Length; k++)
        {
            Harp(s, At(13, k * 0.25), run[k], 0.3, -0.4, 0.4);
        }

        Cymbal(s, At(13), 0.22, 0.25, 51, 0.15, Bar);
        Cymbal(s, At(14), 0.42, 1.2, 52, 0.15);
        Timpani(s, At(14), 38, 0.75);
        Cymbal(s, At(17), 0.48, 1.6, 53, 0.15);
    }

    /// <summary>A timpani roll on <paramref name="midi"/> from <paramref name="start"/> for <paramref name="length"/> seconds, swelling between two levels.</summary>
    private static void Roll(MoonfallSynth s, double start, double length, int midi, double from, double to)
    {
        for (var t = 0.0; t < length; t += 1 / 12.0)
        {
            var progress = t / length;
            Timpani(s, start + t, midi, from + ((to - from) * progress * progress), 0.05, 0.3);
        }
    }

    private static void Ending(MoonfallSynth s)
    {
        // The held D major chord: strings, horns and the bass, three bars.
        var hold = (3 * Bar) - 0.3;
        foreach (var note in Voice(D, null, 57, 74, 4))
        {
            Strings(s, At(17), note, hold, 0.15, (note - 65) * 0.05, 0.06);
        }

        foreach (var note in Voice(D, null, 50, 66, 3))
        {
            Brass(s, At(17), note, 2 * Bar, 0.18, 0.3);
        }

        Strings(s, At(17), 38, hold, 0.45, 0, 0.04);
        Timpani(s, At(17), 38, 0.85, 0, 0.8);
        Roll(s, At(17, 1), 2 * Bar, 38, 0.18, 0.04);

        // The chime's fifth, high: D6, A6, then D7.
        Celesta(s, At(18, 1), 86, 0.45, -0.25, 0.9);
        Celesta(s, At(18, 2), 93, 0.45, 0.25, 0.9);
        Glass(s, At(19), MoonfallSynth.Hz(98), 0.3, 0.7, 0);
    }
}
