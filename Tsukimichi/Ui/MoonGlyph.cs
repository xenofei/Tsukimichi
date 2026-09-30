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
/// the UI and need no textures. Geometry comes from <see cref="MoonGeometry"/> and <see cref="MoonDetail"/>; this
/// class only paints. A moon means a quest state or a completion fraction and nothing else (accessibility B2): met
/// requirements, obtained rewards and the live indicator use <see cref="Marks"/>.
///
/// The look is docs/design/glyphs/proposal.md v2.1 §3.1 (T9a in 0.5.1, T9b in 0.7.0): a Shadow base disc, a rim
/// stroke of <see cref="Rim"/> px inside the rim in the state's colour, the lit part as the intersection of the base
/// disc with a terminator disc, a soft glow for Ready (a single thin ring below r 9), a dashed rim for Unknown, a
/// diagonal Eclipse bar (plus a notch from r 12) for Foreclosed, and Accepted as the 60 % "sealed early gibbous" with a
/// Silver rim and a Night seal dot. Lit parts are flat below r 9, get a radial gold or silver gradient from r 9 and
/// the interior detail (three maria, three craters, terminator glow, rim vignette; at most
/// <see cref="MoonDetail.PrimitiveBudget"/> primitives) from r 12; Completed adds a highlight arc from r 16.
/// Draw order is disc → rim → lit part → detail → marks, except the ring states (Accepted, ReadyOnOtherJob, Blocked)
/// where the ring goes last.
///
/// With the high-contrast glyph palette (<see cref="Theme.Glyphs"/>, Settings › Display › Glyph palette; accessibility
/// panel §2.2) the same silhouettes are drawn flat from <see cref="GlyphPalette"/>: a keyline disc in the palette's
/// ground, the lit part and a rim of at least 2 px in the state's rung of the luminance ladder, and one in-disc mark
/// per state (<see cref="DrawContrast"/>); the halo gets a thicker stroke over a ground well (<see cref="DrawContrastHalo"/>).
/// </summary>
public static class MoonGlyph
{
    /// <summary>Radius from which lit parts get the radial gradient; smaller glyphs stay flat.</summary>
    public const float ShadingMinRadius = 9f;

    /// <summary>Radius from which Completed (and a complete halo core) gets the highlight arc.</summary>
    public const float HighlightMinRadius = 16f;

    /// <summary>Radius of an inline glyph as a fraction of its square, leaving room for rings, the notch and the glow.</summary>
    public const float InlineRadiusFraction = 0.42f;

    /// <summary>Radius from which the Foreclosed notch is bitten out of the rim; below it the bar alone carries the state.</summary>
    public const float NotchMinRadius = 12f;

    /// <summary>Radius from which the Unknown rim has twelve 16° dashes; below it eight 22° dashes stay readable.</summary>
    public const float FineDashMinRadius = 10f;

    /// <summary>Glow discs for Ready at r ≥ 9, largest first: (radius multiplier, alpha), approximating the proposal's blurred gold disc.</summary>
    private static readonly (float Scale, float Alpha)[] Glow = [(1.70f, 0.05f), (1.42f, 0.09f), (1.20f, 0.15f)];
    private static readonly uint[] GlowColors = Array.ConvertAll(Glow, static g => Theme.WithAlpha(Theme.Moon, g.Alpha));

    /// <summary>Ready below r 9: one 1 px Moon ring at 1.25 r, 35 % — the glow discs vanish at that size.</summary>
    private const float SmallReadyRingScale = 1.25f;
    private static readonly uint SmallReadyRingColor = Theme.WithAlpha(Theme.Moon, 0.35f);

    /// <summary>Unknown's disc: Shadow at 60 %, so the veiled state stays the faintest.</summary>
    private static readonly uint VeiledDiscColor = Theme.WithAlpha(Theme.Shadow, 0.60f);

    /// <summary>Foreclosed bar endpoints (× r) and width rule: a diagonal that survives every colour deficiency.</summary>
    private static readonly Vector2 BarEnd = new(0.636f, 0.636f);
    private const float BarWidthFraction = 0.22f;
    private const float BarMinWidth = 2f;

    /// <summary>Foreclosed notch: a Night disc biting the rim at the upper right.</summary>
    private static readonly Vector2 NotchOffset = new(0.72f, -0.72f);
    private const float NotchRadius = 0.30f;

    /// <summary>Accepted seal: a Night disc on the lit side, the "written in the journal" stamp (proposal §3.1a (f)).</summary>
    private static readonly Vector2 SealOffset = new(0.40f, 0f);
    private const float SealRadiusFraction = 0.16f;
    private const float SealMinRadius = 1.5f;

    /// <summary>Radial gradient: highlight centre (× r), radius (× r) and the middle stop (proposal §3.1).</summary>
    private static readonly Vector2 GradientCenter = new(-0.32f, -0.34f);
    private const float GradientRadius = 1.25f;
    private const float GradientMidStop = 0.42f;

    /// <summary>Rings of the gradient mesh between its centre and the lit outline (the outline is the last).</summary>
    private static readonly float[] GradientRings = [1f / 3f, 2f / 3f, 1f];

    /// <summary>Highlight arc on Completed: radius fraction, start and end angle (radians, y down: 180° is left, 270° is top).</summary>
    private const float ArcRadius = 0.76f;
    private const float ArcStart = 200f * Degrees;
    private const float ArcEnd = 252f * Degrees;
    private const float ArcWidthFraction = 0.06f;
    private static readonly uint ArcColor = Theme.WithAlpha(Theme.SilverHigh, 0.32f);

    /// <summary>Points on a mare's ellipse and rows of the terminator band.</summary>
    private const int MarePoints = 20;
    private const int BandRows = 24;

    private const float Degrees = MathF.PI / 180f;

    /// <summary>Halo gauge: the track on raised cards (Dusk at 80 %, about 3.6 : 1) and the complete gauge's two glow rings.</summary>
    private static readonly uint HaloTrackOnCard = Theme.WithAlpha(Theme.Dusk, 0.80f);
    private static readonly uint HaloGlowOuter = Theme.WithAlpha(Theme.Moon, 0.10f);
    private static readonly uint HaloGlowInner = Theme.WithAlpha(Theme.Moon, 0.16f);

