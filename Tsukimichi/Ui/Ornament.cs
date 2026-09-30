using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The Moon Road ornament primitives (proposal §5), drawn with the draw list so they stay crisp at every scale: the
/// section rule (a fading Gilt hairline), the moon-road divider (two fading arms broken by three moon phases), the
/// sigil star and the pane gradient (sky over water). Colours come from the frame's palette: brass
/// (<see cref="SurfaceColors.Ornament"/>, Gilt on Night) for lines, GiltHigh for points, NightTop for the gradient, and
/// the high-contrast palette's own versions (opaque VeilLine lines, no gradient) when it is on. Brass is decoration only:
/// never text, never a fill over 4 px, never the only carrier of meaning. Callers check <see cref="Theme.Flair"/>
/// (<see cref="Theme.ShowRules"/>, <see cref="Theme.ShowPaneGradient"/>) before drawing. Every method is allocation-free.
/// </summary>
public static class Ornament
{
    /// <summary>The section rule's opacity at its start (proposal §3, GiltRule: Gilt 0.7 → 0).</summary>
    public const float RuleAlpha = 0.7f;

    /// <summary>The divider arms' peak opacity (Gilt 0 → 0.8 → 0).</summary>
    public const float DividerAlpha = 0.8f;

    /// <summary>How far down a pane the gradient runs before the pane is flat Night (proposal §3: 60 %).</summary>
    public const float GradientFade = 0.6f;

    /// <summary>
    /// The section rule: a hairline from <paramref name="start"/> running <paramref name="width"/> px right, the
    /// palette's ornament colour (or <paramref name="color"/>) at <paramref name="alpha"/> fading to nothing; under high
    /// contrast the line is solid and opaque from end to end (proposal §10.2). <paramref name="thickness"/> defaults to
    /// one pixel.
    /// </summary>
    public static void Rule(ImDrawListPtr dl, Vector2 start, float width, float alpha = RuleAlpha, float thickness = 1f, Vector4? color = null)
    {
        if (!(width > 0f))
        {
            return;
        }

        var c = color ?? Theme.Surface.Ornament;
        var top = MathF.Floor(start.Y);
        var min = new Vector2(start.X, top);
        var max = new Vector2(start.X + width, top + MathF.Max(1f, thickness));
        if (Theme.Glyphs.HighContrast)
        {
            dl.AddRectFilled(min, max, Theme.WithAlpha(c, 1f));
            return;
        }

        var from = Theme.WithAlpha(c, Theme.OrnamentAlpha(alpha));
        var to = Theme.WithAlpha(c, 0f);
        dl.AddRectFilledMultiColor(min, max, from, to, to, from);
    }

    /// <summary>
    /// The moon-road divider centred on <paramref name="center"/>, <paramref name="width"/> px wide: two arms fading in
    /// toward the middle, then the three phases (waxing crescent, full moon, waning crescent) from the atlas, drawn
    /// <paramref name="height"/> px tall (12 logical at 1x). Without the atlas the phases are three GiltHigh dots.
    /// </summary>
    public static void Divider(ImDrawListPtr dl, Vector2 center, float width, float height)
    {
        if (!(width > 0f) || !(height > 0f))
        {
            return;
        }

        var phaseWidth = height * (40f / 12f);
        var gap = height * 0.4f;
        var armWidth = MathF.Max(0f, (width - phaseWidth) * 0.5f - gap);
        var y = MathF.Floor(center.Y);
        var line = Theme.Surface.Ornament;
        var peak = Theme.WithAlpha(line, Theme.OrnamentAlpha(DividerAlpha));
        var clear = Theme.WithAlpha(line, 0f);
        var left = center.X - phaseWidth * 0.5f - gap;
        var right = center.X + phaseWidth * 0.5f + gap;
        if (armWidth > 0f)
        {
            dl.AddRectFilledMultiColor(new Vector2(left - armWidth, y), new Vector2(left, y + 1f), clear, peak, peak, clear);
            dl.AddRectFilledMultiColor(new Vector2(right, y), new Vector2(right + armWidth, y + 1f), peak, clear, clear, peak);
        }

        var min = new Vector2(center.X - phaseWidth * 0.5f, center.Y - height * 0.5f);
        if (!OrnamentAtlas.Draw(dl, OrnamentSprite.DividerPhases, min, min + new Vector2(phaseWidth, height)))
        {
            var r = height * 0.2f;
            dl.AddCircleFilled(center, r * 1.3f, Theme.MoonU32);
            dl.AddCircleFilled(center - new Vector2(phaseWidth * 0.3f, 0f), r, Theme.OrnamentHighU32);
            dl.AddCircleFilled(center + new Vector2(phaseWidth * 0.3f, 0f), r, Theme.OrnamentHighU32);
        }
    }

    /// <summary>
    /// The four-point sigil star, <paramref name="size"/> px across (8–12 at 1x), centred on <paramref name="center"/>:
    /// two crossed diamonds (each convex, so the fill is exact) in the palette's ornament highlight (GiltHigh on Night)
    /// and a MoonHigh pip. Allocation-free.
    /// </summary>
    public static void Sigil(ImDrawListPtr dl, Vector2 center, float size, uint? color = null)
    {
        if (!(size > 0f))
        {
            return;
        }

        var c = color ?? Theme.OrnamentHighU32;
        var r = size * 0.5f;
        var w = r * 0.2f;
        dl.AddQuadFilled(center + new Vector2(0f, -r), center + new Vector2(w, 0f), center + new Vector2(0f, r), center + new Vector2(-w, 0f), c);
        dl.AddQuadFilled(center + new Vector2(-r, 0f), center + new Vector2(0f, -w), center + new Vector2(r, 0f), center + new Vector2(0f, w), c);
        dl.AddCircleFilled(center, MathF.Max(0.8f, r * 0.17f), Theme.MoonHighU32);
    }

    /// <summary>
    /// A pane's sky-over-water gradient over the pane's own background: the palette's top colour
    /// (<see cref="SurfaceColors.Top"/>, NightTop on Night) at <paramref name="alpha"/> at the top, fading to clear at
    /// <paramref name="fade"/> of the height, so over Night it reads NightTop → Night and below that the pane is flat.
    /// Drawn as an overlay, it keeps the user's window opacity when <paramref name="alpha"/> is the window's
    /// (<see cref="Theme.WindowAlpha"/>). One <c>AddRectFilledMultiColor</c>, drawn before the pane's content. Callers
    /// draw it only while <see cref="Theme.ShowPaneGradient"/> (Full flair; high contrast is at most Quiet).
    /// </summary>
    public static void PaneGradient(ImDrawListPtr dl, Vector2 min, Vector2 max, float fade = GradientFade, float alpha = 1f)
    {
        if (!(max.X > min.X) || !(max.Y > min.Y) || !(alpha > 0f))
        {
            return;
        }

        var top = Theme.Surface.Top;
        var from = Theme.WithAlpha(top, alpha);
        var to = Theme.WithAlpha(top, 0f);
        var split = min.Y + (max.Y - min.Y) * Math.Clamp(fade, 0f, 1f);
        dl.AddRectFilledMultiColor(min, new Vector2(max.X, split), from, from, to, to);
    }
}
