using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Tsukimichi.Core.Moonfall;
using Tsukimichi.Core.Moonfall.Art;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// The level's scene in play (spec-rich2.md §4–5): the built scene over the opening (fading in over the night sky when it
/// lands), the blurred scene in the window's margins, Fever's lighting change, and the ambient motion of the motion rules:
/// the beams breathing and drifting, dust in them, fireflies, twinkling stars, mist wrapping at its tile's width, the
/// lamps' flicker and the top rail's glint. Nothing moves within 2.5 units of a peg (the beams' texture is masked off
/// every piece), particles keep 8 units with their whole path, and under Reduce motion everything is still.
/// </summary>
public sealed partial class MoonfallWindow
{
    private const double SceneFadeSeconds = 0.25;

    /// <summary>The scene's textures this frame, or null (the interim ground draws).</summary>
    private MoonfallSceneTextures<IDalamudTextureWrap>? RichSceneNow() => Theme.Flair == Core.Ui.Flair.Plain ? null : gameArt?.Scene;

    /// <summary>
    /// The window's margins beside (or round) the board: the level's scene small and blurred, darkened, in the level's
    /// deep colour, falling darker toward the window's edges (spec: "the margins carry the level's scene, blurred").
    /// </summary>
    private void DrawMargins(ImDrawListPtr dl, Vector2 areaMin, Vector2 areaMax, Vector2 origin, Vector2 size)
    {
        if (RichSceneNow() is not { } scene || (origin.X - areaMin.X < 2f && origin.Y - areaMin.Y < 2f))
        {
            return;
        }

        // play2.window: the board blurred over the whole window, at 62% with a fifth of the palette's deep added.
        var deep = scene.Layers.Chrome.Deep;
        dl.AddRectFilled(areaMin, areaMax, Ink(deep));
        dl.AddImage(scene.Backdrop.Handle, areaMin, areaMax, Vector2.Zero, Vector2.One, Ink(new Vector3(0.95f), 1f));
        dl.AddRectFilled(areaMin, areaMax, Ink(deep, 0.12f));
        var mx = origin.X - areaMin.X;
        if (mx > 2f)
        {
            var dark = Ink(MoonfallColor.Hex("#020308"), 0.55f);
            var clear = Ink(MoonfallColor.Hex("#020308"), 0f);
            dl.AddRectFilledMultiColor(areaMin, new Vector2(origin.X, areaMax.Y), dark, clear, clear, dark);
            dl.AddRectFilledMultiColor(new Vector2(origin.X + size.X, areaMin.Y), areaMax, clear, dark, dark, clear);
        }

        var my = origin.Y - areaMin.Y;
        if (my > 2f)
        {
            var dark = Ink(MoonfallColor.Hex("#020308"), 0.45f);
            var clear = Ink(MoonfallColor.Hex("#020308"), 0f);
            dl.AddRectFilledMultiColor(areaMin, new Vector2(areaMax.X, origin.Y), dark, dark, clear, clear);
            dl.AddRectFilledMultiColor(new Vector2(areaMin.X, origin.Y + size.Y), areaMax, clear, clear, dark, dark);
        }
    }

