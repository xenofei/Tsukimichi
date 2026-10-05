using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Tsukimichi.Core.Moonfall;
using Tsukimichi.Core.Moonfall.Art;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// Moonfall's in-play chrome and scene from the game's own art (spec-rich2.md, "rich pass 2"; the approved
/// <c>docs/design/v9/rich2/screens/hud-*</c>): the frame and HUD built from nine UI textures read from the player's
/// install at runtime (<see cref="MoonfallChromeArt"/>), the level's scene built from its recipe
/// (<see cref="MoonfallSceneBuilder"/>), the blurred scene in the margins, the ambient motion (<see cref="MoonfallMotion"/>)
/// and the colour-blind assist (<see cref="MoonfallPegMarks"/>). While the game's art loads, or when it is missing or
/// changed, the board keeps the interim art set's frame and ground (<see cref="MoonfallArtTextures"/>); under Decoration
/// Plain the whole board is the flat inks, as before. Every position is a board unit, so the chrome scales with the
/// board; the text floors hold at the 640 × 480 minimum (<see cref="MoonfallHud"/>). Drawing allocates nothing per frame.
/// </summary>
public sealed partial class MoonfallWindow
{
    // The Medallion's inks (r2lib.C) and the chrome's.
    private static readonly Vector3 Cream = MoonfallColor.Hex("#F4ECD8");
    private static readonly Vector3 GoldHiInk = MoonfallColor.Hex("#FFE3A0");
    private static readonly Vector3 EdgeInk = MoonfallColor.Hex("#05070F");
    private static readonly Vector3 LabelInk = MoonfallColor.Hex("#D8C49A");
    private static readonly Vector3 PlateInk = MoonfallColor.Hex("#0A1030");
    private static readonly Vector3 Abyss = MoonfallColor.Hex("#02030A");
    private static readonly Vector3 Fillet = MoonfallColor.Hex("#03040C");
    private static readonly Vector3 Ink2 = MoonfallColor.Hex("#C3CBEA");

    private MoonfallGameArt<IDalamudTextureWrap>? gameArt;
    private IMoonfallFonts? fonts;
    private IMoonfallOptions? options;

    /// <summary>Whether the rich chrome drew last frame: the bar then leaves out what the chrome shows.</summary>
    private bool richHud;

    /// <summary>The motion level this frame (<see cref="MoonfallMotion.For"/>).</summary>
    private MoonfallMotionLevel motion;

    /// <summary>When the level's scene landed (it fades in over the night sky); NaN until it has.</summary>
    private double sceneShownAt = double.NaN;

    private MoonfallSceneTextures<IDalamudTextureWrap>? sceneFor;

    /// <summary>What the chrome draws with this frame.</summary>
    private readonly record struct ChromePen(ImDrawListPtr Dl, View View, MoonfallChromeSheet Sheet, ImTextureID Tex);

    /// <summary>Text anchors: horizontally left, centre or right; vertically the caps' middle.</summary>
    private enum Anchor : byte
    {
        Left,
        Centre,
        Right,
    }

    private static uint Ink(Vector3 c, float a = 1f) => Theme.WithAlpha(new Vector4(c, 1f), a);

    private static Vector3 Tint(Vector3 c, float k) => Vector3.Lerp(c, MoonfallColor.Hex("#FFF6E8"), k);

    /// <summary>Sets the in-play game art up (never in the load check, which has no Dalamud).</summary>
    private void InitRich(IMoonfallGameArtHost<IDalamudTextureWrap>? host, IMoonfallFonts? fontSource, IMoonfallOptions? settings)
    {
        options = settings;
        fonts = fontSource;
        if (host is null)
        {
            return;
        }

        var errors = new System.Collections.Generic.List<string>();
        var recipes = MoonfallSceneRecipeLoader.LoadBuiltIn(errors);
        foreach (var error in errors)
        {
            log?.Warning($"Moonfall scene recipe not loaded: {error}");
        }

#if DEBUG
        // Debug builds measure the fuller-board rules on every scene they build (the tests do it for the shipped ones).
        gameArt = new MoonfallGameArt<IDalamudTextureWrap>(host, recipes) { CheckScenes = true };
#else
        gameArt = new MoonfallGameArt<IDalamudTextureWrap>(host, recipes);
#endif
    }

    // ---- Text ----

    /// <summary>
    /// Draws <paramref name="text"/> in <paramref name="face"/> at <paramref name="px"/> pixels (the face's cell, as the
    /// design sizes type), its caps' middle on <paramref name="at"/>'s y, with the game's dark edge when
    /// <paramref name="edge"/> is above 0 (its width in pixels); returns its width. The window's font stands in while the
    /// game's fonts build.
    /// </summary>
    private float DrawText(ImDrawListPtr dl, MoonfallFace face, float px, Vector2 at, Anchor anchor, uint colour, ReadOnlySpan<char> text, uint edgeColour = 0, float edge = 0f, float tracking = 0f)
    {
        if (text.IsEmpty || !(px > 0.5f))
        {
            return 0f;
        }

        var (font, size) = FontFor(face, px);
        var width = TextWidth(font, size, text, tracking);
        var x = anchor switch { Anchor.Centre => at.X - (width * 0.5f), Anchor.Right => at.X - width, _ => at.X };
        var y = at.Y - (MoonfallFaceMetrics.CapMiddle(face) * px);
        var pos = new Vector2(MathF.Round(x), MathF.Round(y));
        if (edge > 0f && edgeColour != 0)
        {
            var e = MathF.Max(1f, edge);
            ReadOnlySpan<Vector2> around = [new(-e, 0), new(e, 0), new(0, -e), new(0, e), new(-e * 0.7f, -e * 0.7f), new(e * 0.7f, -e * 0.7f), new(-e * 0.7f, e * 0.7f), new(e * 0.7f, e * 0.7f)];
            foreach (var o in around)
            {
                Glyphs(dl, font, size, pos + o, edgeColour, text, tracking);
            }
        }

        Glyphs(dl, font, size, pos, colour, text, tracking);
        return width;
    }

