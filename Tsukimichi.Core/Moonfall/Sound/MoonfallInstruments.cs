namespace Tsukimichi.Core.Moonfall.Sound;

/// <summary>
/// The voices Moonfall's sounds and finale are built from (plan v9 G8), each a recipe of partials and an envelope on
/// <see cref="MoonfallSynth"/>. Pitches are MIDI notes (D4 = 62) unless a parameter says hertz.
/// </summary>
internal static class MoonfallInstruments
{
    /// <summary>
    /// Glass: the peg chime's timbre. A strong fundamental, a soft octave and twelfth that die first, and one slightly
    /// inharmonic partial (×4.2) for the glassy edge, so it reads as a struck glass or small bell rather than a beep.
    /// </summary>
    private static readonly MoonfallPartial[] GlassPartials = [new(1, 1), new(2, 0.18, 0.5), new(3, 0.05, 0.35), new(4.2, 0.04, 0.2)];

    private static readonly MoonfallPartial[] CelestaPartials = [new(1, 1), new(2, 0.32, 0.55), new(3, 0.08, 0.4), new(4, 0.05, 0.3), new(5.93, 0.03, 0.2)];

    private static readonly MoonfallPartial[] HarpPartials = [new(1, 1), new(2, 0.45, 0.7), new(3, 0.22, 0.5), new(4, 0.11, 0.4), new(5, 0.06, 0.3), new(6, 0.03, 0.25)];

    private static readonly MoonfallPartial[] FlutePartials = [new(1, 1), new(2, 0.28), new(3, 0.1), new(4, 0.03)];

    /// <summary>A sawtooth's 1/n series to the seventh partial: a bowed string's bright but soft spectrum.</summary>
    private static readonly MoonfallPartial[] StringPartials = [new(1, 1), new(2, 0.5), new(3, 0.33), new(4, 0.22), new(5, 0.15), new(6, 0.1), new(7, 0.07)];

    /// <summary>A brass spectrum: the upper partials enter late (Fade below 1 raises them to a power of the envelope), the "bloom" of a horn.</summary>
    private static readonly MoonfallPartial[] BrassPartials = [new(1, 1), new(2, 0.75), new(3, 0.55, 0.8), new(4, 0.38, 0.7), new(5, 0.25, 0.6), new(6, 0.15, 0.5), new(7, 0.09, 0.45), new(8, 0.05, 0.4)];

    /// <summary>A kettle drum's slightly inharmonic modes.</summary>
    private static readonly MoonfallPartial[] TimpaniPartials = [new(1, 1), new(1.5, 0.5, 0.6), new(1.98, 0.32, 0.5), new(2.44, 0.18, 0.4)];

    private static readonly MoonfallPartial[] Sine = [new(1, 1)];

    /// <summary>A struck glass at <paramref name="hz"/>, with a second copy 5 cents sharp at a quarter level: a slow shimmer, like moonlight on water.</summary>
    public static void Glass(MoonfallSynth s, double start, double hz, double level, double decay, double pan = 0)
    {
        var envelope = MoonfallEnvelope.Struck(0.004, decay);
        s.Note(start, hz, 5 * decay, level, GlassPartials, envelope, pan);
        s.Note(start, hz * Math.Pow(2, 5 / 1200.0), 5 * decay, level * 0.25, Sine, envelope, pan);
    }

    public static void Celesta(MoonfallSynth s, double start, double midi, double level, double pan = 0, double decay = 0.6) =>
        s.Note(start, MoonfallSynth.Hz(midi), 5 * decay, level, CelestaPartials, MoonfallEnvelope.Struck(0.003, decay), pan);

    public static void Harp(MoonfallSynth s, double start, double midi, double level, double pan = 0, double decay = 0.7) =>
        s.Note(start, MoonfallSynth.Hz(midi), 5 * decay, level, HarpPartials, MoonfallEnvelope.Struck(0.004, decay), pan);

    /// <summary>A pizzicato: the harp's spectrum, short.</summary>
    public static void Pizzicato(MoonfallSynth s, double start, double midi, double level, double pan = 0) => Harp(s, start, midi, level, pan, 0.16);

    /// <summary>A flute: a gentle attack, a light vibrato and a breath of noise around the note.</summary>
    public static void Flute(MoonfallSynth s, double start, double midi, double hold, double level, double pan = 0)
    {
        var hz = MoonfallSynth.Hz(midi);
        var envelope = new MoonfallEnvelope(0.04, 0.2, 0.82, 0.12);
        s.Note(start, hz, hold, level, FlutePartials, envelope, pan, 5.0, 9);
        s.Noise(start, hold, level * 0.35, envelope, hz * 0.8, hz * 2.5, hz * 2.5, (ulong)(midi * 7919) + (ulong)(start * 1000), pan);
    }

    /// <summary>A string section on one note: two players 6 cents apart with a little vibrato, a bowed attack and release.</summary>
    public static void Strings(MoonfallSynth s, double start, double midi, double hold, double level, double pan = 0, double attack = 0.1)
    {
        var hz = MoonfallSynth.Hz(midi);
        var envelope = new MoonfallEnvelope(attack, 0.4, 0.88, 0.32);
        var cents = Math.Pow(2, 6 / 1200.0);
        s.Note(start, hz * cents, hold, level * 0.5, StringPartials, envelope, pan - 0.08, 5.3, 11);
        s.Note(start + 0.012, hz / cents, hold, level * 0.5, StringPartials, envelope, pan + 0.08, 4.9, 11);
    }

    /// <summary>A horn: a soft attack whose upper partials bloom in after the fundamental.</summary>
    public static void Brass(MoonfallSynth s, double start, double midi, double hold, double level, double pan = 0) =>
        s.Note(start, MoonfallSynth.Hz(midi), hold, level, BrassPartials, new MoonfallEnvelope(0.035, 0.25, 0.8, 0.18), pan, 4.6, 6);

    /// <summary>A kettle drum: its modes and a soft stick noise.</summary>
    public static void Timpani(MoonfallSynth s, double start, double midi, double level, double pan = 0, double decay = 0.55)
    {
        s.Note(start, MoonfallSynth.Hz(midi), 5 * decay, level, TimpaniPartials, MoonfallEnvelope.Struck(0.004, decay), pan);
        s.Noise(start, 0.02, level * 0.25, MoonfallEnvelope.Struck(0.001, 0.012), 200, 1800, 900, (ulong)(start * 10007) + 3, pan);
    }

    /// <summary>A suspended cymbal: bright noise with a long decay.</summary>
    public static void Cymbal(MoonfallSynth s, double start, double level, double decay, ulong seed, double pan = 0, double attack = 0.002) =>
        s.Noise(start, 5 * decay, level, new MoonfallEnvelope(attack, decay, 0, decay), 3500, 12000, 7000, seed, pan);

    /// <summary>One stroke of a snare roll: a short band of noise.</summary>
    public static void Snare(MoonfallSynth s, double start, double level, ulong seed, double pan = 0) =>
        s.Noise(start, 0.09, level, MoonfallEnvelope.Struck(0.0015, 0.035), 350, 5000, 2500, seed, pan);
}
