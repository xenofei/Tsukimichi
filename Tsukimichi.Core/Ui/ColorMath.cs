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

    private static float Linear(float channel)
    {
        channel = float.IsFinite(channel) ? Math.Clamp(channel, 0f, 1f) : 0f;
        return channel <= 0.04045f ? channel / 12.92f : MathF.Pow((channel + 0.055f) / 1.055f, 2.4f);
    }
}

/// <summary>
/// The surface and text roles the chrome draws with (ui-revamp §4.3): the Night palette by default, or one derived
/// from the user's Dalamud style when "Follow Dalamud colours" is on (game UX panel finding 9: one layout, two
/// palettes). Gold, Eclipse and the glyph colours are not part of it; they stay fixed in both palettes.
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
    bool Light)
{
    /// <summary>Minimum contrast of <see cref="TextSecondary"/> on <see cref="Window"/> (WCAG AA for text).</summary>
    public const float TextMinContrast = 4.5f;

    /// <summary>Minimum contrast of <see cref="StrongLine"/> on <see cref="Window"/> (WCAG 1.4.11 for UI components).</summary>
    public const float LineMinContrast = 3f;

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
        return new SurfaceColors(window, sunken, raised, hover, line, strongLine, textOpaque, secondary, tertiary, disabled, light);
    }
}
