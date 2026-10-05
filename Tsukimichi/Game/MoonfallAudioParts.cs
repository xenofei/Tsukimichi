using System;
using System.Runtime.InteropServices;
using System.Threading;
using NAudio.Wave;
using Tsukimichi.Core.Moonfall.Sound;

namespace Tsukimichi.Game;

/// <summary>
/// One playing sound in Moonfall's mixer (plan v9 G8): a cached <see cref="MoonfallSoundBuffer"/> read from start to
/// end, mono panned or stereo, with an optional fade-out. Pooled: <see cref="MoonfallAudio"/> starts the same few
/// voices again and again, so a hit allocates nothing.
/// <para>
/// Threads: <see cref="Start"/> and <see cref="FadeOut"/> come from the framework thread, <see cref="Read"/> from the
/// audio thread inside <c>MixingSampleProvider</c>'s lock. A voice goes inactive only in the read that returns short,
/// which is the read after which the mixer drops it, under that same lock; so a voice seen inactive is out of the
/// mixer, or leaves it before the mixer's lock (which adding it again takes) is free.
/// </para>
/// </summary>
internal sealed class MoonfallVoice : ISampleProvider
{
    private MoonfallSoundBuffer? buffer;
    private int position;
    private float left;
    private float right;
    private float level;
    private int fadeTotal;
    private int fadeLeft;
    private int pendingFade;
    private int active;

    public MoonfallVoice(WaveFormat format)
    {
        WaveFormat = format;
    }

    public WaveFormat WaveFormat { get; }

    /// <summary>Counts starts, so a stop meant for an earlier sound never reaches a later one on the same voice.</summary>
    public int Serial { get; private set; }

    /// <summary>When it started, in starts: the oldest is stolen first.</summary>
    public long Stamp { get; private set; }

    /// <summary>The sound it plays now.</summary>
    public MoonfallSound Sound { get; private set; }

    public bool Active => Volatile.Read(ref active) != 0;

    /// <summary>Starts <paramref name="sound"/>'s buffer at <paramref name="pan"/> (mono only) and <paramref name="gain"/>; the voice must be out of the mixer.</summary>
    public void Start(MoonfallSound sound, MoonfallSoundBuffer sample, float pan, float gain, long stamp)
    {
        buffer = sample;
        Sound = sound;
        position = 0;
        fadeTotal = 0;
        fadeLeft = 0;
        Volatile.Write(ref pendingFade, 0);
        level = gain;
        var angle = (Math.Clamp(pan, -1f, 1f) + 1f) * MathF.PI / 4f;

        // Constant power, normalised so a centred mono sound plays at its own level in each ear.
        left = gain * MathF.Cos(angle) * MathF.Sqrt(2f);
        right = gain * MathF.Sin(angle) * MathF.Sqrt(2f);
        Stamp = stamp;
        Serial++;
        Volatile.Write(ref active, 1);
    }

    /// <summary>Fades it out over <paramref name="frames"/> and ends it.</summary>
    public void FadeOut(int frames) => Volatile.Write(ref pendingFade, Math.Max(1, frames));

    public int Read(float[] dest, int offset, int count)
    {
        var sample = buffer;
        if (sample is null || Volatile.Read(ref active) == 0)
        {
            return 0;
        }

        var fade = Interlocked.Exchange(ref pendingFade, 0);
        if (fade > 0 && (fadeLeft == 0 || fade < fadeLeft))
        {
            fadeTotal = fade;
            fadeLeft = fade;
        }

        var samples = sample.Samples;
        var stereo = sample.Channels == 2;
        var frames = sample.Frames;
        var wanted = count / 2;
        var n = 0;
        while (n < wanted && position < frames)
        {
            float l;
            float r;
            if (stereo)
            {
                l = samples[2 * position] * level;
                r = samples[(2 * position) + 1] * level;
            }
            else
            {
                l = samples[position] * left;
                r = samples[position] * right;
            }

            position++;
            if (fadeTotal > 0)
            {
                var g = fadeLeft / (float)fadeTotal;
                l *= g;
                r *= g;
                if (--fadeLeft <= 0)
                {
                    position = frames;
                }
            }

            dest[offset + (2 * n)] = l;
            dest[offset + (2 * n) + 1] = r;
            n++;
        }

        var read = n * 2;
        if (read < count)
        {
            // The mixer drops an input that returns short, in this same pass.
            Volatile.Write(ref active, 0);
        }

        return read;
    }
}

/// <summary>
/// The output stage between Moonfall's mixer and WASAPI (plan v9 G8): the volume (gliding, so a change never clicks),
/// the pause freeze (it fades out, then stops reading the mixer, so every sound and the music hold their place and go
/// on where they were), and a soft limiter over 0.8, so a pile of chimes over the finale never clips. 32-bit float
/// stereo out; reads into a buffer of its own, so the audio thread allocates nothing.
/// </summary>
internal sealed class MoonfallMaster : IWaveProvider
{
    private const float Knee = 0.8f;

    private readonly ISampleProvider source;
    private float[] scratch = new float[4096];
    private float gain;
    private volatile float target;
    private volatile float rampPerFrame;
    private volatile bool frozen;

    public MoonfallMaster(ISampleProvider source)
    {
        this.source = source;
        WaveFormat = source.WaveFormat;
    }

    public WaveFormat WaveFormat { get; }

    /// <summary>Whether a new sound would be heard: not frozen and above silence.</summary>
    public bool Audible => !frozen && target > 0f;

    /// <summary>Glides to <paramref name="level"/> (0 … 1) over <paramref name="seconds"/>; while <paramref name="freeze"/> it fades out and then holds every sound where it is.</summary>
    public void Set(float level, bool freeze, double seconds)
    {
        target = Math.Clamp(level, 0f, 1f);
        frozen = freeze;
        rampPerFrame = (float)(1.0 / Math.Max(1.0, seconds * WaveFormat.SampleRate));
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        var output = MemoryMarshal.Cast<byte, float>(buffer.AsSpan(offset, count));
        var goal = frozen ? 0f : target;
        if (frozen && gain <= 0f)
        {
            output.Clear();
            return count;
        }

        if (scratch.Length < output.Length)
        {
            scratch = new float[output.Length];
        }

        var read = source.Read(scratch, 0, output.Length);
        var step = rampPerFrame;
        for (var i = 0; i + 1 < output.Length; i += 2)
        {
            if (gain < goal)
            {
                gain = MathF.Min(goal, gain + step);
            }
            else if (gain > goal)
            {
                gain = MathF.Max(goal, gain - step);
            }

            output[i] = i < read ? Limit(scratch[i] * gain) : 0f;
            output[i + 1] = i + 1 < read ? Limit(scratch[i + 1] * gain) : 0f;
        }

        return count;
    }

    private static float Limit(float x)
    {
        var a = MathF.Abs(x);
        if (a <= Knee)
        {
            return x;
        }

        var limited = Knee + ((1f - Knee) * MathF.Tanh((a - Knee) / (1f - Knee)));
        return MathF.CopySign(limited, x);
    }
}
