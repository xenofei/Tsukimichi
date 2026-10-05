namespace Tsukimichi.Core.Moonfall.Sound;

/// <summary>
/// Every Moonfall sound rendered once at one sample rate (plan v9 G8), so playing one is a lookup: nothing is
/// rendered or allocated per hit. About 20 MB at 48 kHz, most of it the finale; made off the frame when the window opens.
/// </summary>
public sealed class MoonfallSoundBank
{
    private readonly MoonfallSoundBuffer[][] buffers;

    private MoonfallSoundBank(int sampleRate, MoonfallSoundBuffer[][] buffers)
    {
        SampleRate = sampleRate;
        this.buffers = buffers;
    }

    public int SampleRate { get; }

    /// <summary>Renders every sound and variant at <paramref name="sampleRate"/>; stops early (null) when <paramref name="cancel"/> is set.</summary>
    public static MoonfallSoundBank? Render(int sampleRate, CancellationToken cancel = default)
    {
        var sounds = Enum.GetValues<MoonfallSound>();
        var buffers = new MoonfallSoundBuffer[sounds.Length][];
        buffers[(int)MoonfallSound.None] = [];
        foreach (var sound in MoonfallSounds.All)
        {
            var variants = new MoonfallSoundBuffer[MoonfallSounds.Variants(sound)];
            for (var v = 0; v < variants.Length; v++)
            {
                if (cancel.IsCancellationRequested)
                {
                    return null;
                }

                variants[v] = MoonfallSounds.Render(sound, v, sampleRate);
            }

            buffers[(int)sound] = variants;
        }

        return new MoonfallSoundBank(sampleRate, buffers);
    }

    /// <summary><paramref name="sound"/>'s <paramref name="variant"/> (clamped to the ones it has); null for <see cref="MoonfallSound.None"/>.</summary>
    public MoonfallSoundBuffer? Get(MoonfallSound sound, int variant = 0)
    {
        var index = (int)sound;
        if (index <= 0 || index >= buffers.Length || buffers[index].Length == 0)
        {
            return null;
        }

        var variants = buffers[index];
        return variants[Math.Clamp(variant, 0, variants.Length - 1)];
    }
}
