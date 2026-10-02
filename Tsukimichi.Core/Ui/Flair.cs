namespace Tsukimichi.Core.Ui;

/// <summary>
/// Settings › Display › Look › Flair (moon-road proposal P5, feature plan v4 V1): how much of the Moon Road ornament
/// shows. Full is the default.
/// </summary>
public enum Flair
{
    /// <summary>Every ornament and the Moon Road's motion: pane gradients, corner marks, glows, rules and dividers.</summary>
    Full = 0,

    /// <summary>Ornament rules and dividers only: no pane gradient, corner marks, glow or Moon Road motion.</summary>
    Quiet = 1,

    /// <summary>
    /// The 1.3 look, plus a banner for every quest (the hero's fallback chain): no Moon Road ornament or motion, and
    /// headings in the Caption and Display roles rather than game fonts.
    /// </summary>
    Plain = 2,
}

/// <summary>
/// What each <see cref="Flair"/> level draws, as pure rules so they are tested without ImGui. <c>Ui.Theme.Flair</c>
/// holds the level in effect this frame (<see cref="Effective"/>), and the drawing code asks these questions of it.
/// </summary>
public static class FlairRules
{
    /// <summary>
    /// The level in effect: the setting, except that the high-contrast palette draws at most <see cref="Flair.Quiet"/>
    /// (proposal §10.2: no pane gradient, star fields or glows). An unknown value reads as <see cref="Flair.Full"/>.
    /// </summary>
    public static Flair Effective(Flair setting, bool highContrast)
    {
        var flair = Enum.IsDefined(setting) ? setting : Flair.Full;
        return highContrast && flair == Flair.Full ? Flair.Quiet : flair;
    }

    /// <summary>Section rules and moon-road dividers: Full and Quiet.</summary>
    public static bool Rules(Flair flair) => flair != Flair.Plain;

    /// <summary>The pane gradient (sky over water): Full only; Quiet and Plain draw flat.</summary>
    public static bool PaneGradient(Flair flair) => flair == Flair.Full;

    /// <summary>Corner marks around the one framed object per pane: Full only.</summary>
    public static bool CornerMarks(Flair flair) => flair == Flair.Full;

    /// <summary>Glows and star fields: Full only.</summary>
    public static bool Glow(Flair flair) => flair == Flair.Full;

    /// <summary>
    /// The Moon Road's own moments (moonrise, road glint, station slide): Full only, and never under Reduce motion.
    /// Motion that shipped before 1.4 (gauge fills, the reveal pulse) follows Reduce motion alone.
    /// </summary>
    public static bool Motion(Flair flair, bool reduceMotion) => flair == Flair.Full && !reduceMotion;

    /// <summary>
    /// Headings in the game's display fonts (TrumpGothic, Jupiter, MiedingerMid): when the user leaves "Game fonts for
    /// headings" on, at any level but <see cref="Flair.Plain"/>.
    /// </summary>
    public static bool GameHeadingFonts(Flair setting, bool gameHeadingFonts) => gameHeadingFonts && setting != Flair.Plain;

    /// <summary>
    /// How a card is framed (feature plan v5 1.8.0, R3 #8 "brass cards"): Full a brass border with corner marks, Quiet the
    /// brass border alone, Plain the hairline it always had. Under the high-contrast palette the level is at most Quiet
    /// (<see cref="Effective"/>) and the brass is opaque VeilLine, so the frame stays a solid line.
    /// </summary>
    public static CardFrame Card(Flair flair) => flair switch
    {
        Flair.Full => CardFrame.BrassCorners,
        Flair.Quiet => CardFrame.Brass,
        _ => CardFrame.Hairline,
    };

    /// <summary>
    /// The quest table in the Moon Road style (R3 #6): the scope's title with its count, the clear header in the
    /// Eyebrow role and the brass rule under it. Full and Quiet; Plain keeps the raised header of 1.3.
    /// </summary>
    public static bool MoonRoadTable(Flair flair) => Rules(flair);

    /// <summary>The faint road line under a Ready row of the quest table (R3 #6, proposal §7.3): Full only.</summary>
    public static bool ReadyRoad(Flair flair) => flair == Flair.Full;
}

/// <summary>What <see cref="FlairRules.Card"/> draws around a card.</summary>
public enum CardFrame
{
    /// <summary>The palette's hairline (Plain).</summary>
    Hairline = 0,

    /// <summary>A brass border (Quiet, and the high-contrast palette's solid line).</summary>
    Brass = 1,

    /// <summary>A brass border with a corner mark in each corner (Full).</summary>
    BrassCorners = 2,
}
