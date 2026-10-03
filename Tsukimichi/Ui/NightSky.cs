using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Config;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Ui.Themes;

namespace Tsukimichi.Ui;

/// <summary>
/// The Full sky (feature plan v7 UI-6, docs/design/v7/ui/spec.md §3 and Revision 3): the one star renderer, used by
/// every empty sky (the rail's gap, the tree's sky under its last node, the table's title band and the Path chart's
/// bands) and gated by Decoration (<see cref="Theme.ShowStars"/>, Full only).
/// <list type="bullet">
/// <item>Three depths and four temperatures (<see cref="StarField"/>), every mark 4 px inside empty sky, seeded so the
/// field never reshuffles.</item>
/// <item>A slow twinkle (7–13 s) and the moving night sky (one rigid drift of 6 px a minute, wrapping with a 10 px edge
/// fade), both on the <see cref="SkyClock"/>, which runs only at Full, with Reduce motion off and the main window
/// focused.</item>
/// <item>The selected quest's region constellation in the first sky that holds a clear 120 × 100 box, the optional
/// Milky Way in the one sky at least 200 px tall, and the meteors (on completion, and a faint one every few minutes) in
/// the largest. These choices are made from last frame's sky rects (<see cref="SkyRects"/>), as the layout is the same;
/// a meteor's sky is chosen as it starts and kept for its flight.</item>
/// </list>
/// Per frame: one offset and a loop over cached fields, about 200 draw calls, nothing allocated.
/// </summary>
public static class NightSky
{
    /// <summary>The temperatures' inks (spec §3.2), from the palette's scene (<see cref="StarInks"/>).</summary>
    private static Vector4 Cool => Theme.Scene.Stars.Cool;

    private static Vector4 MoonWhite => Theme.Scene.Stars.MoonWhite;

    private static Vector4 Gold => Theme.Scene.Stars.Gold;

    private static Vector4 Ember => Theme.Scene.Stars.Ember;

    /// <summary>The constellations' stars (spec §3.5) and the Milky Way's tint (§3.4).</summary>
    private static Vector4 FigureStar => Theme.Scene.Stars.Figure;

    private static Vector4 BandTint => Theme.Scene.Stars.Band;

    /// <summary>Every mark keeps this far inside its sky, logical px (spec §3).</summary>
    private const float InsetLogical = 4f;

    /// <summary>The Milky Way (spec §3.4): its sky's least height, its width, its tilt and its alpha.</summary>
    private const float BandMinHeightLogical = 200f;
    private const float BandWidthLogical = 74f;
    private const float BandTiltDegrees = 28f;
    private const float BandAlpha = 0.06f;
    private const int BandColumns = 24;

    /// <summary>The meteor's travel (64 × 34 logical px, 28° below level) and its tail (46 px of six segments).</summary>
    private static readonly Vector2 MeteorTravelLogical = new(64f, 34f);
    private const float MeteorTailLogical = 46f;
    private const int MeteorTailSegments = 6;

    private static readonly SkyClock Clock = new();
    private static readonly SkyRects Rects = new();
    private static readonly PlacedStar[] Placed = new PlacedStar[512];
    private static readonly Star[] BandStars = StarField.Band(0x4D57, 96);

    // Settings and focus, as of this frame.
    private static bool drift;
    private static bool completionMeteor;
    private static bool milkyWay;
    private static bool focusNoted;
    private static byte? expansionNoted;
    private static byte? expansion;
    private static bool completionNoted;

    // This frame's hosts, chosen from last frame's sky rects.
    private static SkyRect? constellationHost;
    private static SkyRect? bandHost;
    private static SkyRect? meteorHost;

    // The meteor playing, if any.
    private static double meteorStart = double.NegativeInfinity;
    private static float meteorPeak;

    // Where the meteor playing starts, against its sky's canvas (SkyRects.MeteorStart), once its first frame drew it.
    private static bool meteorLatched;
    private static Vector2 meteorFrom;

    /// <summary>The field's drift offset this frame, px.</summary>
    private static float offset;

    /// <summary>Whether the sky drifts this frame (Full, motion on, the setting on), focused or not, so the look holds when focus moves.</summary>
    private static bool drifting;

    /// <summary>
    /// The main window, once per frame while it draws: whether it (or a child) is focused, and the selected quest's
    /// expansion (null for none), whose constellation the sky shows.
    /// </summary>
    public static void NoteMainWindow(bool focused, byte? selectedExpansion)
    {
        focusNoted = focused;
        expansionNoted = selectedExpansion;
    }

