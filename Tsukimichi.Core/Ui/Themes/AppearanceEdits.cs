using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Ui.Themes;

/// <summary>
/// The edits Settings makes to an <see cref="AppearanceConfig"/>, pure so they are tested without ImGui: the Themes page
/// (plan v7 T9) picks a theme, a palette, frames and high contrast, and resets, through these. The 1.15 setters (Moon
/// style, Follow Dalamud colours) stay for the migration's round trip.
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

    /// <summary>
    /// Settings › Themes › Palette: <paramref name="palette"/> as an override of the theme's palette, kept until "From
    /// theme" or a reset (spec-1.16 §B3). Null, or the theme's own palette, follows the theme again, so a theme picked
    /// later brings its palette.
    /// </summary>
    public static void SetPalette(AppearanceConfig config, PaletteInfo? palette)
    {
        ArgumentNullException.ThrowIfNull(config);
        var theme = AppearanceResolver.Resolve(config).Theme;
        config.Palette = palette is null || palette.Id == theme.Palette ? null : palette.Key;
    }

    /// <summary>Settings › Themes › Frames: as <see cref="SetPalette"/>, for the frame kit (null or the theme's own kit: from the theme).</summary>
    public static void SetFrames(AppearanceConfig config, FrameKitInfo? kit)
    {
        ArgumentNullException.ThrowIfNull(config);
        var theme = AppearanceResolver.Resolve(config).Theme;
        config.Frames = kit is null || kit.Id == theme.Frames ? null : kit.Key;
    }

    /// <summary>
    /// Settings › Themes › Mix moons by state (1.17 T10): <paramref name="state"/> draws from <paramref name="set"/>.
    /// Null, or the theme's own set, is "From theme": the state's override is removed (and the mix with it once empty),
    /// so a theme picked later brings its own moon for that state too.
    /// </summary>
    public static void SetGlyph(AppearanceConfig config, QuestState state, GlyphSetInfo? set)
    {
        ArgumentNullException.ThrowIfNull(config);
        var key = AppearanceStates.Key(state);
        var theme = AppearanceResolver.Resolve(config).Theme;
        if (set is null || set.Id == theme.Glyphs)
        {
            if (config.Glyphs is { } glyphs)
            {
                RemoveState(glyphs, key);
                if (glyphs.Count == 0)
                {
                    config.Glyphs = null;
                }
            }

            return;
        }

        var mix = config.Glyphs ??= new Dictionary<string, string>(StringComparer.Ordinal);
        RemoveState(mix, key);
        mix[key] = set.Key;
    }

    /// <summary>Reset mix (spec-1.17 §A5): every state from the theme again. The caller keeps a clone for Undo.</summary>
    public static void ResetMix(AppearanceConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Glyphs = null;
    }

    /// <summary>Whether the appearance picks any state's moon itself (a mix is set, even one this build cannot draw).</summary>
    public static bool HasMix(AppearanceConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.Glyphs is { Count: > 0 };
    }

    // A saved key may differ in case or spacing (the resolver reads it leniently), so every spelling of the state goes.
    private static void RemoveState(Dictionary<string, string> glyphs, string key)
    {
        foreach (var saved in glyphs.Keys.ToArray())
        {
            if (AppearanceStates.TryParse(saved, out var parsed) && AppearanceStates.Key(parsed) == key)
            {
                glyphs.Remove(saved);
            }
        }
    }

    /// <summary>
    /// Reset appearance (spec-1.16 §B5): Menphina's Medallion on Night with Brass frames, no mix and high contrast off.
    /// The caller keeps a <see cref="AppearanceConfig.Clone"/> for Undo.
    /// </summary>
    public static void Reset(AppearanceConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        ApplyTheme(config, ThemePresets.Default);
        config.HighContrast = false;
    }

    /// <summary>Whether <see cref="Reset"/> would change nothing (the default theme, no overrides, standard contrast).</summary>
    public static bool IsDefault(AppearanceConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return IsDefault(config, AppearanceResolver.Resolve(config));
    }

    /// <summary>
    /// <see cref="IsDefault(AppearanceConfig)"/> with <paramref name="resolved"/>, <paramref name="config"/> already
    /// resolved (the frame's appearance), so a per-frame check resolves and allocates nothing.
    /// </summary>
    public static bool IsDefault(AppearanceConfig config, ResolvedAppearance resolved)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(resolved);
        return !IsCustom(config) && !config.HighContrast && resolved.Theme.Id == ThemePresets.Default.Id;
    }

    /// <summary>
    /// <see cref="IsDefault(AppearanceConfig, ResolvedAppearance)"/> with the Follow Umbra palette (1.22.0 M3), which is
    /// kept beside the appearance and wins over it: while it is chosen the window wears Umbra's colours, so Reset still
    /// has something to undo.
    /// </summary>
    public static bool IsDefault(AppearanceConfig config, ResolvedAppearance resolved, bool followUmbra) =>
        IsDefault(config, resolved) && !followUmbra;

    /// <summary>Whether the appearance overrides any axis of its theme ("Custom (based on X)" on the Themes page).</summary>
    public static bool IsCustom(AppearanceConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.Glyphs is { Count: > 0 } || config.Palette is not null || config.Frames is not null;
    }
}
