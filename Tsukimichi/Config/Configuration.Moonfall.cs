namespace Tsukimichi.Config;

/// <summary>
/// Moonfall's settings (feature plan v9 G8): its sound's volume, set in Moonfall's quick settings on the pause screen
/// (<c>Ui/MoonfallWindow.Sound.cs</c>) and played by <c>Game/MoonfallAudio.cs</c>.
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
}
