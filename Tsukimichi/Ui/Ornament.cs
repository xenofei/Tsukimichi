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
/// never text, never a fill over 4 px, never the only carrier of meaning. The rule and the divider follow the level's
/// <see cref="Theme.RuleStyle"/> themselves (brass at Full, a flat hairline at Quiet, the line at Plain); the brass-only
/// art (sigils, the frame, the star field, the gradient) is for callers that checked <see cref="Theme.MoonRoadArt"/>,
/// <see cref="Theme.ShowStars"/> or <see cref="Theme.ShowPaneGradient"/>. Every method is allocation-free.
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

        var top = MathF.Floor(start.Y);
        var min = new Vector2(start.X, top);
        var max = new Vector2(start.X + width, top + MathF.Max(1f, thickness));

        // Quiet and Plain: a flat structure line, full width, no fade (the brass is the Moon Road's alone).
        if (Theme.RuleStyle != RuleStyle.MoonRoad && color is null)
        {
            dl.AddRectFilled(min, max, Theme.U32(Theme.RuleColor));
            return;
        }

        var c = color ?? Theme.Surface.Ornament;
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

        // Quiet and Plain: the structure line across the width, no phases.
        if (Theme.RuleStyle != RuleStyle.MoonRoad)
        {
            var at = MathF.Floor(center.Y);
            dl.AddRectFilled(new Vector2(center.X - (width * 0.5f), at), new Vector2(center.X + (width * 0.5f), at + 1f), Theme.U32(Theme.RuleColor));
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

    /// <summary>The Full sky's zenith on Night (docs/design/flair-v13 §1): the top brightened toward indigo.</summary>
    public static readonly Vector4 Zenith = Core.Ui.ColorMath.FromHex(0x1B2552);

    /// <summary>How far down the Full sky falls to Night (46 %), and where the water's lift begins (70 %).</summary>
    public const float SkyFade = 0.46f;

    public const float WaterFrom = 0.70f;

    /// <summary>The water lift's alpha at the pane's foot (the top colour at 0.35).</summary>
    public const float WaterAlpha = 0.35f;

    /// <summary>
    /// Full's sky over water (docs/design/flair-v13 §1, "Pane background"), deeper than the 1.4 gradient: the zenith
    /// (indigo on Night, the palette's top colour otherwise) at the top, falling to the pane's own tone by
    /// <see cref="SkyFade"/>, then flat, then a faint lift of the top colour over the last 30 % (the water). Drawn as an
    /// overlay at <paramref name="alpha"/> (the window's opacity), before the pane's content.
    /// </summary>
    public static void SkyOverWater(ImDrawListPtr dl, Vector2 min, Vector2 max, float alpha = 1f)
    {
        if (!(max.X > min.X) || !(max.Y > min.Y) || !(alpha > 0f))
        {
            return;
        }

        var s = Theme.Surface;
        var zenith = Theme.FollowingDalamud ? s.Top : Zenith;
        var height = max.Y - min.Y;
        var from = Theme.WithAlpha(zenith, alpha);
        var clear = Theme.WithAlpha(zenith, 0f);
        dl.AddRectFilledMultiColor(min, new Vector2(max.X, min.Y + (height * SkyFade)), from, from, clear, clear);

        var waterTop = min.Y + (height * WaterFrom);
        var lift = Theme.WithAlpha(s.Top, WaterAlpha * alpha);
        var none = Theme.WithAlpha(s.Top, 0f);
        dl.AddRectFilledMultiColor(new Vector2(min.X, waterTop), max, none, none, lift, lift);
    }

    // ------------------------------------------------------------------ the star field (Full)

    /// <summary>A star's alpha by magnitude (spec §1, "Star field"): faint 1 × 1 at 0.16, small r 1 at 0.26, bright r 1.5 and a 7 px cross at 0.36.</summary>
    private const float FaintAlpha = 0.16f;
    private const float SmallAlpha = 0.26f;
    private const float BrightAlpha = 0.36f;

    /// <summary>The cool white most stars are; about one in six is warm (MoonHigh).</summary>
    private static readonly Vector4 StarCool = Core.Ui.ColorMath.FromHex(0xDDE6FF);

    /// <summary>
    /// The seeded star field in <paramref name="min"/>..<paramref name="max"/> (empty sky only: the caller passes a band
    /// with nothing on it), from <paramref name="stars"/> in the band's unit square (<see cref="StarField.Generate"/>), so
    /// it never shimmers frame to frame. Stars under <paramref name="avoidMin"/>..<paramref name="avoidMax"/> are skipped
    /// (a title, a label). Allocation-free.
    /// </summary>
    public static void Stars(ImDrawListPtr dl, Vector2 min, Vector2 max, Star[] stars, Vector2 avoidMin = default, Vector2 avoidMax = default)
    {
        if (!(max.X - min.X > 4f) || !(max.Y - min.Y > 4f))
        {
            return;
        }

        var size = max - min;
        var avoid = avoidMax.X > avoidMin.X && avoidMax.Y > avoidMin.Y;
        var unit = MathF.Max(1f, UiMetrics.Px(1f));
        for (var i = 0; i < stars.Length; i++)
        {
            var star = stars[i];
            var p = new Vector2(MathF.Round(min.X + (star.U * size.X)), MathF.Round(min.Y + (star.V * size.Y)));
            if (avoid && p.X >= avoidMin.X - 4f && p.X <= avoidMax.X + 4f && p.Y >= avoidMin.Y - 4f && p.Y <= avoidMax.Y + 4f)
            {
                continue;
            }

            var warm = (i * 7919 % 6) == 0;
            var tone = warm ? Theme.MoonHigh : StarCool;
            switch (star.Magnitude)
            {
                case StarMagnitude.Faint:
                    dl.AddRectFilled(p, p + new Vector2(unit), Theme.WithAlpha(tone, FaintAlpha));
                    break;
                case StarMagnitude.Small:
                    dl.AddCircleFilled(p, unit, Theme.WithAlpha(tone, SmallAlpha), 8);
                    break;
                default:
                    var arm = 3.5f * unit;
                    var ink = Theme.WithAlpha(tone, BrightAlpha);
                    dl.AddLine(p - new Vector2(arm, 0f), p + new Vector2(arm, 0f), ink, MathF.Max(0.7f, 0.7f * unit));
                    dl.AddLine(p - new Vector2(0f, arm), p + new Vector2(0f, arm), ink, MathF.Max(0.7f, 0.7f * unit));
                    dl.AddCircleFilled(p, 1.5f * unit, Theme.WithAlpha(tone, BrightAlpha * 1.4f), 10);
                    break;
            }
        }
    }

    // ------------------------------------------------------------------ gilt brass (Full's cards and tooltips)

    /// <summary>The brass's four stops, lit from the upper left (spec §1, "Card frame"): highlight, body, shadow, reflected, deep.</summary>
    public static readonly Vector4 BrassHigh = Core.Ui.ColorMath.FromHex(0xE2C78C);
    public static readonly Vector4 BrassShadow = Core.Ui.ColorMath.FromHex(0x6E5732);
    public static readonly Vector4 BrassReflected = Core.Ui.ColorMath.FromHex(0x9C8049);
    public static readonly Vector4 BrassDeep = Core.Ui.ColorMath.FromHex(0x5A4729);

    /// <summary>The corner marks: the top two lit (GiltHigh, brighter), the bottom two a darker brass.</summary>
    public static readonly Vector4 CornerLit = Core.Ui.ColorMath.FromHex(0xF0D9A0);
    public static readonly Vector4 CornerShaded = Core.Ui.ColorMath.FromHex(0xB79755);

    /// <summary>
    /// The brass's colour at <paramref name="t"/> along its light (0 at the upper left, 1 at the lower right), a 160°
    /// gradient: highlight, Gilt at 28 %, shadow at 55 %, the reflected lift at 78 %, deep at the end.
    /// </summary>
    public static Vector4 Brass(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t switch
        {
            < 0.28f => Vector4.Lerp(BrassHigh, Theme.Gilt, t / 0.28f),
            < 0.55f => Vector4.Lerp(Theme.Gilt, BrassShadow, (t - 0.28f) / 0.27f),
            < 0.78f => Vector4.Lerp(BrassShadow, BrassReflected, (t - 0.55f) / 0.23f),
            _ => Vector4.Lerp(BrassReflected, BrassDeep, (t - 0.78f) / 0.22f),
        };
    }

    /// <summary>
    /// A gilt brass border round <paramref name="min"/>..<paramref name="max"/>: one rounded outline, each vertex coloured
    /// by where it sits along the light (a 160° gradient), so the top and left edges are lit and the bottom and right
    /// shaded and the brass reads as a metal edge, not a stroke. Its anti-aliased fringe keeps its own fade. Under the
    /// high-contrast palette (which never reaches Full, but a caller may draw one) it is the opaque ornament line.
    /// </summary>
    public static void BrassBorder(ImDrawListPtr dl, Vector2 min, Vector2 max, float rounding, float thickness)
    {
        if (!(max.X > min.X) || !(max.Y > min.Y))
        {
            return;
        }

        if (Theme.Glyphs.HighContrast)
        {
            dl.AddRect(min, max, Theme.WithAlpha(Theme.Surface.Ornament, 1f), rounding, ImDrawFlags.None, thickness);
            return;
        }

        var first = dl.VtxBuffer.Size;
        dl.AddRect(min, max, 0xFFFFFFFFu, rounding, ImDrawFlags.None, thickness);
        var vertices = dl.VtxBuffer;
        var size = max - min;

        // 160°: mostly down the card, a little to the right.
        var dir = new Vector2(0.342f, 0.940f);
        var span = MathF.Max(1f, MathF.Abs(size.X * dir.X) + MathF.Abs(size.Y * dir.Y));
        for (var i = first; i < vertices.Size; i++)
        {
            var vertex = vertices[i];
            var t = Vector2.Dot(vertex.Pos - min, dir) / span;
            var c = Brass(t);
            c.W = (vertex.Col >> 24) / 255f;
            vertex.Col = Theme.U32(c);
            vertices[i] = vertex;
        }
    }

    /// <summary>
    /// The corner marks of a brass frame: an L of <paramref name="size"/> px at each corner (or only the top-left and
    /// bottom-right with <paramref name="twoOnly"/>, the tooltip's), 1.5 px thick, overlapping the frame by a pixel so
    /// the mark and the frame read as one casting; the top marks lit, the bottom ones a darker brass, each with a faint
    /// warm glow.
    /// </summary>
    public static void CornerMarks(ImDrawListPtr dl, Vector2 min, Vector2 max, float size, bool twoOnly = false)
    {
        if (!(max.X - min.X > 2f * size) || !(max.Y - min.Y > 2f * size))
        {
            return;
        }

        var w = MathF.Max(1.5f, UiMetrics.Px(1.5f));

        // The mark's centre line sits half a pixel outside the frame line, so its 1.5 px cover the frame's pixel.
        var o = 0.25f * w;
        var a = min - new Vector2(o);
        var b = max + new Vector2(o);
        Mark(dl, a, new Vector2(1f, 0f), new Vector2(0f, 1f), size, w, CornerLit);
        if (!twoOnly)
        {
            Mark(dl, new Vector2(b.X, a.Y), new Vector2(-1f, 0f), new Vector2(0f, 1f), size, w, CornerLit);
            Mark(dl, new Vector2(a.X, b.Y), new Vector2(1f, 0f), new Vector2(0f, -1f), size, w, CornerShaded);
        }

        Mark(dl, b, new Vector2(-1f, 0f), new Vector2(0f, -1f), size, w, CornerShaded);
    }

    private static void Mark(ImDrawListPtr dl, Vector2 corner, Vector2 along, Vector2 down, float size, float width, Vector4 tone)
    {
        var glow = Theme.WithAlpha(Theme.Moon, 0.12f);
        dl.AddLine(corner + (along * size), corner, glow, width * 2.6f);
        dl.AddLine(corner, corner + (down * size), glow, width * 2.6f);
        var ink = Theme.U32(tone);
        dl.AddLine(corner + (along * size), corner - (along * (width * 0.5f)), ink, width);
        dl.AddLine(corner - (down * (width * 0.5f)), corner + (down * size), ink, width);
    }

    /// <summary>
    /// A soft shadow under a raised surface, falling straight down (the moon is the one light, upper left, far off):
    /// <paramref name="offset"/> px down, spread over <paramref name="blur"/> px, darkest at <paramref name="alpha"/>.
    /// Drawn before the surface, in a few rounded layers.
    /// </summary>
    public static void DropShadow(ImDrawListPtr dl, Vector2 min, Vector2 max, float rounding, float offset, float blur, float alpha)
    {
        const int Layers = 4;
        var down = new Vector2(0f, offset);
        for (var i = Layers; i >= 1; i--)
        {
            var grow = blur * i / Layers;
            dl.AddRectFilled(min + down - new Vector2(grow * 0.5f), max + down + new Vector2(grow * 0.5f), Theme.WithAlpha(Vector4.UnitW, alpha / Layers), rounding + grow);
        }
    }
}