    /// <summary>The font and its ImGui size for <paramref name="face"/> at a cell of <paramref name="px"/> pixels.</summary>
    private (ImFontPtr Font, float Size) FontFor(MoonfallFace face, float px)
    {
        var size = px * MoonfallFaceMetrics.EmScale(face);
        return fonts is { } f && f.TryGet(face, size, out var gameFont) ? (gameFont, size) : (ImGui.GetFont(), px * 0.8f);
    }

    /// <summary>The width of <paramref name="text"/> in <paramref name="face"/> at a cell of <paramref name="px"/> pixels.</summary>
    private float MeasureText(MoonfallFace face, float px, ReadOnlySpan<char> text, float tracking = 0f)
    {
        var (font, size) = FontFor(face, px);
        return TextWidth(font, size, text, tracking);
    }

    /// <summary>The caps' height share of <paramref name="face"/>'s cell (for the text floors).</summary>
    private static float CapRatio(MoonfallFace face, float px)
    {
        _ = px;
        return MoonfallFaceMetrics.CapHeight(face);
    }

    private static float TextWidth(ImFontPtr font, float px, ReadOnlySpan<char> text, float tracking)
    {
        int remaining;
        if (tracking == 0f)
        {
            return ImGui.CalcTextSizeA(font, px, float.MaxValue, 0f, text, out remaining).X;
        }

        var w = 0f;
        for (var i = 0; i < text.Length; i++)
        {
            w += ImGui.CalcTextSizeA(font, px, float.MaxValue, 0f, text.Slice(i, 1), out remaining).X + (i + 1 < text.Length ? tracking : 0f);
        }

        return w;
    }

    private static void Glyphs(ImDrawListPtr dl, ImFontPtr font, float px, Vector2 pos, uint colour, ReadOnlySpan<char> text, float tracking)
    {
        if (tracking == 0f)
        {
            dl.AddText(font, px, pos, colour, text);
            return;
        }

        int remaining;
        for (var i = 0; i < text.Length; i++)
        {
            var one = text.Slice(i, 1);
            dl.AddText(font, px, pos, colour, one);
            pos.X += ImGui.CalcTextSizeA(font, px, float.MaxValue, 0f, one, out remaining).X + tracking;
        }
    }

    // ---- Parts of the chrome sheet ----

    /// <summary>
    /// A part (or its sub-rectangle <paramref name="u0"/>..<paramref name="u1"/>, <paramref name="v0"/>..<paramref name="v1"/>
    /// in its own hr pixels) over a board rectangle, mirrored when asked.
    /// </summary>
    private static void Part(in ChromePen c, MoonfallChromePart part, double x0, double y0, double x1, double y1, uint tint, bool flipX = false, bool flipY = false,
        float u0 = 0, float v0 = 0, float u1 = -1, float v1 = -1)
    {
        if (c.Sheet[part] is null)
        {
            return;
        }

        var (uvA, uvB) = c.Sheet.Uv(part, u0, v0, u1, v1);
        if (flipX)
        {
            (uvA.X, uvB.X) = (uvB.X, uvA.X);
        }

        if (flipY)
        {
            (uvA.Y, uvB.Y) = (uvB.Y, uvA.Y);
        }

        c.Dl.AddImage(c.Tex, c.View.Map(x0, y0), c.View.Map(x1, y1), uvA, uvB, tint);
    }

    /// <summary>One of Lord of Verminion's rings scaled so its hole is <paramref name="hole"/> units, centred on (cx, cy); returns its outer radius.</summary>
    private static float GiltRing(in ChromePen c, double cx, double cy, float hole, bool great = false, uint tint = uint.MaxValue)
    {
        var part = great ? MoonfallChromePart.RingGreat : MoonfallChromePart.Ring;
        var g = great ? MoonfallChromeArt.RingGreatGeometry : MoonfallChromeArt.RingGeometry;
        if (c.Sheet[part] is not { } r)
        {
            return hole;
        }

        var s = hole / g.Z;
        Part(c, part, cx - (g.X * s), cy - (g.Y * s), cx - (g.X * s) + (r.W * s), cy - (g.Y * s) + (r.H * s), tint);
        return g.W * s;
    }

    /// <summary>r2kit.pill: the Gold Saucer's pill, its body filling the rectangle (units) and its shadow spilling below.</summary>
    private static void Pill(in ChromePen c, double x0, double y0, double x1, double y1, uint tint = uint.MaxValue)
    {
        if (c.Sheet[MoonfallChromePart.Pill] is not { } r)
        {
            c.Dl.AddRectFilled(c.View.Map(x0, y0), c.View.Map(x1, y1), Ink(MoonfallColor.Hex("#1A2A66")), c.View.Size((y1 - y0) / 2));
            return;
        }

        var s = (y1 - y0) / 58.0;
        const float Cap = 44;
        var ox = 14 * s;
        var oy = 18 * s;
        var top = y0 - oy;
        var foot = top + (r.H * s);
        var left = x0 - ox;
        var right = x1 + ox;
        Part(c, MoonfallChromePart.Pill, left, top, left + (Cap * s), foot, tint, u0: 0, u1: Cap);
        Part(c, MoonfallChromePart.Pill, left + (Cap * s), top, right - (Cap * s), foot, tint, u0: Cap + 0.5f, u1: Cap + 1.5f);
        Part(c, MoonfallChromePart.Pill, right - (Cap * s), top, right, foot, tint, u0: r.W - Cap, u1: r.W);
    }

    // ---- The frame: rails, walls, the outer frame ----

