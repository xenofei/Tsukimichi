namespace Tsukimichi.Config;

/// <summary>
/// 1.22.0 What's new (spec-1.22 W1, W3): whether the popup shows after an update. The version it was last seen for is
/// <see cref="LastSeenVersion"/>. Drawn under Settings › Advanced › What's new (<c>Ui/ConfigWindow.WhatsNew.cs</c>).
/// </summary>
public sealed partial class Configuration
{
    /// <summary>
    /// After an update, What's new shows once at the first quiet moment. On by default; off records each new version
    /// silently, and the notes stay in Settings.
    /// </summary>
    public bool ShowWhatsNewAfterUpdate { get; set; } = true;
}
