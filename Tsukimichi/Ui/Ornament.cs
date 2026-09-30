using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The Moon Road ornament primitives (proposal §5), drawn with the draw list so they stay crisp at every scale: the
/// section rule (a fading Gilt hairline), the moon-road divider (two fading arms broken by three moon phases), the
/// sigil star and the pane gradient (sky over water). Brass (Gilt) is decoration only: never text, never a fill over
/// 4 px, never the only carrier of meaning. Every method is allocation-free; colours are packed once.
/// </summary>
public static class Ornament
{
    public static readonly Vector4 Gilt = ColorMath.FromHex(OrnamentTokens.GiltHex);
    public static readonly Vector4 GiltHigh = ColorMath.FromHex(OrnamentTokens.GiltHighHex);
    public static readonly Vector4 NightTop = ColorMath.FromHex(OrnamentTokens.NightTopHex);
    public static readonly Vector4 Abyss = ColorMath.FromHex(OrnamentTokens.AbyssHex);
    public static readonly Vector4 Tide = ColorMath.FromHex(OrnamentTokens.TideHex);

    public static readonly uint GiltU32 = Theme.U32(Gilt);
    public static readonly uint GiltHighU32 = Theme.U32(GiltHigh);
    public static readonly uint NightTopU32 = Theme.U32(NightTop);
    public static readonly uint AbyssU32 = Theme.U32(Abyss);

    /// <summary>The section rule's opacity at its start (proposal §3, GiltRule: Gilt 0.7 → 0).</summary>
    public const float RuleAlpha = 0.7f;

    /// <summary>The divider arms' peak opacity (Gilt 0 → 0.8 → 0).</summary>
    public const float DividerAlpha = 0.8f;

    private static readonly uint GiltClear = Theme.WithAlpha(Gilt, 0f);

    /// <summary>
    /// The section rule: a hairline from <paramref name="start"/> running <paramref name="width"/> px right, Gilt at
    /// <paramref name="alpha"/> fading to nothing. <paramref name="thickness"/> defaults to one pixel. High contrast
    /// passes VeilLine and alpha 1 (proposal §10.2).
    /// </summary>
    public static void Rule(ImDrawListPtr dl, Vector2 start, float width, float alpha = RuleAlpha, float thickness = 1f, Vector4? color = null)
    {
        if (!(width > 0f))
        {
            return;
        }

        var c = color ?? Gilt;
        var from = Theme.WithAlpha(c, alpha);
        var to = Theme.WithAlpha(c, 0f);
        var top = MathF.Floor(start.Y);
        dl.AddRectFilledMultiColor(new Vector2(start.X, top), new Vector2(start.X + width, top + MathF.Max(1f, thickness)), from, to, to, from);
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
        var peak = Theme.WithAlpha(Gilt, DividerAlpha);
        var left = center.X - phaseWidth * 0.5f - gap;
        var right = center.X + phaseWidth * 0.5f + gap;
        if (armWidth > 0f)
        {
            dl.AddRectFilledMultiColor(new Vector2(left - armWidth, y), new Vector2(left, y + 1f), GiltClear, peak, peak, GiltClear);
            dl.AddRectFilledMultiColor(new Vector2(right, y), new Vector2(right + armWidth, y + 1f), peak, GiltClear, GiltClear, peak);
        }

        var min = new Vector2(center.X - phaseWidth * 0.5f, center.Y - height * 0.5f);
        if (!OrnamentAtlas.Draw(dl, OrnamentSprite.DividerPhases, min, min + new Vector2(phaseWidth, height)))
        {
            var r = height * 0.2f;
            dl.AddCircleFilled(center, r * 1.3f, Theme.MoonU32);
            dl.AddCircleFilled(center - new Vector2(phaseWidth * 0.3f, 0f), r, GiltHighU32);
            dl.AddCircleFilled(center + new Vector2(phaseWidth * 0.3f, 0f), r, GiltHighU32);
        }
    }

    /// <summary>
    /// The four-point sigil star, <paramref name="size"/> px across (8–12 at 1x), centred on <paramref name="center"/>:
    /// two GiltHigh diamonds crossed (each convex, so the fill is exact) and a MoonHigh pip. Allocation-free.
    /// </summary>
    public static void Sigil(ImDrawListPtr dl, Vector2 center, float size, uint? color = null)
    {
        if (!(size > 0f))
        {
            return;
        }

        var c = color ?? GiltHighU32;
        var r = size * 0.5f;
        var w = r * 0.2f;
        dl.AddQuadFilled(center + new Vector2(0f, -r), center + new Vector2(w, 0f), center + new Vector2(0f, r), center + new Vector2(-w, 0f), c);
        dl.AddQuadFilled(center + new Vector2(-r, 0f), center + new Vector2(0f, -w), center + new Vector2(r, 0f), center + new Vector2(0f, w), c);
        dl.AddCircleFilled(center, MathF.Max(0.8f, r * 0.17f), Theme.MoonHighU32);
    }

    /// <summary>
    /// A pane's sky-over-water gradient: NightTop at the top fading to <paramref name="bottom"/> (Night by default) at
    /// <paramref name="fade"/> of the height; with <paramref name="fillBelow"/> the rest is filled flat. One
    /// <c>AddRectFilledMultiColor</c>, drawn first, behind everything. Quiet and Plain flair skip it.
    /// </summary>
    public static void PaneGradient(ImDrawListPtr dl, Vector2 min, Vector2 max, float fade = 0.6f, bool fillBelow = false, uint? bottom = null)
    {
        if (!(max.X > min.X) || !(max.Y > min.Y))
        {
            return;
        }

        var low = bottom ?? Theme.NightU32;
        var split = min.Y + (max.Y - min.Y) * Math.Clamp(fade, 0f, 1f);
        dl.AddRectFilledMultiColor(min, new Vector2(max.X, split), NightTopU32, NightTopU32, low, low);
        if (fillBelow && split < max.Y)
        {
            dl.AddRectFilled(new Vector2(min.X, split), max, low);
        }
    }
}
