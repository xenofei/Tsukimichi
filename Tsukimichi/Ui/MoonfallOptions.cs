using System;
using Tsukimichi.Config;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>Moonfall's own options, as the window reads and sets them (the plugin's configuration, or a stand-in offline).</summary>
public interface IMoonfallOptions
{
    /// <summary>The colour-blind assist: a mark on each peg kind (decision 27: off by default).</summary>
    bool PegMarks { get; set; }

    /// <summary>Whether the peg marks' one-time hint has been seen.</summary>
    bool PegMarksHintSeen { get; set; }

    /// <summary>
    /// Moonfall's Decoration (spec-rich2.md §4, rule 4; the owner's answer: Full by default): Full, Simple (Tsukimichi's
    /// Quiet: halos, beams and the lantern only) or Off (Plain: still, flat inks).
    /// </summary>
    Flair Decoration { get; set; }

    /// <summary>Reduce motion: Tsukimichi's own setting (it always means still, over Decoration).</summary>
    bool ReduceMotion { get; set; }

    /// <summary>Moonfall's sound, 0 (off) to 100 % in steps of 10, on top of the game's master volume (the sound reads it each frame).</summary>
    int SoundPercent { get; set; }

    /// <summary>Saves a change.</summary>
    void Save();
}

/// <summary>Moonfall's options kept in the plugin's configuration (<see cref="Configuration.MoonfallPegMarks"/>).</summary>
internal sealed class MoonfallConfigOptions(Configuration settings, Action save) : IMoonfallOptions
{
    private readonly Configuration settings = settings ?? throw new ArgumentNullException(nameof(settings));
    private readonly Action save = save ?? throw new ArgumentNullException(nameof(save));

    public bool PegMarks
    {
        get => settings.MoonfallPegMarks;
        set => settings.MoonfallPegMarks = value;
    }

    public bool PegMarksHintSeen
    {
        get => settings.MoonfallPegMarksHintSeen;
        set => settings.MoonfallPegMarksHintSeen = value;
    }

    public Flair Decoration
    {
        get => settings.MoonfallDecoration;
        set => settings.MoonfallDecoration = value;
    }

    public bool ReduceMotion
    {
        get => settings.ReduceMotion;
        set
        {
            // The plugin's own Reduce motion, set from Moonfall: chosen now, so it no longer follows the OS.
            settings.ReduceMotion = value;
            settings.ReduceMotionChosen = true;
        }
    }

    public int SoundPercent
    {
        get => Math.Clamp(settings.MoonfallSoundPercent, 0, 100);
        set => settings.MoonfallSoundPercent = Math.Clamp(value / 10 * 10, 0, 100);
    }

    public void Save() => save();
}
