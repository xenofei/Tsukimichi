namespace Tsukimichi.Core.Ui.Themes;

/// <summary>The three 1.15 settings an appearance replaces, as a downgrade reads them.</summary>
/// <param name="MoonStyle">Settings › Look › Moon style.</param>
/// <param name="GlyphPalette">Settings › Look › Moon colours.</param>
/// <param name="FollowDalamudColours">Settings › Look › Follow Dalamud colours.</param>
public readonly record struct LegacyAppearance(MoonStyle MoonStyle, GlyphPaletteKind GlyphPalette, bool FollowDalamudColours);

/// <summary>
/// Moving the 1.15 look settings into an <see cref="AppearanceConfig"/> and back (theme-system §5.3, "Migration"):
/// <list type="bullet">
/// <item>Moon style Classic becomes the Classic theme; Medallion the default theme.</item>
/// <item>Moon colours High contrast becomes <see cref="AppearanceConfig.HighContrast"/>.</item>
/// <item>Follow Dalamud colours becomes the "dalamud" palette.</item>
/// </list>
/// The old settings stay in the configuration file and are written from the appearance on every save
/// (<see cref="ToLegacy"/>), so a downgrade to 1.15 finds the look it knows; an upgrade after such a downgrade migrates
/// again from what 1.15 saved. Every 1.15 combination survives the round trip (AppearanceMigrationTests).
/// </summary>
public static class AppearanceMigration
{
    /// <summary>The appearance for the 1.15 settings <paramref name="legacy"/>.</summary>
    public static AppearanceConfig FromLegacy(LegacyAppearance legacy) => new()
    {
        Theme = MoonStyleRules.Effective(legacy.MoonStyle) == MoonStyle.Classic ? ThemePresets.Classic.Key : ThemePresets.Default.Key,
        HighContrast = legacy.GlyphPalette == GlyphPaletteKind.HighContrast,
        Palette = legacy.FollowDalamudColours ? PaletteChoices.FollowDalamud.Key : null,
    };

    /// <summary>The 1.15 settings closest to <paramref name="config"/> (any theme but Classic reads as Medallion there).</summary>
    public static LegacyAppearance ToLegacy(AppearanceConfig? config)
    {
        var resolved = AppearanceResolver.Resolve(config);
        return new LegacyAppearance(resolved.MoonStyle, resolved.GlyphPalette, resolved.FollowDalamud);
    }

    /// <summary>
    /// Tidies a loaded appearance in place: a missing theme reads as the default, keys are trimmed and lower-cased, empty
    /// overrides are dropped, and an older format version is brought to <see cref="AppearanceConfig.CurrentVersion"/>.
    /// Keys this build does not know are kept as written (a newer build may have saved them; the resolver reads them as the
    /// theme's own choice). Returns those unknown keys, as "field: key", for a one-time notice; empty when none.
    /// </summary>
    public static IReadOnlyList<string> Sanitize(AppearanceConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.Version < AppearanceConfig.CurrentVersion)
        {
            config.Version = AppearanceConfig.CurrentVersion;
        }

        config.Theme = Normalize(config.Theme) ?? ThemePresets.Default.Key;
        config.Palette = Normalize(config.Palette);
        config.Frames = Normalize(config.Frames);

        if (config.Glyphs is { } glyphs)
        {
            Dictionary<string, string>? tidy = null;
            foreach (var (stateKey, setKey) in glyphs)
            {
                var state = Normalize(stateKey);
                var set = Normalize(setKey);
                if (state is not null && set is not null)
                {
                    (tidy ??= new Dictionary<string, string>(StringComparer.Ordinal))[state] = set;
                }
            }

            config.Glyphs = tidy;
        }

        return AppearanceResolver.Resolve(config).Unknown;
    }

    private static string? Normalize(string? key) =>
        string.IsNullOrWhiteSpace(key) ? null : key.Trim().ToLowerInvariant();
}
