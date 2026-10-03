using Dalamud.Interface;

namespace Tsukimichi.Ui;

/// <summary>
/// The FontAwesome glyphs of the actions the game has no icon for (docs/design/v7/ui/spec-1.15.md B1: Stop, Pin, Path,
/// Link, Copy, Report, More), shared by every icon-and-label button so one action wears one glyph everywhere. Actions
/// with a game icon take it from <see cref="Core.Ui.ActionIcons"/> instead.
/// </summary>
internal static class ActionGlyphs
{
    /// <summary>Stop a hand-off (Walk, Go to giver, a followed route).</summary>
    public static readonly string Stop = FontAwesomeIcon.Stop.ToIconString();

    /// <summary>Pin or unpin.</summary>
    public static readonly string Pin = FontAwesomeIcon.Thumbtack.ToIconString();

    /// <summary>Route to this, and the Route window's own sign.</summary>
    public static readonly string Route = FontAwesomeIcon.MapSigns.ToIconString();

    /// <summary>Follow a route.</summary>
    public static readonly string Follow = FontAwesomeIcon.Route.ToIconString();

    /// <summary>Copy to the clipboard.</summary>
    public static readonly string Copy = FontAwesomeIcon.Copy.ToIconString();

    /// <summary>Reveal the quest in the Journal tab's list.</summary>
    public static readonly string Reveal = FontAwesomeIcon.Search.ToIconString();

    /// <summary>Open the quest in Tsukimichi (the panels beside game windows).</summary>
    public static readonly string Open = FontAwesomeIcon.Moon.ToIconString();
}
