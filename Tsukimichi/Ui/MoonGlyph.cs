using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// Procedural moon-phase glyphs for quest states (spec §2.1), drawn with ImDrawList primitives so they scale with
/// the UI and need no textures. Geometry comes from <see cref="MoonGeometry"/>; this class only paints.
///
/// The look replicates assets/icons/render_icons.py: a dark base disc, the lit part as the intersection of the base
/// disc with an offset terminator disc, rings whose thickness scales with the radius, a soft glow for Ready, a
/// dashed ring for Unknown and a notch bitten out of the Foreclosed ring.
/// </summary>
public static class MoonGlyph
{
    /// <summary>Ring thickness as a fraction of the radius (never thinner than one pixel).</summary>
    private const float RingFraction = 0.07f;

    /// <summary>Glow discs for Ready, largest first: (radius multiplier, alpha). Approximates the Pillow quadratic halo.</summary>
    private static readonly (float Scale, float Alpha)[] Glow = [(1.6f, 0.04f), (1.35f, 0.07f), (1.15f, 0.10f)];

    /// <summary>Scratch polygon; UI drawing happens on one thread.</summary>
    private static readonly List<Vector2> Scratch = new(256);

    /// <summary>Draws the glyph for <paramref name="state"/> centred at <paramref name="center"/> with the given pixel radius.</summary>
    public static void Draw(ImDrawListPtr dl, Vector2 center, float radius, QuestState state)
    {
        if (!(radius > 0.5f)) return;

        var segments = MoonGeometry.SegmentsFor(radius);
        var ring = RingThickness(radius);

        switch (state)
        {
            case QuestState.Completed:                       // full moon
                FillDisc(dl, center, radius, segments, Theme.MoonU32);
                break;

            case QuestState.Accepted:                        // waxing gibbous, gold, thin gold ring
                FillDisc(dl, center, radius, segments, Theme.UnlitDiscU32);
                FillPhase(dl, center, radius, MoonPhase.WaxingGibbous, segments, Theme.MoonU32);
                Ring(dl, center, radius, segments, ring, Theme.MoonU32);
                break;

            case QuestState.Ready:                           // first quarter, gold, outer glow
                foreach (var (scale, alpha) in Glow)
                    dl.AddCircleFilled(center, radius * scale, Theme.WithAlpha(Theme.Moon, alpha));
                FillDisc(dl, center, radius, segments, Theme.UnlitDiscU32);
                FillPhase(dl, center, radius, MoonPhase.FirstQuarter, segments, Theme.MoonU32);
                break;

            case QuestState.ReadyOnOtherJob:                 // first quarter, silver, gold ring
                FillDisc(dl, center, radius, segments, Theme.UnlitDiscU32);
                FillPhase(dl, center, radius, MoonPhase.FirstQuarter, segments, Theme.SilverU32);
                Ring(dl, center, radius, segments, ring, Theme.MoonU32);
                break;

            case QuestState.DoneThisCycle:                   // waning gibbous, silver
                FillDisc(dl, center, radius, segments, Theme.UnlitDiscU32);
                FillPhase(dl, center, radius, MoonPhase.WaningGibbous, segments, Theme.SilverU32);
                break;

            case QuestState.Blocked:                         // new moon, thin silver ring
                FillDisc(dl, center, radius, segments, Theme.UnlitDiscU32);
                Ring(dl, center, radius, segments, ring, Theme.SilverU32);
                break;

            case QuestState.Foreclosed:                      // eclipsed: dark, eclipse ring, notch at the upper right
                FillDisc(dl, center, radius, segments, Theme.UnlitDiscU32);
                Ring(dl, center, radius, segments, ring * 1.3f, Theme.EclipseU32);
                dl.AddCircleFilled(center + new Vector2(0.72f, -0.72f) * radius, radius * 0.30f, Theme.NightU32);
                break;

            case QuestState.Unknown:                         // veiled: dark, dashed ring
            default:
                FillDisc(dl, center, radius, segments, Theme.UnlitDiscU32);
                DashedRing(dl, center, radius, ring, Theme.DuskU32);
                break;
        }
    }

