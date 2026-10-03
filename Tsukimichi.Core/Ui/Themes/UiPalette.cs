using System.Numerics;
using System.Runtime.CompilerServices;

namespace Tsukimichi.Core.Ui.Themes;

/// <summary>
/// One UI colour palette (plan v7 T2; theme-system §8.1, docs/design/v7/ui/spec-1.16.md §A): every colour the chrome
/// draws with, by role. The plugin's <c>Ui.Theme</c> resolves the palette in effect once per palette change and every
/// pane reads its roles from there, so a palette (Night, the light Ishgard Snow, Follow Dalamud) recolours the window.
///
/// <para>What is not here stays fixed, on purpose: the glyphs and medals (<see cref="GlyphTokens"/>,
/// <see cref="GlyphPalette"/>, the atlases), which carry their own enamel wells and keylines so they read on any palette,
/// and picture pixels (game art and its tints). Gold keeps its meaning of "act now" in every palette: a palette may only
/// shift its lightness (<see cref="Accent"/>, the pill material in <see cref="Pills"/>).</para>
///
/// <para>Anything that assumes a dark background reads <see cref="IsLight"/> or a <see cref="Scene"/> token rather than
/// a literal: shadows, glows and washes, text halos, top highlights, the sky, the star field, the banner and portrait
/// night grades and the title on banner art.</para>
/// </summary>
public sealed record UiPalette
{
    /// <summary>The appearance's palette id (<see cref="PaletteId"/>; the registry is <see cref="UiPalettes"/>).</summary>
    public required PaletteId Id { get; init; }

    /// <summary>The stable key the palette is saved and shared as (<see cref="PaletteChoices"/>: "night", "dalamud", …).</summary>
    public required string Key { get; init; }

    /// <summary>The palette's name in English (the Themes page localises its own label).</summary>
    public required string Name { get; init; }

    /// <summary>The 17 surface and text roles (Window … CoolDeep), with the <see cref="SurfaceColors.Light"/> flag.</summary>
    public required SurfaceColors Surface { get; init; }

    /// <summary>Gold as text and small ink (pill labels, the active tab icon): Moon on Night, a deep gold on a light palette.</summary>
    public required Vector4 Accent { get; init; }

    /// <summary>The quieter gold of finished things, as text (MoonDim on Night).</summary>
    public required Vector4 AccentDim { get; init; }

    /// <summary>
    /// Text in the ornament (Full's Section headings, the sorted column header, the drawer's heads): the medallion's
    /// GiltLight #E6CF98 on Night, a large-text ink at 3 : 1 at least (spec-1.16 §A2: Snow's #3F4862 with the Came kit).
    /// </summary>
    public required Vector4 OrnamentLight { get; init; }

    /// <summary>The chrome inks beyond the surface roles: gold fills and lines, the danger tone, the Unknown text, the gauges and toggles.</summary>
    public required PaletteInks Inks { get; init; }

    /// <summary>The eight quest states' tones, status words and table stripes.</summary>
    public required StateInks States { get; init; }

    /// <summary>The drawn scene: sky, stars, shadows, glows or washes, halos, highlights and the night grades.</summary>
    public required SceneTokens Scene { get; init; }

    /// <summary>The Decoration frame metal: the card frame's ramp and the corner marks (brass on Night; a frame kit's metal later).</summary>
    public required BrassTokens Brass { get; init; }

    /// <summary>The giver portrait plate: its well, keylines, fallback ink and outer line.</summary>
    public required PlateTokens Plate { get; init; }

    /// <summary>The gauges' inks: the medal's material on Night, their own gilt and lead on a light palette (spec-1.16 §A6).</summary>
    public required GaugeInks Gauges { get; init; }

    /// <summary>
    /// The pane colour under Quiet's light medal rim (<see cref="MedalTokens.RimGap"/>): the gap between the medal's well
    /// and its hairline, so the medal reads as laid on the pane. Night's darkest Quiet tone #0E1322; on a light palette the
    /// pane the rows sit on (Ishgard Snow's #EEF1F6, a light host's own window).
    /// </summary>
    public required Vector4 MedalRimGap { get; init; }

    /// <summary>The table's alternate row tint; null draws the disabled tone at .16 (Night's 1.15 zebra).</summary>
    public Vector4? Zebra { get; init; }

    /// <summary>Full's lit pills (the raised gradient and the gold pill material); null where none is designed (flat pills, as under Follow Dalamud).</summary>
    public PillSurfaces? Pills { get; init; }

    /// <summary>Quiet's designed pane tones; null mixes them from <see cref="Surface"/> (<see cref="FlairTones.For(Flair, UiPalette)"/>).</summary>
    public FlairTones? QuietTones { get; init; }

    /// <summary>Plain's designed ledger tones; null mixes them from <see cref="Surface"/>.</summary>
    public FlairTones? PlainTones { get; init; }

    /// <summary>The filter drawer's designed tones per level; null mixes them from <see cref="Surface"/>.</summary>
    public DrawerToneSet? DrawerTones { get; init; }