    /// <summary>
    /// chrome2.rails: the enamel rails round the opening in the level's palette (the journal ground's grain), the inner
    /// shade along the top and left walls, the dark reveal inside every wall and the fillet between wall and gilt, the
    /// journal's gilt band round the opening (wholly outside it) and the outer frame with its vine and reed corners.
    /// </summary>
    private void RichFrame(in ChromePen c, ImTextureID? enamel, int enamelW, int enamelH, MoonfallChromePalette palette)
    {
        var dl = c.Dl;
        var v = c.View;
        const double L = MoonfallFramingCheck.WallL, R = MoonfallFramingCheck.WallR, T = MoonfallFramingCheck.Top, F = MoonfallFramingCheck.Foot;
        ReadOnlySpan<(double X0, double Y0, double X1, double Y1)> rails =
        [
            (0, 0, MoonfallRules.Width, T),
            (0, T, L, MoonfallRules.Height),
            (R, T, MoonfallRules.Width, MoonfallRules.Height),
            (L, F, R, MoonfallRules.Height),
        ];
        foreach (var (x0, y0, x1, y1) in rails)
        {
            var min = v.Map(x0, y0);
            var max = v.Map(x1, y1);
            if (enamel is { } tex && enamelW > 0)
            {
                // The grain tile repeated over the rail, its own board units a pixel.
                dl.AddImage(tex, min, max, new Vector2((float)(x0 / enamelW), (float)(y0 / enamelH)), new Vector2((float)(x1 / enamelW), (float)(y1 / enamelH)));
            }
            else
            {
                dl.AddRectFilled(min, max, Ink(palette.Sky));
            }

            // The light from above: 1.12 at the top to 0.82 at the foot (the tile is baked at 1.12).
            var top = Ink(Vector3.Zero, (float)(0.268 * (y0 / MoonfallRules.Height)));
            var foot = Ink(Vector3.Zero, (float)(0.268 * (y1 / MoonfallRules.Height)));
            dl.AddRectFilledMultiColor(min, max, top, top, foot, foot);
            // The jewel's glow in the lower right and the sheen toward the light.
            var jewel = Ink(palette.Jewel2, 0f);
            var lit = Ink(palette.Jewel2, (float)(0.10 * (x1 / MoonfallRules.Width) * (y1 / MoonfallRules.Height)));
            dl.AddRectFilledMultiColor(min, max, jewel, jewel, lit, jewel);
            var sheen = Ink(MoonfallColor.Hex("#9FB2E8"), (float)(0.08 * (1 - (x0 / MoonfallRules.Width)) * (1 - (y0 / MoonfallRules.Height))));
            var none = Ink(MoonfallColor.Hex("#9FB2E8"), 0f);
            dl.AddRectFilledMultiColor(min, max, sheen, none, none, none);
        }

        // The rails stand proud of the scene: a shade along the top and the left inside the opening.
        var dark = Ink(Abyss, 0.55f);
        var clear = Ink(Abyss, 0f);
        dl.AddRectFilledMultiColor(v.Map(L, T), v.Map(R, T + 10), dark, dark, clear, clear);
        dl.AddRectFilledMultiColor(v.Map(L, T), v.Map(L + 10, F), dark, clear, clear, dark);
        // The dark reveal inside every wall (2.5 units) and the fillet outside it (4 units), so a peg at a wall sits on dark.
        var reveal = Ink(Abyss, 0.7f);
        var revealClear = Ink(Abyss, 0f);
        dl.AddRectFilledMultiColor(v.Map(L, T), v.Map(L + 2.5, F), reveal, revealClear, revealClear, reveal);
        dl.AddRectFilledMultiColor(v.Map(R - 2.5, T), v.Map(R, F), revealClear, reveal, reveal, revealClear);
        dl.AddRectFilledMultiColor(v.Map(L, T), v.Map(R, T + 2.5), reveal, reveal, revealClear, revealClear);
        var fillet = Ink(Fillet, 0.8f);
        dl.AddRectFilled(v.Map(L - 4.2, T - 4.2), v.Map(L, F), fillet);
        dl.AddRectFilled(v.Map(R, T - 4.2), v.Map(R + 4.2, F), fillet);
        dl.AddRectFilled(v.Map(L, T - 4.2), v.Map(R, T), fillet);

        // The walls: the journal's gilt band round the opening, mitred, its inner edge at the fillet.
        GiltBand(c, L - 17, T - 17, R + 17, F + 30, 0.46);
        // The outer frame: the journal's frame with its corners, in the rails.
        GiltFrame(c, 0, 0, MoonfallRules.Width, MoonfallRules.Height, 0.40);
        // The rails' shelf: the journal's short rule across each side rail, under the instruments.
        if (c.Sheet[MoonfallChromePart.ShortRule] is { } rule)
        {
            foreach (var x0 in (ReadOnlySpan<double>)[MoonfallHud.LeftRail - (MoonfallHud.RailWidth / 2), MoonfallHud.RightRail - (MoonfallHud.RailWidth / 2)])
            {
                Part(c, MoonfallChromePart.ShortRule, x0, MoonfallHud.Shelf, x0 + MoonfallHud.RailWidth, MoonfallHud.Shelf + (rule.H * 0.40), uint.MaxValue, u0: 20, u1: 60);
            }
        }
    }