    /// <summary>The scene over the opening, Fever's light and the ambient motion over it (under the pegs).</summary>
    private void RichScene(in ArtPen p, MoonfallSceneTextures<IDalamudTextureWrap> scene, MoonfallGame g)
    {
        var layers = scene.Layers;
        var v = p.View;
        if (!ReferenceEquals(sceneFor, scene))
        {
            sceneFor = scene;
            sceneShownAt = boardClock;
        }

        var still = motion == MoonfallMotionLevel.Still;
        var fade = still ? 1f : (float)Math.Clamp((boardClock - sceneShownAt) / SceneFadeSeconds, 0, 1);
        Image(p.Dl, v, scene.Base, layers.Base.Board, Ink(Vector3.One, fade));

        if (g.Fever)
        {
            FeverLight(p, scene, g, still);
        }

        // The beams: their moving share breathes ±15% and its break-up drifts; still, it stands as baked.
        if (scene.BeamsA is { } beamsA)
        {
            var (a, b) = MoonfallMotion.Beams(boardClock, still);
            Image(p.Dl, v, beamsA, layers.BeamsA!.Board, Ink(layers.BeamColour, a * fade));
            if (scene.BeamsB is { } beamsB && b > 0.002f)
            {
                Image(p.Dl, v, beamsB, layers.BeamsB!.Board, Ink(layers.BeamColour, b * fade));
            }
        }

        // Mist: each layer scrolls and wraps at its tile's width, drawn as the tile's two pieces either side of the seam.
        for (var i = 0; i < layers.Mist.Count && i < scene.Mist.Count; i++)
        {
            var m = layers.Mist[i];
            var r = m.Tile.Board;
            var width = r.Z - r.X;
            var offset = motion == MoonfallMotionLevel.Full ? MoonfallMotion.MistOffset(boardClock, m.Speed, width) : 0f;
            var u = offset / width;
            var tint = Ink(m.Colour, m.Alpha * fade);
            var split = r.X + width - offset;
            p.Dl.AddImage(scene.Mist[i].Handle, v.Map(r.X, r.Y), v.Map(split, r.W), new Vector2(u, 0), new Vector2(1, 1), tint);
            if (offset > 0.01f)
            {
                p.Dl.AddImage(scene.Mist[i].Handle, v.Map(split, r.Y), v.Map(r.Z, r.W), new Vector2(0, 0), new Vector2(u, 1), tint);
            }
        }

        var full = motion == MoonfallMotionLevel.Full;
        ref readonly var soft = ref p.Atlas[MoonfallSprite.Soft];
        if (full)
        {
            // Stars twinkle on the scene's own bright points (±35% over 2 and 3 s).
            for (var si = 0; si < layers.Stars.Count; si++)
            {
                var star = layers.Stars[si];
                var lift = MoonfallMotion.Twinkle(star, boardClock, still: false);
                if (lift > 0.01f)
                {
                    Put(p, soft, star.X, star.Y, star.Radius * 1.8f / 4f, Ink(MoonfallColor.Hex("#E8EEFF"), lift * 0.6f * fade));
                }
            }
        }

        // Fireflies: a core and a halo, wandering small closed loops (Full) or resting, always 8 units clear of every piece.
        for (var fi = 0; fi < layers.Fireflies.Count; fi++)
        {
            var f = layers.Fireflies[fi];
            var at = MoonfallMotion.FireflyAt(f, boardClock, !full);
            var pulse = MoonfallMotion.FireflyPulse(f, boardClock, !full);
            Put(p, soft, at.X, at.Y, MoonfallMotion.FireflyHalo * f.Size * 2.2f / 4f, Ink(MoonfallColor.Hex("#FFB45E"), 0.22f * pulse * fade));
            Put(p, soft, at.X, at.Y, MoonfallMotion.FireflyCore * f.Size * 2.2f / 4f, Ink(layers.FireflyColour, 0.95f * pulse * fade));
        }

        // Moondust drifting in the moving beams, never within 8 units of a piece.
        if (full && layers.BeamAmount is { } beam && layers.Clearance is { } clearance)
        {
            var dust = Ink(MoonfallColor.Hex("#E4ECFF"), 1f);
            for (var i = 0; i < layers.Dust; i++)
            {
                var (at, life) = MoonfallMotion.Dust(i, boardClock);
                if (!MoonfallMotion.DustVisible(clearance, at))
                {
                    continue;
                }

                var amount = beam.Sample(at.X * MoonfallSceneBuilder.LowScale, at.Y * MoonfallSceneBuilder.LowScale);
                var a = life * 0.5f * Math.Clamp(amount * 30f, 0f, 1f) * fade;
                if (a > 0.01f)
                {
                    p.Dl.AddCircleFilled(v.Map(at.X, at.Y), MathF.Max(0.8f, v.Size(MoonfallMotion.DustRadius)), (dust & 0x00FFFFFFu) | ((uint)(a * 255) << 24), 8);
                }
            }
        }

        // The lamps' flicker (±10%; Simple keeps it, still keeps them steady).
        for (var li = 0; li < layers.Flickers.Count; li++)
        {
            var l = layers.Flickers[li];
            var flick = MoonfallMotion.Flicker(boardClock, l.Phase, still);
            var a = Math.Clamp((flick - 0.9f) / 0.2f, 0f, 1f) * 0.16f;
            Put(p, soft, l.X, l.Y, l.Halo * 1.2f / 4f, Ink(l.Colour, a * fade));
        }
    }

