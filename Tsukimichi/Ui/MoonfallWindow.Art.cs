using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Tsukimichi.Core.Moonfall;
using Tsukimichi.Core.Moonfall.Art;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Ui;

/// <summary>
/// Moonfall's board drawn from its art set (feature plan v9 G8; <see cref="MoonfallAtlas"/>): one hook per part, the
/// ground (the level's scene, else the night sky), the bricks, the pegs, the frame, the launcher, the bucket the campaign
/// draws, the ball and the HUD parts on the board (the free-ball gauge, the Fever cups, the banner's band and rule). Every
/// part comes from an atlas region named in <see cref="MoonfallSprites"/>, placed by its anchor, and every colour from
/// the manifest's inks, so a richer art set drops in by replacing the files in <c>assets/moonfall/</c>.
/// <para>
/// The textures load while the window is open and go when it closes (<see cref="MoonfallArtTextures"/>); until the
/// manifest and a sheet are in, or when they are missing or broken, the board draws the stage 1 primitives
/// (<see cref="DrawBoard"/>). The camera, the interpolation, the popups and the HUD bar are stage 1's. Reduce motion
/// keeps the camera still, clears pegs with a 120 ms fade and keeps the lantern steady; Decoration Quiet drops the
/// moondust, the slow-motion trail and the enamel's mottling; Plain draws flat pegs and bricks in the manifest's flat
/// inks on the flat ground, a flat frame with a keyline, and no glows. The board is the art set's own night scene on
/// every palette, so its contrast does not depend on the palette (the art tests check the pegs against its ground).
/// </para>
/// </summary>
public sealed partial class MoonfallWindow
{
    /// <summary>Above this many screen px per board unit the 2x sheet is drawn.</summary>
    private const float ArtTwoXAbove = 1.01f;

    private const double ClearSeconds = 0.30;
    private const double ClearFadeSeconds = 0.12;
    private const int DustSpecks = 28;
    private const int TrailLength = 5;
    private const double NotchSeconds = 0.4;

    private MoonfallArtLoader<IDalamudTextureWrap>? art;
    private MoonfallGame? artFor;
    private double[] artClearedAt = [];
    private int artDrawnFrame = int.MinValue / 2;
    private readonly Vector2[] artTrail = new Vector2[TrailLength];
    private int artTrailCount;
    private int artTrailBall;
    private double artNotchAt = double.NegativeInfinity;
    private float artNotch;
    private readonly List<(float S, float U)> artColumns = [];

    /// <summary>What a sprite is drawn with this frame.</summary>
    private readonly record struct ArtPen(ImDrawListPtr Dl, View View, MoonfallAtlas Atlas, ImTextureID Sheet);

    /// <summary>Sets the art up when the plugin gives textures (never in the load check, which has none).</summary>
    private void InitArt(IMoonfallArtHost<IDalamudTextureWrap>? host, string? folder)
    {
        if (host is not null && folder is not null)
        {
            art = new MoonfallArtLoader<IDalamudTextureWrap>(host, folder);
        }
    }

    /// <summary>
    /// Every frame, open or not, before the window draws: when it did not draw last frame (collapsed, so Dalamud skips
    /// Draw), the sound is told the board holds, so nothing (the finale least of all) plays on under a collapsed window,
    /// and the board pauses as it does when the window loses focus. Then disposes released textures, and lets the art go
    /// once the window has stopped drawing.
    /// </summary>
    public override void PreOpenCheck()
    {
        base.PreOpenCheck();
        if (IsOpen && drawWatch.Missed(ImGui.GetFrameCount()))
        {
            HoldWhileHidden();
        }

        if (art is null)
        {
            return;
        }

        art.Tick();
        gameArt?.Tick();
        // Two frames without a board drawn (closed, or past its close fade): the textures go back.
        if (ImGui.GetFrameCount() - artDrawnFrame > 2)
        {
            art.Release();
            gameArt?.Release();
            sceneFor = null;
        }
    }

    /// <summary>On unload: the art's textures are disposed now.</summary>
    public void DisposeArt()
    {
        art?.Dispose();
        art = null;
        gameArt?.Dispose();
        gameArt = null;
        fonts?.Dispose();
        fonts = null;
    }

    /// <summary>Reads the board's events the art needs: when each peg cleared, and the free-ball notch.</summary>
    private void ArtEvent(in MoonfallEvent e)
    {
        switch (e.Kind)
        {
            case MoonfallEventKind.PegCleared or MoonfallEventKind.StuckClear when (uint)e.Peg < (uint)artClearedAt.Length && ReferenceEquals(artFor, game):
                artClearedAt[e.Peg] = boardClock;
                break;

            case MoonfallEventKind.FreeBall when e.Value > 0:
                var thresholds = MoonfallRules.FreeBallThresholds;
                artNotch = MoonfallArtMath.Notch(e.Value, thresholds);
                artNotchAt = boardClock;
                break;
        }
    }

