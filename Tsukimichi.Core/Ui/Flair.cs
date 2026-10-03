using System.Numerics;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Ui;

/// <summary>
/// Settings › Display › Look › Decoration (moon-road proposal P5, feature plan v4 V1, redesigned for 1.13 in
/// docs/design/flair-v13): three looks that differ in pane tone, art, card material, row density and medal finish, so
/// each is told apart at a glance. Full is the default.
/// </summary>
public enum Flair
{
    /// <summary>
    /// The Moon Road: night sky over water, gilt brass cards, the night-graded quest banner, game display fonts, glows,
    /// motion and generous spacing (34 px rows).
    /// </summary>
    Full = 0,

    /// <summary>
    /// Still water: flat tonal panes about 3 % apart, hairline rules, borderless raised cards, the standard font,
    /// silver-rimmed medals, no banner or glow (30 px rows).
    /// </summary>
    Quiet = 1,

    /// <summary>
    /// The ledger: zebra rows at 24 px, a header band with column dividers, an icon-only rail, a key-value detail pane,
    /// flat glyphs, colour only on states; no cards, art or motion.
    /// </summary>
    Plain = 2,
}

/// <summary>
/// What each <see cref="Flair"/> level draws (docs/design/flair-v13/spec.md §1 and §2), as pure rules so they are tested
/// without ImGui. <c>Ui.Theme.Flair</c> holds the level in effect this frame (<see cref="Effective"/>), and the drawing
/// code asks these questions of it.
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

    /// <summary>
    /// How a rule or divider draws: the Moon Road's brass art at Full (fading rules, moon-road dividers, sigils), a
    /// flat hairline at Quiet, the palette's line at Plain. Brass art is <see cref="RuleStyle.MoonRoad"/> only;
    /// structure lines are a hairline or a line at the other levels.
    /// </summary>
    public static RuleStyle Rule(Flair flair) => flair switch
    {
        Flair.Full => RuleStyle.MoonRoad,
        Flair.Quiet => RuleStyle.Hairline,
        _ => RuleStyle.Line,
    };

    /// <summary>The panes' backdrop: the sky-over-water gradient at Full, three tones of the window at Quiet, flat at Plain.</summary>
    public static PaneTone Panes(Flair flair) => flair switch
    {
        Flair.Full => PaneTone.Gradient,
        Flair.Quiet => PaneTone.Tonal,
        _ => PaneTone.Flat,
    };

    /// <summary>The pane gradient (sky over water): Full only; Quiet and Plain draw flat tones.</summary>
    public static bool PaneGradient(Flair flair) => flair == Flair.Full;

    /// <summary>The faint seeded star field in empty sky (the rail, under the tree, the table's title band): Full only.</summary>
    public static bool StarField(Flair flair) => flair == Flair.Full;

    /// <summary>Corner marks on cards and tooltips: Full only.</summary>
    public static bool CornerMarks(Flair flair) => flair == Flair.Full;

    /// <summary>Glows (the active station, Ready rows and medals, the divider lozenge, the primary pill): Full only.</summary>
    public static bool Glow(Flair flair) => flair == Flair.Full;

    /// <summary>
    /// The hero medal's gold halo (supervisor fix 4, "glow is light"): only where <see cref="Glow"/> draws, and only for a
    /// quest the player can act on now, Ready or Ready on another job. Nothing blocked, done or completed glows.
    /// </summary>
    public static bool HeroHalo(Flair flair, QuestState state) =>
        Glow(flair) && state is QuestState.Ready or QuestState.ReadyOnOtherJob;

    /// <summary>The night grade on the quest banner (spec §1.2, <see cref="BannerGrade"/>): Full only, the only level with a banner.</summary>
    public static bool BannerGrade(Flair flair) => Banner(flair);

    /// <summary>The quest banner in the detail pane and Flight's zone banner: Full only.</summary>
    public static bool Banner(Flair flair) => flair == Flair.Full;

    /// <summary>
    /// The Moon Road's own moments (the hero medal rising, the road glint, the station slide): Full only, and never
    /// under Reduce motion.
    /// </summary>
    public static bool Motion(Flair flair, bool reduceMotion) => flair == Flair.Full && !reduceMotion;

    /// <summary>
    /// The interface's small motion (hover fades, gauge fills, the reveal pulse): Full and Quiet, never under Reduce
    /// motion. Plain draws every state at once.
    /// </summary>
    public static bool UiMotion(Flair flair, bool reduceMotion) => flair != Flair.Plain && !reduceMotion;

    /// <summary>
    /// Headings in the game's display fonts (TrumpGothic, Jupiter, MiedingerMid): when the user leaves "Game fonts for
    /// headings" on, at Full only. Quiet and Plain draw headings in the Dalamud body font.
    /// </summary>
    public static bool GameHeadingFonts(Flair setting, bool gameHeadingFonts) =>
        gameHeadingFonts && (Enum.IsDefined(setting) ? setting : Flair.Full) == Flair.Full;

    /// <summary>
    /// How a card is framed: gilt brass with corner marks at Full, a borderless tonal plane at Quiet, no card at all at
    /// Plain (a heading row with a line under it). Under the high-contrast palette the level is at most Quiet
    /// (<see cref="Effective"/>), and its tonal cards take a border (<see cref="CardBorder"/>).
    /// </summary>
    public static CardFrame Card(Flair flair) => flair switch
    {
        Flair.Full => CardFrame.BrassCorners,
        Flair.Quiet => CardFrame.Tonal,
        _ => CardFrame.None,
    };

    /// <summary>
    /// Whether a card draws a border: brass always does; a tonal card only under the high-contrast palette, where tone
    /// alone is not a boundary for low-vision users (it then takes the VeilLine border).
    /// </summary>
    public static bool CardBorder(Flair flair, bool highContrast) => Card(flair) switch
    {
        CardFrame.BrassCorners => true,
        CardFrame.Tonal => highContrast,
        _ => false,
    };

    /// <summary>
    /// The quest table's header: the Moon Road title and tracked caps over a brass rule at Full, the title in the body
    /// font over a hairline at Quiet, the raised band with column dividers at Plain (no title: the chips say the scope).
    /// </summary>
    public static TableHeaderStyle TableHeader(Flair flair) => flair switch
    {
        Flair.Full => TableHeaderStyle.MoonRoad,
        Flair.Quiet => TableHeaderStyle.Eyebrow,
        _ => TableHeaderStyle.Raised,
    };

    /// <summary>The faint road line under a Ready row of the quest table (proposal §7.3), with its glint: Full only.</summary>
    public static bool ReadyRoad(Flair flair) => flair == Flair.Full;

    /// <summary>Zebra rows in the quest table: always at Plain; at the other levels only in Dense rows.</summary>
    public static bool Zebra(Flair flair, RowDensity density) => flair == Flair.Plain || density == RowDensity.Dense;

    /// <summary>
    /// The detail pane's hero: the night-graded banner with the medal rising over its edge at Full, a title block and a
    /// 52 px medal plate at Quiet, a key-value list with no medal at Plain.
    /// </summary>
    public static HeroStyle Hero(Flair flair) => flair switch
    {
        Flair.Full => HeroStyle.Banner,
        Flair.Quiet => HeroStyle.Plate,
        _ => HeroStyle.Ledger,
    };

    /// <summary>
    /// The medals' finish: Menphina's Medallion as shipped at Full, the same face with a thin silver rim at Quiet, the
    /// flat <c>MedalTokens.Plain</c> ladder at Plain. Moon style Classic keeps the 1.11 glyphs at every level.
    /// </summary>
    public static MedalFinish Medal(Flair flair, MoonStyle moonStyle)
    {
        if (MoonStyleRules.Effective(moonStyle) == MoonStyle.Classic)
        {
            return MedalFinish.Classic;
        }

        return flair switch
        {
            Flair.Full => MedalFinish.Gilt,
            Flair.Quiet => MedalFinish.LightRim,
            _ => MedalFinish.Plain,
        };
    }

    /// <summary>
    /// The least radius of a quest-table row medal, in icon-scaled logical px: the row tier is 18 px at Full, 16 at
    /// Quiet and 12 at Plain (<see cref="TableGeometry.GlyphRadius"/> grows it with the row).
    /// </summary>
    public static float RowGlyphFloorLogical(Flair flair) => flair switch
    {
        Flair.Full => 6f,
        Flair.Quiet => 5.4f,
        _ => 4.2f,
    };

    /// <summary>The Journal tree's gauge: the orbit (arc, knob and core) at Full, the ring arc alone at Quiet, none at Plain (a percentage column).</summary>
    public static TreeGauge Gauge(Flair flair) => flair switch
    {
        Flair.Full => TreeGauge.Orbit,
        Flair.Quiet => TreeGauge.Ring,
        _ => TreeGauge.None,
    };

    /// <summary>The status bar: brass rule, gradient and the MSQ pill at Full, a hairline at Quiet, a line of text at Plain.</summary>
    public static StatusBarStyle StatusBar(Flair flair) => flair switch
    {
        Flair.Full => StatusBarStyle.MoonRoad,
        Flair.Quiet => StatusBarStyle.Quiet,
        _ => StatusBarStyle.Text,
    };

    /// <summary>Tooltips: a brass frame with corner marks at Full, flat with a neutral border at Quiet, a plain box at Plain.</summary>
    public static TooltipStyle Tooltip(Flair flair) => flair switch
    {
        Flair.Full => TooltipStyle.Brass,
        Flair.Quiet => TooltipStyle.Flat,
        _ => TooltipStyle.Plain,
    };

    /// <summary>The rail: labels under each station at Full and Quiet; icons only at Plain (the compact rail).</summary>
    public static bool CompactRail(Flair flair) => flair == Flair.Plain;

    /// <summary>The spacing tokens of a level (spec §1, "Spacing tokens"), in logical px.</summary>
    public static FlairSpacing Spacing(Flair flair) => flair switch
    {
        Flair.Full => FlairSpacing.Full,
        Flair.Quiet => FlairSpacing.Quiet,
        _ => FlairSpacing.Plain,
    };
}

