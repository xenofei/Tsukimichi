using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The quest-state glyphs and the progress gauges, behind the call sites they have always had. Since 1.12 a state is a
/// medal of "Menphina's Medallion" (<see cref="MedalGlyph"/>, feature plan v6 G1) and the gauges are drawn in the
/// medal's material, a gilt arc in a lapis groove round a moonstone moon (<see cref="MedalGauge"/>, G5). A medal means a
/// quest state and nothing else (accessibility B2): met requirements, obtained rewards and the live indicator use
/// <see cref="Marks"/>. The high-contrast gauges keep their flat 1.11 look (<see cref="LegacyMoonGlyph"/>).
/// </summary>
public static class MoonGlyph
{
    /// <summary>Radius of an inline glyph as a fraction of its square (as it was for the moons, so layouts hold).</summary>
    public const float InlineRadiusFraction = 0.42f;

    /// <summary>Alpha of the veiled medal standing in for an icon the game does not have, at full strength.</summary>
    private const float VeiledMedalAlpha = 0.7f;

    /// <summary>Draws the medal for <paramref name="state"/> centred at <paramref name="center"/> with the given keyline radius (px).</summary>
    public static void Draw(ImDrawListPtr dl, Vector2 center, float radius, QuestState state) =>
        MedalGlyph.Draw(dl, center, radius, state);

    /// <summary>The medal with the job a Ready on another job quest is ready on, for its badge (0: unknown).</summary>
    public static void Draw(ImDrawListPtr dl, Vector2 center, float radius, QuestState state, byte job) =>
        MedalGlyph.Draw(dl, center, radius, state, job);

    /// <summary>Reserves a <paramref name="size"/> × <paramref name="size"/> item at the cursor and draws the medal inside it.</summary>
    public static void DrawInline(QuestState state, float size) => MedalGlyph.DrawInline(state, size);

    /// <summary>
    /// The Not checked medal faded to <paramref name="alpha"/>, as a stand-in where an icon would go and none exists
    /// (Moonlit rewards of a kind without sheet art). Reserves a square item like <see cref="DrawInline"/>, so the caller
    /// can hang a tooltip on it; it says nothing about state or progress.
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
        if (!(radius > 0.5f))
        {
            return;
        }

        var (min, size) = MedalGlyph.Box(center, radius);
        MedalGlyph.DrawMesh(dl, MedalArt.Medal(QuestState.Unknown, MedalTokens.For(Theme.Glyphs), row: size < MedalLayout.RowTierMaxPx), min, size, alpha * VeiledMedalAlpha);
    }

    /// <summary>
    /// The halo gauge, the progress glyph (geometry in <see cref="GaugeGeometry"/>): a groove at 0.80 R, a gilt arc from
    /// 12 o'clock clockwise with round caps whose length is the floored fraction, and from R 12 a core moon filling to the
    /// fraction. At exactly 1 the ring is whole gilt with a soft glow round a full moon, or, with
    /// <paramref name="dimComplete"/>, the gilt's shaded side round a quiet moon. Below R 12 only groove and arc are
    /// drawn and the caller writes the number beside it; below R 8 nothing is drawn and the caller writes the number alone.
    /// High contrast draws the 1.11 flat gauge. Allocation-free.
    /// </summary>
    /// <param name="radius">Half the box, R.</param>
    /// <param name="onCard">Drawn on a raised card (the high-contrast track uses it; the groove carries its own keyline).</param>
    /// <param name="dimComplete">Whether a complete gauge steps back instead of glowing; the Journal tree sets it so finished nodes recede.</param>
    public static void DrawHalo(ImDrawListPtr dl, Vector2 center, float radius, float fraction, bool onCard = false, bool dimComplete = false)
    {
        var mode = GaugeGeometry.ModeFor(radius);
        if (mode == HaloMode.NumberOnly)
        {
            return;
        }

        if (Theme.Glyphs.HighContrast)
        {
            LegacyMoonGlyph.DrawHalo(dl, center, radius, fraction, onCard, dimComplete);
            return;
        }

        center = Snap(center, radius);
        var f = GaugeGeometry.Clamp01(fraction);
        var track = GaugeGeometry.TrackRadius(radius);
        var stroke = GaugeGeometry.Stroke(radius);
        var segments = MoonGeometry.SegmentsFor(track);
        var core = GaugeGeometry.CoreRadius(radius);

        if (f >= 1f)
        {
            if (dimComplete)
            {
                MedalGauge.Groove(dl, center, track, stroke, segments);
                dl.AddCircle(center, track, MedalGauge.GiltDimU32, segments, stroke);
                if (mode == HaloMode.Core)
                {
                    MedalGauge.FillingMoon(dl, center, core, 1f, dim: true);
                }

                return;
            }

            dl.AddCircle(center, track, MedalGauge.GlowOuterU32, segments, stroke * 2.4f);
            dl.AddCircle(center, track, MedalGauge.GlowInnerU32, segments, stroke * 1.6f);
            MedalGauge.Groove(dl, center, track, stroke, segments);
            MedalGauge.GiltArc(dl, center, track, stroke, GaugeGeometry.StartAngle, 2f * MathF.PI, radius, segments);
            if (mode == HaloMode.Core)
            {
                MedalGauge.FillingMoon(dl, center, core, 1f);
            }

            return;
        }

        MedalGauge.Groove(dl, center, track, stroke, segments);
        MedalGauge.GiltArc(dl, center, track, stroke, GaugeGeometry.StartAngle, GaugeGeometry.Sweep(f, radius), radius, segments);
        if (mode == HaloMode.Core)
        {
            MedalGauge.FillingMoon(dl, center, core, GaugeGeometry.CoreLitWidth(f, core));
        }
    }

    /// <summary>
    /// Inline form of <see cref="DrawHalo"/>: reserves a <paramref name="size"/> square item at the cursor (so a tooltip
    /// can hang off it) and draws the halo with R = size / 2. Returns the <see cref="HaloMode"/> it drew, so the caller
    /// knows whether a number belongs beside it (<see cref="HaloMode.Ring"/>) or instead of it (<see cref="HaloMode.NumberOnly"/>).
    /// This overload eases the fill through <see cref="Motion.Gauge"/> under <paramref name="motionKey"/>.
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
    /// moon, with the terminator floor of <see cref="MoonGeometry.FillingLayers"/>, in moonstone over its dark side
    /// (G5). The rail's Journal station and foot gauge, the loading moon and the orbit cores draw it.
    /// </summary>
    public static void DrawFilling(ImDrawListPtr dl, Vector2 center, float radius, float fraction)
    {
        if (!(radius > 0.5f))
        {
            return;
        }

        if (Theme.Glyphs.HighContrast)
        {
            LegacyMoonGlyph.DrawFilling(dl, center, radius, fraction);
            return;
        }

        MedalGauge.FillingMoon(dl, Snap(center, radius), radius, MoonGeometry.FlooredFraction(fraction, radius));
    }

    /// <summary>Snaps the centre so a ring of this radius lands on whole pixels (integer for an even diameter, half for odd).</summary>
    private static Vector2 Snap(Vector2 center, float radius)
    {
        var offset = (int)MathF.Round(2f * radius) % 2 == 1 ? 0.5f : 0f;
        return new Vector2(MathF.Round(center.X - offset) + offset, MathF.Round(center.Y - offset) + offset);
    }
}