    /// <summary>
    /// Draws the board from the art set; false, with nothing drawn, while the art cannot draw (the caller draws the stage 1
    /// primitives instead).
    /// </summary>
    private bool DrawBoardArt(ImDrawListPtr dl, in View view, MoonfallGame g, double alpha, Vector2 origin, Vector2 size)
    {
        if (art is null)
        {
            return false;
        }

        artDrawnFrame = ImGui.GetFrameCount();
        var wantTwoX = view.Scale * view.Zoom > ArtTwoXAbove;
        var plain = decoration == Flair.Plain;

        // The game's own art (spec-rich2.md §6): the chrome, the companions' cards and the level's scene, built at the
        // board's tier (the zoom is not counted: a scene is not rebuilt for Full Moon's close-up). Not under Plain.
        var level = g.Level;
        if (!plain)
        {
            gameArt?.Frame(level, view.Scale > ArtTwoXAbove, options?.PegMarks == true);
        }

        // The interim picture stands in only where no recipe draws the scene: a level without one, or one whose build failed.
        // A scene the spoiler shield hides shows no picture either: the night sky.
        var recipe = plain ? null : gameArt?.RecipeFor(level);
        var hidden = !plain && (gameArt?.RecipeHidden(level) ?? modes.SceneVeiled(level, level.Scene));
        var picture = plain || hidden ? null : recipe is null ? g.Level.Scene : gameArt!.SceneState == MoonfallSceneState.Failed ? recipe.Fallback : null;
        art.Frame(wantTwoX, picture);
        if (art.Atlas is not { } atlas || art.Sheet(wantTwoX, out _) is not { } sheet)
        {
            richHud = false;
            return false;
        }

        if (!ReferenceEquals(artFor, g))
        {
            artFor = g;
            artClearedAt = new double[g.PegCount];
            Array.Fill(artClearedAt, double.NaN);
            artTrailCount = 0;
            artNotchAt = double.NegativeInfinity;
        }

        var pen = new ArtPen(dl, view, atlas, sheet.Handle);
        var scene = RichSceneNow();
        ChromePen? rich = !plain && gameArt?.Chrome is { FrameReady: true } chromeSheet && gameArt.ChromeTexture is { } ui
            ? new ChromePen(dl, view, chromeSheet, ui.Handle) : null;
        richHud = rich is not null;
        if (scene is not null)
        {
            dl.AddRectFilled(origin, origin + size, Theme.U32(atlas.Ground));
            RichScene(pen, scene, g);
        }
        else
        {
            var ground = art.Ground(wantTwoX, out var kind);
            ArtGround(pen, origin, size, ground?.Handle, kind, plain);
        }

        // Under the rich chrome Fever lifts the sky instead of dimming it; the approach still dims.
        if (scene is null || !g.Fever)
        {
            ArtFeverShade(pen, g, plain);
        }

        ArtBand(pen, g, plain);
        if (scene is not null)
        {
            RichVeil(pen, scene, g, alpha);
        }

        ArtPegs(pen, g, alpha, plain);
        if (!plain)
        {
            PegMarks(pen, g, alpha);
        }

        if (scene is not null)
        {
            LanternSpill(pen, g, alpha);
        }

        if (rich is { } cp)
        {
            var enamel = scene?.Enamel;
            RichFrame(cp, enamel?.Handle, enamel?.Width ?? 0, enamel?.Height ?? 0, scene?.Layers.Chrome ?? MoonfallChromePalette.Medallion);
            RichCrest(cp, pen);
        }
        else
        {
            ArtFrame(pen, plain);
        }

        ArtLauncher(pen, g, plain);
        ArtBucket(pen, g, alpha, plain);
        if (rich is { } cw)
        {
            RichWings(cw, pen, g, alpha);
            RichFeverCups(pen, g, plain);
        }
        else
        {
            ArtFeverCups(pen, g, plain);
        }

        ArtBall(pen, g, alpha, plain);
        if (rich is { } ch)
        {
            RichHud(ch, pen, g);
            TopRailGlint(pen);
            PowerFlash(pen);
            Ribbons(ch);
            FeverMoment(ch, pen, g);
            if (!cardInMargin)
            {
                PowerRibbon(ch);
            }
        }
        else
        {
            ArtRule(pen, origin, size, g);
        }

        return true;
    }

    // ---- The ground ----

    /// <summary>The flat ground, then the level's scene over the whole board or the night sky over its opening (not under Plain).</summary>
    private static void ArtGround(in ArtPen p, Vector2 origin, Vector2 size, ImTextureID? picture, MoonfallGround kind, bool plain)
    {
        p.Dl.AddRectFilled(origin, origin + size, Theme.U32(p.Atlas.Ground));
        if (plain || picture is not { } handle)
        {
            return;
        }

        if (kind == MoonfallGround.Scene)
        {
            p.Dl.AddImage(handle, p.View.Map(0, 0), p.View.Map(MoonfallRules.Width, MoonfallRules.Height));
        }
        else
        {
            var sky = p.Atlas.Sky;
            p.Dl.AddImage(handle, p.View.Map(sky.X, sky.Y), p.View.Map(sky.X + sky.W, sky.Y + sky.H));
        }
    }

    /// <summary>The sky dims a little in the approach to the last orange, and more under Full Moon.</summary>
    private static void ArtFeverShade(in ArtPen p, MoonfallGame g, bool plain)
    {
        if (plain || !(g.Approaching || g.Fever))
        {
            return;
        }

        p.Dl.AddRectFilled(p.View.Map(MoonfallRules.LeftWall, Top), p.View.Map(MoonfallRules.RightWall, MoonfallRules.Height), Theme.WithAlpha(p.Atlas.Dim, g.Fever ? 0.21f : 0.12f));
    }

    /// <summary>The Full Moon banner's soft dark band, behind the pegs.</summary>
    private static void ArtBand(in ArtPen p, MoonfallGame g, bool plain)
    {
        if (plain || !g.BannerVisible)
        {
            return;
        }

        ref readonly var band = ref p.Atlas[MoonfallSprite.FeverBand];
        var y = MoonfallRules.Height * BannerHeight;
        Stretch(p, band, 0, 0, band.W, band.H, MoonfallRules.LeftWall, y - 85, MoonfallRules.RightWall, y + 85, Theme.WithAlpha(p.Atlas.Band, 0.55f));
    }