    /// <summary>
    /// r2lib.gilt_band: the frame's gilt triple rule round a box, its cross-section (one column of the rule) laid along
    /// each side and turned down the sides, joined by 45° mitres, so there are no corner blocks or seams.
    /// </summary>
    private static void GiltBand(in ChromePen c, double x0, double y0, double x1, double y1, double scale)
    {
        if (c.Sheet[MoonfallChromePart.RuleTop] is not { } r)
        {
            return;
        }

        var o = 6 * scale;
        var h = r.H * scale;
        double X0 = x0 - o, Y0 = y0 - o, X1 = x1 + o, Y1 = y1 + o;
        var (a, b) = c.Sheet.Uv(MoonfallChromePart.RuleTop, 19.5f, 0, 20.5f, r.H);
        var u = (a.X + b.X) * 0.5f;
        var outer = new Vector2(u, a.Y);
        var inner = new Vector2(u, b.Y);
        var v = c.View;
        var dl = c.Dl;
        // Top, bottom (its outer edge below), left and right (outer edges outward); each a trapezoid to the mitres.
        dl.AddImageQuad(c.Tex, v.Map(X0, Y0), v.Map(X1, Y0), v.Map(X1 - h, Y0 + h), v.Map(X0 + h, Y0 + h), outer, outer, inner, inner);
        dl.AddImageQuad(c.Tex, v.Map(X0 + h, Y1 - h), v.Map(X1 - h, Y1 - h), v.Map(X1, Y1), v.Map(X0, Y1), inner, inner, outer, outer);
        dl.AddImageQuad(c.Tex, v.Map(X0, Y0), v.Map(X0 + h, Y0 + h), v.Map(X0 + h, Y1 - h), v.Map(X0, Y1), outer, inner, inner, outer);
        dl.AddImageQuad(c.Tex, v.Map(X1 - h, Y0 + h), v.Map(X1, Y0), v.Map(X1, Y1), v.Map(X1 - h, Y1 - h), inner, outer, outer, inner);
    }

    /// <summary>r2lib.gilt_frame: the journal's frame round a box (units), its rules stretched along the sides and its corner art at the corners.</summary>
    private static void GiltFrame(in ChromePen c, double x0, double y0, double x1, double y1, double scale, uint tint = uint.MaxValue)
    {
        if (c.Sheet[MoonfallChromePart.CornerTop] is not { } tl || c.Sheet[MoonfallChromePart.CornerBottom] is not { } bl
            || c.Sheet[MoonfallChromePart.RuleTop] is not { } hr || c.Sheet[MoonfallChromePart.RuleBottom] is not { } hb || c.Sheet[MoonfallChromePart.RuleSide] is not { } vr)
        {
            return;
        }

        var o = 6 * scale;
        double w = x1 - x0, h = y1 - y0;
        double cw = tl.W * scale, ch = tl.H * scale, bw = bl.W * scale, bh = bl.H * scale;
        var topLen = w - (2 * cw) + (2 * o) + 2;
        if (topLen > 0)
        {
            Part(c, MoonfallChromePart.RuleTop, x0 - o + cw - 1, y0 - o, x0 - o + cw - 1 + topLen, y0 - o + (hr.H * scale), tint);
        }

        var botLen = w - (2 * bw) + (2 * o) + 2;
        if (botLen > 0)
        {
            var by = y1 + o - (hb.H * scale) + (2 * scale);
            Part(c, MoonfallChromePart.RuleBottom, x0 - o + bw - 1, by, x0 - o + bw - 1 + botLen, by + (hb.H * scale), tint);
        }

        var sideLen = h - ch - bh + (2 * o) + 2;
        if (sideLen > 0)
        {
            var sy = y0 - o + ch - 1;
            Part(c, MoonfallChromePart.RuleSide, x0 - o, sy, x0 - o + (vr.W * scale), sy + sideLen, tint);
            Part(c, MoonfallChromePart.RuleSide, x1 + o - (vr.W * scale), sy, x1 + o, sy + sideLen, tint, flipX: true);
        }

        Part(c, MoonfallChromePart.CornerTop, x0 - o, y0 - o, x0 - o + cw, y0 - o + ch, tint);
        Part(c, MoonfallChromePart.CornerTop, x1 + o - cw, y0 - o, x1 + o, y0 - o + ch, tint, flipX: true);
        Part(c, MoonfallChromePart.CornerBottom, x0 - o, y1 + o - bh, x0 - o + bw, y1 + o, tint);
        Part(c, MoonfallChromePart.CornerBottom, x1 + o - bw, y1 + o - bh, x1 + o, y1 + o, tint, flipX: true);
    }

    /// <summary>chrome2.crest: the PvP emblem's gilt wings either side of the launcher's yoke, under a moonstone in a ring.</summary>
    private void RichCrest(in ChromePen c, in ArtPen p)
    {
        if (c.Sheet[MoonfallChromePart.Wing] is not { } wing)
        {
            return;
        }

        const double W = 66;
        var h = W * wing.H / wing.W;
        Part(c, MoonfallChromePart.Wing, 400 - 6 - W, 2, 400 - 6, 2 + h, uint.MaxValue);
        Part(c, MoonfallChromePart.Wing, 406, 2, 406 + W, 2 + h, uint.MaxValue, flipX: true);
        c.Dl.AddCircleFilled(c.View.Map(400, 20), c.View.Size(9.5), Ink(PlateInk), 32);
        Moonstone(c.Dl, c.View, 400, 20, 6);
        GiltRing(c, 400, 20, 7.6f);
        _ = p;
    }

    /// <summary>A small moonstone: a pale disc lit from the upper left with a soft cool edge.</summary>
    private static void Moonstone(ImDrawListPtr dl, in View v, double x, double y, double r)
    {
        dl.AddCircleFilled(v.Map(x, y), v.Size(r), Ink(MoonfallColor.Hex("#8E97BC")), 32);
        dl.AddCircleFilled(v.Map(x - (r * 0.15), y - (r * 0.15)), v.Size(r * 0.82), Ink(MoonfallColor.Hex("#C9CFE6")), 32);
        dl.AddCircleFilled(v.Map(x - (r * 0.3), y - (r * 0.3)), v.Size(r * 0.45), Ink(MoonfallColor.Hex("#E9EDF6")), 24);
    }

    // ---- The HUD's instruments ----

