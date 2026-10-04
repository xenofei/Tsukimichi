using System;
using System.Numerics;

namespace Tsukimichi.Core.Ui;

/// <summary>
/// Colour arithmetic on RGBA vectors (sRGB channels 0..1): WCAG 2.x relative luminance and contrast ratio, alpha
/// compositing and a "nudge until it reads" helper. Used to derive the "Follow Dalamud colours" palette from the host
/// style (<see cref="SurfaceColors.FromHost"/>) and to check the Night tokens' contrast claims in tests.
/// </summary>
public static class ColorMath
{
    /// <summary>An opaque colour from a 0xRRGGBB literal, as the design tokens are written.</summary>
    public static Vector4 FromHex(uint rgb) => new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

    /// <summary>WCAG 2.x relative luminance of the colour's RGB (alpha ignored), 0 (black) to 1 (white).</summary>
    public static float Luminance(Vector4 color) =>
        0.2126f * Linear(color.X) + 0.7152f * Linear(color.Y) + 0.0722f * Linear(color.Z);

    /// <summary>WCAG contrast ratio of two opaque colours, 1 to 21, whichever is lighter.</summary>
    public static float Contrast(Vector4 a, Vector4 b)
    {
        var la = Luminance(a);
        var lb = Luminance(b);
        return (MathF.Max(la, lb) + 0.05f) / (MathF.Min(la, lb) + 0.05f);
    }

    /// <summary>WCAG 2.x AA for body text (1.4.3): 4.5 : 1.</summary>
    public const float AaText = 4.5f;

    /// <summary>WCAG 2.x AA for large text and UI components (1.4.3, 1.4.11): 3 : 1.</summary>
    public const float AaNonText = 3f;

    /// <summary>WCAG 2.x AAA for body text (1.4.6): 7 : 1.</summary>
    public const float AaaText = 7f;

    /// <summary>
    /// The contrast of <paramref name="ink"/> on <paramref name="ground"/> as drawn over an opaque
    /// <paramref name="backdrop"/>: the ground composited over the backdrop, then the ink over that, each with its alpha.
    /// </summary>
    public static float ContrastOn(Vector4 ink, Vector4 ground, Vector4 backdrop)
    {
        var bg = Over(ground, Opaque(backdrop));
        return Contrast(Over(ink, bg), bg);
    }

    /// <summary>Whether <paramref name="ink"/> reads as body text on the opaque <paramref name="ground"/> (<see cref="AaText"/>).</summary>
    public static bool ReadsAsText(Vector4 ink, Vector4 ground) => Contrast(ink, ground) >= AaText;

    /// <summary>The colour's RGB as 0xRRGGBB (rounded; alpha ignored): for messages and the palette tests.</summary>
    public static uint ToHex(Vector4 color)
    {
        static uint Channel(float v) => (uint)Math.Clamp((int)MathF.Round((float.IsFinite(v) ? v : 0f) * 255f), 0, 255);
        return (Channel(color.X) << 16) | (Channel(color.Y) << 8) | Channel(color.Z);
    }

    /// <summary><paramref name="top"/> composited over an opaque <paramref name="bottom"/> with the top's alpha; the result is opaque.</summary>
    public static Vector4 Over(Vector4 top, Vector4 bottom)
    {
        var a = Math.Clamp(float.IsFinite(top.W) ? top.W : 1f, 0f, 1f);
        return new Vector4(
            top.X * a + bottom.X * (1f - a),
            top.Y * a + bottom.Y * (1f - a),
            top.Z * a + bottom.Z * (1f - a),
            1f);
    }

    /// <summary>Linear mix of two colours' RGB by <paramref name="t"/> (clamped 0..1); alpha from <paramref name="a"/>.</summary>
    public static Vector4 Mix(Vector4 a, Vector4 b, float t)
    {
        t = float.IsFinite(t) ? Math.Clamp(t, 0f, 1f) : 0f;
        return new Vector4(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t, a.W);
    }

    /// <summary>The same colour fully opaque.</summary>
    public static Vector4 Opaque(Vector4 color) => color with { W = 1f };