    /// <summary>The banner's text sits at this share of the board's height (as <see cref="DrawBanner"/> places it).</summary>
    private const double BannerHeight = 0.32;

    /// <summary>The top rail's foot: the board's opening starts here.</summary>
    private const double Top = 41;

    // ---- Pegs and bricks ----

    private void ArtPegs(in ArtPen p, MoonfallGame g, double alpha, bool plain)
    {
        var full = decoration == Flair.Full;
        var reduce = UiMetrics.ReduceMotion;
        var highContrast = Theme.Glyphs.HighContrast;
        var lastOrange = g.Approaching && g.OrangesLeft == 1;
        for (var i = 0; i < g.PegCount; i++)
        {
            var peg = g.Peg(i, alpha);
            var lit = peg.Lit;
            var fade = 1f;
            var scale = 1f;
            var drop = 0.0;
            if (peg.Cleared)
            {
                var age = boardClock - artClearedAt[i];
                if (double.IsNaN(age) || age < 0 || age > (reduce || plain ? ClearFadeSeconds : ClearSeconds))
                {
                    continue;
                }

                lit = true;
                if (reduce || plain)
                {
                    fade = 1f - (float)(age / ClearFadeSeconds);
                }
                else if (age < 0.06)
                {
                    // 0-60 ms: a bloom.
                    scale = 1.06f;
                    if (peg.Shape == PegShape.Round)
                    {
                        Put(p, p.Atlas[MoonfallSprite.Bloom], peg.X, peg.Y, (float)(peg.Radius / MoonfallRules.PegRadius) * scale, Theme.U32(p.Atlas.Glow(peg.Colour)));
                    }
                }
                else
                {
                    // 60-150 ms: the moon dims and sets a little; then only its dust is left (150-300 ms).
                    if (full)
                    {
                        ArtDust(p, i, peg, (float)Math.Clamp((age - 0.06) / 0.24, 0, 1));
                    }

                    if (age >= 0.15)
                    {
                        continue;
                    }

                    scale = 0.70f;
                    fade = 0.45f;
                    drop = 4 * ((age - 0.06) / 0.09);
                }
            }

            if (peg.Shape != PegShape.Round)
            {
                ArtBrick(p, peg, lit, plain, fade);
                continue;
            }

            var size = (float)(peg.Radius / MoonfallRules.PegRadius) * scale;
            var y = peg.Y + drop;
            var glow = p.Atlas.Glow(peg.Colour);
            if (plain)
            {
                var flat = p.Atlas.Flat(peg.Colour);
                Put(p, p.Atlas[MoonfallSprite.Disc], peg.X, y, size, Theme.WithAlpha(lit ? ColorMath.Mix(flat, Vector4.One, 0.35f) : flat, fade));
                Put(p, p.Atlas[MoonfallSprite.Sliver], peg.X, y, size, Theme.WithAlpha(p.Atlas.FlatShade(peg.Colour), fade));
                if (lit)
                {
                    Put(p, p.Atlas[MoonfallSprite.Ring], peg.X, y, size, Theme.WithAlpha(glow, fade));
                }

                continue;
            }

            if (lit)
            {
                // The halo is the lit state, not a glow: it stays under Quiet and Reduce motion.
                Put(p, p.Atlas[MoonfallSprite.Halo], peg.X, y, size, Theme.WithAlpha(glow, fade));
            }
            else if (lastOrange && peg.Colour == PegColour.Orange && !peg.Cleared)
            {
                // The last orange's halo widens in the approach.
                Put(p, p.Atlas[MoonfallSprite.Halo], peg.X, y, size * 1.6f, Theme.WithAlpha(glow, 0.6f));
            }

            Put(p, p.Atlas.Peg(peg.Colour, i, lit), peg.X, y, size, Theme.WithAlpha(Vector4.One, fade));
            if (lit && highContrast)
            {
                Put(p, p.Atlas[MoonfallSprite.Ring], peg.X, y, size, Theme.WithAlpha(Vector4.One, fade));
            }
        }
    }

    /// <summary>A cleared peg's light sifting down as moondust (Full only): 28 specks, the same for the same peg.</summary>
    private static void ArtDust(in ArtPen p, int index, in MoonfallPegView peg, float t)
    {
        var r = peg.Shape == PegShape.Round ? peg.Radius : peg.Thickness * 0.5;
        var (x, y) = peg.Shape == PegShape.Line ? ((peg.X + peg.X2) * 0.5, (peg.Y + peg.Y2) * 0.5)
            : peg.Shape == PegShape.Arc ? (peg.X + (peg.Radius * Math.Cos(peg.StartRadians + (peg.SweepRadians * 0.5))), peg.Y + (peg.Radius * Math.Sin(peg.StartRadians + (peg.SweepRadians * 0.5))))
            : (peg.X, peg.Y);
        var glow = ColorMath.Mix(p.Atlas.Glow(peg.Colour), Vector4.One, 0.3f);
        var fade = MathF.Pow(1 - t, 1.6f);
        ref readonly var speck = ref p.Atlas[MoonfallSprite.Speck];
        var seed = (uint)(index * 7919) + 17u;
        for (var k = 0; k < DustSpecks; k++)
        {
            var a = Next(ref seed) < 0.75f ? 0.15 + (Next(ref seed) * (Math.PI - 0.3)) : Next(ref seed) * 2 * Math.PI;
            var rad = r * (0.25 + (0.9 * Next(ref seed))) * (0.6 + (0.6 * t));
            var mx = x + (Math.Cos(a) * rad * 0.9);
            var my = y + (Math.Sin(a) * rad * 0.7) + (r * 1.4 * t * (0.6 + (0.8 * Next(ref seed))));
            var mr = (float)(r * (0.035 + (0.035 * Next(ref seed))));
            Put(p, speck, mx, my, mr, Theme.WithAlpha(glow, fade * (0.45f + (0.4f * Next(ref seed)))));
        }
    }