    private string stageText = string.Empty;
    private int stageFor = -1;
    private string countText = string.Empty;
    private int countFor = -1;
    private string orangesCount = string.Empty;
    private int orangesCountFor = -1;
    private string multiplierDigits = string.Empty;
    private int multiplierDigitsFor = -1;

    private void RefreshHudText(MoonfallGame g)
    {
        if (stageFor != levelIndex)
        {
            stageFor = levelIndex;
            stageText = string.Create(CultureInfo.InvariantCulture, $"{MoonfallCharacters.Stage(levelIndex)}-{MoonfallCharacters.LevelInStage(levelIndex)}");
        }

        if (countFor != g.BallsLeft)
        {
            countFor = g.BallsLeft;
            countText = g.BallsLeft.ToString(CultureInfo.CurrentCulture);
        }

        if (orangesCountFor != g.OrangesLeft)
        {
            orangesCountFor = g.OrangesLeft;
            orangesCount = g.OrangesLeft.ToString(CultureInfo.CurrentCulture);
        }

        if (multiplierDigitsFor != g.Multiplier)
        {
            multiplierDigitsFor = g.Multiplier;
            multiplierDigits = g.Multiplier.ToString(CultureInfo.CurrentCulture);
        }
    }

    /// <summary>A label's size in pixels at its design size in units, 0 when it cannot meet the label floor (left out, spec §1).</summary>
    private float LabelPx(in View v, float units, MoonfallFace face)
    {
        var px = v.Size(units);
        return MoonfallHud.LabelSize(units, px / units, CapRatio(face, px));
    }

    /// <summary>A number's size in pixels: its design size, raised to the number floor when it falls short.</summary>
    private float NumberPx(in View v, float units, MoonfallFace face)
    {
        var px = v.Size(units);
        return MoonfallHud.NumberSize(units, px / units, CapRatio(face, px));
    }

    /// <summary>A name's size in pixels (it must show): its design size, raised to the label floor when it falls short.</summary>
    private float NamePx(in View v, float units, MoonfallFace face)
    {
        var px = v.Size(units);
        return MoonfallHud.RequiredLabelSize(units, px / units, CapRatio(face, px));
    }

    /// <summary>
    /// chrome2's instruments: the level's name plate, the score plate, the ball tube with its count, the multiplier
    /// dial, the oranges left, and the companion's medallion with the power's name and the turns-left gems.
    /// </summary>
    private void RichHud(in ChromePen c, in ArtPen p, MoonfallGame g)
    {
        RefreshHudText(g);
        var dl = c.Dl;
        var v = c.View;
        var still = motion == MoonfallMotionLevel.Still;

        // The level's name plate.
        var (nx0, ny0, nx1, ny1) = MoonfallHud.NamePlate;
        Pill(c, nx0, ny0, nx1, ny1);
        dl.AddCircleFilled(v.Map(103, 21), v.Size(10), Ink(PlateInk), 32);
        GiltRing(c, 103, 21, 10);
        DrawText(dl, MoonfallFace.Trump, NumberPx(v, 16.5f, MoonfallFace.Trump), v.Map(103, 21.5), Anchor.Centre, Ink(GoldHiInk), stageText, Ink(EdgeInk), v.Size(0.6));
        var nameMax = v.Size(nx1 - 124 - 6);
        var namePx = NamePx(v, 27, MoonfallFace.Jupiter);
        var levelName = campaigns[campaign].Levels[levelIndex].Name;
        var w = MeasureText(MoonfallFace.Jupiter, namePx, levelName);
        if (w > nameMax && w > 0)
        {
            namePx *= nameMax / w;
        }

        DrawText(dl, MoonfallFace.Jupiter, namePx, v.Map(124, 21), Anchor.Left, Ink(Cream), levelName, Ink(EdgeInk), v.Size(0.8));

        // The score plate: a recess in the pill, the score in TrumpGothic gold.
        var (sx0, sy0, sx1, sy1) = MoonfallHud.ScorePlate;
        Pill(c, sx0, sy0, sx1, sy1);
        dl.AddRectFilled(v.Map(598, 12.5), v.Map(706, 29.5), Ink(MoonfallColor.Hex("#060A1E")), v.Size(8.5));
        dl.AddRectFilledMultiColor(v.Map(606, 12.5), v.Map(698, 15.5), Ink(Vector3.Zero, 0.6f), Ink(Vector3.Zero, 0.6f), Ink(Vector3.Zero, 0f), Ink(Vector3.Zero, 0f));
        var scoreLabel = LabelPx(v, 11.5f, MoonfallFace.Axis);
        if (scoreLabel > 0)
        {
            DrawText(dl, MoonfallFace.Axis, scoreLabel, v.Map(604, 21), Anchor.Left, Ink(LabelInk), Strings.MoonfallHudScore);
        }

        DrawText(dl, MoonfallFace.Trump, NumberPx(v, 26, MoonfallFace.Trump), v.Map(701, 21.5), Anchor.Right, Ink(GoldHiInk), scoreText, Ink(MoonfallColor.Hex("#120A02")), v.Size(0.8));

        BallTube(c, p, g);
        Dial(c, g);
        // The oranges left: an orange moon and its count.
        Put(p, p.Atlas.Peg(PegColour.Orange, 1, false), MoonfallHud.RightRail - 12, MoonfallHud.OrangesY, 0.75f, uint.MaxValue);
        DrawText(dl, MoonfallFace.Trump, NumberPx(v, 24, MoonfallFace.Trump), v.Map(MoonfallHud.RightRail - 2, MoonfallHud.OrangesY + 0.5), Anchor.Left, Ink(Cream), orangesCount, Ink(EdgeInk), v.Size(0.6));
        var orangesLabel = LabelPx(v, 11, MoonfallFace.Axis);
        if (orangesLabel > 0)
        {
            DrawText(dl, MoonfallFace.Axis, orangesLabel, v.Map(MoonfallHud.RightRail, 170), Anchor.Centre, Ink(LabelInk), Strings.MoonfallHudOranges, Ink(EdgeInk), v.Size(0.6));
        }

        Medallion(c, p, g, still);
    }