    /// <summary>A quest the player can see was just completed (the waxing moon's trigger): the sky may send a meteor.</summary>
    public static void NoteCompletion() => completionNoted = true;

    /// <summary>
    /// Once per frame, after <see cref="Motion.NoteCompletions"/> and before any window draws: reads the settings,
    /// advances the clocks by last frame's time if the main window was focused, chooses this frame's hosts from last
    /// frame's sky rects, and starts a meteor that fell due.
    /// </summary>
    public static void BeginFrame(Configuration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var now = ImGui.GetTime();
        var animates = Theme.FlairMotion && Theme.ShowStars;
        drift = settings.MovingNightSky;
        completionMeteor = settings.CompletionMeteor;
        milkyWay = settings.MilkyWay;
        drifting = animates && drift;

        var focused = focusNoted;
        focusNoted = false;
        expansion = expansionNoted;
        expansionNoted = null;
        Clock.Advance(ImGui.GetIO().DeltaTime, animates, focused, drift);
        offset = drifting ? Clock.Offset(UiMetrics.Px(MotionTokens.SkyDriftPxPerMinute)) : 0f;

        Rects.Swap();
        var rects = Rects.Previous;
        var inset = UiMetrics.Px(InsetLogical);
        constellationHost = At(rects, SkyRects.ConstellationHost(rects, Constellations.ClearBoxLogical * UiMetrics.Scale, inset));
        bandHost = milkyWay ? At(rects, SkyRects.BandHost(rects, UiMetrics.Px(BandMinHeightLogical))) : null;
        var playing = now - meteorStart < MotionTokens.Meteor;
        var started = false;
        if (completionNoted)
        {
            completionNoted = false;
            if (animates && completionMeteor && Clock.TakeCompletion(now))
            {
                meteorStart = now;
                meteorPeak = MotionTokens.MeteorPeak;
                playing = true;
                started = true;
            }
        }

        if (Clock.TakeAmbient(now) && !playing)
        {
            meteorStart = now;
            meteorPeak = MotionTokens.AmbientMeteorPeak;
            started = true;
        }

        // A meteor's sky is chosen once, as it starts, and kept for its flight (with where it starts, latched on its
        // first drawn frame): chosen again every frame it could hop to another sky mid-flight when the largest changed.
        if (started)
        {
            meteorHost = At(rects, SkyRects.Largest(rects));
            meteorLatched = false;
        }
    }

    /// <summary>
    /// Draws the sky's stars in <paramref name="skyMin"/>..<paramref name="skyMax"/> (empty sky only: nothing the player
    /// reads is in it), from a field laid over <paramref name="canvasMin"/>..<paramref name="canvasMax"/> (its pane, so a
    /// sky that grows uncovers stars rather than stretching them), and whatever this sky hosts: the Milky Way under the
    /// stars, the constellation and a meteor over them. <paramref name="site"/> and <paramref name="key"/> name the sky
    /// for the next frame's choices. Full only; allocation-free.
    /// </summary>
    public static void Field(ImDrawListPtr dl, SkySite site, int key, ReadOnlySpan<Star> stars, Vector2 canvasMin, Vector2 canvasMax, Vector2 skyMin, Vector2 skyMax)
    {
        if (!Theme.ShowStars)
        {
            return;
        }

        Rects.Add(site, key, skyMin, skyMax);
        var view = new SkyView(canvasMin, canvasMax, skyMin, skyMax, UiMetrics.Px(InsetLogical), offset, UiMetrics.Px(MotionTokens.SkyEdgeFadeLogical), Clock.Time, Theme.FlairMotion, drifting);
        if (!view.IsOpen)
        {
            return;
        }

        var clipMin = new Vector2(view.Left, view.Top);
        var clipMax = new Vector2(view.Right, view.Bottom);
        var band = Hosts(bandHost, site, key);
        var shown = StarField.Place(Thin(stars), view, Placed);
        if (band)
        {
            dl.PushClipRect(clipMin, clipMax, true);
            DrawBand(dl, view, shown);
            dl.PopClipRect();
        }

        DrawStars(dl, shown);

        if (Hosts(constellationHost, site, key) && expansion is { } region && Constellations.For(region) is { } figure)
        {
            dl.PushClipRect(clipMin, clipMax, true);
            DrawConstellation(dl, figure, view);
            dl.PopClipRect();
        }

        if (Hosts(meteorHost, site, key))
        {
            var progress = (float)((ImGui.GetTime() - meteorStart) / MotionTokens.Meteor);
            if (progress is >= 0f and < 1f)
            {
                if (!meteorLatched)
                {
                    meteorFrom = SkyRects.MeteorStart(canvasMin, skyMin, skyMax);
                    meteorLatched = true;
                }

                dl.PushClipRect(clipMin, clipMax, true);
                DrawMeteor(dl, canvasMin, progress);
                dl.PopClipRect();
            }
        }
    }

