namespace Tsukimichi.Core.Ui.Themes;

/// <summary>
/// The edits Settings makes to an <see cref="AppearanceConfig"/>, pure so they are tested without ImGui. The 1.15
/// controls (Moon style, Moon colours, Follow Dalamud colours) edit the appearance through these until the Themes page
/// (plan v7 T9) replaces them.
/// </summary>
public static class AppearanceEdits
{
    /// <summary>
    /// Settings › Look › Moon style: Classic picks the Classic theme; Medallion leaves Classic for the default theme and
    /// otherwise keeps the theme the user has (any non-Classic theme already reads as "Medallion" there).
    /// </summary>
    public static void SetMoonStyle(AppearanceConfig config, MoonStyle style)
    {
        ArgumentNullException.ThrowIfNull(config);
        var classic = AppearanceResolver.Resolve(config).Classic;
        if (MoonStyleRules.Effective(style) == MoonStyle.Classic)
        {
            config.Theme = ThemePresets.Classic.Key;
        }
        else if (classic)
        {
            config.Theme = ThemePresets.Default.Key;
        }
    }

    /// <summary>Settings › Look › Moon colours: the shared high-contrast set on or off.</summary>
    public static void SetHighContrast(AppearanceConfig config, bool on)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.HighContrast = on;
    }

    /// <summary>
    /// Settings › Look › Follow Dalamud colours: on picks the "dalamud" palette; off returns to the theme's own palette
    /// (and leaves any other palette the user picked alone).
    /// </summary>
    public static void SetFollowDalamud(AppearanceConfig config, bool on)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (on)
        {
            config.Palette = PaletteChoices.FollowDalamud.Key;
        }
        else if (AppearanceResolver.Resolve(config).FollowDalamud)
        {
            config.Palette = null;
        }
    }

    /// <summary>
    /// Picks <paramref name="theme"/>: its glyphs, palette and frames all follow it, so any mix, palette or frames override
    /// is cleared (theme-system §3.1). High contrast is the user's need, not the theme's, and stays.
    /// </summary>
    public static void ApplyTheme(AppearanceConfig config, ThemePreset theme)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(theme);
        config.Theme = theme.Key;
        config.Glyphs = null;
        config.Palette = null;
        config.Frames = null;
    }

    /// <summary>Whether the appearance overrides any axis of its theme ("Custom (based on X)" on the Themes page).</summary>
    public static bool IsCustom(AppearanceConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.Glyphs is { Count: > 0 } || config.Palette is not null || config.Frames is not null;
    }
}