    /// <summary>Whether this is a palette's high-contrast form (<see cref="HighContrast"/>).</summary>
    public bool IsHighContrast { get; init; }

    /// <summary>Whether the window is light (dark ink on a light ground): shadows, glows and halos go light-aware.</summary>
    public bool IsLight => Surface.Light;

    /// <summary>
    /// The high-contrast form, built once per palette instance and kept (allocation-free after the first call): the
    /// palette's own <see cref="HighContrastBuilder"/>, or the generic §A3 transform (<see cref="ToHighContrast"/>). A
    /// high-contrast form is its own high-contrast form. The form is kept beside the palette, not in it, so a copy made
    /// with <c>with</c> builds its own from its own roles, and the cache never takes part in equality.
    /// </summary>
    public UiPalette HighContrast => IsHighContrast ? this : HighContrastForms.GetValue(this, BuildHighContrast);

    /// <summary>
    /// How this palette builds its high-contrast form when it is not the generic transform: a designed form (the
    /// spec's Night HC and Snow HC hexes), or Night's 1.15 form (<see cref="SurfaceColors.ForHighContrast"/> alone).
    /// Not part of the palette's equality: two palettes with the same roles are the same palette.
    /// </summary>
    public Func<UiPalette, UiPalette>? HighContrastBuilder
    {
        get => builder.Value;
        init => builder = new(value);
    }

    private readonly EqualityExempt<Func<UiPalette, UiPalette>?> builder;

    /// <summary>Each palette instance's high-contrast form, held only as long as the palette is.</summary>
    private static readonly ConditionalWeakTable<UiPalette, UiPalette> HighContrastForms = new();

    private static readonly ConditionalWeakTable<UiPalette, UiPalette>.CreateValueCallback BuildHighContrast =
        static p => p.HighContrastBuilder?.Invoke(p) ?? p.ToHighContrast(pushInks: true);

    /// <summary>A field the record's generated equality skips: every value equals every other.</summary>
    private readonly struct EqualityExempt<T>(T value) : IEquatable<EqualityExempt<T>>
    {
        public T Value { get; } = value;

        public bool Equals(EqualityExempt<T> other) => true;

        public override bool Equals(object? obj) => obj is EqualityExempt<T>;

        public override int GetHashCode() => 0;
    }

    /// <summary>
    /// The generic high-contrast form (theme-system §8.3, spec-1.16 §A3): the surface's high-contrast roles (no sky
    /// gradient, the strong line as an opaque ornament, Cool at 7 : 1), no sky stops nor rail edge, the designed Quiet, Plain and drawer
    /// tones dropped so they are mixed from that surface, and with <paramref name="pushInks"/> the accent, the danger and
    /// Unknown text and every state word pushed towards the text colour until they reach 7 : 1 on the window, and the
    /// stripes to 3 : 1.
    /// </summary>
    public UiPalette ToHighContrast(bool pushInks)
    {
        var surface = Surface.ForHighContrast();
        var scene = Scene with { Zenith = surface.Window, SkyStops = null, RailEdge = null };
        var form = this with
        {
            Key = Key + "-hc",
            Surface = surface,
            Scene = scene,
            QuietTones = null,
            PlainTones = null,
            DrawerTones = null,
            IsHighContrast = true,
            HighContrastBuilder = null,
        };
        if (!pushInks)
        {
            return form;
        }

        const float text = SurfaceColors.HighContrastTextMinContrast;
        const float line = SurfaceColors.LineMinContrast;
        Vector4 Text(Vector4 ink) => ColorMath.EnsureContrast(ink, surface.Text, surface.Window, text);
        Vector4 Stripe(Vector4 ink) => ColorMath.EnsureContrast(ink, surface.Text, surface.Window, line);
        return form with
        {
            Accent = Text(Accent),
            AccentDim = Text(AccentDim),
            Inks = Inks with { DangerText = Text(Inks.DangerText), UnknownText = Text(Inks.UnknownText) },
            States = States.Map(Text, Stripe),
        };
    }

