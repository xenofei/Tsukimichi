using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The bead ring (Moon Road proposal §7.7, geometry in <see cref="BeadRingMath"/>): one segment per quest current, a
/// lit one a full-width Moon stroke with round ends, an unlit one a thin OrbitTrack hairline, so lit and unlit differ in
/// weight as well as colour (never colour alone, §10.3). Under the high-contrast palette the lit strokes are 1.5×
/// thicker and the unlit ones opaque VeilLine (§10.2). A ring of more segments than <see cref="BeadRingMath.MaxSegments"/>
/// is a track and a Moon arc. Allocation-free.
/// </summary>
public static class BeadRing
{
    private const int CircleSegments = 32;

    /// <summary>
    /// Draws the ring centred on <paramref name="center"/> with radius <paramref name="radius"/>: <paramref name="lit"/> of
    /// <paramref name="segments"/> lit from 12 o'clock clockwise, the last lit one at <paramref name="headAlpha"/> (a
    /// segment lighting up). <paramref name="stroke"/> is the lit width.
    /// </summary>
    public static void Draw(ImDrawListPtr dl, Vector2 center, float radius, int segments, int lit, float stroke, float headAlpha = 1f)
    {
        if (!(radius > 1f) || !(stroke > 0f))
        {
            return;
        }

        var highContrast = Theme.Glyphs.HighContrast;
        var litStroke = highContrast ? stroke * 1.5f : stroke;
        var unlitStroke = highContrast ? MathF.Max(1f, stroke * 0.6f) : MathF.Max(1f, stroke * 0.5f);
        var unlitColor = highContrast ? Theme.VeilLineU32 : Orbit.TrackU32;
        lit = Math.Clamp(lit, 0, Math.Max(0, segments));

        if (!BeadRingMath.Segmented(segments))
        {
            // One segment, none, or too many to count: a track and a Moon arc of the fraction.
            dl.AddCircle(center, radius, unlitColor, CircleSegments, unlitStroke);
            var fraction = segments > 0 ? (float)lit / segments : 0f;
            if (fraction >= 1f)
            {
                dl.AddCircle(center, radius, Theme.WithAlpha(Theme.Moon, headAlpha), CircleSegments, litStroke);
            }
            else if (fraction > 0f)
            {
                var sweep = 2f * MathF.PI * fraction;
                Arc(dl, center, radius, GaugeGeometry.StartAngle, sweep, Theme.MoonU32, litStroke);
            }

            return;
        }

        var span = BeadRingMath.SegmentSweep(segments);
        for (var i = 0; i < segments; i++)
        {
            var start = BeadRingMath.SegmentStart(i, segments);
            if (i >= lit)
            {
                Arc(dl, center, radius, start, span, unlitColor, unlitStroke, caps: false);
                continue;
            }

            var color = i == lit - 1 && headAlpha < 1f ? Theme.WithAlpha(Theme.Moon, Math.Clamp(headAlpha, 0f, 1f)) : Theme.MoonU32;
            if (i == lit - 1 && headAlpha < 1f)
            {
                // Lighting up: the hairline stays under it until the gold has faded in.
                Arc(dl, center, radius, start, span, unlitColor, unlitStroke, caps: false);
            }

            Arc(dl, center, radius, start, span, color, litStroke);
        }
    }

    private static void Arc(ImDrawListPtr dl, Vector2 center, float radius, float start, float sweep, uint color, float stroke, bool caps = true)
    {
        dl.PathClear();
        dl.PathArcTo(center, radius, start, start + sweep, GaugeGeometry.ArcSegments(CircleSegments, sweep));
        dl.PathStroke(color, ImDrawFlags.None, stroke);
        if (!caps)
        {
            return;
        }

        var (s0, c0) = MathF.SinCos(start);
        var (s1, c1) = MathF.SinCos(start + sweep);
        dl.AddCircleFilled(center + (new Vector2(c0, s0) * radius), stroke * 0.5f, color);
        dl.AddCircleFilled(center + (new Vector2(c1, s1) * radius), stroke * 0.5f, color);
    }
}