    /// <summary>Gold and silver lit tones: gradient stops and the detail's light colours.</summary>
    private static readonly Tone GoldTone = new(Theme.MoonHigh, Theme.Moon, Theme.MoonDeep, silver: false);
    private static readonly Tone SilverTone = new(Theme.SilverHigh, Theme.Silver, Theme.SilverDeep, silver: true);

    // Scratch polygons; UI drawing happens on one thread. FillDisc always writes Scratch, so anything that must
    // survive the base disc (the filling overlay) lives in its own list.
    private static readonly List<Vector2> Scratch = new(256);
    private static readonly List<Vector2> Overlay = new(256);

    /// <summary>Rim stroke for a state moon of radius <paramref name="radius"/> px: 0.12 r clamped to 1.5–3 px (ui-revamp §2.7).</summary>
    public static float Rim(float radius) => Math.Clamp(0.12f * radius, 1.5f, 3f);

    /// <summary>Draws the glyph for <paramref name="state"/> centred at <paramref name="center"/> with the given pixel radius.</summary>
    public static void Draw(ImDrawListPtr dl, Vector2 center, float radius, QuestState state)
    {
        if (!(radius > 0.5f)) return;

        center = Snap(center, radius);
        var palette = Theme.Glyphs;
        if (palette.HighContrast)
        {
            DrawContrast(dl, center, radius, palette, palette.Style(state));
            return;
        }

        var segments = MoonGeometry.SegmentsFor(radius);
        var rim = Rim(radius);

        switch (state)
        {
            case QuestState.Completed:                       // full moon: the only rimless disc
                FillDisc(dl, center, radius, segments, Theme.MoonU32);
                Shade(dl, center, radius, Scratch, GoldTone);
                Detail(dl, center, radius, segments, LitRegion.Full, GoldTone);
                if (radius >= HighlightMinRadius) HighlightArc(dl, center, radius);
                break;

            case QuestState.Accepted:                        // sealed early gibbous: 60 % lit, seal, Silver rim last
                FillDisc(dl, center, radius, segments, Theme.ShadowU32);
                FillPhase(dl, center, radius, MoonPhase.WaxingGibbous, segments, Theme.MoonU32);
                Shade(dl, center, radius, Scratch, GoldTone);
                Detail(dl, center, radius, segments, LitRegion.ForWidth(MoonGeometry.AcceptedLitWidth), GoldTone);
                dl.AddCircleFilled(center + SealOffset * radius, MathF.Max(SealRadiusFraction * radius, SealMinRadius), Theme.NightU32);
                Ring(dl, center, radius, segments, rim, Theme.SilverU32);
                break;

            case QuestState.Ready:                           // first quarter, gold, Dusk rim under the lit half, glow or thin outer ring
                if (radius < ShadingMinRadius)
                    dl.AddCircle(center, radius * SmallReadyRingScale, SmallReadyRingColor, segments, 1f);
                else
                    for (var i = 0; i < Glow.Length; i++)
                        dl.AddCircleFilled(center, radius * Glow[i].Scale, GlowColors[i]);
                FillDisc(dl, center, radius, segments, Theme.ShadowU32);
                Ring(dl, center, radius, segments, rim, Theme.DuskU32);
                FillPhase(dl, center, radius, MoonPhase.FirstQuarter, segments, Theme.MoonU32);
                Shade(dl, center, radius, Scratch, GoldTone);
                Detail(dl, center, radius, segments, LitRegion.ForWidth(0.5f), GoldTone);
                break;

            case QuestState.ReadyOnOtherJob:                 // first quarter, silver, Moon ring last
                FillDisc(dl, center, radius, segments, Theme.ShadowU32);
                FillPhase(dl, center, radius, MoonPhase.FirstQuarter, segments, Theme.SilverU32);
                Shade(dl, center, radius, Scratch, SilverTone);
                Detail(dl, center, radius, segments, LitRegion.ForWidth(0.5f), SilverTone);
                Ring(dl, center, radius, segments, rim, Theme.MoonU32);
                break;

            case QuestState.DoneThisCycle:                   // waning gibbous, silver, Dusk rim under the lit part
                FillDisc(dl, center, radius, segments, Theme.ShadowU32);
                Ring(dl, center, radius, segments, rim, Theme.DuskU32);
                FillPhase(dl, center, radius, MoonPhase.WaningGibbous, segments, Theme.SilverU32);
                Shade(dl, center, radius, Scratch, SilverTone);
                Detail(dl, center, radius, segments, LitRegion.WaningLens, SilverTone);
                break;

            case QuestState.Blocked:                         // new moon, Silver ring: the only plain empty ring
                FillDisc(dl, center, radius, segments, Theme.ShadowU32);
                Ring(dl, center, radius, segments, rim, Theme.SilverU32);
                break;

            case QuestState.Foreclosed:                      // eclipsed: Eclipse rim, diagonal bar, notch from r 12
                FillDisc(dl, center, radius, segments, Theme.ShadowU32);
                Ring(dl, center, radius, segments, rim, Theme.EclipseU32);
                dl.AddLine(center - BarEnd * radius, center + BarEnd * radius, Theme.EclipseU32, MathF.Max(BarMinWidth, BarWidthFraction * radius));
                if (radius >= NotchMinRadius)
                    dl.AddCircleFilled(center + NotchOffset * radius, radius * NotchRadius, Theme.NightU32);
                break;

            case QuestState.Unknown:                         // veiled: faint disc, dashed Dusk rim
            default:
                FillDisc(dl, center, radius, segments, VeiledDiscColor);
                if (radius >= FineDashMinRadius)
                    DashedRing(dl, center, radius, segments, rim, Theme.DuskU32, dashes: 12, dashDegrees: 16f);
                else
                    DashedRing(dl, center, radius, segments, rim, Theme.DuskU32, dashes: 8, dashDegrees: 22f);
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
        Draw(ImGui.GetWindowDrawList(), pos + new Vector2(size * 0.5f), size * InlineRadiusFraction, state);
    }

    /// <summary>
    /// The veiled (Unknown) glyph faded to <paramref name="alpha"/>, as a stand-in where an icon would go and none
    /// exists (Moonlit rewards of a kind without sheet art). Reserves a square item like <see cref="DrawInline"/>, so
    /// the caller can hang a tooltip on it; the faint disc and dashed rim both carry the alpha, and it says nothing
    /// about state or progress.
    /// </summary>
    public static void DrawVeiledInline(float size, float alpha)
    {
        var pos = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(size, size));
        DrawVeiled(ImGui.GetWindowDrawList(), pos + new Vector2(size * 0.5f), size * InlineRadiusFraction, alpha);
    }