    /// <summary>
    /// Fever's lighting change (spec-rich2.md §4): the sky lifts toward the carrier's colour over 0.8 s and the board's moon
    /// swells and brightens behind the framing over 0.5 s; under Reduce motion the lit state at once.
    /// </summary>
    private void FeverLight(in ArtPen p, MoonfallSceneTextures<IDalamudTextureWrap> scene, MoonfallGame g, bool still)
    {
        var v = p.View;
        var since = boardClock - feverAt;
        var tSky = still ? 1f : (float)Math.Clamp(since / 0.8, 0, 1);
        var tSwell = still ? 1f : (float)Math.Clamp(since / 0.5, 0, 1);
        var accent = MoonfallCards.For(g.Power)?.Accent ?? MoonfallColor.Hex("#FFD27A");
        Image(p.Dl, v, scene.SkyMask, scene.Layers.SkyMask.Board, Ink(accent, 0.16f * tSky));
        if (scene.Layers.Moon is not { } moon)
        {
            return;
        }

        var r = moon.R * (1 + (0.45f * tSwell));
        ref readonly var soft = ref p.Atlas[MoonfallSprite.Soft];
        // The glow stays inside the framing laid back over it (MoonRect: 3 radii of the moon), so no seam shows at its edge.
        Put(p, soft, moon.X, moon.Y, r * 2.0f / 4f, Ink(MoonfallColor.Hex("#FFF0D8"), 0.34f * tSwell));
        p.Dl.AddCircleFilled(v.Map(moon.X, moon.Y), v.Size(r), Ink(MoonfallColor.Hex("#FFF6EA"), tSwell), 48);
        if (scene.MoonFront is { } front)
        {
            Image(p.Dl, v, front, scene.Layers.MoonFront!.Board, uint.MaxValue);
        }
    }

    /// <summary>
    /// The veil under each live round peg, at its place this frame (a mover's too), fading with the peg as it clears:
    /// the scene recedes round the layout and nothing is left where a peg has gone (the bricks' veil is baked).
    /// </summary>
    private void RichVeil(in ArtPen p, MoonfallSceneTextures<IDalamudTextureWrap> scene, MoonfallGame g, double alpha)
    {
        var k = scene.Layers.VeilK;
        if (k <= 0 || gameArt?.Veil is not { } veil)
        {
            return;
        }

        var v = p.View;
        var quick = UiMetrics.ReduceMotion || Theme.Flair == Flair.Plain;
        for (var i = 0; i < g.PegCount; i++)
        {
            var peg = g.Peg(i, alpha);
            if (peg.Shape != PegShape.Round)
            {
                continue;
            }

            var fade = 1f;
            if (peg.Cleared)
            {
                var age = artClearedAt is { } at && i < at.Length ? boardClock - at[i] : double.NaN;
                var span = quick ? ClearFadeSeconds : ClearSeconds;
                if (double.IsNaN(age) || age < 0 || age > span)
                {
                    continue;
                }

                fade = 1f - (float)(age / span);
            }

            var reach = (float)(peg.Radius * MoonfallVeil.Reach);
            p.Dl.AddImage(veil.Handle, v.Map(peg.X - reach, peg.Y - reach), v.Map(peg.X + reach, peg.Y + reach), Vector2.Zero, Vector2.One, Ink(Vector3.Zero, k * fade));
        }
    }

    /// <summary>A texture over a board rectangle (x0, y0, x1, y1).</summary>
    private static void Image(ImDrawListPtr dl, in View v, IDalamudTextureWrap texture, Vector4 board, uint tint) =>
        dl.AddImage(texture.Handle, v.Map(board.X, board.Y), v.Map(board.Z, board.W), Vector2.Zero, Vector2.One, tint);

