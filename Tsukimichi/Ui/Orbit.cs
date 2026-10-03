using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The orbit (Moon Road proposal §6.3): a journal node's identity icon inside a ring that carries its progress. The
/// icon sits in the middle (an official game icon, or a gap glyph from the ornament atlas); an OrbitTrack ring runs
/// round it; a Moon arc from 12 o'clock clockwise is the completed fraction, with round caps and a moon bead (a
/// MoonHigh disc on a Night disc) at its head, so a 3 % arc still shows. At 100 % the ring closes and the bead becomes
/// a full-moon pip at 12 o'clock; at 0 % only the track shows and the icon draws at 60 % alpha. Ready is a gold spark
/// at 2 o'clock. Settings-independent: every size and switch is a parameter; nothing allocates.
/// </summary>
public static class Orbit
{
    /// <summary>The proposal's geometry in its 28 px box: an 18 px icon, a ring of radius 13 and stroke 2, a bead of 2.2 on 2.8.</summary>
    public const float BoxLogical = 28f;
    public const float IconLogical = 18f;
    public const float RingRadiusLogical = 13f;
    public const float StrokeLogical = 2f;
    public const float BeadLogical = 2.2f;
    public const float BeadRimLogical = 2.8f;
    public const float ReadyPipLogical = 3f;

    /// <summary>Icon alpha for a node with nothing done (the audit: untouched chapters recede).</summary>
    public const float UntouchedIconAlpha = 0.6f;

    /// <summary>At or below this many physical pixels a game icon draws its native 32 px texture (HiRes off): crisper than a scaled-down hi-res.</summary>
    public const float LowResMaxPx = 24f;

    private const int RingSegments = 32;

    /// <summary>The ring's unlit track: the palette's gauge track (the strong line at 0.55 on Night, proposal §3, OrbitTrack; a light palette's keyline).</summary>
    public static uint TrackU32 => Theme.U32(Theme.Gauges.Track);

    /// <summary>A flat ring's lit arc (high contrast, the Classic moons, Flight's bead ring): Moon on Night, a light palette's gauge gilt.</summary>
    public static uint ArcU32 => Theme.U32(Theme.Gauges.Arc);

    /// <summary>The filling moon's radius inside an orbit (the mockup's 8.5 in a ring of 14).</summary>
    public const float MoonLogical = 8f;

    /// <summary>
    /// Draws the orbit in the square box of side <paramref name="box"/> px at <paramref name="min"/>.
    /// <paramref name="fraction"/> is done/total (clamped; NaN is 0). <paramref name="rounding"/> rounds the icon's
    /// corners in px (-1: 3 logical for square icons, none for the circular expansion rings).
    /// <paramref name="highContrast"/> draws a 3 px arc with a Night gap, a full-alpha track and a Silver keyline round
    /// official icons (proposal §10.2). While the icon's texture loads a NightRaised square stands in; see
    /// <see cref="TryDraw"/> to draw nothing instead.
    /// </summary>
    public static void Draw(
        ImDrawListPtr dl,
        ITextureProvider textures,
        Vector2 min,
        float box,
        NodeIcon icon,
        float fraction,
        bool ready = false,
        float rounding = -1f,
        bool highContrast = false)
    {
        if (!(box > 0f))
        {
            return;
        }

        var k = box / BoxLogical;
        var center = min + new Vector2(box * 0.5f);
        var f = GaugeGeometry.Clamp01(fraction);
        var (iconMin, iconMax) = IconRect(center, k);
        var round = Rounding(icon, rounding, k);
        DrawIcon(dl, textures, icon, iconMin, iconMax, f > 0f ? 1f : UntouchedIconAlpha, round);
        Finish(dl, icon, center, k, f, iconMin, iconMax, round, ready, highContrast);
    }

    /// <summary>
    /// <see cref="Draw"/> only once the icon can show: false, with nothing drawn, for an empty icon or while its texture
    /// (the game icon, or the ornament atlas for a gap glyph) is still loading, so the caller can draw its own
    /// placeholder (the Journal tree keeps the moon halo). One texture lookup per call; nothing allocates.
    /// </summary>
    public static bool TryDraw(
        ImDrawListPtr dl,
        ITextureProvider textures,
        Vector2 min,
        float box,
        NodeIcon icon,
        float fraction,
        bool ready = false,
        float rounding = -1f,
        bool highContrast = false)
    {
        if (!(box > 0f) || icon.IsEmpty)
        {
            return false;
        }

        var k = box / BoxLogical;
        var center = min + new Vector2(box * 0.5f);
        var f = GaugeGeometry.Clamp01(fraction);
        var (iconMin, iconMax) = IconRect(center, k);
        var round = Rounding(icon, rounding, k);
        var tint = Theme.WithAlpha(Vector4.One, f > 0f ? 1f : UntouchedIconAlpha);
        if (icon.IsOfficial)
        {
            // An icon the game does not have fails like one still loading: the caller draws the halo.
            if (!textures.TryGetFromGameIcon(Lookup(icon, iconMax.X - iconMin.X), out var texture) || !texture.TryGetWrap(out var wrap, out _))
            {
                return false;
            }

            // A non-square icon (a duty's emblem) whole and centred.
            var (fitMin, fitMax) = GameIcon.Fit(wrap, iconMin, iconMax);
            dl.AddImageRounded(wrap.Handle, fitMin, fitMax, Vector2.Zero, Vector2.One, tint, round);
        }
        else if (!OrnamentAtlas.IsReady || !OrnamentAtlas.Draw(dl, icon.Glyph, iconMin, iconMax, tint))
        {
            return false;
        }

        Finish(dl, icon, center, k, f, iconMin, iconMax, round, ready, highContrast);
        return true;
    }

