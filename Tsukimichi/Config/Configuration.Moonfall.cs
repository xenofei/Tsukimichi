using Tsukimichi.Core.Ui;

namespace Tsukimichi.Config;

/// <summary>
/// Moonfall's settings (feature plan v9): its sound's volume (G8), set in Moonfall's quick settings on the pause screen
/// (<c>Ui/MoonfallWindow.Sound.cs</c>) and played by <c>Game/MoonfallAudio.cs</c>; and the colour-blind assist and
/// whether its first-run hint has been seen (spec-rich2.md §4, decision 27), set in Moonfall's own Options
/// (Ui/MoonfallWindow.cs).
/// </summary>
public sealed partial class Configuration
{
    /// <summary>The volume Moonfall starts at (the pause screen's mock: "Sound 70%").</summary>
    public const int DefaultMoonfallSoundPercent = 70;

    /// <summary>
    /// Moonfall's sound, 0 (off) to 100 % in steps of 10, on top of the game's master volume. 70 by default. Read clamped,
    /// so a hand-edited value out of range plays as the nearest end.
    /// </summary>
    public int MoonfallSoundPercent { get; set; } = DefaultMoonfallSoundPercent;

    /// <summary>
    /// "Peg marks": a crescent on orange, a leaf on green and a four-point star on purple, so the kinds differ by more
    /// than hue. Off by default, with a one-time hint (decision 27).
    /// </summary>
    public bool MoonfallPegMarks { get; set; }

    /// <summary>Whether the peg marks' first-run hint has been seen (it shows once, then never again).</summary>
    public bool MoonfallPegMarksHintSeen { get; set; }

    /// <summary>
    /// Moonfall's Decoration, apart from Tsukimichi's own (the owner's answer: Full by default): Full, Simple (Quiet) or
    /// Off (Plain). Reduce motion, Tsukimichi's own setting, still means still whatever this is.
    /// </summary>
    public Flair MoonfallDecoration { get; set; } = Flair.Full;
}