    /// <summary>
    /// Every text-on-surface pair the palette promises reads at WCAG AA (4.5 : 1), as (role, ink, ground): spec-1.16 §A8
    /// (the text inks, accent, danger and the status words on every surface, <see cref="Surfaces"/>; Cool and the Not
    /// checked word on all but hovered rows) and the pill and button inks.
    /// <c>PaletteContrastTests</c> holds every palette and form to this list.
    /// </summary>
    public IEnumerable<(string Role, Vector4 Ink, Vector4 Ground)> TextPairs()
    {
        var s = Surface;
        foreach (var (name, ground) in Surfaces())
        {
            yield return ($"Text on {name}", s.Text, ground);
            yield return ($"TextSecondary on {name}", s.TextSecondary, ground);
            yield return ($"TextTertiary on {name}", s.TextTertiary, ground);
            yield return ($"Accent on {name}", Accent, ground);
            yield return ($"AccentDim on {name}", AccentDim, ground);
            yield return ($"DangerText on {name}", Inks.DangerText, ground);

            // Cool and the Not checked word are held on the window, cards and wells, not on hovered rows (spec-1.16
            // §A8 lists both on the window and cards): nothing draws them on a hover fill (Cool is no text in the
            // panes yet; a hovered path step's name turns to Text and the table's state word is Text). Night's Tide
            // and Veil text sit at 4.2 and 4.46 : 1 on Hover, so a pane that starts drawing them there adds the pair.
            var hovered = name == HoverSurface;
            if (!hovered)
            {
                yield return ($"Cool on {name}", s.Cool, ground);
                yield return ($"UnknownText on {name}", Inks.UnknownText, ground);
            }

            foreach (var state in StateInks.Order)
            {
                if (!(hovered && state == Model.QuestState.Unknown))
                {
                    yield return ($"{state} word on {name}", States.Text(state), ground);
                }
            }
        }

        yield return ("Text on Deep", s.Text, s.Deep);
        yield return ("Text on Top", s.Text, s.Top);
        yield return ("OnGold on Gold", Inks.OnGold, Inks.Gold);
        yield return ("OnDanger on the destructive button", Inks.OnDanger, ColorMath.Over(Inks.Danger with { W = PaletteInks.DangerButtonAlpha }, s.Window));
        if (Pills is { } pills)
        {
            yield return ("Gold pill ink", pills.GoldInk, Vector4.Lerp(pills.GoldTop, pills.GoldFoot, 0.5f));
        }
    }

    /// <summary>The four surfaces text is drawn on (spec-1.16 §A8, "every surface"): the window, cards, wells and hovered rows.</summary>
    public (string Name, Vector4 Color)[] Surfaces() =>
        [("Window", Surface.Window), ("Raised", Surface.Raised), ("Sunken", Surface.Sunken), (HoverSurface, Surface.Hover)];

    private const string HoverSurface = "Hover";

    /// <summary>
    /// The non-text pairs the palette promises reach 3 : 1 (WCAG 1.4.11 and large text), as (role, ink, ground): the
    /// strong line, the ornament point, the ornament's text (large), the gold and Locked out stripes, and under high
    /// contrast the opaque ornament line.
    /// </summary>
    public IEnumerable<(string Role, Vector4 Ink, Vector4 Ground)> LinePairs()
    {
        var s = Surface;
        yield return ("StrongLine on Window", s.StrongLine, s.Window);
        yield return ("StrongLine on Raised", s.StrongLine, s.Raised);
        yield return ("OrnamentHigh on Window", s.OrnamentHigh, s.Window);
        yield return ("OrnamentLight on Window", OrnamentLight, s.Window);
        yield return ("OrnamentLight on Raised", OrnamentLight, s.Raised);
        yield return ("Ready stripe on Window", States.Stripe(Model.QuestState.Ready), s.Window);
        yield return ("Locked out stripe on Window", States.Stripe(Model.QuestState.Foreclosed), s.Window);
        if (IsHighContrast)
        {
            yield return ("Ornament on Window", s.Ornament, s.Window);
        }

        foreach (var pair in GaugePairs())
        {
            yield return pair;
        }
    }

    /// <summary>
    /// The gauges' pairs that must reach 3 : 1 as UI graphics (WCAG 1.4.11; spec-1.16 §A8): the flat gauges' arc and the
    /// tree ring on the window and the zenith, and on a light palette, whose gauges have their own ink, every colour of
    /// the arc on the window, the groove and the zenith, the keylines on the window, and the moon's lit face on its dark
    /// side. Night's medal gauges are the medal's own material (glyph art, keyed by Abyss keylines), held to the medal
    /// gates instead.
    /// </summary>
    public IEnumerable<(string Role, Vector4 Ink, Vector4 Ground)> GaugePairs()
    {
        var s = Surface;
        var g = Gauges;
        var zenith = ColorMath.Opaque(Scene.Zenith);
        yield return ("Flat gauge arc on Window", g.Arc, s.Window);
        yield return ("Flat gauge arc on Zenith", g.Arc, zenith);
        yield return ("Tree ring arc on Window", Inks.GaugeArc, s.Window);
        yield return ("Tree ring (done) on Window", Inks.GaugeDone, s.Window);
        if (!IsLight)
        {
            yield break;
        }

        foreach (var arc in g.ArcColors())
        {
            var hex = $"#{ColorMath.ToHex(arc):X6}";
            yield return ($"Gauge arc {hex} on Window", arc, s.Window);
            yield return ($"Gauge arc {hex} on Groove", arc, g.Groove);
            yield return ($"Gauge arc {hex} on Zenith", arc, zenith);
        }

        yield return ("Gauge keyline on Window", g.Keyline, s.Window);
        yield return ("Gauge track on Window", g.Track, s.Window);
        yield return ("Gauge knob on its rim", g.Knob, g.KnobRim);
        yield return ("Filling moon on its dark side", g.MoonLit, g.DarkSide);
        yield return ("Moon dark side on Window", g.DarkSide, s.Window);
    }
}
