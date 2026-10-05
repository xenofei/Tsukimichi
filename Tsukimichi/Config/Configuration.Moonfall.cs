namespace Tsukimichi.Config;

/// <summary>
/// Moonfall's options (feature plan v9; spec-rich2.md §4, decision 27): the colour-blind assist and whether its
/// first-run hint has been seen. Set in Moonfall's own Options (Ui/MoonfallWindow.Rich.cs).
/// </summary>
public sealed partial class Configuration
{
    /// <summary>
    /// "Peg marks": a crescent on orange, a leaf on green and a four-point star on purple, so the kinds differ by more
    /// than hue. Off by default, with a one-time hint (decision 27).
    /// </summary>
    public bool MoonfallPegMarks { get; set; }

    /// <summary>Whether the peg marks' first-run hint has been seen (it shows once, then never again).</summary>
    public bool MoonfallPegMarksHintSeen { get; set; }
}