    /// <summary>A small deterministic random number in [0, 1).</summary>
    private static float Next(ref uint seed)
    {
        seed = (seed * 1664525u) + 1013904223u;
        return (seed >> 8) / (float)(1 << 24);
    }

    /// <summary>
    /// A brick from its sprite, laid along its line or arc: the sprite's left half-height is the start cap, its right
    /// half-height the end cap, and its middle repeats, mirrored each time, to the brick's length (so the stone runs on
    /// without a seam), the whole stretched across the brick's thickness. Its ends are rounded like the engine's capsule.
    /// </summary>
    private void ArtBrick(in ArtPen p, in MoonfallPegView peg, bool lit, bool plain, float fade)
    {
        ref readonly var r = ref plain ? ref p.Atlas[MoonfallSprite.BrickFlat] : ref p.Atlas.Brick(peg.Colour, lit);
        var tint = plain ? Theme.WithAlpha(lit ? ColorMath.Mix(p.Atlas.Flat(peg.Colour), Vector4.One, 0.35f) : p.Atlas.Flat(peg.Colour), fade) : Theme.WithAlpha(Vector4.One, fade);
        var arc = peg.Shape == PegShape.Arc;
        var thickness = (float)peg.Thickness;
        var half = thickness * 0.5;
        var length = arc ? peg.Radius * peg.SweepRadians : Math.Sqrt(((peg.X2 - peg.X) * (peg.X2 - peg.X)) + ((peg.Y2 - peg.Y) * (peg.Y2 - peg.Y)));
        var total = (float)(length + thickness);
        if (!(total > 0) || !(r.H > 0))
        {
            return;
        }

        // Columns along the brick: (distance from its start, texture u in units of the sprite); an arc's every 8 units.
        MoonfallArtMath.BrickColumns(total, r.W, r.H, thickness, arc ? 8f : float.MaxValue, artColumns);
        if (artColumns.Count < 2)
        {
            return;
        }

        // Where a distance along the brick lies, and the brick's normal there (turned so the sprite's top faces up).
        double ax = peg.X, ay = peg.Y, dx = 0, dy = 0, start = 0;
        if (arc)
        {
            start = peg.StartRadians - (half / peg.Radius);
        }
        else
        {
            dx = (peg.X2 - peg.X) / Math.Max(length, 1e-6);
            dy = (peg.Y2 - peg.Y) / Math.Max(length, 1e-6);
            ax -= dx * half;
            ay -= dy * half;
        }

        var flip = arc ? Math.Sin(peg.StartRadians + (peg.SweepRadians * 0.5)) < 0 : dx < 0;
        p.Dl.PushTextureID(p.Sheet);
        var count = artColumns.Count;
        p.Dl.PrimReserve(6 * (count - 1), 2 * count);
        var first = p.Dl.VtxCurrentIdx;
        var (uv0, uv1) = p.Atlas.Uv(r);
        foreach (var (s, u) in artColumns)
        {
            double cx, cy, nx, ny;
            if (arc)
            {
                var a = start + (s / peg.Radius);
                (nx, ny) = (Math.Cos(a), Math.Sin(a));
                (cx, cy) = (peg.X + (peg.Radius * nx), peg.Y + (peg.Radius * ny));
            }
            else
            {
                (cx, cy) = (ax + (dx * s), ay + (dy * s));
                (nx, ny) = (-dy, dx);
            }

            if (flip)
            {
                (nx, ny) = (-nx, -ny);
            }

            var tu = uv0.X + ((uv1.X - uv0.X) * (u / r.W));
            p.Dl.PrimWriteVtx(p.View.Map(cx - (nx * half), cy - (ny * half)), new Vector2(tu, uv0.Y), tint);
            p.Dl.PrimWriteVtx(p.View.Map(cx + (nx * half), cy + (ny * half)), new Vector2(tu, uv1.Y), tint);
        }

        for (var c = 0u; c < count - 1; c++)
        {
            var a = first + (2 * c);
            p.Dl.PrimWriteIdx((ushort)a);
            p.Dl.PrimWriteIdx((ushort)(a + 1));
            p.Dl.PrimWriteIdx((ushort)(a + 3));
            p.Dl.PrimWriteIdx((ushort)a);
            p.Dl.PrimWriteIdx((ushort)(a + 3));
            p.Dl.PrimWriteIdx((ushort)(a + 2));
        }

        p.Dl.PopTextureID();
    }

    // ---- The frame ----