    /// <summary>The veiled stand-in of <see cref="DrawVeiledInline"/> drawn at <paramref name="center"/> without an item (a gallery tile).</summary>
    public static void DrawVeiled(ImDrawListPtr dl, Vector2 center, float radius, float alpha)
    {
        if (!(radius > 0.5f)) return;

        center = Snap(center, radius);
        var segments = MoonGeometry.SegmentsFor(radius);
        var rim = Rim(radius);
        FillDisc(dl, center, radius, segments, Theme.WithAlpha(Theme.Shadow, 0.60f * alpha));
        var rimColor = Theme.WithAlpha(Theme.Dusk, alpha);
        if (radius >= FineDashMinRadius)
            DashedRing(dl, center, radius, segments, rim, rimColor, dashes: 12, dashDegrees: 16f);
        else
            DashedRing(dl, center, radius, segments, rim, rimColor, dashes: 8, dashDegrees: 22f);
    }

    /// <summary>
    /// The halo gauge, the progress glyph (proposal v2.1 §3.2, imgui-notes §3; geometry in <see cref="GaugeGeometry"/>):
    /// a track ring at 0.80 R (VeilLine, or Dusk at 80 % on raised cards), a Moon arc from 12 o'clock clockwise with
    /// round caps whose length is the floored fraction, and from R 12 a core — a Shadow disc with a Dusk rim that
    /// holds a filling moon with its own floor. At exactly 1 the ring is full Moon with two soft glow rings and the core
    /// a full gold moon, or, with <paramref name="dimComplete"/>, a MoonDim ring without glow around a flat MoonDim core
    /// (no gradient or detail). Below R 12 only track and arc are drawn and the
    /// caller writes the number beside it; below R 8 nothing is drawn and the caller writes the number alone.
    /// Allocation-free.
    /// </summary>
    /// <param name="radius">Half the box, R.</param>
    /// <param name="onCard">Drawn on a raised card: the track is Dusk at 80 % to keep 3 : 1 there.</param>
    /// <param name="dimComplete">Whether a complete gauge steps back to MoonDim instead of glowing gold; the Journal tree
    /// sets it so finished nodes recede and gold stays with what is actionable (feature plan T11).</param>
    public static void DrawHalo(ImDrawListPtr dl, Vector2 center, float radius, float fraction, bool onCard = false, bool dimComplete = false)
    {
        var mode = GaugeGeometry.ModeFor(radius);
        if (mode == HaloMode.NumberOnly) return;

        center = Snap(center, radius);
        if (Theme.Glyphs.HighContrast)
        {
            DrawContrastHalo(dl, center, radius, fraction, mode, Theme.Glyphs, onCard, dimComplete);
            return;
        }

        var f = GaugeGeometry.Clamp01(fraction);
        var track = GaugeGeometry.TrackRadius(radius);
        var stroke = GaugeGeometry.Stroke(radius);
        var segments = MoonGeometry.SegmentsFor(track);
        var core = GaugeGeometry.CoreRadius(radius);

        if (f >= 1f)
        {
            if (dimComplete)
            {
                dl.AddCircle(center, track, Theme.MoonDimU32, segments, stroke);
                if (mode == HaloMode.Core)
                    FillDisc(dl, center, core, MoonGeometry.SegmentsFor(core), Theme.MoonDimU32);
                return;
            }

            dl.AddCircle(center, track, HaloGlowOuter, segments, stroke * 2.2f);
            dl.AddCircle(center, track, HaloGlowInner, segments, stroke * 1.5f);
            dl.AddCircle(center, track, Theme.MoonU32, segments, stroke);
            if (mode == HaloMode.Core)
                FillingMoon(dl, center, core, MoonGeometry.SegmentsFor(core), 1f);
            return;
        }

        dl.AddCircle(center, track, onCard ? HaloTrackOnCard : Theme.VeilLineU32, segments, stroke);
        var sweep = GaugeGeometry.Sweep(f, radius);
        if (sweep > 0f)
        {
            var start = GaugeGeometry.StartAngle;
            dl.PathClear();
            dl.PathArcTo(center, track, start, start + sweep, GaugeGeometry.ArcSegments(segments, sweep));
            dl.PathStroke(Theme.MoonU32, ImDrawFlags.None, stroke);

            // Round caps: PathStroke has none.
            var (from, to) = GaugeGeometry.Caps(center, radius, f);
            dl.AddCircleFilled(from, stroke * 0.5f, Theme.MoonU32);
            dl.AddCircleFilled(to, stroke * 0.5f, Theme.MoonU32);
        }

        if (mode != HaloMode.Core) return;
        FillingMoon(dl, center, core, MoonGeometry.SegmentsFor(core), GaugeGeometry.CoreLitWidth(f, core));
    }

    /// <summary>
    /// Inline form of <see cref="DrawHalo"/>: reserves a <paramref name="size"/> square item at the cursor (so a tooltip
    /// can hang off it) and draws the halo with R = size / 2. Returns the <see cref="HaloMode"/> it drew, so the caller
    /// knows whether a number belongs beside it (<see cref="HaloMode.Ring"/>) or instead of it (<see cref="HaloMode.NumberOnly"/>).
    /// This overload eases the fill through <see cref="Motion.Gauge"/> under <paramref name="motionKey"/>, so it moves
    /// only when the fraction changes.
    /// </summary>
    public static HaloMode DrawHaloInline(ulong motionKey, float fraction, float size, bool onCard = false, bool dimComplete = false) =>
        DrawHaloInline(Motion.Gauge(motionKey, fraction), size, onCard, dimComplete);