    /// <summary>
    /// <paramref name="color"/> if it already reaches <paramref name="minRatio"/> against <paramref name="background"/>;
    /// otherwise the first step of the mix towards <paramref name="towards"/> (in 5 % steps) that does, or
    /// <paramref name="towards"/> itself when no step does. Used so a derived secondary text colour never drops below
    /// AA whatever the host style is.
    /// </summary>
    public static Vector4 EnsureContrast(Vector4 color, Vector4 towards, Vector4 background, float minRatio)
    {
        if (Contrast(color, background) >= minRatio)
        {
            return color;
        }

        for (var step = 1; step <= 20; step++)
        {
            var candidate = Mix(color, towards, step * 0.05f);
            if (Contrast(candidate, background) >= minRatio)
            {
                return candidate;
            }
        }

        return towards with { W = color.W };
    }

    /// <summary>
    /// <see cref="EnsureContrast(Vector4, Vector4, Vector4, float)"/> against every one of <paramref name="backgrounds"/>
    /// (the grounds a mark sits on in turn: a list item at rest, hovered, selected): <paramref name="color"/> if it reaches
    /// <paramref name="minRatio"/> on all of them, else the first 5 % step towards <paramref name="towards"/> that does, or
    /// <paramref name="towards"/> itself. Allocation-free.
    /// </summary>
    public static Vector4 EnsureContrast(Vector4 color, Vector4 towards, ReadOnlySpan<Vector4> backgrounds, float minRatio)
    {
        for (var step = 0; step <= 20; step++)
        {
            var candidate = step == 0 ? color : Mix(color, towards, step * 0.05f);
            var reads = true;
            foreach (var background in backgrounds)
            {
                reads &= Contrast(candidate, background) >= minRatio;
            }

            if (reads)
            {
                return candidate;
            }
        }

        return towards with { W = color.W };
    }

    private static float Linear(float channel)
    {
        channel = float.IsFinite(channel) ? Math.Clamp(channel, 0f, 1f) : 0f;
        return channel <= 0.04045f ? channel / 12.92f : MathF.Pow((channel + 0.055f) / 1.055f, 2.4f);
    }
}