/// <summary>What <see cref="FlairRules.Rule"/> draws for a rule or divider.</summary>
public enum RuleStyle
{
    /// <summary>The palette's 1 px line and nothing else (Plain).</summary>
    Line = 0,

    /// <summary>A flat 1 px hairline, full width, no fade and no sigil (Quiet).</summary>
    Hairline = 1,

    /// <summary>The Moon Road's brass art: fading rules, moon-road dividers, sigils (Full).</summary>
    MoonRoad = 2,
}

/// <summary>What <see cref="FlairRules.Panes"/> paints behind the panes.</summary>
public enum PaneTone
{
    /// <summary>One flat tone everywhere (Plain).</summary>
    Flat = 0,

    /// <summary>The tree, table and detail panes in three steps of the window tone (Quiet).</summary>
    Tonal = 1,

    /// <summary>Sky over water (Full).</summary>
    Gradient = 2,
}

/// <summary>What <see cref="FlairRules.Card"/> draws around a card.</summary>
public enum CardFrame
{
    /// <summary>No card: a heading row with a line under it, the content on the pane (Plain).</summary>
    None = 0,

    /// <summary>A borderless plane a tone lighter than the pane (Quiet); a VeilLine border under high contrast.</summary>
    Tonal = 1,

    /// <summary>Gilt brass lit from the upper left, with a corner mark in each corner (Full).</summary>
    BrassCorners = 2,
}