    /// <inheritdoc cref="DrawHaloInline(ulong, float, float, bool, bool)"/>
    public static HaloMode DrawHaloInline(float fraction, float size, bool onCard = false, bool dimComplete = false)
    {
        var pos = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(size, size));
        var radius = size * 0.5f;
        DrawHalo(ImGui.GetWindowDrawList(), pos + new Vector2(radius), radius, fraction, onCard, dimComplete);
        return GaugeGeometry.ModeFor(radius);
    }

    /// <summary>
    /// The filling moon: lit <paramref name="fraction"/> from 0 (new) to 1 (full), filling right to left like a waxing
    /// moon, with the terminator floor of <see cref="MoonGeometry.FillingLayers"/>. The dark side is Shadow with a Dusk
    /// rim around it; the lit side is rimless, like Completed. Progress everywhere else is the halo gauge
    /// (<see cref="DrawHalo"/>, T10); this stays for the glyph window's side-by-side comparison for one release.
    /// </summary>
    public static void DrawFilling(ImDrawListPtr dl, Vector2 center, float radius, float fraction)
    {
        if (!(radius > 0.5f)) return;

        center = Snap(center, radius);
        var segments = MoonGeometry.SegmentsFor(radius);
        FillingMoon(dl, center, radius, segments, MoonGeometry.FlooredFraction(fraction, radius));
    }

    /// <summary>
    /// A rimmed filling moon for an already floored lit <paramref name="width"/>: new is a plain rimmed Shadow disc,
    /// crescents are a lit disc with the shadow lens on top, gibbous moons a dark disc with the lit lens on top; the
    /// Dusk rim runs around the dark side only. Shared by <see cref="DrawFilling"/> and the halo core.
    /// </summary>
    private static void FillingMoon(ImDrawListPtr dl, Vector2 center, float radius, int segments, float width)
    {
        var rim = Rim(radius);

        // The overlay goes into its own list: FillDisc rewrites Scratch with the base polygon, and painting that
        // polygon in the overlay tone is exactly the bug that showed every partial moon as full (or new).
        var baseLit = MoonGeometry.TerminatorLayers(center, radius, width, segments, Overlay);
        var hasOverlay = Overlay.Count >= 3;
        var region = LitRegion.ForWidth(width);

        if (baseLit)
        {
            // Crescent (or full): the lit disc is shaded first, then the shadow lens covers the dark side and the
            // rim goes around that side only.
            FillDisc(dl, center, radius, segments, Theme.MoonU32);
            Shade(dl, center, radius, Scratch, GoldTone);
            if (hasOverlay)
            {
                FillPolygon(dl, Overlay, Theme.ShadowU32);
                DarkSideRim(dl, center, radius, segments, rim, Theme.DuskU32);
            }

            Detail(dl, center, radius, segments, region, GoldTone);
            if (!hasOverlay && radius >= HighlightMinRadius) HighlightArc(dl, center, radius);
        }
        else if (hasOverlay)
        {
            // Gibbous or half: dark disc, rim under the lit lens, then the lit lens.
            FillDisc(dl, center, radius, segments, Theme.ShadowU32);
            DarkSideRim(dl, center, radius, segments, rim, Theme.DuskU32);
            FillPolygon(dl, Overlay, Theme.MoonU32);
            Shade(dl, center, radius, Overlay, GoldTone);
            Detail(dl, center, radius, segments, region, GoldTone);
        }
        else
        {
            // New: a plain rimmed dark disc, so an empty node still has an outline.
            FillDisc(dl, center, radius, segments, Theme.ShadowU32);
            Ring(dl, center, radius, segments, rim, Theme.DuskU32);
        }
    }

    // ------------------------------------------------------------------ high contrast

    /// <summary>Ready's bar: centre x (× r), half height (× r) and width rule.</summary>
    private const float ContrastBarX = -0.42f;
    private const float ContrastBarHalfHeight = 0.48f;
    private const float ContrastBarWidthFraction = 0.20f;
    private const float ContrastBarMinWidth = 2f;

    /// <summary>Ready on another job's hollow bar: width rule and outline.</summary>
    private const float ContrastHollowWidthFraction = 0.28f;
    private const float ContrastHollowMinWidth = 4f;
    private const float ContrastHollowStrokeFraction = 0.08f;

    /// <summary>In journal's large seal: centre on the lit side and radius rule (the Standard seal is 0.16 r, 1.5 px).</summary>
    private static readonly Vector2 ContrastSealOffset = new(0.32f, 0f);
    private const float ContrastSealRadiusFraction = 0.24f;
    private const float ContrastSealMinRadius = 2f;

    /// <summary>Done's check on the waning lens (× r): short leg down-right, long leg up-right.</summary>
    private static readonly Vector2 ContrastCheckA = new(-0.60f, -0.02f);
    private static readonly Vector2 ContrastCheckB = new(-0.36f, 0.26f);
    private static readonly Vector2 ContrastCheckC = new(0.04f, -0.30f);
    private const float ContrastCheckStrokeFraction = 0.14f;

    /// <summary>Locked out's thickened bar: width max(3 px, 0.32 r) (Standard: max(2, 0.22 r)).</summary>
    private const float ContrastDiagonalWidthFraction = 0.32f;
    private const float ContrastDiagonalMinWidth = 3f;

    /// <summary>
    /// A state glyph in the high-contrast palette: the ground as a disc of the full radius (the unlit side, and a
    /// keyline of <see cref="GlyphPalette.Keyline"/> px around everything else, so the glyph carries its own background),
    /// then inside it the lit part flat in the state's rung, the rim (≥ 2 px; Blocked's thick, Not checked's dashed) and
    /// the state's mark: bold bar, hollow bar, large seal, check, thick diagonal bar. No gradient, detail or glow.
    /// </summary>
    private static void DrawContrast(ImDrawListPtr dl, Vector2 center, float radius, GlyphPalette palette, in GlyphStyle style)
    {
        var segments = MoonGeometry.SegmentsFor(radius);
        FillDisc(dl, center, radius, segments, Theme.U32(palette.Ground));

        var r = radius - palette.Keyline(radius);
        if (!(r > 0.5f)) return;

        var lit = Theme.U32(style.Lit);
        if (style.Phase == MoonPhase.Full)
            FillDisc(dl, center, r, segments, lit);
        else if (style.HasLit)
            FillPhase(dl, center, r, style.Phase, segments, lit);

        var rim = style.RimWidth(r);
        var rimColor = Theme.U32(style.Rim);
        if (style.RimStyle == RimStyle.Solid)
        {
            Ring(dl, center, r, segments, rim, rimColor);
        }
        else if (style.RimStyle == RimStyle.Dashed)
        {
            if (r >= FineDashMinRadius)
                DashedRing(dl, center, r, segments, rim, rimColor, dashes: 12, dashDegrees: 18f);
            else
                DashedRing(dl, center, r, segments, rim, rimColor, dashes: 8, dashDegrees: 24f);
        }

        var mark = Theme.U32(style.MarkColor);
        switch (style.Mark)
        {
            case GlyphMark.Bar:
            {
                var w = MathF.Max(ContrastBarMinWidth, MathF.Round(ContrastBarWidthFraction * r));
                var x0 = MathF.Round(center.X + ContrastBarX * r - w * 0.5f);
                var h = ContrastBarHalfHeight * r;
                dl.AddRectFilled(new Vector2(x0, center.Y - h), new Vector2(x0 + w, center.Y + h), mark);
                break;
            }

            case GlyphMark.HollowBar:
            {
                var w = MathF.Max(ContrastHollowMinWidth, MathF.Round(ContrastHollowWidthFraction * r));
                var x0 = MathF.Round(center.X + ContrastBarX * r - w * 0.5f);
                var h = ContrastBarHalfHeight * r;
                var t = MathF.Max(1f, MathF.Round(ContrastHollowStrokeFraction * r));
                dl.AddRect(new Vector2(x0, center.Y - h), new Vector2(x0 + w, center.Y + h), mark, 0f, ImDrawFlags.None, t);
                break;
            }

            case GlyphMark.LargeSeal:
                dl.AddCircleFilled(center + ContrastSealOffset * r, MathF.Max(ContrastSealRadiusFraction * r, ContrastSealMinRadius), mark);
                break;

            case GlyphMark.Check:
                dl.PathClear();
                dl.PathLineTo(center + ContrastCheckA * r);
                dl.PathLineTo(center + ContrastCheckB * r);
                dl.PathLineTo(center + ContrastCheckC * r);
                dl.PathStroke(mark, ImDrawFlags.None, MathF.Max(1.5f, ContrastCheckStrokeFraction * r));
                break;

            case GlyphMark.ThickDiagonalBar:
                dl.AddLine(center - BarEnd * r, center + BarEnd * r, mark, MathF.Max(ContrastDiagonalMinWidth, ContrastDiagonalWidthFraction * r));
                break;
        }
    }

    /// <summary>
    /// The halo gauge in the high-contrast palette: a ground well under the track (a keyline wide on each side), the
    /// track in the palette's dim rung and the arc in its bright one (≥ 3 : 1 against the background and each other,
    /// see the palette tests), stroke <see cref="GlyphPalette.HaloStroke"/> = max(2.5, 0.2 R); from R 12 a flat core
    /// moon inside its own keyline. Complete: the ring in the arc colour (or the dim rung with
    /// <paramref name="dimComplete"/>) around a solid core, no glow.
    /// </summary>
    private static void DrawContrastHalo(ImDrawListPtr dl, Vector2 center, float radius, float fraction, HaloMode mode, GlyphPalette palette, bool onCard, bool dimComplete)
    {
        var f = GaugeGeometry.Clamp01(fraction);
        var track = GaugeGeometry.TrackRadius(radius);
        var stroke = palette.HaloStroke(radius);
        var keyline = palette.Keyline(radius);
        var segments = MoonGeometry.SegmentsFor(track);
        var ground = Theme.U32(palette.Ground);
        var arc = Theme.U32(palette.HaloArc);
        var core = mode == HaloMode.Core ? GaugeGeometry.CoreRadius(radius, stroke) : 0f;

        dl.AddCircle(center, track, ground, segments, stroke + 2f * keyline);

        if (f >= 1f)
        {
            var ring = dimComplete ? Theme.U32(palette.HaloCompleteDim) : arc;
            dl.AddCircle(center, track, ring, segments, stroke);
            if (core > 0.5f)
            {
                var coreSegments = MoonGeometry.SegmentsFor(core);
                FillDisc(dl, center, core, coreSegments, ground);
                FillDisc(dl, center, core - palette.Keyline(core), coreSegments, ring);
            }

            return;
        }

        dl.AddCircle(center, track, Theme.U32(onCard ? palette.HaloTrackOnCard : palette.HaloTrack), segments, stroke);
        var sweep = GaugeGeometry.Sweep(f, radius, stroke);
        if (sweep > 0f)
        {
            var start = GaugeGeometry.StartAngle;
            dl.PathClear();
            dl.PathArcTo(center, track, start, start + sweep, GaugeGeometry.ArcSegments(segments, sweep));
            dl.PathStroke(arc, ImDrawFlags.None, stroke);

            var (from, to) = GaugeGeometry.Caps(center, radius, f, stroke);
            dl.AddCircleFilled(from, stroke * 0.5f, arc);
            dl.AddCircleFilled(to, stroke * 0.5f, arc);
        }

        if (core > 0.5f)
            ContrastFillingMoon(dl, center, core, GaugeGeometry.CoreLitWidth(f, core), palette);
    }

    /// <summary>The halo core in high contrast: a ground keyline disc, then a flat filling moon in the bright rung with the dim rim on its dark side.</summary>
    private static void ContrastFillingMoon(ImDrawListPtr dl, Vector2 center, float radius, float width, GlyphPalette palette)
    {
        var segments = MoonGeometry.SegmentsFor(radius);
        var ground = Theme.U32(palette.Ground);
        var lit = Theme.U32(palette.HaloArc);
        var rimColor = Theme.U32(palette.HaloTrack);
        FillDisc(dl, center, radius, segments, ground);

        var r = radius - palette.Keyline(radius);
        if (!(r > 0.5f)) return;

        // The overlay lives in its own list: FillDisc rewrites Scratch.
        var rim = palette.Style(QuestState.Ready).RimWidth(r);
        var baseLit = MoonGeometry.TerminatorLayers(center, r, width, segments, Overlay);
        var hasOverlay = Overlay.Count >= 3;
        if (baseLit)
        {
            FillDisc(dl, center, r, segments, lit);
            if (hasOverlay)
            {
                FillPolygon(dl, Overlay, ground);
                DarkSideRim(dl, center, r, segments, rim, rimColor);
            }
        }
        else if (hasOverlay)
        {
            DarkSideRim(dl, center, r, segments, rim, rimColor);
            FillPolygon(dl, Overlay, lit);
        }
        else
        {
            Ring(dl, center, r, segments, rim, rimColor);
        }
    }

    /// <summary>
    /// Snaps the centre so the rim lands on whole pixels: integer centres for an even diameter, half-integer for an
    /// odd one (imgui-notes §1). Moves the glyph by at most half a pixel; the reserved box is not touched.
    /// </summary>
    private static Vector2 Snap(Vector2 center, float radius)
    {
        var offset = (int)MathF.Round(2f * radius) % 2 == 1 ? 0.5f : 0f;
        return new Vector2(MathF.Round(center.X - offset) + offset, MathF.Round(center.Y - offset) + offset);
    }

    /// <summary>Base disc through the same polygon the overlays use, so their rim chords coincide with it.</summary>
    private static void FillDisc(ImDrawListPtr dl, Vector2 center, float radius, int segments, uint color)
    {
        MoonGeometry.CirclePolygon(center, radius, segments, Scratch);
        FillPolygon(dl, Scratch, color);
    }

    private static void FillPhase(ImDrawListPtr dl, Vector2 center, float radius, MoonPhase phase, int segments, uint color)
    {
        MoonGeometry.PhaseLayers(center, radius, phase, segments, Scratch);
        FillPolygon(dl, Scratch, color);
    }

    private static void FillPolygon(ImDrawListPtr dl, List<Vector2> polygon, uint color)
    {
        if (polygon.Count < 3) return;
        var span = CollectionsMarshal.AsSpan(polygon);
        dl.AddConvexPolyFilled(ref MemoryMarshal.GetReference(span), span.Length, color);
    }

    // ------------------------------------------------------------------ shading and interior detail

    /// <summary>
    /// Radial gradient over a lit region given as a convex polygon <paramref name="lit"/> (already filled flat and
    /// anti-aliased by the caller): one vertex-coloured mesh from a point inside the region out to its outline, each
    /// vertex coloured by its distance from the highlight centre (−0.32 r, −0.34 r) over 1.25 r with the stops
    /// High → Mid at 42 % → Deep (proposal §3.1). One primitive; skipped below <see cref="ShadingMinRadius"/>.
    /// </summary>
    private static void Shade(ImDrawListPtr dl, Vector2 center, float radius, List<Vector2> lit, in Tone tone)
    {
        var n = lit.Count;
        if (radius < ShadingMinRadius || n < 3) return;

        var highlight = center + GradientCenter * radius;
        var hub = ContainsPoint(lit, highlight) ? highlight : Centroid(lit);
        var span = 1f / (GradientRadius * radius);
        var rings = GradientRings.Length;
        var uv = ImGui.GetFontTexUvWhitePixel();

        dl.PrimReserve(3 * n + 6 * n * (rings - 1), 1 + rings * n);
        var i0 = dl.VtxCurrentIdx;
        dl.PrimWriteVtx(hub, uv, tone.Gradient(Vector2.Distance(hub, highlight) * span));
        foreach (var t in GradientRings)
        {
            foreach (var p in lit)
            {
                var q = hub + (p - hub) * t;
                dl.PrimWriteVtx(q, uv, tone.Gradient(Vector2.Distance(q, highlight) * span));
            }
        }

        // Hub to the first ring, then quads between consecutive rings.
        for (var k = 0; k < n; k++)
        {
            var next = (k + 1) % n;
            WriteTriangle(dl, i0, i0 + 1 + (uint)k, i0 + 1 + (uint)next);
        }

        for (var ring = 0; ring < rings - 1; ring++)
        {
            var inner = i0 + 1 + (uint)(ring * n);
            var outer = inner + (uint)n;
            for (var k = 0; k < n; k++)
            {
                var next = (uint)((k + 1) % n);
                WriteQuad(dl, inner + (uint)k, inner + next, outer + next, outer + (uint)k);
            }
        }
    }

    /// <summary>
    /// The interior detail of a lit part from r 12 (proposal §3.6, imgui-notes §2b), clipped to <paramref name="region"/>:
    /// three soft Umbra maria (one mesh each, vertices on the dark side pulled onto the terminator), three craters (floor,
    /// a highlight wall and, for the largest, a shadow wall; a crater whose centre is dark is skipped and one that
    /// straddles the terminator is drawn at half alpha), the terminator glow band and the rim vignette. At most
    /// <see cref="MoonDetail.PrimitiveBudget"/> primitives, all in this draw list, no texture switch.
    /// </summary>
    private static void Detail(ImDrawListPtr dl, Vector2 center, float radius, int segments, LitRegion region, in Tone tone)
    {
        if (radius < MoonDetail.MinRadius || region.Shape == LitShape.None) return;

        var uv = ImGui.GetFontTexUvWhitePixel();

        // 1–3: maria.
        foreach (var mare in MoonDetail.Maria)
        {
            if (!region.IsLit(mare.Center)) continue;
            Mare(dl, center, radius, mare, region, Theme.WithAlpha(Theme.Umbra, tone.Silver ? mare.SilverAlpha : mare.GoldAlpha), uv);
        }

        // 4–10: craters.
        var line = MathF.Max(1f, MoonDetail.CraterLineFraction * radius);
        var highlightAlpha = tone.Silver ? MoonDetail.CraterHighlightSilver : MoonDetail.CraterHighlightGold;
        foreach (var crater in MoonDetail.Craters)
        {
            var visibility = MoonDetail.Visibility(region, crater.Center, crater.Radius);
            if (visibility <= 0f) continue;

            var c = center + crater.Center * radius;
            var rc = crater.Radius * radius;
            var arcSegments = ArcSegments(segments, 120f * Degrees);
            dl.AddCircleFilled(c, rc, Theme.WithAlpha(Theme.Umbra, MoonDetail.CraterFloorAlpha * visibility));
            if (crater.HasShadowArc)
            {
                dl.PathClear();
                dl.PathArcTo(c, rc * MoonDetail.CraterShadowRadius, MoonDetail.CraterShadowFrom * Degrees, MoonDetail.CraterShadowTo * Degrees, arcSegments);
                dl.PathStroke(Theme.WithAlpha(Theme.Umbra, MoonDetail.CraterShadowAlpha * visibility), ImDrawFlags.None, line);
            }

            dl.PathClear();
            dl.PathArcTo(c, rc * MoonDetail.CraterHighlightRadius, MoonDetail.CraterHighlightFrom * Degrees, MoonDetail.CraterHighlightTo * Degrees, arcSegments);
            dl.PathStroke(Theme.WithAlpha(Theme.SilverHigh, highlightAlpha * visibility), ImDrawFlags.None, line);
        }

        // 11: terminator glow band, a strip from the terminator 0.12 r into the light, fading on its inner edge.
        if (region.HasTerminator)
            Band(dl, center, radius, region, tone, uv);

        // 12: rim vignette, Umbra rising quadratically from 0.72 r to the rim, clipped to the lit part.
        Vignette(dl, center, radius, segments, region, Theme.WithAlpha(Theme.Umbra, tone.Silver ? MoonDetail.VignetteSilver : MoonDetail.VignetteGold), uv);
    }

    /// <summary>A soft ellipse: full alpha out to 0.70 of its radii, 0 at the edge; one mesh.</summary>
    private static void Mare(ImDrawListPtr dl, Vector2 center, float radius, in Mare mare, LitRegion region, uint color, Vector2 uv)
    {
        var clear = color & 0x00FFFFFFu;
        var (sin, cos) = MathF.SinCos(mare.RotationDegrees * Degrees);

        dl.PrimReserve(3 * MarePoints + 6 * MarePoints, 1 + 2 * MarePoints);
        var i0 = dl.VtxCurrentIdx;
        dl.PrimWriteVtx(center + region.Clamp(mare.Center) * radius, uv, color);
        for (var ring = 0; ring < 2; ring++)
        {
            var scale = ring == 0 ? MoonDetail.MareSoftStart : 1f;
            for (var k = 0; k < MarePoints; k++)
            {
                var (s, c) = MathF.SinCos(k * (2f * MathF.PI / MarePoints));
                var local = new Vector2(c * mare.Radii.X, s * mare.Radii.Y) * scale;
                var unit = mare.Center + new Vector2(local.X * cos - local.Y * sin, local.X * sin + local.Y * cos);
                dl.PrimWriteVtx(center + region.Clamp(unit) * radius, uv, ring == 0 ? color : clear);
            }
        }

        for (var k = 0; k < MarePoints; k++)
        {
            var next = (uint)((k + 1) % MarePoints);
            WriteTriangle(dl, i0, i0 + 1 + (uint)k, i0 + 1 + next);
            WriteQuad(dl, i0 + 1 + (uint)k, i0 + 1 + next, i0 + 1 + MarePoints + next, i0 + 1 + MarePoints + (uint)k);
        }
    }

    /// <summary>
    /// The glow on the lit side of the terminator: rows from pole to pole (cusp to cusp for the waning lens), each
    /// with a point on the terminator and at half and full band width; the outer two carry the band alpha, the inner
    /// edge none, standing in for the sheet's blur. One mesh.
    /// </summary>
    private static void Band(ImDrawListPtr dl, Vector2 center, float radius, LitRegion region, in Tone tone, Vector2 uv)
    {
        var color = tone.Band;
        var clear = color & 0x00FFFFFFu;
        var direction = region.LitOnLeft ? -1f : 1f;
        var half = region.BoundaryHalfHeight;

        dl.PrimReserve(BandRows * 12, 3 * (BandRows + 1));
        var i0 = dl.VtxCurrentIdx;
        for (var row = 0; row <= BandRows; row++)
        {
            var y = -half + 2f * half * row / BandRows;
            var x = region.BoundaryX(y);
            var width = MoonDetail.BandWidthAt(region, y) * direction;
            dl.PrimWriteVtx(center + new Vector2(x, y) * radius, uv, color);
            dl.PrimWriteVtx(center + new Vector2(x + width * 0.5f, y) * radius, uv, color);
            dl.PrimWriteVtx(center + new Vector2(x + width, y) * radius, uv, clear);
        }

        for (var row = 0; row < BandRows; row++)
        {
            var a = i0 + (uint)(3 * row);
            var b = a + 3;
            WriteQuad(dl, a, a + 1, b + 1, b);
            WriteQuad(dl, a + 1, a + 2, b + 2, b + 1);
        }
    }

    /// <summary>
    /// The limb darkening: an annulus from 0.72 r to the rim on the disc's own angular grid, alpha 0 → 25 % → full
    /// (the quadratic ramp at its midpoint), vertices on the dark side pulled onto the terminator. One mesh.
    /// </summary>
    private static void Vignette(ImDrawListPtr dl, Vector2 center, float radius, int segments, LitRegion region, uint color, Vector2 uv)
    {
        var clear = color & 0x00FFFFFFu;
        var middle = (MoonDetail.VignetteStart + 1f) * 0.5f;
        var mid = Theme.WithAlpha(Theme.Umbra, ((color >> 24) / 255f) * MoonDetail.VignetteRamp(middle));
        ReadOnlySpan<float> radii = [MoonDetail.VignetteStart, middle, 1f];

        dl.PrimReserve(segments * 12, 3 * segments);
        var i0 = dl.VtxCurrentIdx;
        var step = 2f * MathF.PI / segments;
        for (var k = 0; k < segments; k++)
        {
            var (s, c) = MathF.SinCos(k * step);
            var direction = new Vector2(c, s);
            for (var j = 0; j < 3; j++)
            {
                var unit = region.Clamp(direction * radii[j]);
                dl.PrimWriteVtx(center + unit * radius, uv, j == 0 ? clear : j == 1 ? mid : color);
            }
        }

        for (var k = 0; k < segments; k++)
        {
            var a = i0 + (uint)(3 * k);
            var b = i0 + (uint)(3 * ((k + 1) % segments));
            WriteQuad(dl, a, b, b + 1, a + 1);
            WriteQuad(dl, a + 1, b + 1, b + 2, a + 2);
        }
    }

    /// <summary>Thin white arc at 0.76 r from 200° to 252° on a full disc: the specular highlight, r ≥ 16 only.</summary>
    private static void HighlightArc(ImDrawListPtr dl, Vector2 center, float radius)
    {
        dl.PathClear();
        dl.PathArcTo(center, radius * ArcRadius, ArcStart, ArcEnd, ArcSegments(MoonGeometry.SegmentsFor(radius), ArcEnd - ArcStart));
        dl.PathStroke(ArcColor, ImDrawFlags.None, MathF.Max(1f, radius * ArcWidthFraction));
    }

    private static void WriteTriangle(ImDrawListPtr dl, uint a, uint b, uint c)
    {
        dl.PrimWriteIdx((ushort)a);
        dl.PrimWriteIdx((ushort)b);
        dl.PrimWriteIdx((ushort)c);
    }

    private static void WriteQuad(ImDrawListPtr dl, uint a, uint b, uint c, uint d)
    {
        WriteTriangle(dl, a, b, c);
        WriteTriangle(dl, a, c, d);
    }

    private static Vector2 Centroid(List<Vector2> polygon)
    {
        var sum = Vector2.Zero;
        foreach (var p in polygon) sum += p;
        return sum / polygon.Count;
    }

    /// <summary>Point inside a convex polygon: every edge cross product has the same sign (either winding).</summary>
    private static bool ContainsPoint(List<Vector2> polygon, Vector2 p)
    {
        var n = polygon.Count;
        if (n < 3) return false;

        var positive = false;
        var negative = false;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var a = polygon[j];
            var b = polygon[i];
            var cross = (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
            if (cross > 0f) positive = true;
            else if (cross < 0f) negative = true;
            if (positive && negative) return false;
        }

        return true;
    }

    // ------------------------------------------------------------------ rims

    /// <summary>Ring just inside the rim, like Pillow's inward outline: stroke centred at r − thickness/2.</summary>
    private static void Ring(ImDrawListPtr dl, Vector2 center, float radius, int segments, float thickness, uint color)
    {
        dl.AddCircle(center, radius - thickness * 0.5f, color, segments, thickness);
    }

    /// <summary>The rim arc over the dark side of a filling moon: the left half-circle, pole to pole, butt ends.</summary>
    private static void DarkSideRim(ImDrawListPtr dl, Vector2 center, float radius, int segments, float thickness, uint color)
    {
        dl.PathClear();
        dl.PathArcTo(center, radius - thickness * 0.5f, MathF.PI / 2f, 3f * MathF.PI / 2f, ArcSegments(segments, MathF.PI));
        dl.PathStroke(color, ImDrawFlags.None, thickness);
    }

    /// <summary><paramref name="dashes"/> dashes of <paramref name="dashDegrees"/> each, evenly spaced from 12 o'clock.</summary>
    private static void DashedRing(ImDrawListPtr dl, Vector2 center, float radius, int segments, float thickness, uint color, int dashes, float dashDegrees)
    {
        var r = radius - thickness * 0.5f;
        var sweep = dashDegrees * Degrees;
        var arcSegments = ArcSegments(segments, sweep);

        dl.PathClear();
        for (var k = 0; k < dashes; k++)
        {
            var start = (k * 360f / dashes - 90f) * Degrees;
            dl.PathArcTo(center, r, start, start + sweep, arcSegments);
            dl.PathStroke(color, ImDrawFlags.None, thickness);
        }
    }

    /// <summary>Segments for an arc of <paramref name="sweep"/> radians at the disc's segment density, at least six.</summary>
    private static int ArcSegments(int segments, float sweep) =>
        Math.Max(6, (int)MathF.Ceiling(segments * sweep / (2f * MathF.PI)));

    /// <summary>A lit tone: the three gradient stops and the detail's light colours, precomputed so drawing packs little.</summary>
    private readonly struct Tone(Vector4 high, Vector4 mid, Vector4 deep, bool silver)
    {
        public bool Silver { get; } = silver;

        /// <summary>Terminator glow: MoonHigh at 30 % on gold, white at 25 % on silver.</summary>
        public uint Band { get; } = Theme.WithAlpha(high, silver ? MoonDetail.BandSilver : MoonDetail.BandGold);

        /// <summary>The gradient colour at <paramref name="t"/> (distance over the gradient radius, clamped to 0..1).</summary>
        public uint Gradient(float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            var color = t <= GradientMidStop
                ? Vector4.Lerp(high, mid, t / GradientMidStop)
                : Vector4.Lerp(mid, deep, (t - GradientMidStop) / (1f - GradientMidStop));
            return Theme.WithAlpha(color, 1f);
        }
    }
}