    /// <summary>
    /// A still field in <paramref name="min"/>..<paramref name="max"/> (the Settings preview): the same marks, without
    /// drift, twinkle, fade or anything hosted, and 2 px inside its small strip.
    /// </summary>
    public static void Still(ImDrawListPtr dl, ReadOnlySpan<Star> stars, Vector2 min, Vector2 max)
    {
        var view = new SkyView(min, max, min, max, UiMetrics.Px(2f), 0f, 0f, 0d, false, false);
        if (!view.IsOpen)
        {
            return;
        }

        var shown = StarField.Place(Thin(stars), view, Placed);
        DrawStars(dl, shown);
    }

    /// <summary>The share of a field the palette's sky shows (<see cref="StarInks.Density"/>: all on Night, half on Kugane Lacquer).</summary>
    private static ReadOnlySpan<Star> Thin(ReadOnlySpan<Star> stars) => stars[..StarField.Thinned(stars.Length, Theme.Scene.Stars.Density)];

    /// <summary>The first <paramref name="count"/> placed stars: far dots, mid discs, near stars with their cross and halo.</summary>
    private static void DrawStars(ImDrawListPtr dl, int count)
    {
        var unit = MathF.Max(1f, UiMetrics.Px(1f));
        var line = MathF.Max(1f, unit);
        var arm = 2.5f * unit;
        for (var i = 0; i < count; i++)
        {
            ref readonly var star = ref Placed[i];
            var p = star.Position;
            var tone = star.Layer == StarLayer.Far ? FarTone(star.Temperature) : Tone(star.Temperature);
            switch (star.Layer)
            {
                case StarLayer.Far when drifting:
                    // Drifting, a soft disc glides where a 1 × 1 rect would step a whole pixel every ten seconds.
                    dl.AddCircleFilled(p, 0.6f * unit, Theme.WithAlpha(tone, star.Alpha), 6);
                    break;
                case StarLayer.Far:
                    dl.AddRectFilled(p, p + new Vector2(unit), Theme.WithAlpha(tone, star.Alpha));
                    break;
                case StarLayer.Mid:
                    dl.AddCircleFilled(p, 0.95f * unit, Theme.WithAlpha(tone, star.Alpha), 8);
                    break;
                default:
                    // The cross keeps its length while the star breathes: only its alpha follows the star's.
                    dl.AddCircleFilled(p, 3.4f * unit, Theme.WithAlpha(tone, star.Alpha * 0.13f), 12);
                    var cross = Theme.WithAlpha(tone, star.Alpha * 0.4f);
                    dl.AddLine(p - new Vector2(arm, 0f), p + new Vector2(arm, 0f), cross, line);
                    dl.AddLine(p - new Vector2(0f, arm), p + new Vector2(0f, arm), cross, line);
                    dl.AddCircleFilled(p, 1.35f * unit, Theme.WithAlpha(tone, star.Alpha), 10);
                    break;
            }
        }
    }

