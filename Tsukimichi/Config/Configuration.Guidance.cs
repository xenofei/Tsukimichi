namespace Tsukimichi.Config;

/// <summary>
/// 1.21.0 guidance (plan v7 P8): "Say what's next in chat", drawn under Settings › In game › Chat
/// (<c>Ui/ConfigWindow.Guidance.cs</c>).
/// </summary>
public sealed partial class Configuration
{
    /// <summary>
    /// After each step or quest the logged-in character finishes, print the <c>/tsuki next</c> line (plain sentences for
    /// text-to-speech), at most one line every 10 s (<c>Game.ChatNotifier</c>). Off by default.
    /// </summary>
    public bool SayWhatsNext { get; set; }
}
