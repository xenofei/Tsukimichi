using Tsukimichi.Core.Ui.Themes;

namespace Tsukimichi.Config;

// ---- 1.16.0: themes (feature plan v7 T1) ----
public sealed partial class Configuration
{
    /// <summary>The saved appearance as read from the file; null in a configuration from before 1.16.0.</summary>
    [Newtonsoft.Json.JsonProperty(nameof(Appearance))]
    private AppearanceConfig? appearance;

    /// <summary>
    /// The appearance (docs/research/plan-v7/theme-system.md §5.3): the theme, the user's overrides of its moons, palette
    /// and frames, and high contrast. A configuration from before 1.16.0 has none, and gets one built from its Moon style,
    /// Moon colours and Follow Dalamud colours (<see cref="AppearanceMigration.FromLegacy"/>). Those three settings are
    /// still saved, written from this on every <see cref="Save"/>, so a downgrade finds the look it knows; read this, not them.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    public AppearanceConfig Appearance
    {
        get => appearance ??= AppearanceMigration.FromLegacy(new LegacyAppearance(MoonStyle, GlyphPalette, FollowDalamudColours));
        set => appearance = value ?? throw new System.ArgumentNullException(nameof(value));
    }

    /// <summary>Writes the 1.15 look settings from <see cref="Appearance"/> (for a downgrade); <see cref="Save"/> calls it.</summary>
    private void SyncLegacyAppearance()
    {
        var legacy = AppearanceMigration.ToLegacy(Appearance);
        MoonStyle = legacy.MoonStyle;
        GlyphPalette = legacy.GlyphPalette;
        FollowDalamudColours = legacy.FollowDalamudColours;
    }

    /// <summary>Migrates (on first read) and tidies the appearance at load; logs keys this build does not know, once.</summary>
    private static void LoadAppearance(Configuration config, Dalamud.Plugin.Services.IPluginLog? log)
    {
        var migrated = config.appearance is null;
        var unknown = AppearanceMigration.Sanitize(config.Appearance);
        if (migrated)
        {
            log?.Information("Appearance migrated from Moon style {MoonStyle}, Moon colours {Palette}, Follow Dalamud colours {Follow}", config.MoonStyle, config.GlyphPalette, config.FollowDalamudColours);
        }

        if (unknown.Count > 0)
        {
            log?.Warning("Appearance keys this build does not know draw as the theme's own: {Keys}", string.Join(", ", unknown));
        }
    }
}