    /// <summary>chrome2.ball_tube: the glass tube and its balls in a cage of the journal's rules, spire finials, and the count.</summary>
    private void BallTube(in ChromePen c, in ArtPen p, MoonfallGame g)
    {
        var dl = c.Dl;
        var v = c.View;
        const float Cx = MoonfallHud.LeftRail;
        const float X0 = Cx - MoonfallHud.TubeHalf, X1 = Cx + MoonfallHud.TubeHalf, Y0 = MoonfallHud.TubeTop, Y1 = MoonfallHud.TubeFoot;
        dl.AddRectFilled(v.Map(X0, Y0), v.Map(X1, Y1), Ink(MoonfallColor.Hex("#03040C"), 0.6f), v.Size(12));
        ref readonly var ball = ref p.Atlas[MoonfallSprite.Ball];
        var balls = Math.Min(g.BallsLeft, 10);
        for (var i = 0; i < balls; i++)
        {
            Put(p, ball, Cx, Y1 - 13 - (i * 25.5), 10.5f / 6f, uint.MaxValue);
        }

        // The glass: a bright stripe on the lit side, a cool one on the shade side, and its edges.
        dl.AddRectFilled(v.Map(X0 + (26 * 0.19), Y0 + 6), v.Map(X0 + (26 * 0.23), Y1 - 6), Ink(MoonfallColor.Hex("#F4F2EA"), 0.35f), v.Size(1));
        dl.AddRectFilled(v.Map(X0 + (26 * 0.84), Y0 + 8), v.Map(X0 + (26 * 0.88), Y1 - 8), Ink(MoonfallColor.Hex("#8FA4DA"), 0.18f), v.Size(1));
        dl.AddRect(v.Map(X0, Y0), v.Map(X1, Y1), Ink(MoonfallColor.Hex("#C3CEE4"), 0.25f), v.Size(12), ImDrawFlags.None, MathF.Max(1f, v.Size(0.8)));
        if (c.Sheet[MoonfallChromePart.TubeRule] is { } rule)
        {
            Part(c, MoonfallChromePart.TubeRule, X0 - 7.5, Y0 - 2, X0 + 2, Y1 + 2, uint.MaxValue, v0: 6, v1: rule.H - 6);
            Part(c, MoonfallChromePart.TubeRule, X1 - 2, Y0 - 2, X1 + 7.5, Y1 + 2, uint.MaxValue, flipX: true, v0: 6, v1: rule.H - 6);
        }

        if (c.Sheet[MoonfallChromePart.Spire] is { } spire)
        {
            const double Sw = 15;
            var sh = Sw * spire.H / spire.W * 0.42;
            var cut = spire.H * 0.42f;
            Part(c, MoonfallChromePart.Spire, Cx - (Sw / 2), Y0 - sh + 4, Cx + (Sw / 2), Y0 + 4, uint.MaxValue, v1: cut);
            Part(c, MoonfallChromePart.Spire, Cx - (Sw / 2), Y1 - 4, Cx + (Sw / 2), Y1 - 4 + sh, uint.MaxValue, flipY: true, v1: cut);
        }

        Pill(c, Cx - 20, MoonfallHud.CountTop, Cx + 20, MoonfallHud.CountFoot);
        DrawText(dl, MoonfallFace.Trump, NumberPx(v, 24, MoonfallFace.Trump), v.Map(Cx, 383.5), Anchor.Centre, Ink(Cream), countText, Ink(EdgeInk), v.Size(0.6));
        var label = LabelPx(v, 11.5f, MoonfallFace.Axis);
        if (label > 0)
        {
            DrawText(dl, MoonfallFace.Axis, label, v.Map(Cx, 408), Anchor.Centre, Ink(LabelInk), Strings.MoonfallHudBalls, Ink(EdgeInk), v.Size(0.6));
        }
    }