/// <summary>
/// The surface and text roles the chrome draws with (ui-revamp §4.3): the Night palette by default, or one derived
/// from the user's Dalamud style when "Follow Dalamud colours" is on (game UX panel finding 9: one layout, two
/// palettes). It is one part of a <see cref="Themes.UiPalette"/>; gold, the danger tone and the scene live beside it
/// there, and the glyph colours stay fixed in every palette.
/// </summary>
/// <param name="Window">Window body (Night).</param>
/// <param name="Sunken">Wells: search pill, gauge wells, level pills (NightSunken).</param>
/// <param name="Raised">Cards, chips, table header (NightRaised).</param>
/// <param name="Hover">Hover fill for rows and buttons (NightHover).</param>
/// <param name="Line">Hairlines and card borders (NightLine).</param>
/// <param name="StrongLine">Dividers and non-focus outlines that must reach 3 : 1 (VeilLine).</param>
/// <param name="Text">Primary text (Silver).</param>
/// <param name="TextSecondary">Secondary text and hints, at least 4.5 : 1 on <paramref name="Window"/> (Mist).</param>
/// <param name="TextTertiary">Tertiary text, rings, chevrons (Dusk).</param>
/// <param name="TextDisabled">Disabled text (Veil).</param>
/// <param name="Light">The window colour is light (luminance over 0.5), so outlines and shadows go light instead of dark.</param>
/// <param name="Deep">The deepest surface: the rail, wells behind banners (Abyss).</param>
/// <param name="Top">The top stop of a pane's sky-over-water gradient (NightTop); the window itself where there is none.</param>
/// <param name="Ornament">Ornament hairlines: rules, dividers, corner marks, the rail thread (Gilt). At least 3 : 1 on the window when opaque.</param>
/// <param name="OrnamentHigh">Ornament highlight points under 4 px (GiltHigh). At least 3 : 1 on the window.</param>
/// <param name="Cool">The cool accent, usable as text: at least 4.5 : 1 on the window (Tide).</param>
/// <param name="CoolDeep">The cool surface stop of drawn skies and water (TideDeep); part of a picture, so it stays fixed.</param>
public readonly record struct SurfaceColors(
    Vector4 Window,
    Vector4 Sunken,
    Vector4 Raised,
    Vector4 Hover,
    Vector4 Line,
    Vector4 StrongLine,
    Vector4 Text,
    Vector4 TextSecondary,
    Vector4 TextTertiary,
    Vector4 TextDisabled,
    bool Light,
    Vector4 Deep,
    Vector4 Top,
    Vector4 Ornament,
    Vector4 OrnamentHigh,
    Vector4 Cool,
    Vector4 CoolDeep)
{
    /// <summary>Minimum contrast of <see cref="TextSecondary"/> on <see cref="Window"/> (WCAG AA for text).</summary>
    public const float TextMinContrast = 4.5f;

    /// <summary>
    /// The lighter gilt that text in the gilt is drawn in (plan v7: Full's Section headings, the sorted column header):
    /// the medallion's GiltLight <c>#E6CF98</c>, pushed towards <see cref="Text"/> until it reads as text on the
    /// window (<see cref="TextMinContrast"/>). On Night it is the hex itself (about 9.5 : 1 on Raised).
    /// </summary>
    public Vector4 OrnamentLight => ColorMath.EnsureContrast(GlyphTokens.Medallion.GiltHigh, Text, Window, TextMinContrast);

    /// <summary>Minimum contrast of <see cref="StrongLine"/> on <see cref="Window"/> (WCAG 1.4.11 for UI components).</summary>
    public const float LineMinContrast = 3f;

    /// <summary>Minimum contrast of <see cref="Cool"/> on <see cref="Window"/> in the high-contrast palette (WCAG AAA for text).</summary>
    public const float HighContrastTextMinContrast = 7f;

    /// <summary>How far towards black <see cref="Deep"/> is under a dark host window (proposal §3: WindowBg darkened 35 %).</summary>
    public const float DeepDarkenDark = 0.35f;

    /// <summary>The same under a light host window: a light rail stays light, a little under the sunken wells.</summary>
    public const float DeepDarkenLight = 0.10f;

    /// <summary>
    /// #8B94B3 – Night's tertiary text since 1.16 (spec-1.16 §A2): Dusk #7C86A8 was 3.8 : 1 on Hover and 4.3 : 1 on
    /// Raised; this reads at 4.5 : 1 on all four surfaces. The glyphs keep Dusk.
    /// </summary>
    public const uint NightTextTertiaryHex = 0x8B94B3;

    /// <summary>
    /// #646D8A – Night's strong line since 1.16 (spec-1.16 §A2): VeilLine #5C6584 was 2.7 : 1 on Raised; this is 3 : 1.
    /// The glyphs keep VeilLine.
    /// </summary>
    public const uint NightStrongLineHex = 0x646D8A;

    /// <summary>
    /// The Night palette (ui-revamp §4.3, moon-road proposal §3, the spec-1.16 §A2 contrast fixes): what the chrome
    /// draws with unless the user follows Dalamud's colours.
    /// </summary>
    public static readonly SurfaceColors Night = new(
        GlyphTokens.Night,
        GlyphTokens.NightSunken,
        GlyphTokens.NightRaised,
        GlyphTokens.NightHover,
        GlyphTokens.NightLine,
        ColorMath.FromHex(NightStrongLineHex),
        GlyphTokens.Silver,
        GlyphTokens.Mist,
        ColorMath.FromHex(NightTextTertiaryHex),
        GlyphTokens.Veil,
        Light: false,
        Deep: GlyphTokens.Abyss,
        Top: GlyphTokens.NightTop,
        Ornament: GlyphTokens.Gilt,
        OrnamentHigh: GlyphTokens.GiltHigh,
        Cool: GlyphTokens.Tide,
        CoolDeep: GlyphTokens.TideDeep);

    /// <summary>
    /// The Moon Road roles as the high-contrast palette draws them (proposal §10.2): no pane gradient
    /// (<see cref="Top"/> is the window), ornament lines in <see cref="StrongLine"/> (VeilLine on Night, 3.2 : 1, drawn
    /// at full alpha) instead of brass at partial alpha, highlight points at least 3 : 1, and the cool accent pushed
    /// towards the text colour until it reads at 7 : 1. The other roles are unchanged.
    /// </summary>
    public SurfaceColors ForHighContrast() => this with
    {
        Top = Window,
        Ornament = StrongLine,
        OrnamentHigh = ColorMath.EnsureContrast(OrnamentHigh, Text, Window, LineMinContrast),
        Cool = ColorMath.EnsureContrast(Cool, Text, Window, HighContrastTextMinContrast),
    };

    /// <summary>
    /// The palette mapped from a host style (the user's Dalamud colours), per the UX panel's mapping: Window ←
    /// WindowBg, Raised ← FrameBg over it, Hover ← FrameBgHovered over it, Line ← Border over it, Text ← Text, tertiary ←
    /// TextDisabled. Secondary text is the Text/TextDisabled midpoint, pushed towards Text until it reaches
    /// <see cref="TextMinContrast"/> on the window; the strong line is pushed towards Text until it reaches
    /// <see cref="LineMinContrast"/>. Every colour is opaque. Translucent host colours are composited over the window.
    /// </summary>
    public static SurfaceColors FromHost(
        Vector4 windowBg,
        Vector4 frameBg,
        Vector4 frameBgHovered,
        Vector4 border,
        Vector4 text,
        Vector4 textDisabled)
    {
        var window = ColorMath.Opaque(windowBg);
        var light = ColorMath.Luminance(window) > 0.5f;
        var textOpaque = ColorMath.Opaque(text);
        var raised = ColorMath.Over(frameBg, window);
        if (ColorMath.Contrast(raised, window) < 1.08f)
        {
            // A host whose frames are invisible on the window still needs cards that read as raised.
            raised = ColorMath.Mix(window, textOpaque, 0.07f);
        }

        var hover = ColorMath.Over(frameBgHovered, window);
        if (ColorMath.Contrast(hover, window) < 1.12f)
        {
            hover = ColorMath.Mix(window, textOpaque, 0.12f);
        }

        var black = new Vector4(0f, 0f, 0f, 1f);
        var sunken = ColorMath.Mix(window, black, light ? 0.06f : 0.3f);
        var line = ColorMath.Over(border, window);
        var strongLine = ColorMath.EnsureContrast(ColorMath.Mix(line, textOpaque, 0.25f), textOpaque, window, LineMinContrast);
        var tertiary = ColorMath.Opaque(textDisabled);
        var secondary = ColorMath.EnsureContrast(ColorMath.Mix(textOpaque, tertiary, 0.5f), textOpaque, window, TextMinContrast);
        var disabled = ColorMath.Mix(tertiary, window, 0.3f);

        // The Moon Road roles (proposal §3): the deep surface is the window darkened; the gradient's top is a small
        // step towards the text colour on a dark window (sky lighter than water) and towards white on a light one; the
        // brass and tide inks keep their hue, pushed towards the text colour until they read on the window.
        var white = new Vector4(1f, 1f, 1f, 1f);
        var deep = ColorMath.Mix(window, black, light ? DeepDarkenLight : DeepDarkenDark);
        var top = light ? ColorMath.Mix(window, white, 0.5f) : ColorMath.Mix(window, textOpaque, 0.04f);
        var ornament = ColorMath.EnsureContrast(GlyphTokens.Gilt, textOpaque, window, LineMinContrast);
        var ornamentHigh = ColorMath.EnsureContrast(GlyphTokens.GiltHigh, textOpaque, window, LineMinContrast);
        var cool = ColorMath.EnsureContrast(GlyphTokens.Tide, textOpaque, window, TextMinContrast);
        return new SurfaceColors(
            window, sunken, raised, hover, line, strongLine, textOpaque, secondary, tertiary, disabled, light,
            deep, top, ornament, ornamentHigh, cool, GlyphTokens.TideDeep);
    }
}