    /// <summary>
    /// The orbit round a filling moon instead of an icon: the rail's Journal station and its foot gauge (proposal §6.3,
    /// "the filling moon stays wherever a scope is summarised without an icon"). The moon is lit to
    /// <paramref name="fraction"/> inside the same track, arc and bead.
    /// </summary>
    public static void DrawMoon(ImDrawListPtr dl, Vector2 min, float box, float fraction, bool highContrast = false)
    {
        if (!(box > 0f))
        {
            return;
        }

        var k = box / BoxLogical;
        var center = min + new Vector2(box * 0.5f);
        var f = GaugeGeometry.Clamp01(fraction);
        MoonGlyph.DrawFilling(dl, center, MoonLogical * k, f);
        Ring(dl, center, k, f, highContrast);
    }

    /// <summary>
    /// Ready: a gold four-point spark with a gold heart on the ring at 2 o'clock, outlined in Night so it separates from
    /// the arc. Its place never moves and its shape (a star, not a disc) tells it from the moon bead without colour
    /// (proposal §6.3, §10.3).
    /// </summary>
    public static void ReadyMark(ImDrawListPtr dl, Vector2 center, float ringRadius, float k)
    {
        var (sin, cos) = MathF.SinCos(GaugeGeometry.StartAngle + (MathF.PI / 3f));
        var at = center + (new Vector2(cos, sin) * ringRadius);
        var pip = ReadyPipLogical * k;
        var spark = (2f * pip) + (3f * k);
        dl.AddCircleFilled(at, pip + k, Theme.U32(Theme.Surface.Window));
        Ornament.Sigil(dl, at, spark + (2.5f * k), Theme.U32(Theme.Surface.Window));
        Ornament.Sigil(dl, at, spark, Theme.GoldU32);
        dl.AddCircleFilled(at, pip * 0.75f, Theme.GoldU32);
    }

    private static (Vector2 Min, Vector2 Max) IconRect(Vector2 center, float k)
    {
        var half = new Vector2(IconLogical * k * 0.5f);
        return (center - half, center + half);
    }

    /// <summary>The expansion rings (<c>ExVersion.Icon</c>, <see cref="NodeIcons.IsExpansionRing"/>) are circles, drawn unrounded and keylined as circles.</summary>
    private static bool IsCircular(NodeIcon icon) => NodeIcons.IsExpansionRing(icon.IconId);

    private static float Rounding(NodeIcon icon, float rounding, float k) =>
        rounding >= 0f ? rounding : IsCircular(icon) ? 0f : 3f * k;

    /// <summary>The game icon at its native 32 px (HiRes off) for ≤ 24 physical px, the hi-res texture above that.</summary>
    private static GameIconLookup Lookup(NodeIcon icon, float sizePx) => new(icon.IconId, false, sizePx > LowResMaxPx);

    /// <summary>After the icon: the high-contrast keyline, the ring and the Ready mark.</summary>
    private static void Finish(ImDrawListPtr dl, NodeIcon icon, Vector2 center, float k, float f, Vector2 iconMin, Vector2 iconMax, float rounding, bool ready, bool highContrast)
    {
        if (highContrast && icon.IsOfficial)
        {
            // A 1 px Silver keyline, so dark tribe tiles do not melt into Night (proposal §10.2).
            if (IsCircular(icon))
            {
                dl.AddCircle(center, (iconMax.X - iconMin.X) * 0.5f, Theme.U32(Theme.Surface.Text), 24, 1f);
            }
            else
            {
                dl.AddRect(iconMin, iconMax, Theme.U32(Theme.Surface.Text), rounding, ImDrawFlags.None, 1f);
            }
        }

        Ring(dl, center, k, f, highContrast);
        if (ready)
        {
            ReadyMark(dl, center, RingRadiusLogical * k, k);
        }
    }