    private void ArtFrame(in ArtPen p, bool plain)
    {
        var atlas = p.Atlas;
        var view = p.View;
        ReadOnlySpan<(double X0, double Y0, double X1, double Y1)> rails =
        [
            (0, 0, MoonfallRules.Width, Top),
            (0, Top, MoonfallRules.LeftWall, MoonfallRules.Height),
            (MoonfallRules.RightWall, Top, MoonfallRules.Width, MoonfallRules.Height),
        ];
        if (plain)
        {
            var enamel = Theme.U32(atlas.Enamel);
            foreach (var (x0, y0, x1, y1) in rails)
            {
                p.Dl.AddRectFilled(view.Map(x0, y0), view.Map(x1, y1), enamel);
            }

            var keyline = Theme.U32(atlas.Keyline);
            p.Dl.AddRect(view.Map(MoonfallRules.LeftWall, Top), view.Map(MoonfallRules.RightWall, MoonfallRules.Height + 2), keyline, 0f, ImDrawFlags.None, 1f);
            p.Dl.AddRect(view.Map(0, 0) + new Vector2(0.5f), view.Map(MoonfallRules.Width, MoonfallRules.Height) - new Vector2(0.5f), keyline, 0f, ImDrawFlags.None, 1f);
            return;
        }

        // The enamel: lit at the upper left, deep at the lower right (a linear ramp, so four corners carry it exactly).
        foreach (var (x0, y0, x1, y1) in rails)
        {
            p.Dl.AddRectFilledMultiColor(view.Map(x0, y0), view.Map(x1, y1), Enamel(atlas, x0, y0), Enamel(atlas, x1, y0), Enamel(atlas, x1, y1), Enamel(atlas, x0, y1));
        }

        if (decoration == Flair.Full)
        {
            ref readonly var mottle = ref atlas[MoonfallSprite.FrameMottle];
            foreach (var (x0, y0, x1, y1) in rails)
            {
                Tile(p, mottle, x0, y0, x1, y1);
            }
        }

        // The rails stand proud of the sky: a contact shade on the board along the top and the left.
        var dark = Theme.WithAlpha(atlas.Contact, 0.55f);
        var clear = Theme.WithAlpha(atlas.Contact, 0f);
        p.Dl.AddRectFilledMultiColor(view.Map(MoonfallRules.LeftWall, Top), view.Map(MoonfallRules.RightWall, Top + 7), dark, dark, clear, clear);
        p.Dl.AddRectFilledMultiColor(view.Map(MoonfallRules.LeftWall, Top), view.Map(MoonfallRules.LeftWall + 7, MoonfallRules.Height), dark, clear, clear, dark);

        NineSlice(p, atlas[MoonfallSprite.FrameBeadOuter], atlas.OuterSlice, 0, 0, MoonfallRules.Width, MoonfallRules.Height, bottom: true);
        NineSlice(p, atlas[MoonfallSprite.FrameBeadInner], atlas.InnerSlice, MoonfallRules.LeftWall - 3.5, Top - 3.5, MoonfallRules.RightWall + 3.5, MoonfallRules.Height, bottom: false);
    }

    private static uint Enamel(MoonfallAtlas atlas, double x, double y) =>
        Theme.U32(ColorMath.Mix(atlas.Enamel, atlas.EnamelDeep, (float)Math.Clamp((x / MoonfallRules.Width * 0.4) + (y / MoonfallRules.Height * 0.6), 0, 1)));

    // ---- The launcher ----

    private void ArtLauncher(in ArtPen p, MoonfallGame g, bool plain)
    {
        var atlas = p.Atlas;
        const double px = MoonfallRules.LauncherX, py = MoonfallRules.LauncherY;
        Put(p, atlas[MoonfallSprite.LauncherYoke], px, py, 1f, uint.MaxValue);
        Put(p, atlas[MoonfallSprite.LauncherGauge], px, py, 1f, uint.MaxValue);
        var fill = g.Phase is MoonfallPhase.Flying or MoonfallPhase.Clearing ? MoonfallArtMath.GaugeShare(g.ShotScore, MoonfallRules.FreeBallThresholds) : 0f;
        if (fill > 0f)
        {
            ArtGaugeFill(p, fill);
        }

        var notchAge = boardClock - artNotchAt;
        if (!plain && notchAge is >= 0 and < NotchSeconds)
        {
            // A free ball earned: the notch's glass blooms with moonlight, 0.4 s.
            var a = (atlas.GaugeFrom + ((atlas.GaugeTo - atlas.GaugeFrom) * artNotch)) * (MathF.PI / 180f);
            var (nx, ny) = (px + (25.5 * MathF.Cos(a)), py + (25.5 * MathF.Sin(a)));
            var f = 1f - (float)(notchAge / NotchSeconds);
            ref readonly var soft = ref atlas[MoonfallSprite.Soft];
            Put(p, soft, nx, ny, 4.5f / 4f, Theme.WithAlpha(atlas.Moonlight, 0.75f * f));
            Put(p, soft, nx, ny, 12f / 4f, Theme.WithAlpha(atlas.Moonlight, 0.22f * f));
        }

        var (dx, dy) = MoonfallGame.Direction(aim);
        PutTurned(p, atlas[MoonfallSprite.LauncherTube], px, py, (float)dy, (float)-dx, uint.MaxValue);
        Put(p, atlas[MoonfallSprite.LauncherHub], px, py, 1f, uint.MaxValue);

        var aiming = g.Phase == MoonfallPhase.Aiming && g.BallsLeft > 0 && PlayerAims;
        if (aiming && !pause.Paused)
        {
            var n = g.Guide(aim, guide);
            ref readonly var dot = ref atlas[MoonfallSprite.Dot];
            for (var k = 1; k < n; k++)
            {
                Put(p, dot, guide[k].X, guide[k].Y, 1f, Theme.WithAlpha(Vector4.One, 0.95f - (0.02f * k)));
            }
        }

        if (boardClock < freeBallUntil)
        {
            var fade = (float)Math.Clamp((freeBallUntil - boardClock) / 0.3, 0, 1);
            p.Dl.AddText(p.View.Map(MoonfallRules.LauncherX + 30, MoonfallRules.LauncherY - 30), Theme.WithAlpha(Theme.GoldHigh, fade), Strings.MoonfallFreeBall);
        }
    }

