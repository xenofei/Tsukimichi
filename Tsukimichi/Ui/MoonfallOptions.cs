using System;
using Tsukimichi.Config;

namespace Tsukimichi.Ui;

/// <summary>Moonfall's own options, as the window reads and sets them (the plugin's configuration, or a stand-in offline).</summary>
public interface IMoonfallOptions
{
    /// <summary>The colour-blind assist: a mark on each peg kind (decision 27: off by default).</summary>
    bool PegMarks { get; set; }

    /// <summary>Whether the peg marks' one-time hint has been seen.</summary>
    bool PegMarksHintSeen { get; set; }

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

    public void Save() => save();
}