    /// <summary>chrome2.mult_dial: an enamel face, the oranges-cleared arc in gold with the multiplier's notches, the great lattice ring, and "×" in AXIS with the digits in TrumpGothic.</summary>
    private void Dial(in ChromePen c, MoonfallGame g)
    {
        var dl = c.Dl;
        var v = c.View;
        const float Cx = MoonfallHud.RightRail, Cy = MoonfallHud.DialY, R = MoonfallHud.DialR;
        var centre = v.Map(Cx, Cy);
        dl.AddCircleFilled(centre, v.Size(R), Ink(MoonfallColor.Hex("#070B22")), 40);
        dl.AddCircleFilled(centre, v.Size(R * 0.86), Ink(MoonfallColor.Hex("#111A44")), 40);
        dl.AddCircleFilled(centre, v.Size(R * 0.45), Ink(MoonfallColor.Hex("#1C2A66")), 32);
        dl.AddCircle(centre, v.Size(R - 2.4), Ink(MoonfallColor.Hex("#04060F"), 0.8f), 40, v.Size(3.2));
        var cleared = MoonfallRules.OrangeCount - g.OrangesLeft;
        var frac = Math.Clamp(cleared / (float)MoonfallRules.OrangeCount, 0f, 1f);
        if (frac > 0)
        {
            // The arc from the top, clockwise, gold turning amber, in short pieces.
            const int Pieces = 24;
            var a0 = -MathF.PI / 2;
            for (var k = 0; k < Pieces; k++)
            {
                var t0 = frac * k / Pieces;
                var t1 = frac * (k + 1) / Pieces;
                var col = Vector3.Lerp(MoonfallColor.Hex("#FFCF6A"), MoonfallColor.Hex("#FF8A3A"), (t0 + t1) * 0.5f);
                dl.PathArcTo(centre, v.Size(R - 2.4), a0 + (t0 * 2 * MathF.PI), a0 + (t1 * 2 * MathF.PI), 3);
                dl.PathStroke(Ink(col), ImDrawFlags.None, v.Size(2.6));
            }
        }

        foreach (var left in (ReadOnlySpan<int>)[15, 10, 6, 3])
        {
            var a = ((MoonfallRules.OrangeCount - left) / (float)MoonfallRules.OrangeCount * 2 * MathF.PI) - (MathF.PI / 2);
            dl.AddCircleFilled(v.Map(Cx + (MathF.Cos(a) * (R - 2.4f)), Cy + (MathF.Sin(a) * (R - 2.4f))), v.Size(0.8), Ink(MoonfallColor.Hex("#FFF0C0")), 8);
        }

        GiltRing(c, Cx, Cy, R + 0.6f, great: true);
        var digitsPx = NumberPx(v, 18, MoonfallFace.Trump);
        var timesPx = NumberPx(v, 15, MoonfallFace.Axis);
        var wd = MeasureText(MoonfallFace.Trump, digitsPx, multiplierDigits);
        var wx = MeasureText(MoonfallFace.Axis, timesPx, "×");
        var x0 = centre.X - ((wd + wx) * 0.5f);
        var glow = g.Multiplier > 1 ? Ink(MoonfallColor.Hex("#FFB45E"), 0.25f) : 0u;
        if (glow != 0)
        {
            dl.AddCircleFilled(centre, v.Size(R * 0.6), glow, 24);
        }

        DrawText(dl, MoonfallFace.Axis, timesPx, new Vector2(x0, centre.Y + v.Size(1)), Anchor.Left, Ink(Cream), "×", Ink(EdgeInk), v.Size(0.8));
        DrawText(dl, MoonfallFace.Trump, digitsPx, new Vector2(x0 + wx, centre.Y + v.Size(1)), Anchor.Left, Ink(Cream), multiplierDigits, Ink(EdgeInk), v.Size(0.8));
    }

    private MoonfallPower medallionFor = MoonfallPower.None;
    private string[] powerNameLines = [];
    private float powerNameSize;
    private int powerNameLanguage = -1;
    private float powerNameScale;
    private bool powerNameFonts;

    /// <summary>
    /// chrome2.power_medallion: the companion's face (the card's face crop) in a lattice ring, glowing in their colour while
    /// the power is active (breathing ±10% over 3 s); the power's name on a dark plate in their colour, on one line or
    /// two (left out where it cannot meet the label floor); and one gem for each turn the power lasts, lit for those left.
    /// </summary>
    private void Medallion(in ChromePen c, in ArtPen p, MoonfallGame g, bool still)
    {
        if (MoonfallCards.For(g.Power) is not { } companion)
        {
            return;
        }

        var dl = c.Dl;
        var v = c.View;
        const float Mx = MoonfallHud.RightRail, My = MoonfallHud.MedallionY, Mr = MoonfallHud.MedallionR;
        var left = g.PowerShotsLeft(g.Power);
        var active = g.PowerActive(g.Power) || left > 0;
        var firing = boardClock - powerFiredAt is >= 0 and < PowerMomentSeconds;
        if (active || firing)
        {
            // The glow round the portrait: breathing ±10% over 3 s; pulsing at once while the power fires on a small window.
            var breath = MoonfallMotion.Breath(boardClock, 3f, 0.10f, still || motion != MoonfallMotionLevel.Full);
            var pulse = firing && !cardInMargin ? 1f + (0.6f * MathF.Max(0f, MathF.Sin((float)(boardClock - powerFiredAt) * 6f))) : 1f;
            Put(p, p.Atlas[MoonfallSprite.Soft], Mx, My, Mr * 1.9f / 4f, Ink(companion.Accent, Math.Clamp(0.42f * breath * pulse, 0f, 1f)));
        }

        dl.AddCircleFilled(v.Map(Mx, My), v.Size(Mr * 1.04), Ink(PlateInk), 32);
        if (gameArt?.Card(g.Power) is { } card)
        {
            var (fx, fy, fs) = companion.Face;
            var uv0 = new Vector2(fx / (float)MoonfallCards.CardWidth, fy / (float)MoonfallCards.CardHeight);
            var uv1 = new Vector2((fx + fs) / (float)MoonfallCards.CardWidth, (fy + fs) / (float)MoonfallCards.CardHeight);
            var r = Mr * 1.04f;
            dl.AddImageRounded(card.Handle, v.Map(Mx - r, My - r), v.Map(Mx + r, My + r), uv0, uv1, uint.MaxValue, v.Size(r));
        }

        GiltRing(c, Mx, My, Mr);

        // The power's name: one line if it fits the rail less 6 units a side, else two, at the largest size from 18 down to 11.
        var fontsReady = fonts is { } f && f.TryGet(MoonfallFace.Jupiter, 20f, out _);
        if (medallionFor != g.Power || powerNameLanguage != Localization.Loc.Version || powerNameScale != v.Scale || powerNameFonts != fontsReady)
        {
            medallionFor = g.Power;
            powerNameLanguage = Localization.Loc.Version;
            powerNameScale = v.Scale;
            powerNameFonts = fontsReady;
            (powerNameLines, powerNameSize) = FitName(PowerUpper(g.Power), MoonfallHud.RailWidth - 6, v);
        }

        var y = MoonfallHud.PowerNameY;
        var namePx = powerNameLines.Length > 0 ? v.Size(powerNameSize) : 0f;
        if (namePx > 0 && namePx * CapRatio(MoonfallFace.Jupiter, namePx) + 1e-3f >= MoonfallHud.LabelFloor)
        {
            var hh = (12.5f * powerNameLines.Length) + 5;
            dl.AddRectFilled(v.Map(Mx - (MoonfallHud.RailWidth / 2), y - 8), v.Map(Mx + (MoonfallHud.RailWidth / 2), y - 8 + hh), Ink(MoonfallColor.Hex("#060816"), 0.85f), v.Size(4));
            foreach (var line in powerNameLines)
            {
                DrawText(dl, MoonfallFace.Jupiter, namePx, v.Map(Mx, y), Anchor.Centre, Ink(Tint(companion.Accent, 0.15f)), line, Ink(EdgeInk), v.Size(0.6));
                y += 12.5f;
            }
        }
        else
        {
            y = 262;
        }

        Gems(c, Mx, y + 8, MoonfallHud.RailWidth - 2, companion.Turns, Math.Min(left, companion.Turns), companion.Accent, firing && !cardInMargin);
    }