    /// <summary>
    /// Reserves a <paramref name="size"/> × <paramref name="size"/> item at the cursor and draws the glyph inside it. The
    /// moon radius is 42% of the box so rings, the notch and the glow stay within the line.
    /// </summary>
    public static void DrawInline(QuestState state, float size)
    {
        var pos = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(size, size));
        Draw(ImGui.GetWindowDrawList(), pos + new Vector2(size * 0.5f), size * 0.42f, state);
    }

    /// <summary>
    /// Progress moon for tree nodes: lit <paramref name="fraction"/> from 0 (new) to 1 (full), filling right to left like a
    /// waxing moon, with a thin Veil ring for definition on any background.
    /// </summary>
    public static void DrawFilling(ImDrawListPtr dl, Vector2 center, float radius, float fraction)
    {
        if (!(radius > 0.5f)) return;

        var segments = MoonGeometry.SegmentsFor(radius);
        var baseLit = MoonGeometry.FillingLayers(center, radius, fraction, segments, Scratch);

        FillDisc(dl, center, radius, segments, baseLit ? Theme.MoonU32 : Theme.UnlitDiscU32);
        FillScratch(dl, baseLit ? Theme.UnlitDiscU32 : Theme.MoonU32);
        Ring(dl, center, radius, segments, RingThickness(radius), Theme.VeilU32);
    }

    /// <summary>Inline form of <see cref="DrawFilling"/>: reserves a square item and draws at the cursor.</summary>
    public static void DrawFillingInline(float fraction, float size)
    {
        var pos = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(size, size));
        DrawFilling(ImGui.GetWindowDrawList(), pos + new Vector2(size * 0.5f), size * 0.42f, fraction);
    }

    private static float RingThickness(float radius) => MathF.Max(1f, radius * RingFraction);

    /// <summary>Base disc through the same polygon the overlays use, so their rim chords coincide with it.</summary>
    private static void FillDisc(ImDrawListPtr dl, Vector2 center, float radius, int segments, uint color)
    {
        MoonGeometry.CirclePolygon(center, radius, segments, Scratch);
        FillScratch(dl, color);
    }

    private static void FillPhase(ImDrawListPtr dl, Vector2 center, float radius, MoonPhase phase, int segments, uint color)
    {
        MoonGeometry.PhaseLayers(center, radius, phase, segments, Scratch);
        FillScratch(dl, color);
    }

    private static void FillScratch(ImDrawListPtr dl, uint color)
    {
        if (Scratch.Count < 3) return;
        var span = CollectionsMarshal.AsSpan(Scratch);
        dl.AddConvexPolyFilled(ref MemoryMarshal.GetReference(span), span.Length, color);
    }

    /// <summary>Ring just inside the rim, like Pillow's inward outline.</summary>
    private static void Ring(ImDrawListPtr dl, Vector2 center, float radius, int segments, float thickness, uint color)
    {
        dl.AddCircle(center, radius - thickness * 0.5f, color, segments, thickness);
    }

    /// <summary>Twelve 16° dashes every 30°, starting at the top (Pillow: start = k·30 − 90).</summary>
    private static void DashedRing(ImDrawListPtr dl, Vector2 center, float radius, float thickness, uint color)
    {
        const int Dashes = 12;
        const float DashDegrees = 16f;
        var r = radius - thickness * 0.5f;
        var arcSegments = Math.Max(2, (int)MathF.Ceiling(radius / 8f));

        dl.PathClear();
        for (var k = 0; k < Dashes; k++)
        {
            var start = (k * 360f / Dashes - 90f) * (MathF.PI / 180f);
            var end = start + DashDegrees * (MathF.PI / 180f);
            dl.PathArcTo(center, r, start, end, arcSegments);
            dl.PathStroke(color, ImDrawFlags.None, thickness);
        }
    }
}