    /// <summary>The gauge's light up to <paramref name="share"/> of its arc, cut from its sprite as a fan round the pivot.</summary>
    private static void ArtGaugeFill(in ArtPen p, float share)
    {
        var atlas = p.Atlas;
        ref readonly var r = ref atlas[MoonfallSprite.LauncherGaugeFill];
        const float px = (float)MoonfallRules.LauncherX, py = (float)MoonfallRules.LauncherY;
        var left = px - r.AnchorX;
        var top = py - r.AnchorY;
        var from = atlas.GaugeFrom;
        var to = from + ((atlas.GaugeTo - from) * Math.Clamp(share, 0f, 1f));
        var steps = Math.Max(1, (int)MathF.Ceiling((to - from) / 3f));
        p.Dl.PushTextureID(p.Sheet);
        p.Dl.PrimReserve(6 * steps, 2 * (steps + 1));
        var first = p.Dl.VtxCurrentIdx;
        for (var k = 0; k <= steps; k++)
        {
            var a = (from + ((to - from) * k / steps)) * (MathF.PI / 180f);
            var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
            // The ray's span inside the sprite's box, so every sample stays inside its region.
            var (near, far) = MoonfallArtMath.RayInBox(new Vector2(px, py), dir, new Vector2(left, top), new Vector2(left + r.W, top + r.H));
            foreach (var d in (ReadOnlySpan<float>)[near, far])
            {
                var at = new Vector2(px, py) + (dir * d);
                p.Dl.PrimWriteVtx(p.View.Map(at.X, at.Y), atlas.UvAt(r, at.X - left, at.Y - top), uint.MaxValue);
            }
        }

        for (var k = 0u; k < steps; k++)
        {
            var a = first + (2 * k);
            p.Dl.PrimWriteIdx((ushort)a);
            p.Dl.PrimWriteIdx((ushort)(a + 1));
            p.Dl.PrimWriteIdx((ushort)(a + 3));
            p.Dl.PrimWriteIdx((ushort)a);
            p.Dl.PrimWriteIdx((ushort)(a + 3));
            p.Dl.PrimWriteIdx((ushort)(a + 2));
        }

        p.Dl.PopTextureID();
    }

    // ---- The bucket ----

    private void ArtBucket(in ArtPen p, MoonfallGame g, double alpha, bool plain)
    {
        if (g.Fever)
        {
            return;
        }

        var atlas = p.Atlas;
        var x = g.BucketXAt(alpha);
        const double rim = MoonfallRules.BucketTop;
        if (atlas.BucketFor(campaign) == MoonfallBucketStyle.Cradle)
        {
            ref readonly var rail = ref atlas[MoonfallSprite.BucketRail];
            Stretch(p, rail, 0, 0, rail.W, rail.H, MoonfallRules.LeftWall, 588 - rail.AnchorY, MoonfallRules.RightWall, 588 - rail.AnchorY + rail.H, uint.MaxValue);
            Put(p, atlas[MoonfallSprite.BucketCradle], x, rim, 1f, uint.MaxValue);
            return;
        }

        if (atlas.BucketFor(campaign) == MoonfallBucketStyle.Cart)
        {
            ArtCart(p, x, rim, plain);
            return;
        }

        const double water = 584;
        Put(p, atlas[MoonfallSprite.BucketWater], 75, water, 1f, uint.MaxValue);
        ref readonly var boat = ref atlas[MoonfallSprite.BucketBoat];
        ArtReflection(p, boat, x, rim, water);
        var flicker = MoonfallMotion.Flicker(boardClock, 0f, UiMetrics.ReduceMotion || motion == MoonfallMotionLevel.Still);
        var lantern = atlas.BoatLantern;
        Put(p, atlas[MoonfallSprite.BucketColumn], x + lantern.X, water, 1f, Theme.WithAlpha(Vector4.One, Math.Clamp(flicker, 0f, 1f)));
        Put(p, boat, x, rim, 1f, uint.MaxValue);
        Put(p, atlas[MoonfallSprite.BucketBoatContact], x, rim, 1f, uint.MaxValue);
        if (!plain)
        {
            ref readonly var soft = ref atlas[MoonfallSprite.Soft];
            Put(p, soft, x + lantern.X, rim + lantern.Y, 9f / 4f, Theme.WithAlpha(atlas.Lantern, 0.40f * flicker));
            Put(p, soft, x + lantern.X, rim + lantern.Y, 24f / 4f, Theme.WithAlpha(atlas.Lantern, 0.10f * flicker));
        }
    }

