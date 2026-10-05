namespace Tsukimichi.Core.Moonfall.Sound;

/// <summary>
/// A rendered sound (plan v9 G8): 32-bit float PCM, interleaved when stereo, made once and played many times. The
/// samples are the player's to read, never to change.
/// </summary>
public sealed class MoonfallSoundBuffer
{
    private readonly float[] samples;

    public MoonfallSoundBuffer(float[] samples, int channels, int sampleRate)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentOutOfRangeException.ThrowIfLessThan(channels, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(channels, 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleRate, 8000);
        this.samples = samples;
        Channels = channels;
        SampleRate = sampleRate;
    }

    /// <summary>The samples, interleaved left and right when <see cref="Channels"/> is 2.</summary>
    public ReadOnlySpan<float> Samples => samples;

    /// <summary>1 (mono, panned when played) or 2 (stereo, the finale).</summary>
    public int Channels { get; }

    public int SampleRate { get; }

    /// <summary>Sample frames (one sample per channel).</summary>
    public int Frames => samples.Length / Channels;

    public double Seconds => Frames / (double)SampleRate;

    /// <summary>The largest absolute sample.</summary>
    public float Peak()
    {
        var peak = 0f;
        foreach (var s in samples)
        {
            peak = MathF.Max(peak, MathF.Abs(s));
        }

        return peak;
    }

    /// <summary>The peak in dB below full scale (−∞ for silence).</summary>
    public double PeakDbfs() => MoonfallSynth.ToDbfs(Peak());
}

/// <summary>A partial of a voice: its frequency as a ratio of the note's, its level, and how fast it fades against the note (1 the same, 0.5 twice as fast).</summary>
public readonly record struct MoonfallPartial(double Ratio, double Level, double Fade = 1);

/// <summary>
/// An ADSR envelope with exponential decay and release: <see cref="Attack"/> seconds up (a raised cosine, so it never
/// clicks), then a decay towards <see cref="Sustain"/> with time constant <see cref="Decay"/>, held until the note's
/// hold ends, then a release with time constant <see cref="Release"/>. A bell is Sustain 0: it only decays.
/// </summary>
public readonly record struct MoonfallEnvelope(double Attack, double Decay, double Sustain, double Release)
{
    /// <summary>A struck or plucked sound: a short attack and a free decay.</summary>
    public static MoonfallEnvelope Struck(double attack, double decay) => new(attack, decay, 0, decay);

    /// <summary>The level at <paramref name="t"/> seconds into a note held for <paramref name="hold"/> seconds.</summary>
    public double At(double t, double hold)
    {
        if (t < 0)
        {
            return 0;
        }

        if (t <= hold)
        {
            return Held(t);
        }

        return Held(hold) * Math.Exp(-(t - hold) / Math.Max(Release, 1e-4));
    }

    /// <summary>How long a note held for <paramref name="hold"/> sounds, to about −60 dB.</summary>
    public double Length(double hold) => hold + (7 * Math.Max(Release, 1e-4));

    private double Held(double t)
    {
        if (t < Attack)
        {
            return 0.5 - (0.5 * Math.Cos(Math.PI * t / Attack));
        }

        return Sustain + ((1 - Sustain) * Math.Exp(-(t - Attack) / Math.Max(Decay, 1e-4)));
    }
}

/// <summary>
/// Moonfall's synthesiser (plan v9 G8): additive voices with envelopes, filtered noise, pitch-dropping thumps and a
/// small hall reverb, mixed into a stereo buffer of fixed length. Pure and deterministic: no clock, no shared state, the
/// noise from a seeded <see cref="MoonfallRandom"/>, so the same calls render the same samples on every run.
/// </summary>
public sealed class MoonfallSynth
{
    private const int TableBits = 13;
    private const int TableSize = 1 << TableBits;
    private const int MaxPartials = 16;

    /// <summary>Samples per control step (envelopes, vibrato, filter glides): under a millisecond at 48 kHz.</summary>
    private const int ControlBlock = 32;

    /// <summary>One cycle of a sine, with a guard point, read with linear interpolation.</summary>
    private static readonly double[] SineTable = MakeSine();

    private readonly double[] left;
    private readonly double[] right;

