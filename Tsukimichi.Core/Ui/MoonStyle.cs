namespace Tsukimichi.Core.Ui;

/// <summary>
/// Settings › Display › Look › Moon style (feature plan v6 G3): which renderer draws the quest-state glyphs and the
/// progress gauges, for the owner's in-game comparison. Medallion is the default.
/// </summary>
public enum MoonStyle
{
    /// <summary>Menphina's Medallion (1.12): the medals, their badges and the gauges in the medal's material.</summary>
    Medallion = 0,

    /// <summary>The 1.11 moons and gauges, kept for the glyph window's A/B sheet.</summary>
    Classic = 1,
}

/// <summary>The <see cref="MoonStyle"/> rules, pure so they are tested without ImGui.</summary>
public static class MoonStyleRules
{
    /// <summary>The style in effect: the setting, or <see cref="MoonStyle.Medallion"/> for a value this build does not know.</summary>
    public static MoonStyle Effective(MoonStyle setting) => Enum.IsDefined(setting) ? setting : MoonStyle.Medallion;
}