    /// <summary>One glint along the top rail every 6 s (0.8 s; Full only), clipped to the rail.</summary>
    private void TopRailGlint(in ArtPen p)
    {
        var t = MoonfallMotion.Glint(boardClock, 6f, 3f, motion != MoonfallMotionLevel.Full);
        if (t < 0)
        {
            return;
        }

        var v = p.View;
        p.Dl.PushClipRect(v.Map(0, 0), v.Map(MoonfallRules.Width, 41), true);
        var x = -60 + (t * 920);
        var a = 0.30f * MathF.Sin(t * MathF.PI);
        ref readonly var soft = ref p.Atlas[MoonfallSprite.Soft];
        var (uv0, uv1) = p.Atlas.Uv(soft);
        var tint = Ink(MoonfallColor.Hex("#FFF4D0"), a);
        // A slanted soft band (the light from the upper left), 14 units across, the rail's height tall.
        p.Dl.AddImageQuad(p.Sheet, v.Map(x - 7 + 12, 0), v.Map(x + 7 + 12, 0), v.Map(x + 7 - 12, 41), v.Map(x - 7 - 12, 41),
            new Vector2(uv0.X, (uv0.Y + uv1.Y) * 0.5f), new Vector2(uv1.X, (uv0.Y + uv1.Y) * 0.5f), new Vector2(uv1.X, (uv0.Y + uv1.Y) * 0.5f), new Vector2(uv0.X, (uv0.Y + uv1.Y) * 0.5f), tint);
        p.Dl.PopClipRect();
    }

    /// <summary>
    /// The bucket lantern's light on the pegs near it (composite.lantern_spill): a peg within about 34 units takes a faint
    /// warm light on the side of its moon that faces the lantern.
    /// </summary>
    private void LanternSpill(in ArtPen p, MoonfallGame g, double alpha)
    {
        if (g.Fever)
        {
            return;
        }

        var lx = g.BucketXAt(alpha) + 50;
        const double Ly = 541;
        var flick = MoonfallMotion.Flicker(boardClock, 0f, motion == MoonfallMotionLevel.Still);
        ref readonly var soft = ref p.Atlas[MoonfallSprite.Soft];
        for (var i = 0; i < g.PegCount; i++)
        {
            var peg = g.Peg(i, alpha);
            if (peg.Cleared || peg.Shape != PegShape.Round)
            {
                continue;
            }

            var dx = lx - peg.X;
            var dy = Ly - peg.Y;
            var dist = Math.Sqrt((dx * dx) + (dy * dy));
            if (dist > peg.Radius + 34 || dist < 1)
            {
                continue;
            }

            var fall = 1.0 / (1.0 + Math.Pow((dist - peg.Radius) / 14.0, 2));
            var ox = peg.X + (dx / dist * peg.Radius * 0.45);
            var oy = peg.Y + (dy / dist * peg.Radius * 0.45);
            Put(p, soft, ox, oy, (float)(peg.Radius * 0.75 / 4), Ink(MoonfallColor.Hex("#FFB060"), (float)(0.40 * fall * flick)));
        }
    }

    /// <summary>
    /// The colour-blind assist (<see cref="MoonfallPegMarks"/>): a crescent on orange, a leaf on green, a star with its
    /// light rim on purple, engraved on every peg not yet cleared; blue stays plain.
    /// </summary>
    private void PegMarks(in ArtPen p, MoonfallGame g, double alpha)
    {
        if (options is not { PegMarks: true } || gameArt?.Marks is not { } marks)
        {
            return;
        }

        var v = p.View;
        var ink = Theme.WithAlpha(new Vector4(MoonfallPegMarks.Ink.X, MoonfallPegMarks.Ink.Y, MoonfallPegMarks.Ink.Z, 1), MoonfallPegMarks.Ink.W);
        var rim = Theme.WithAlpha(new Vector4(MoonfallPegMarks.RimInk.X, MoonfallPegMarks.RimInk.Y, MoonfallPegMarks.RimInk.Z, 1), MoonfallPegMarks.RimInk.W);
        for (var i = 0; i < g.PegCount; i++)
        {
            var peg = g.Peg(i, alpha);
            if (peg.Cleared || peg.Shape != PegShape.Round)
            {
                continue;
            }

            var mark = MoonfallPegMarks.For(peg.Colour);
            if (mark == MoonfallPegMark.None)
            {
                continue;
            }

            var min = v.Map(peg.X - peg.Radius, peg.Y - peg.Radius);
            var max = v.Map(peg.X + peg.Radius, peg.Y + peg.Radius);
            if (mark == MoonfallPegMark.Star)
            {
                var (r0, r1) = MoonfallPegMarks.Uv(MoonfallPegMark.StarRim);
                p.Dl.AddImage(marks.Handle, min, max, r0, r1, rim);
            }

            var (uv0, uv1) = MoonfallPegMarks.Uv(mark);
            p.Dl.AddImage(marks.Handle, min, max, uv0, uv1, ink);
        }
    }
}