    /// <summary>
    /// The region constellation (spec §3.5): drawn at 80 px in the clear box at the sky's lower right, drifting with the
    /// field and wrapping on its canvas, hairline links at .09, stars r 1.15 at .48 and the lead star r 1.45 at .6, each
    /// with a soft halo at .07. Faded at the sky's edges as the stars are.
    /// </summary>
    private static void DrawConstellation(ImDrawListPtr dl, Constellation figure, in SkyView view)
    {
        var box = Constellations.ClearBoxLogical * UiMetrics.Scale;
        var size = UiMetrics.Px(Constellations.SizeLogical);
        var corner = SkyRects.ConstellationBox(view.SkyMin, view.SkyMax, box, view.Inset) + ((box - new Vector2(size)) * 0.5f);
        var width = view.CanvasMax.X - view.CanvasMin.X;
        var left = view.CanvasMin.X + StarField.Wrap(corner.X - view.CanvasMin.X - view.Offset, width);
        var unit = MathF.Max(1f, UiMetrics.Px(1f));
        for (var copy = 0; copy < 2; copy++)
        {
            var origin = new Vector2(left - (copy * width), corner.Y);
            if (origin.X > view.Right || origin.X + size < view.Left)
            {
                continue;
            }

            foreach (var (from, to) in figure.Edges)
            {
                var a = origin + (figure.Points[from] * size);
                var b = origin + (figure.Points[to] * size);
                var fade = MathF.Min(StarField.EdgeFade(a.X, view.Left, view.Right, view.Fade), StarField.EdgeFade(b.X, view.Left, view.Right, view.Fade));
                if (fade > 0f)
                {
                    dl.AddLine(a, b, Theme.WithAlpha(Cool, 0.09f * fade), 1f);
                }
            }

            for (var i = 0; i < figure.Points.Length; i++)
            {
                var p = origin + (figure.Points[i] * size);
                var fade = StarField.EdgeFade(p.X, view.Left, view.Right, view.Fade);
                if (fade <= 0f)
                {
                    continue;
                }

                var lead = i == Constellations.Lead;
                dl.AddCircleFilled(p, (lead ? 3.4f : 2.6f) * unit, Theme.WithAlpha(Cool, 0.07f * fade), 12);
                dl.AddCircleFilled(p, (lead ? 1.45f : 1.15f) * unit, Theme.WithAlpha(FigureStar, (lead ? 0.6f : 0.48f) * fade), 10);
            }
        }
    }

    /// <summary>
    /// The Milky Way (spec §3.4): one soft, mottled band about 74 px wide, rising 28° to the right through the sky's lower
    /// part, as a single vertex-coloured mesh at .06 of #C9D3F0 (clear at its edges and ends), with about 40 % more far
    /// stars scattered along its axis. It drifts with the field and leaves the sky entirely before it comes round again.
    /// </summary>
    private static void DrawBand(ImDrawListPtr dl, in SkyView view, int shown)
    {
        var width = view.CanvasMax.X - view.CanvasMin.X;
        var tilt = BandTiltDegrees * MathF.PI / 180f;
        var axis = new Vector2(MathF.Cos(tilt), -MathF.Sin(tilt));
        var normal = new Vector2(-axis.Y, axis.X);
        var halfSpan = (width * 0.5f) + UiMetrics.Px(30f);
        var halfLength = halfSpan / axis.X;
        var halfWidth = UiMetrics.Px(BandWidthLogical) * 0.5f;
        var centerX = view.CanvasMin.X + StarField.Wrap((width * 0.5f) - view.Offset + halfSpan, width + (2f * halfSpan)) - halfSpan;
        var center = new Vector2(centerX, view.SkyMax.Y - UiMetrics.Px(110f));

        // Five vertices across (clear, half, full, half, clear) at each of the columns along the axis.
        ReadOnlySpan<float> across = [-1f, -0.5f, 0f, 0.5f, 1f];
        ReadOnlySpan<float> profile = [0f, 0.55f, 1f, 0.55f, 0f];
        var uv = ImGui.GetFontTexUvWhitePixel();
        var columns = BandColumns + 1;
        dl.PrimReserve(BandColumns * 4 * 6, columns * 5);
        var first = dl.VtxCurrentIdx;
        for (var c = 0; c < columns; c++)
        {
            var t = c / (float)BandColumns;
            var along = (t * 2f) - 1f;
            var ends = MathF.Min(1f, (1f - MathF.Abs(along)) / 0.18f);
            var mottle = 0.62f + (0.38f * Mottle(c));
            var swell = 0.85f + (0.15f * MathF.Sin((c * 1.7f) + 0.4f));
            var spine = center + (axis * (along * halfLength));
            for (var r = 0; r < 5; r++)
            {
                var alpha = BandAlpha * profile[r] * ends * mottle;
                dl.PrimWriteVtx(spine + (normal * (across[r] * halfWidth * swell)), uv, Theme.WithAlpha(BandTint, alpha));
            }
        }

        for (var c = 0; c < BandColumns; c++)
        {
            for (var r = 0; r < 4; r++)
            {
                var a = first + (uint)((c * 5) + r);
                var b = a + 5;
                dl.PrimWriteIdx((ushort)a);
                dl.PrimWriteIdx((ushort)(a + 1));
                dl.PrimWriteIdx((ushort)(b + 1));
                dl.PrimWriteIdx((ushort)a);
                dl.PrimWriteIdx((ushort)(b + 1));
                dl.PrimWriteIdx((ushort)b);
            }
        }

        // The band's own far stars: about 40 % of the field's, scattered close to its axis.
        var unit = MathF.Max(1f, UiMetrics.Px(1f));
        var count = Math.Min(BandStars.Length, (int)MathF.Round(shown * 0.4f));
        for (var i = 0; i < count; i++)
        {
            ref readonly var star = ref BandStars[i];
            var p = center + (axis * (((star.U * 2f) - 1f) * halfLength)) + (normal * (star.V * halfWidth * 0.8f));
            if (p.Y < view.Top || p.Y > view.Bottom)
            {
                continue;
            }

            var fade = StarField.EdgeFade(p.X, view.Left, view.Right, view.Fade);
            if (fade > 0f)
            {
                dl.AddCircleFilled(p, 0.6f * unit, Theme.WithAlpha(FarTone(star.Temperature), star.Alpha * fade), 6);
            }
        }
    }