    /// <summary>chrome2.fit_name: the name on one line, else two at a word or hyphen, at the largest size from 18 down to 11 units that fits.</summary>
    private (string[] Lines, float Size) FitName(string name, float avail, in View v)
    {
        var words = name.Replace("-", "- ", StringComparison.Ordinal).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var size = 18f; size >= 11f; size -= 0.5f)
        {
            var px = v.Size(size);
            var room = v.Size(avail);
            if (MeasureText(MoonfallFace.Jupiter, px, name) <= room)
            {
                return ([name], size);
            }

            for (var k = 1; k < words.Length; k++)
            {
                var a = string.Join(' ', words[..k]).Replace("- ", "-", StringComparison.Ordinal);
                var b = string.Join(' ', words[k..]).Replace("- ", "-", StringComparison.Ordinal);
                if (MeasureText(MoonfallFace.Jupiter, px, a) <= room && MeasureText(MoonfallFace.Jupiter, px, b) <= room)
                {
                    return ([a, b], size);
                }
            }
        }

        return ([], 0f);
    }

    /// <summary>
    /// r2kit.gems_n: the turns left, one lozenge gem for each turn the power lasts (the scholar gauge's own setting for
    /// three; a slim gilt bar otherwise), lit in the companion's colour for the turns left.
    /// </summary>
    private void Gems(in ChromePen c, float cx, float cy, float w, int total, int lit, Vector3 accent, bool pulse)
    {
        var dl = c.Dl;
        var v = c.View;
        if (total == 3 && c.Sheet[MoonfallChromePart.Gems] is { } frame)
        {
            var gw = MathF.Min(w, 46);
            var gh = gw * frame.H / frame.W;
            Part(c, MoonfallChromePart.Gems, cx - (gw / 2), cy - (gh / 2), cx + (gw / 2), cy + (gh / 2), uint.MaxValue);
            ReadOnlySpan<float> windows = [0.215f, 0.5f, 0.785f];
            for (var i = 0; i < 3; i++)
            {
                Gem(dl, v, cx - (gw / 2) + (windows[i] * gw), cy - (gh / 2) + (0.47f * gh), gh * 0.22f, i < lit, accent, pulse);
            }

            return;
        }

        var g = MathF.Min(10f, (w - 6) / Math.Max(total, 1));
        var x0 = cx - (g * total / 2);
        dl.AddRectFilled(v.Map(x0 - 3, cy - 6.5), v.Map(x0 + (g * total) + 3, cy + 6.5), Ink(MoonfallColor.Hex("#C9A15A")), v.Size(6.5));
        dl.AddRectFilled(v.Map(x0 - 2, cy - 5.5), v.Map(x0 + (g * total) + 2, cy + 5.5), Ink(MoonfallColor.Hex("#070A1C"), 0.95f), v.Size(5.5));
        for (var i = 0; i < total; i++)
        {
            Gem(dl, v, x0 + (g * (i + 0.5f)), cy, MathF.Min(g * 0.45f, 4.6f), i < lit, accent, pulse);
        }
    }

    /// <summary>A lozenge gem: lit, white at its top through the accent to deep blue, with a glow; unlit, dark glass.</summary>
    private static void Gem(ImDrawListPtr dl, in View v, float x, float y, float r, bool lit, Vector3 accent, bool pulse)
    {
        var top = v.Map(x, y - r);
        var right = v.Map(x + r, y);
        var foot = v.Map(x, y + r);
        var left = v.Map(x - r, y);
        uint cTop, cMid, cFoot;
        if (lit)
        {
            dl.AddCircleFilled(v.Map(x, y), v.Size(r * (pulse ? 2.2f : 1.6f)), Ink(accent, pulse ? 0.45f : 0.30f), 16);
            // Lit in the companion's own colour: a pale tint of it at the top, never plain white.
            (cTop, cMid, cFoot) = (Ink(Tint(accent, 0.45f)), Ink(accent), Ink(MoonfallColor.Hex("#1A2A6A")));
        }
        else
        {
            (cTop, cMid, cFoot) = (Ink(MoonfallColor.Hex("#2A3260")), Ink(MoonfallColor.Hex("#1A2048")), Ink(MoonfallColor.Hex("#0A0E24")));
        }

        dl.PrimReserve(6, 4);
        var first = dl.VtxCurrentIdx;
        var white = ImGui.GetFontTexUvWhitePixel();
        dl.PrimWriteVtx(top, white, cTop);
        dl.PrimWriteVtx(right, white, cMid);
        dl.PrimWriteVtx(foot, white, cFoot);
        dl.PrimWriteVtx(left, white, cMid);
        dl.PrimWriteIdx((ushort)first);
        dl.PrimWriteIdx((ushort)(first + 1));
        dl.PrimWriteIdx((ushort)(first + 2));
        dl.PrimWriteIdx((ushort)first);
        dl.PrimWriteIdx((ushort)(first + 2));
        dl.PrimWriteIdx((ushort)(first + 3));
    }
}