/// <summary>What <see cref="FlairRules.TableHeader"/> draws over the quest table.</summary>
public enum TableHeaderStyle
{
    /// <summary>A raised band with 1 px column dividers and no title (Plain).</summary>
    Raised = 0,

    /// <summary>The title in the body font, the column header in body type, a hairline under it (Quiet).</summary>
    Eyebrow = 1,

    /// <summary>The title in Jupiter with a gilt count, tracked caps, a brass rule under it (Full).</summary>
    MoonRoad = 2,
}

/// <summary>What <see cref="FlairRules.Hero"/> draws at the top of the detail pane.</summary>
public enum HeroStyle
{
    /// <summary>Name and path, then State · Level · Giver as a key-value list (Plain).</summary>
    Ledger = 0,

    /// <summary>A title block over a hairline, then a 52 px medal plate (Quiet).</summary>
    Plate = 1,

    /// <summary>The night-graded banner with the medal rising over its edge (Full).</summary>
    Banner = 2,
}

/// <summary>How <see cref="FlairRules.Medal"/> finishes the quest-state medals.</summary>
public enum MedalFinish
{
    /// <summary>The flat <c>MedalTokens.Plain</c> ladder, row tier only (Plain).</summary>
    Plain = 0,

    /// <summary>The medal face with a 1 px silver hairline for a rim (Quiet).</summary>
    LightRim = 1,

