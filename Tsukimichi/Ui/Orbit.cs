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
/// a full-moon pip at 12 o'clock; at 0 % only the track shows and the icon draws at 60 % alpha. Ready is a gold pip
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

    /// <summary>The ring's unlit track: VeilLine at 0.55 (proposal §3, OrbitTrack).</summary>
    public static readonly uint TrackU32 = Theme.WithAlpha(Theme.VeilLine, 0.55f);

    /// <summary>
    /// Draws the orbit in the square box of side <paramref name="box"/> px at <paramref name="min"/>.
    /// <paramref name="fraction"/> is done/total (clamped; NaN is 0). <paramref name="rounding"/> rounds the icon's
    /// corners in px (3 logical for square icons; pass 0 for circular ones such as the expansion rings).
    /// <paramref name="highContrast"/> draws a 3 px arc with a Night gap and a full-alpha track (proposal §10.2).
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

        // Icon.
        var iconSize = IconLogical * k;
        var iconMin = center - new Vector2(iconSize * 0.5f);
        var iconMax = iconMin + new Vector2(iconSize);
        var alpha = f > 0f ? 1f : UntouchedIconAlpha;
        DrawIcon(dl, textures, icon, iconMin, iconMax, alpha, rounding < 0f ? 3f * k : rounding);

        // Track and arc.
        var r = RingRadiusLogical * k;
        var stroke = (highContrast ? 3f : StrokeLogical) * k;
        var track = highContrast ? Theme.VeilLineU32 : TrackU32;
        dl.AddCircle(center, r, track, RingSegments, stroke);

        var start = GaugeGeometry.StartAngle;
        if (f >= 1f)
        {
            dl.AddCircle(center, r, Theme.MoonU32, RingSegments, stroke);
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
                dl.PathStroke(Theme.NightU32, ImDrawFlags.None, stroke + 2f * k);
            }

            dl.PathClear();
            dl.PathArcTo(center, r, start, start + sweep, GaugeGeometry.ArcSegments(RingSegments, sweep));
            dl.PathStroke(Theme.MoonU32, ImDrawFlags.None, stroke);
            dl.AddCircleFilled(center + new Vector2(0f, -r), stroke * 0.5f, Theme.MoonU32);
            var (sin, cos) = MathF.SinCos(start + sweep);
            Bead(dl, center + new Vector2(cos, sin) * r, k, full: false);
        }

        // Ready: a gold pip on the ring at 2 o'clock, ringed in Night so it separates from the arc.
        if (ready)
        {
            var (sin, cos) = MathF.SinCos(start + MathF.PI / 3f);
            var at = center + new Vector2(cos, sin) * r;
            dl.AddCircleFilled(at, (ReadyPipLogical + 1f) * k, Theme.NightU32);
            dl.AddCircleFilled(at, ReadyPipLogical * k, Theme.MoonU32);
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
            var hiRes = max.X - min.X > LowResMaxPx;
            if (textures.GetFromGameIcon(new GameIconLookup(icon.IconId, false, hiRes)).TryGetWrap(out var wrap, out _))
            {
                dl.AddImageRounded(wrap.Handle, min, max, Vector2.Zero, Vector2.One, tint, rounding);
            }
            else
            {
                dl.AddRectFilled(min, max, Theme.NightRaisedU32, rounding);
            }

            return;
        }

        OrnamentAtlas.Draw(dl, icon.Glyph == OrnamentGlyph.None ? OrnamentGlyph.Other : icon.Glyph, min, max, tint);
    }

    private static void Bead(ImDrawListPtr dl, Vector2 at, float k, bool full)
    {
        dl.AddCircleFilled(at, BeadRimLogical * k, Theme.NightU32);
        dl.AddCircleFilled(at, BeadLogical * k, full ? Theme.MoonU32 : Theme.MoonHighU32);
    }
}