    /// <summary>
    /// A meteor at <paramref name="progress"/> (spec §3.6): the head r 1.6 in MoonHigh with an r 4.5 halo at .25 of its
    /// alpha, and a 46 px tail of six segments from .45 (at the completion meteor's peak) down to nothing. It flies from
    /// its latched start on its sky's canvas, now at <paramref name="canvasMin"/>.
    /// </summary>
    private static void DrawMeteor(ImDrawListPtr dl, Vector2 canvasMin, float progress)
    {
        var alpha = MotionTokens.MeteorAlpha(progress, meteorPeak);
        if (alpha <= 0f)
        {
            return;
        }

        var travel = MeteorTravelLogical * UiMetrics.Scale;
        var head = SkyRects.MeteorHeadFrom(canvasMin, meteorFrom, travel, progress);
        var back = -Vector2.Normalize(MeteorTravelLogical);
        var length = UiMetrics.Px(MeteorTailLogical);
        var tail = alpha * (MotionTokens.MeteorTailFrom / MotionTokens.MeteorPeak);
        var thickness = MathF.Max(1f, UiMetrics.Px(1.2f));
        for (var k = 0; k < MeteorTailSegments; k++)
        {
            var from = head + (back * (length * k / MeteorTailSegments));
            var to = head + (back * (length * (k + 1) / MeteorTailSegments));
            var segment = tail * (1f - ((k + 0.5f) / MeteorTailSegments));
            dl.AddLine(from, to, Theme.WithAlpha(Theme.Scene.Moonlight, segment), thickness);
        }

        var unit = MathF.Max(1f, UiMetrics.Px(1f));
        dl.AddCircleFilled(head, 4.5f * unit, Theme.WithAlpha(Theme.Scene.Moonlight, alpha * 0.25f), 16);
        dl.AddCircleFilled(head, 1.6f * unit, Theme.WithAlpha(Theme.Scene.Moonlight, alpha), 10);
    }

    private static Vector4 Tone(StarTemperature temperature) => temperature switch
    {
        StarTemperature.Moon => MoonWhite,
        StarTemperature.Gold => Gold,
        StarTemperature.Ember => Ember,
        _ => Cool,
    };

    /// <summary>A far star's ink: the palette's far inks (Night's cool and moon white; Kugane Lacquer's warmer pair).</summary>
    private static Vector4 FarTone(StarTemperature temperature) =>
        temperature == StarTemperature.Moon ? Theme.Scene.Stars.FarMoon : Theme.Scene.Stars.FarCool;

    /// <summary>A fixed, seeded 0..1 per band column, so the band is mottled the same way every frame.</summary>
    private static float Mottle(int column)
    {
        var state = unchecked(((uint)column * 2654435761u) ^ 0x5BD1E995u) & 0x7FFFFFFFu;
        state = unchecked((state * 1103515245u) + 12345u) & 0x7FFFFFFFu;
        return state / (float)0x7FFFFFFF;
    }

    private static SkyRect? At(ReadOnlySpan<SkyRect> rects, int index) => index >= 0 ? rects[index] : null;

    private static bool Hosts(SkyRect? host, SkySite site, int key) => host is { } h && h.Site == site && h.Key == key;
}