    /// <summary>Menphina's Medallion as shipped, gilt rim and all (Full).</summary>
    Gilt = 2,

    /// <summary>The 1.11 moons (Moon style Classic), at every level.</summary>
    Classic = 3,
}

/// <summary>What <see cref="FlairRules.Gauge"/> draws beside a Journal node.</summary>
public enum TreeGauge
{
    /// <summary>No gauge; a right-aligned percentage column (Plain).</summary>
    None = 0,

    /// <summary>A 2 px ring arc, no knob and no core (Quiet).</summary>
    Ring = 1,

    /// <summary>The orbit: gold arc, moon knob and phase core (Full).</summary>
    Orbit = 2,
}

/// <summary>What <see cref="FlairRules.StatusBar"/> draws at the foot of the main window.</summary>
public enum StatusBarStyle
{
    /// <summary>Text only, over a 1 px line (Plain).</summary>
    Text = 0,

    /// <summary>A hairline above, the ring halo, MSQ as gold text (Quiet).</summary>
    Quiet = 1,

    /// <summary>A brass rule brightest at the centre, a Deep gradient, the MSQ pill (Full).</summary>
    MoonRoad = 2,
}

/// <summary>How <see cref="FlairRules.Tooltip"/> frames a tooltip.</summary>
public enum TooltipStyle
{
    /// <summary>A plain box, radius 2, padding 5 × 7 (Plain).</summary>
    Plain = 0,

    /// <summary>Flat, a 1 px neutral border, radius 6 (Quiet).</summary>
    Flat = 1,

    /// <summary>The cards' brass frame with corner marks top left and bottom right (Full).</summary>
    Brass = 2,
}

/// <summary>
/// A level's spacing tokens in logical px (spec §1): the padding inside a pane, the gap between sections, a card's
/// padding (x, y), the height of an action pill, and a card's rounding.
/// </summary>
public readonly record struct FlairSpacing(float PanePad, float Gap, Vector2 CardPad, float PillHeight, float CardRounding)
{
    /// <summary>Full: room to breathe.</summary>
    public static readonly FlairSpacing Full = new(11f, 12f, new Vector2(14f, 11f), 30f, 4f);

    /// <summary>Quiet: calm, a little tighter.</summary>
    public static readonly FlairSpacing Quiet = new(8f, 8f, new Vector2(11f, 9f), 28f, 6f);

    /// <summary>Plain: dense, no card padding (there are no cards), 22 px buttons.</summary>
    public static readonly FlairSpacing Plain = new(5f, 4f, Vector2.Zero, 22f, 3f);
}
