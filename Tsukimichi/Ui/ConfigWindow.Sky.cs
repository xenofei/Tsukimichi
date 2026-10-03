namespace Tsukimichi.Ui;

/// <summary>
/// Settings › General › Look, the Full sky's rows (feature plan v7 UI-6, docs/design/v7/ui/spec.md §3.4, §3.6 and
/// Revision 3): "Milky Way in the sky" under Decoration, and "Moving night sky" and "Shooting star on completion" under
/// Reduce motion. Each is a sub-row, dimmed with its reason while Decoration is not Full (or, for the two moving ones,
/// while Reduce motion is on), so the page never moves as the level changes.
/// </summary>
public sealed partial class ConfigWindow
{
    /// <summary>The Milky Way's row, under Decoration: Full only, off by default.</summary>
    private void DrawSkyLook()
    {
        var full = Theme.ShowStars;
        var milkyWay = settings.MilkyWay;
        if (Toggle(Strings.SettingsMilkyWay, Strings.SettingsMilkyWayHint, ref milkyWay, "milky way band galaxy stars sky night full", enabled: full, sub: true, reason: Strings.SettingsSkyFullReason))
        {
            settings.MilkyWay = milkyWay;
            Save();
        }
    }

    /// <summary>The moving sky's rows, under Reduce motion: Full only, and stilled by Reduce motion.</summary>
    private void DrawSkyMotion()
    {
        var full = Theme.ShowStars;
        var moves = full && !settings.ReduceMotion;
        var reason = full ? Strings.SettingsSkyMotionReason : Strings.SettingsSkyFullReason;

        var drift = settings.MovingNightSky;
        if (Toggle(Strings.SettingsMovingNightSky, Strings.SettingsMovingNightSkyHint, ref drift, "moving night sky stars drift animation twinkle shooting star meteor", enabled: moves, sub: true, reason: reason))
        {
            settings.MovingNightSky = drift;
            Save();
        }

        var meteor = settings.CompletionMeteor;
        if (Toggle(Strings.SettingsCompletionMeteor, Strings.SettingsCompletionMeteorHint, ref meteor, "shooting star meteor completion complete quest animation", enabled: moves, sub: true, reason: reason))
        {
            settings.CompletionMeteor = meteor;
            Save();
        }
    }
}
