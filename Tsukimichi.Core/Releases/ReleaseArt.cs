using Tsukimichi.Core.Ui;

namespace Tsukimichi.Core.Releases;

/// <summary>
/// Where a release's picture lives and which one a look shows (spec-1.22 W2, Option B, the designer's production
/// recipe): six pre-rendered JPEGs per release, one per theme, 1120 × 440 sRGB, at
/// <c>assets/whatsnew/&lt;version&gt;/&lt;theme&gt;.jpg</c> in the plugin's folder, beside the theme atlases. Full
/// shows the theme's own; Quiet shows Classic's (the painting as painted); Plain loads nothing (decision 4). A missing
/// file is no picture, never an error. Pure.
/// </summary>
public static class ReleaseArt
{
    /// <summary>The picture's size in px (the 2x tier of the 560 × 220 band).</summary>
    public const int Width = 1120;

    /// <inheritdoc cref="Width"/>
    public const int Height = 440;

    /// <summary>The RGBA bytes of the picture at its full (2x) size.</summary>
    public const long TextureBytes = (long)Width * Height * 4;

    /// <summary>
    /// The RGBA bytes of the picture at the 1x tier (560 × 220), what the popup holds while it is open at UI scale 1:
    /// the figure the 12 MB budget counts at 1x, as the theme atlases' 1x tiers are (ATLAS-CONTRACT §6).
    /// </summary>
    public const long TextureBytes1x = (long)(Width / 2) * (Height / 2) * 4;

    /// <summary>The texture budget the popup's pictures live in (spec-1.22 W2, "Cost"): 12 MB.</summary>
    public const long BudgetBytes = 12L * 1024 * 1024;

    /// <summary>
    /// The pictures the popup holds at most: the page's and, while a page change cross-fades, the outgoing one
    /// (<see cref="ReleaseArtSlots{T}"/>).
    /// </summary>
    public const int HeldSlots = 2;

    /// <summary>The most the popup's pictures take, both slots at the full (2x) size: inside <see cref="BudgetBytes"/>.</summary>
    public const long HeldBytes = HeldSlots * TextureBytes;

    /// <summary>
    /// The size the popup keeps the picture at for a band <paramref name="bandWidth"/> px wide: the 1x tier (560 × 220,
    /// a quarter of the bytes) while the band fits it, else the full 1120 × 440, as the theme atlases keep their 2x tier
    /// only above the largest 1x size.
    /// </summary>
    public static (int Width, int Height) TierFor(float bandWidth) =>
        bandWidth <= Width / 2 ? (Width / 2, Height / 2) : (Width, Height);

    /// <summary>The folder under the plugin's own, relative, with the release folders in it.</summary>
    public static readonly string Folder = Path.Combine("assets", "whatsnew");

    /// <summary>Every picture is a JPEG.</summary>
    public const string Extension = ".jpg";

    /// <summary>The theme whose picture Quiet shows: the painting as painted.</summary>
    public const string QuietTheme = "classic";

    /// <summary>The theme picture <paramref name="flair"/> shows for the theme <paramref name="themeKey"/>; null at Plain.</summary>
    public static string? ThemeFor(Flair flair, string themeKey) => flair switch
    {
        Flair.Plain => null,
        Flair.Quiet => QuietTheme,
        _ => themeKey,
    };

    /// <summary>The picture's path for <paramref name="version"/> and <paramref name="theme"/> under <paramref name="pluginDir"/>.</summary>
    public static string PathFor(string pluginDir, string version, string theme) =>
        Path.Combine(pluginDir, Folder, version, theme + Extension);

    /// <summary>
    /// The picture to load for a release in a look, or null when none applies: Plain, an unreadable version or theme,
    /// or no such file (<paramref name="exists"/>, the file system's check in the plugin).
    /// </summary>
    public static string? Find(string pluginDir, string version, Flair flair, string themeKey, Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(exists);
        if (ThemeFor(flair, themeKey) is not { Length: > 0 } theme || ChangelogSection.NormalizeVersion(version) is not { Length: > 0 } normalized || string.IsNullOrEmpty(pluginDir))
        {
            return null;
        }

        // A key is a theme id ("ishgard-glass"); anything that could leave the folder is no picture.
        if (theme.AsSpan().IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || theme.Contains("..", StringComparison.Ordinal))
        {
            return null;
        }

        var path = PathFor(pluginDir, normalized, theme);
        return exists(path) ? path : null;
    }
}