    /// <summary>
    /// Bucket C: the moon road, the cart on it, and its paper lantern's warmth in the air and on the road (the boat's
    /// lantern, flickering the same way).
    /// </summary>
    private void ArtCart(in ArtPen p, double x, double rim, bool plain)
    {
        var atlas = p.Atlas;
        const double road = 586;
        // The road's left half, then the same mirrored to the right wall (the atlas holds the half).
        ref readonly var half = ref atlas[MoonfallSprite.BucketRoad];
        Put(p, half, MoonfallFramingCheck.WallL, road, 1f, uint.MaxValue);
        Stretch(p, half, half.W, 0, 0, half.H, MoonfallFramingCheck.WallR - half.W, road - half.AnchorY, MoonfallFramingCheck.WallR, road - half.AnchorY + half.H, uint.MaxValue);
        var flicker = Math.Clamp(MoonfallMotion.Flicker(boardClock, 0f, UiMetrics.ReduceMotion || motion == MoonfallMotionLevel.Still), 0f, 1f);
        var lantern = atlas.BoatLantern;
        ref readonly var soft = ref atlas[MoonfallSprite.Soft];
        if (!plain)
        {
            // The pool on the road under the lantern: the soft light's lower half, flattened.
            var lx = x + lantern.X;
            Stretch(p, soft, 0, soft.H / 2f, soft.W, soft.H, lx - 52, road, lx + 52, road + 6.4, Theme.WithAlpha(atlas.Lantern, 0.22f * flicker));
        }

        Put(p, atlas[MoonfallSprite.BucketCart], x, rim, 1f, uint.MaxValue);
        if (!plain)
        {
            Put(p, soft, x + lantern.X, rim + lantern.Y, 9f / 4f, Theme.WithAlpha(atlas.Lantern, 0.40f * flicker));
            Put(p, soft, x + lantern.X, rim + lantern.Y, 24f / 4f, Theme.WithAlpha(atlas.Lantern, 0.10f * flicker));
        }
    }

    /// <summary>The boat mirrored in the water, darker and broken into level bands by ripples, a row a unit.</summary>
    private void ArtReflection(in ArtPen p, in MoonfallSpriteRect boat, double x, double anchorY, double water)
    {
        var top = anchorY - boat.AnchorY;
        var left = x - boat.AnchorX;
        for (var j = 0; j < 10; j++)
        {
            var source = water - j - 1 - top;
            if (source < 0 || source + 1 > boat.H)
            {
                continue;
            }

            var shift = Math.Sin(((water + j) * 0.55) + 1.3) * (0.6 + (0.1 * j));
            var dark = 0.62f - (0.20f * j / 10f);
            var band = 0.78f + (0.22f * MathF.Sin((float)(water + j) * 2.1f));
            Stretch(p, boat, 0, (float)source + 1, boat.W, (float)source, left + shift, water + j, left + shift + boat.W, water + j + 1,
                Theme.U32(new Vector4(dark, dark, dark, band)));
        }
    }

    // ---- Fever and the banner ----

    private void ArtFeverCups(in ArtPen p, MoonfallGame g, bool plain)
    {
        if (!g.Fever)
        {
            return;
        }

        var shown = (float)g.FeverBucketsShown;
        var lift = (1 - shown) * 30;
        var values = MoonfallRules.FeverBucketValues;
        var atlas = p.Atlas;
        for (var k = 0; k < values.Length; k++)
        {
            var cx = MoonfallRules.LeftWall + ((k + 0.5) * MoonfallBucket.FeverBucketWidth);
            var centre = k == values.Length / 2;
            ref readonly var cup = ref atlas[centre ? MoonfallSprite.FeverCupCentre : MoonfallSprite.FeverCup];
            var lit = g.FeverBucket == k;
            if (lit && !plain)
            {
                Put(p, atlas[MoonfallSprite.Soft], cx, 572 + lift, 10f, Theme.WithAlpha(atlas.CupLit, 0.35f * shown));
            }

            // The value sits on the cup's plate, at its anchor.
            Put(p, cup, cx, 581 + lift, 1f, Theme.WithAlpha(Vector4.One, shown));
            var text = g.Perfect ? perfectText : feverTexts[k];
            var textSize = ImGui.CalcTextSize(text);
            p.Dl.AddText(p.View.Map(cx, 581 + lift) - (textSize * 0.5f), Theme.WithAlpha(lit || centre ? atlas.CupLit : Theme.Surface.Text, shown), text);
        }
    }

    /// <summary>The banner's gilt rule, under its text (placed as <see cref="DrawBanner"/> places the text).</summary>
    private static void ArtRule(in ArtPen p, Vector2 origin, Vector2 size, MoonfallGame g)
    {
        if (!g.BannerVisible)
        {
            return;
        }

        var fontSize = MathF.Max(ImGui.GetFontSize() * 2.2f, size.Y * 0.085f);
        ref readonly var rule = ref p.Atlas[MoonfallSprite.FeverRule];
        var scale = p.View.Scale;
        var mid = new Vector2(origin.X + (size.X * 0.5f), origin.Y + (size.Y * (float)BannerHeight) + (fontSize * 0.62f));
        var min = mid - (new Vector2(rule.AnchorX, rule.AnchorY) * scale);
        var (uv0, uv1) = p.Atlas.Uv(rule);
        p.Dl.AddImage(p.Sheet, min, min + (new Vector2(rule.W, rule.H) * scale), uv0, uv1);
    }

    // ---- The ball ----