    /// <param name="sampleRate">Samples per second.</param>
    /// <param name="seconds">The buffer's length; anything later is cut.</param>
    public MoonfallSynth(int sampleRate, double seconds)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleRate, 8000);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(seconds);
        SampleRate = sampleRate;
        var frames = (int)Math.Round(seconds * sampleRate);
        left = new double[frames];
        right = new double[frames];
    }

    public int SampleRate { get; }

    public int Frames => left.Length;

    /// <summary>The frequency of MIDI note <paramref name="midi"/> in equal temperament, A4 (69) = 440 Hz.</summary>
    public static double Hz(double midi) => 440.0 * Math.Pow(2, (midi - 69) / 12.0);

    /// <summary>An amplitude in dB below full scale.</summary>
    public static double ToDbfs(double amplitude) => amplitude <= 0 ? double.NegativeInfinity : 20 * Math.Log10(amplitude);

    /// <summary>A level in dB below full scale as an amplitude.</summary>
    public static double FromDbfs(double dbfs) => Math.Pow(10, dbfs / 20);

    /// <summary>
    /// A note: <paramref name="partials"/> over <paramref name="hz"/>, shaped by <paramref name="envelope"/> held for
    /// <paramref name="hold"/> seconds, at <paramref name="level"/>, placed at <paramref name="pan"/> (−1 left … 1 right),
    /// with an optional vibrato that eases in over 0.3 s. Partials above 90 % of Nyquist are left out.
    /// </summary>
    public void Note(double start, double hz, double hold, double level, ReadOnlySpan<MoonfallPartial> partials, in MoonfallEnvelope envelope, double pan = 0, double vibratoHz = 0, double vibratoCents = 0)
    {
        if (partials.Length > MaxPartials)
        {
            throw new ArgumentOutOfRangeException(nameof(partials), "at most 16 partials");
        }

        var first = Math.Max(0, (int)Math.Round(start * SampleRate));
        var last = Math.Min(Frames, (int)Math.Round((start + envelope.Length(hold)) * SampleRate));
        if (first >= last || hz <= 0)
        {
            return;
        }

        var (gl, gr) = Pan(pan);
        Span<double> phase = stackalloc double[MaxPartials];
        Span<double> step = stackalloc double[MaxPartials];
        Span<double> gain = stackalloc double[MaxPartials];
        Span<double> fade = stackalloc double[MaxPartials];
        var count = 0;
        var nyquist = SampleRate * 0.45;
        foreach (var p in partials)
        {
            if (hz * p.Ratio >= nyquist || p.Level == 0)
            {
                continue;
            }

            // A fixed phase per partial (not 0) keeps the partials from all peaking together at the onset.
            phase[count] = (count * 0.137) % 1.0;
            step[count] = hz * p.Ratio / SampleRate;
            gain[count] = p.Level;
            fade[count] = 1 / Math.Max(p.Fade, 0.05);
            count++;
        }

        // The envelope and the vibrato run at a control rate: worked out at each block's ends and interpolated between.
        var vibrato = vibratoHz > 0 && vibratoCents > 0;
        Span<double> from = stackalloc double[MaxPartials];
        Span<double> to = stackalloc double[MaxPartials];
        for (var block = first; block < last; block += ControlBlock)
        {
            var end = Math.Min(last, block + ControlBlock);
            var t0 = (block - first) / (double)SampleRate;
            var t1 = (end - first) / (double)SampleRate;
            var e0 = envelope.At(t0, hold);
            var e1 = envelope.At(t1, hold);
            for (var k = 0; k < count; k++)
            {
                from[k] = gain[k] * level * (fade[k] == 1 ? e0 : Math.Pow(e0, fade[k]));
                to[k] = gain[k] * level * (fade[k] == 1 ? e1 : Math.Pow(e1, fade[k]));
            }

            var tm = (t0 + t1) * 0.5;
            var bend = vibrato ? Math.Pow(2, vibratoCents * Math.Min(1, tm / 0.3) * Sine(vibratoHz * tm) / 1200) : 1;
            var span = end - block;
            for (var i = block; i < end; i++)
            {
                var f = (i - block) / (double)span;
                var s = 0.0;
                for (var k = 0; k < count; k++)
                {
                    s += (from[k] + ((to[k] - from[k]) * f)) * Sine(phase[k]);
                    phase[k] += step[k] * bend;
                    if (phase[k] >= 1)
                    {
                        phase[k] -= 1;
                    }
                }

                left[i] += s * gl;
                right[i] += s * gr;
            }
        }
    }

    /// <summary>
    /// Noise through a one-pole high-pass at <paramref name="highHz"/> and a one-pole low-pass that glides from
    /// <paramref name="lowHz"/> to <paramref name="lowHzEnd"/> over the sound, shaped by <paramref name="envelope"/>.
    /// The noise comes from <paramref name="seed"/>, so it is the same every render.
    /// </summary>
    public void Noise(double start, double hold, double level, in MoonfallEnvelope envelope, double highHz, double lowHz, double lowHzEnd, ulong seed, double pan = 0)
    {
        var first = Math.Max(0, (int)Math.Round(start * SampleRate));
        var length = envelope.Length(hold);
        var last = Math.Min(Frames, (int)Math.Round((start + length) * SampleRate));
        if (first >= last)
        {
            return;
        }

        var (gl, gr) = Pan(pan);
        var random = new MoonfallRandom(seed);
        var high = 0.0;
        var low = 0.0;
        var a = OnePole(highHz);
        var b = 0.0;
        var e = 0.0;
        for (var i = first; i < last; i++)
        {
            if ((i - first) % ControlBlock == 0)
            {
                var t = (i - first) / (double)SampleRate;
                b = OnePole(lowHz * Math.Pow(lowHzEnd / lowHz, Math.Min(1, t / length)));
                e = level * envelope.At(t, hold);
            }

            var x = ((random.NextBits() >> 11) * (1.0 / (1UL << 53)) * 2) - 1;
            high += a * (x - high);
            low += b * (x - high - low);
            var s = low * e;
            left[i] += s * gl;
            right[i] += s * gr;
        }
    }

    /// <summary>A sine whose pitch falls from <paramref name="fromHz"/> to <paramref name="toHz"/> (a drum's body, a launch's thump), with a struck envelope.</summary>
    public void Thump(double start, double fromHz, double toHz, double drop, double decay, double level, double pan = 0)
    {
        var envelope = MoonfallEnvelope.Struck(0.002, decay);
        var hold = 5 * decay;
        var first = Math.Max(0, (int)Math.Round(start * SampleRate));
        var last = Math.Min(Frames, (int)Math.Round((start + envelope.Length(hold)) * SampleRate));
        var (gl, gr) = Pan(pan);
        var phase = 0.0;
        for (var i = first; i < last; i++)
        {
            var t = (i - first) / (double)SampleRate;
            var hz = toHz + ((fromHz - toHz) * Math.Exp(-t / Math.Max(drop, 1e-4)));
            var s = level * envelope.At(t, hold) * Sine(phase);
            phase += hz / SampleRate;
            phase -= Math.Floor(phase);
            left[i] += s * gl;
            right[i] += s * gr;
        }
    }

    /// <summary>
    /// A conductor's dynamics over the mix so far: a gain in dB at each of <paramref name="points"/>' times, joined by
    /// straight lines (held flat before the first point and after the last).
    /// </summary>
    public void Dynamics(ReadOnlySpan<(double Seconds, double Db)> points)
    {
        if (points.IsEmpty)
        {
            return;
        }

        var next = 0;
        for (var i = 0; i < Frames; i++)
        {
            var t = i / (double)SampleRate;
            while (next < points.Length && points[next].Seconds <= t)
            {
                next++;
            }

            double db;
            if (next == 0)
            {
                db = points[0].Db;
            }
            else if (next == points.Length)
            {
                db = points[^1].Db;
            }
            else
            {
                var (t0, d0) = points[next - 1];
                var (t1, d1) = points[next];
                db = d0 + ((d1 - d0) * (t - t0) / (t1 - t0));
            }

            var g = FromDbfs(db);
            left[i] *= g;
            right[i] *= g;
        }
    }

    /// <summary>
    /// A small hall (Schroeder–Moorer, as in Freeverb: six damped combs and two all-passes per side, the right side's
    /// delays a little longer for width) over the whole mix: <paramref name="wet"/> of reverb on top of the dry sound.
    /// </summary>
    public void Reverb(double wet, double room = 0.8, double damp = 0.35)
    {
        if (wet <= 0)
        {
            return;
        }

        int[] combs = [1116, 1188, 1277, 1356, 1422, 1557];
        int[] allpasses = [556, 441];
        const int Spread = 23;
        var scale = SampleRate / 44100.0;
        var outL = Run(combs, allpasses, 0, scale, room, damp);
        var outR = Run(combs, allpasses, Spread, scale, room, damp);
        for (var i = 0; i < Frames; i++)
        {
            left[i] += outL[i] * wet;
            right[i] += outR[i] * wet;
        }
    }

    /// <summary>
    /// The finished sound: a 4 ms fade at the very end (no click when a buffer stops), scaled so its peak sits at
    /// <paramref name="peakDbfs"/> (at most −1 dBFS), as mono (the two sides averaged) or stereo.
    /// </summary>
    public MoonfallSoundBuffer Finish(double peakDbfs, bool stereo)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(peakDbfs, -1.0);
        var tail = Math.Min(Frames, (int)(0.004 * SampleRate));
        for (var k = 0; k < tail; k++)
        {
            var g = k / (double)tail;
            left[Frames - 1 - k] *= g;
            right[Frames - 1 - k] *= g;
        }

        var peak = 0.0;
        for (var i = 0; i < Frames; i++)
        {
            peak = stereo ? Math.Max(peak, Math.Max(Math.Abs(left[i]), Math.Abs(right[i]))) : Math.Max(peak, Math.Abs((left[i] + right[i]) * 0.5));
        }

        // A hair under the target, so float rounding can never lift the peak above it.
        var gain = peak > 0 ? FromDbfs(peakDbfs) * 0.999 / peak : 0;
        var channels = stereo ? 2 : 1;
        var samples = new float[Frames * channels];
        for (var i = 0; i < Frames; i++)
        {
            if (stereo)
            {
                samples[2 * i] = (float)(left[i] * gain);
                samples[(2 * i) + 1] = (float)(right[i] * gain);
            }
            else
            {
                samples[i] = (float)((left[i] + right[i]) * 0.5 * gain);
            }
        }

        return new MoonfallSoundBuffer(samples, channels, SampleRate);
    }

    /// <summary>sin(2π·<paramref name="cycles"/>) from the table.</summary>
    internal static double Sine(double cycles)
    {
        var x = (cycles - Math.Floor(cycles)) * TableSize;
        var i = (int)x;
        var f = x - i;
        return SineTable[i] + ((SineTable[i + 1] - SineTable[i]) * f);
    }

    /// <summary>Constant-power gains for <paramref name="pan"/> (−1 … 1).</summary>
    internal static (double Left, double Right) Pan(double pan)
    {
        var angle = (Math.Clamp(pan, -1, 1) + 1) * Math.PI / 4;
        return (Math.Cos(angle), Math.Sin(angle));
    }

    private double OnePole(double hz) => 1 - Math.Exp(-2 * Math.PI * Math.Min(hz, SampleRate * 0.45) / SampleRate);

    private double[] Run(int[] combs, int[] allpasses, int spread, double scale, double room, double damp)
    {
        var output = new double[Frames];
        foreach (var length in combs)
        {
            var buffer = new double[Math.Max(1, (int)((length + spread) * scale))];
            var index = 0;
            var store = 0.0;
            for (var i = 0; i < Frames; i++)
            {
                var input = (left[i] + right[i]) * 0.015;
                var y = buffer[index];
                store = (y * (1 - damp)) + (store * damp);
                buffer[index] = input + (store * room);
                index = index + 1 == buffer.Length ? 0 : index + 1;
                output[i] += y;
            }
        }

        foreach (var length in allpasses)
        {
            var buffer = new double[Math.Max(1, (int)((length + spread) * scale))];
            var index = 0;
            for (var i = 0; i < Frames; i++)
            {
                var b = buffer[index];
                var y = b - output[i];
                buffer[index] = output[i] + (b * 0.5);
                index = index + 1 == buffer.Length ? 0 : index + 1;
                output[i] = y;
            }
        }

        // Freeverb's wet scale.
        for (var i = 0; i < Frames; i++)
        {
            output[i] *= 3;
        }

        return output;
    }

    private static double[] MakeSine()
    {
        var table = new double[TableSize + 1];
        for (var i = 0; i <= TableSize; i++)
        {
            table[i] = Math.Sin(2 * Math.PI * i / TableSize);
        }

        return table;
    }
}