    /// <summary>
    /// The track, the arc from 12 o'clock with round caps, and the bead at its head (a full-moon pip once closed). Since
    /// 1.12 (feature plan v6 G5) in the medal's material: a lapis groove between Abyss keylines, a gilt arc lit from the
    /// upper left and a moonstone pearl (<see cref="MedalGauge"/>); high contrast and the Classic moon style
    /// (<see cref="Theme.ClassicMoons"/>) keep the 1.11 ring.
    /// </summary>
    private static void Ring(ImDrawListPtr dl, Vector2 center, float k, float f, bool highContrast)
    {
        if (!highContrast && !Theme.ClassicMoons)
        {
            MedalRing(dl, center, k, f);
            return;
        }

        var r = RingRadiusLogical * k;
        var stroke = (highContrast ? 3f : StrokeLogical) * k;
        var track = highContrast ? Theme.U32(Theme.Surface.StrongLine) : TrackU32;
        dl.AddCircle(center, r, track, RingSegments, stroke);

        var start = GaugeGeometry.StartAngle;
        if (f >= 1f)
        {
            dl.AddCircle(center, r, ArcU32, RingSegments, stroke);
            Bead(dl, center + new Vector2(0f, -r), k, full: true);
        }
        else if (f > 0f)
        {
            // A small visual floor so the arc always reads past its caps; the bead carries the rest.
            var visual = MathF.Max(f, (stroke + 1.5f) / (2f * MathF.PI * r));
            var sweep = 2f * MathF.PI * MathF.Min(visual, 0.999f);
            if (highContrast)
            {
                dl.PathClear();
                dl.PathArcTo(center, r, start, start + sweep, GaugeGeometry.ArcSegments(RingSegments, sweep));
                dl.PathStroke(Theme.U32(Theme.Surface.Window), ImDrawFlags.None, stroke + (2f * k));
            }

            dl.PathClear();
            dl.PathArcTo(center, r, start, start + sweep, GaugeGeometry.ArcSegments(RingSegments, sweep));
            dl.PathStroke(ArcU32, ImDrawFlags.None, stroke);
            dl.AddCircleFilled(center + new Vector2(0f, -r), stroke * 0.5f, ArcU32);
            var (sin, cos) = MathF.SinCos(start + sweep);
            Bead(dl, center + (new Vector2(cos, sin) * r), k, full: false);
        }
    }

    /// <summary>
    /// The icon alone, in <paramref name="min"/>..<paramref name="max"/>: a game icon through the shared texture cache
    /// (the native 32 px texture at ≤ 24 physical px, the hi-res one above), else the atlas glyph; a NightRaised square
    /// while either loads.
    /// </summary>
    public static void DrawIcon(ImDrawListPtr dl, ITextureProvider textures, NodeIcon icon, Vector2 min, Vector2 max, float alpha = 1f, float rounding = 0f)
    {
        var tint = Theme.WithAlpha(Vector4.One, alpha);
        if (icon.IsOfficial)
        {
            if (!textures.TryGetFromGameIcon(Lookup(icon, max.X - min.X), out var texture))
            {
                // The game has no such icon: the generic gap glyph stands in for good.
                OrnamentAtlas.Draw(dl, OrnamentGlyph.Other, min, max, tint);
            }
            else if (texture.TryGetWrap(out var wrap, out _))
            {
                var (fitMin, fitMax) = GameIcon.Fit(wrap, min, max);
                dl.AddImageRounded(wrap.Handle, fitMin, fitMax, Vector2.Zero, Vector2.One, tint, rounding);
            }
            else
            {
                dl.AddRectFilled(min, max, Theme.U32(Theme.Surface.Raised), rounding);
            }

            return;
        }

        OrnamentAtlas.Draw(dl, icon.Glyph == OrnamentGlyph.None ? OrnamentGlyph.Other : icon.Glyph, min, max, tint);
    }

    /// <summary>The ring in the medal's material: groove, gilt arc with its floor, and a moonstone pearl on an Abyss bead.</summary>
    private static void MedalRing(ImDrawListPtr dl, Vector2 center, float k, float f)
    {
        var r = RingRadiusLogical * k;
        var stroke = StrokeLogical * k;
        var box = BoxLogical * k * 0.5f;
        MedalGauge.Groove(dl, center, r, stroke, RingSegments);
        var start = GaugeGeometry.StartAngle;
        if (f >= 1f)
        {
            MedalGauge.GiltArc(dl, center, r, stroke, start, 2f * MathF.PI, box, RingSegments);
            dl.AddCircleFilled(center + new Vector2(0f, -r), BeadRimLogical * k, MedalGauge.PearlRimU32);
            dl.AddCircleFilled(center + new Vector2(0f, -r), BeadLogical * k, MedalGauge.PearlU32);
            return;
        }

        if (f > 0f)
        {
            var visual = MathF.Max(f, (stroke + 1.5f) / (2f * MathF.PI * r));
            var sweep = 2f * MathF.PI * MathF.Min(visual, 0.999f);
            MedalGauge.GiltArc(dl, center, r, stroke, start, sweep, box, RingSegments);
            var (sin, cos) = MathF.SinCos(start + sweep);
            var at = center + (new Vector2(cos, sin) * r);
            dl.AddCircleFilled(at, BeadRimLogical * k, MedalGauge.PearlRimU32);
            dl.AddCircleFilled(at, BeadLogical * k, MedalGauge.PearlU32);
        }
    }

    private static void Bead(ImDrawListPtr dl, Vector2 at, float k, bool full)
    {
        dl.AddCircleFilled(at, BeadRimLogical * k, Theme.U32(Theme.Surface.Window));
        dl.AddCircleFilled(at, BeadLogical * k, full ? Theme.GoldU32 : Theme.GoldHighU32);
    }
}