    /// <summary>
    /// The ball in the launcher, or every ball in play (a twin too) with the ball sprite, and in the approach the
    /// slow-motion trail behind the ball the camera follows (<see cref="MoonfallGame.CameraBall"/>).
    /// </summary>
    private void ArtBall(in ArtPen p, MoonfallGame g, double alpha, bool plain)
    {
        ref readonly var ball = ref p.Atlas[MoonfallSprite.Ball];
        var size = (float)(MoonfallRules.BallRadius / 6.0);
        if (g.Phase == MoonfallPhase.Aiming && g.BallsLeft > 0)
        {
            artTrailCount = 0;
            var (dx, dy) = MoonfallGame.Direction(aim);
            Put(p, ball, MoonfallRules.LauncherX + (dx * MoonfallRules.BarrelLength), MoonfallRules.LauncherY + (dy * MoonfallRules.BarrelLength), size, uint.MaxValue);
            return;
        }

        if (!g.BallInPlay)
        {
            artTrailCount = 0;
            return;
        }

        // The slow-motion trail in the approach to the last orange: fading copies (Full only).
        if (g.Approaching && decoration == Flair.Full && !plain)
        {
            // Another ball to follow starts a trail of its own.
            if (g.CameraBall != artTrailBall)
            {
                artTrailBall = g.CameraBall;
                artTrailCount = 0;
            }

            var (x, y) = g.BallAt(artTrailBall, alpha);
            var at = new Vector2((float)x, (float)y);
            if (artTrailCount == 0 || Vector2.Distance(artTrail[0], at) >= 3f)
            {
                Array.Copy(artTrail, 0, artTrail, 1, TrailLength - 1);
                artTrail[0] = at;
                artTrailCount = Math.Min(TrailLength, artTrailCount + 1);
            }

            for (var k = artTrailCount - 1; k >= 1; k--)
            {
                Put(p, ball, artTrail[k].X, artTrail[k].Y, size, Theme.WithAlpha(Vector4.One, 0.10f + (0.05f * (TrailLength - k))));
            }
        }
        else
        {
            artTrailCount = 0;
        }

        for (var k = 0; k < g.BallsInPlay; k++)
        {
            var (x, y) = g.BallAt(k, alpha);
            Put(p, ball, x, y, size, uint.MaxValue);
        }
    }

    // ---- Sprite placement ----

    /// <summary>A sprite with its anchor at board point (<paramref name="x"/>, <paramref name="y"/>), <paramref name="scale"/> times its size.</summary>
    private static void Put(in ArtPen p, in MoonfallSpriteRect r, double x, double y, float scale, uint tint)
    {
        var min = p.View.Map(x - (r.AnchorX * scale), y - (r.AnchorY * scale));
        var max = p.View.Map(x + ((r.W - r.AnchorX) * scale), y + ((r.H - r.AnchorY) * scale));
        var (uv0, uv1) = p.Atlas.Uv(r);
        p.Dl.AddImage(p.Sheet, min, max, uv0, uv1, tint);
    }

    /// <summary>A sprite turned about its anchor by the angle whose cosine and sine are given.</summary>
    private static void PutTurned(in ArtPen p, in MoonfallSpriteRect r, double x, double y, float cos, float sin, uint tint)
    {
        var view = p.View;
        Vector2 Corner(float lx, float ly) => view.Map(x + (lx * cos) - (ly * sin), y + (lx * sin) + (ly * cos));
        var (uv0, uv1) = p.Atlas.Uv(r);
        p.Dl.AddImageQuad(
            p.Sheet,
            Corner(-r.AnchorX, -r.AnchorY),
            Corner(r.W - r.AnchorX, -r.AnchorY),
            Corner(r.W - r.AnchorX, r.H - r.AnchorY),
            Corner(-r.AnchorX, r.H - r.AnchorY),
            uv0,
            new Vector2(uv1.X, uv0.Y),
            uv1,
            new Vector2(uv0.X, uv1.Y),
            tint);
    }

    /// <summary>
    /// Part of a sprite (<paramref name="u0"/>..<paramref name="u1"/>, <paramref name="v0"/>..<paramref name="v1"/> in
    /// units from its top-left; reversed to mirror) stretched over a board rectangle.
    /// </summary>
    private static void Stretch(in ArtPen p, in MoonfallSpriteRect r, float u0, float v0, float u1, float v1, double x0, double y0, double x1, double y1, uint tint) =>
        p.Dl.AddImage(p.Sheet, p.View.Map(x0, y0), p.View.Map(x1, y1), p.Atlas.UvAt(r, u0, v0), p.Atlas.UvAt(r, u1, v1), tint);

    /// <summary>A nine-slice round a board rectangle, without its centre (and without its bottom row unless <paramref name="bottom"/>).</summary>
    private static void NineSlice(in ArtPen p, in MoonfallSpriteRect r, float m, double x0, double y0, double x1, double y1, bool bottom)
    {
        ReadOnlySpan<float> us = [0, m, r.W - m, r.W];
        ReadOnlySpan<float> vs = [0, m, r.H - m, r.H];
        ReadOnlySpan<double> xs = [x0, x0 + m, x1 - m, x1];
        ReadOnlySpan<double> ys = [y0, y0 + m, bottom ? y1 - m : y1, y1];
        for (var row = 0; row < (bottom ? 3 : 2); row++)
        {
            for (var col = 0; col < 3; col++)
            {
                if (row == 1 && col == 1)
                {
                    continue;
                }

                // Without a bottom row the sides run down to the rectangle's foot.
                var vEnd = !bottom && row == 1 ? vs[2] : vs[row + 1];
                Stretch(p, r, us[col], vs[row], us[col + 1], vEnd, xs[col], ys[row], xs[col + 1], ys[row + 1], uint.MaxValue);
            }
        }
    }

    /// <summary>A tile repeated over a board rectangle, cut at its far edges.</summary>
    private static void Tile(in ArtPen p, in MoonfallSpriteRect r, double x0, double y0, double x1, double y1)
    {
        for (var y = y0; y < y1; y += r.H)
        {
            var h = Math.Min(r.H, y1 - y);
            for (var x = x0; x < x1; x += r.W)
            {
                var w = Math.Min(r.W, x1 - x);
                Stretch(p, r, 0, 0, (float)w, (float)h, x, y, x + w, y + h, uint.MaxValue);
            }
        }
    }
}
