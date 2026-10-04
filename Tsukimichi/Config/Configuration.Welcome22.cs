using System.Collections.Generic;

namespace Tsukimichi.Config;

/// <summary>
/// 1.22.0 "Welcome home" settings for updates and Umbra (plan v8 U1, M1, M3): the update check and its chat line, the
/// version the player chose Later for, the Follow Umbra palette, and keeping clear of Umbra's toolbar. Drawn in
/// Settings › About (<c>Ui/ConfigWindow.Updates.cs</c>, <c>Ui/ConfigWindow.Umbra.cs</c>) and Settings › Themes.
/// </summary>
public sealed partial class Configuration
{
    /// <summary>
    /// "Tell me when a new version is ready" (U1): ask Dalamud at login and every 3 hours whether a newer Tsukimichi is
    /// waiting. On by default. Dalamud reads the repository data it already refreshes; Tsukimichi never goes online.
    /// </summary>
    public bool UpdateCheck { get; set; } = true;

    /// <summary>"Also say it in chat" (U1, decision 6): one line in Tsukimichi's echo channel when a version is ready. Off by default.</summary>
    public bool UpdateChatLine { get; set; }

    /// <summary>The newest version the player chose Later (×) for; the note stays hidden until a newer one. Empty for none.</summary>
    public string UpdateDismissedVersion { get; set; } = string.Empty;

    /// <summary>The newest version "Also say it in chat" already said, so a login never repeats it. Empty for none.</summary>
    public string UpdateChatSaidVersion { get; set; } = string.Empty;

    /// <summary>
    /// The Follow Umbra palette (M3, decision 4): the window's colours from Umbra's colour profile, read-only, Night when it
    /// can't be read. Off by default; chosen with the palette tiles in Settings › Themes.
    /// </summary>
    public bool FollowUmbraPalette { get; set; }

    /// <summary>
    /// The toolbar height Tsukimichi assumes while Umbra runs and its settings can't be read (M3), in logical px: 32 (Umbra's
    /// default bar) unless the player sets it; 0 assumes no bar.
    /// </summary>
    public int UmbraAssumedBarHeight { get; set; } = Core.Umbra.UmbraToolbar.DefaultHeight;

    /// <summary>
    /// The player's own places of surfaces the clearance moved (M3, "The saved place: never rewritten"), by surface
    /// ("todo"), as [x, y] in screen px: given back when Umbra's bar leaves, forgotten when the player moves the surface.
    /// </summary>
    public Dictionary<string, float[]> UmbraPlaces { get; set; } = [];
}
