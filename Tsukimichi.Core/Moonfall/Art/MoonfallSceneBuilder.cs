using System.Diagnostics;
using System.Numerics;

namespace Tsukimichi.Core.Moonfall.Art;

/// <summary>An RGBA picture ready to become a texture, and the board rectangle (units) it is drawn over.</summary>
/// <param name="Width">Pixels across.</param>
/// <param name="Height">Pixels down.</param>
/// <param name="Pixels">RGBA, 4 bytes a pixel, straight alpha.</param>
/// <param name="Board">(x0, y0, x1, y1) in board units.</param>
public sealed record MoonfallRgba(int Width, int Height, byte[] Pixels, Vector4 Board)
{
    /// <summary>The bytes the texture holds on the GPU.</summary>
    public long Bytes => (long)Width * Height * 4;
}

/// <summary>A firefly's rest place and size (its wander is 9 units across and 4 up and down, round it).</summary>
public readonly record struct MoonfallFirefly(float X, float Y, float Size, float Phase, float Period);

/// <summary>A star that twinkles on the scene's own bright point.</summary>
public readonly record struct MoonfallStar(float X, float Y, float Radius, float Phase, float Period);

/// <summary>A small light that flickers in play (a lamp, a lantern), board units.</summary>
public readonly record struct MoonfallFlicker(float X, float Y, float Halo, Vector3 Colour, float Phase);

/// <summary>A mist layer's tile and how it scrolls (wrapping at its tile's width).</summary>
public sealed record MoonfallMistLayer(MoonfallRgba Tile, float Speed, float Alpha, Vector3 Colour);

/// <summary>
/// One level's scene, built (<see cref="MoonfallSceneBuilder"/>): the textures and the places the in-play drawing
/// needs. <see cref="Base"/> is the graded, lit, dressed and veiled painting over the board's opening; the rest are small
/// layers for the margins, the motion and Fever.
/// </summary>
public sealed class MoonfallSceneLayers
{
    /// <summary>The recipe's name.</summary>
    public required string Name { get; init; }

    /// <summary>Pixels a board unit of <see cref="Base"/>.</summary>
    public float Scale { get; init; }

    /// <summary>The scene over the opening (x 75–725, y 41–594).</summary>
    public required MoonfallRgba Base { get; init; }

    /// <summary>The whole board small and blurred: the window's margins beside the board (spec-rich2.md §4, HUD).</summary>
    public required MoonfallRgba Backdrop { get; init; }

    /// <summary>The moving shafts at two places of their drift (white, alpha the light), masked off every piece; null with no moving shaft.</summary>
    public MoonfallRgba? BeamsA { get; init; }

    /// <inheritdoc cref="BeamsA"/>
    public MoonfallRgba? BeamsB { get; init; }

    /// <summary>The moving shafts' colour.</summary>
    public Vector3 BeamColour { get; init; } = Vector3.One;

    /// <summary>The shaft light per board cell (2 units a cell over the opening), for the dust that drifts in it.</summary>
    public MoonfallPlane? BeamAmount { get; init; }

    /// <summary>The open sky (white, alpha where Fever lifts it toward the carrier's colour; none under the framing).</summary>
    public required MoonfallRgba SkyMask { get; init; }

    /// <summary>The framing in front of the moon (alpha its coverage), drawn over the moon Fever swells; null without a moon.</summary>
    public MoonfallRgba? MoonFront { get; init; }

    public MoonfallMoon? Moon { get; init; }

    public IReadOnlyList<MoonfallFirefly> Fireflies { get; init; } = [];

    public Vector3 FireflyColour { get; init; } = MoonfallColor.Hex("#FFD27A");

    public IReadOnlyList<MoonfallStar> Stars { get; init; } = [];

    public IReadOnlyList<MoonfallFlicker> Flickers { get; init; } = [];

    public IReadOnlyList<MoonfallMistLayer> Mist { get; init; } = [];

    /// <summary>The glints running along their tracks.</summary>
    public IReadOnlyList<MoonfallGlintTrack> Glints { get; init; } = [];

    /// <summary>The fireflies' halo colour.</summary>
    public Vector3 FireflyHaloColour { get; init; } = MoonfallColor.Hex("#FFB45E");

    /// <summary>How many dust motes drift in the beams.</summary>
    public int Dust { get; init; }

    /// <summary>The veil's strength round each round peg, drawn in play under the live pegs (<see cref="MoonfallVeil"/>).</summary>
    public float VeilK { get; init; }

    /// <summary>The pieces' clearance the scene was dressed against (the motion keeps its particles clear with it).</summary>
    public MoonfallClearance? Clearance { get; init; }

    /// <summary>The rails' enamel and the margins' palette.</summary>
    public MoonfallChromePalette Chrome { get; init; } = MoonfallChromePalette.Medallion;

    /// <summary>The fuller-board checks, when the build ran them (tests, Debug builds).</summary>
    public MoonfallFramingReport? Report { get; init; }

    /// <summary>Framing elements and lights dropped to keep clear of the pieces.</summary>
    public int Dropped { get; init; }

    /// <summary>How long the build took on the CPU.</summary>
    public TimeSpan Cost { get; init; }

    /// <summary>The build's stages and their milliseconds, for the cost report.</summary>
    public IReadOnlyList<(string Stage, double Ms)> Timings { get; init; } = [];

    /// <summary>The textures' bytes on the GPU together.</summary>
    public long Bytes => Base.Bytes + Backdrop.Bytes + SkyMask.Bytes + (BeamsA?.Bytes ?? 0) + (BeamsB?.Bytes ?? 0) + (MoonFront?.Bytes ?? 0)
        + Mist.Sum(static m => m.Tile.Bytes);
}

/// <summary>
/// Builds a level's scene from its recipe (spec-rich2.md §6, level-method.md §8), once per scene at load, off the
/// framework thread: the painting cut to the board and night-graded, the paint layers, the vignette and grain, the
/// jewel palette (lightness kept), the light, the framing silhouettes (each element dropped that would come within 6
/// units of a piece, and a hard clamp behind that), the small lights (8 units clear), the veil, and the small layers the
/// in-play drawing moves. Pure: the caller hands in the painting's pixels (a game texture or a shipped picture) and
/// takes the RGBA layers to the GPU.
/// </summary>
public static partial class MoonfallSceneBuilder
{
    /// <summary>The opening the base layer covers, board units.</summary>
    public static readonly Vector4 OpeningRect = new(75, 41, 725, 594);

    /// <summary>Pixels a unit of the small layers (beams, sky): 1 per 2 units.</summary>
    public const float LowScale = 0.5f;

    /// <summary>How far every piece keeps from a moon's disc, units (F6: the brightest face never sits behind a peg).</summary>
    public const float MoonKeep = 18f;

    /// <summary>
    /// The moving share of a moving shaft: it breathes ±30% round the still (twice round 1's ±15%, which could not be seen:
    /// game designer runtime round 1, m3), so its texture holds 60% of the shaft and 70% is baked.
    /// </summary>
    public const float BeamBreath = 0.30f;

    /// <summary>The least a recipe's star mask (<see cref="MoonfallMotionRecipe.StarWhere"/>) must be where a star sits.</summary>
    public const float StarMaskLeast = 0.9f;

    /// <summary>
    /// Builds <paramref name="recipe"/> over <paramref name="level"/> at <paramref name="s"/> pixels a unit (1 or 2) from
    /// <paramref name="painting"/>. A <paramref name="fallback"/> painting (a shipped picture standing in for a missing
    /// game texture) is taken whole and not graded: it is painted in the night's values already. With
    /// <paramref name="check"/> the fuller-board rules are measured and reported (<see cref="MoonfallSceneLayers.Report"/>).
    /// <paramref name="plates"/> are the recipe's plates by picture name (<see cref="PlateNames"/>), read by the caller; a
    /// plate missing from it is left out.
    /// </summary>
    public static MoonfallSceneLayers Build(MoonfallSceneRecipe recipe, MoonfallLevel level, MoonfallImage painting, int s, bool fallback = false, bool check = false,
        MoonfallClearance? clearance = null, IReadOnlyDictionary<string, MoonfallImage>? plates = null)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(painting);
        if (s is not (1 or 2))
        {
            throw new ArgumentOutOfRangeException(nameof(s), "A scene is built at 1 or 2 pixels a unit.");
        }

        var timer = Stopwatch.StartNew();
        var timings = new List<(string Stage, double Ms)>();
        var lap = Stopwatch.StartNew();
        void Lap(string stage)
        {
            timings.Add((stage, lap.Elapsed.TotalMilliseconds));
            lap.Restart();
        }

        clearance ??= MoonfallClearance.For(level);
        var ctx = new MoonfallDressContext(s, clearance) { Plates = plates };
        Lap("clearance");
        var source = !fallback && recipe.Source.Erase.Count > 0 ? Erase(painting, recipe.Source.Erase) : painting;
        var px = Cut(recipe.Source, source, s, fallback);
        if (UsesLand(recipe))
        {
            // A map's land comes from its own painting; a fallback picture has none, so a land term finds no land there.
            var cut1 = fallback ? null : s == 1 ? px.Copy() : Cut(recipe.Source, source, 1);
            var lands = new Dictionary<(float, int), MoonfallPlane>();
            ctx.Land = (threshold, grow) =>
            {
                if (!lands.TryGetValue((threshold, grow), out var land))
                {
                    land = cut1 is null ? new MoonfallPlane(ctx.W, ctx.H) : LandMask(cut1, threshold, grow, s);
                    lands[(threshold, grow)] = land;
                }

                return land;
            };
        }

        Lap("cut");
        if (!fallback && recipe.Grade is { } grade)
        {
            px = MoonfallGrade.NightLab(px, grade, s);
            Lap("grade");
        }

        foreach (var layer in recipe.Paint)
        {
            // A plate under the palette is drawn on the painting itself (a chart's engraved roads): it belongs to the place,
            // so over a fallback picture it is left out, as the spoiler shield's story-safe recipe leaves it (critic m8).
            if (fallback && layer is MoonfallPlate)
            {
                continue;
            }

            ApplyLight(ctx, px, layer, still: true, beams: null);
        }

        if (recipe.Vignette > 0)
        {
            MoonfallGrade.Vignette(px, recipe.Vignette);
        }

        if (recipe.Grain > 0)
        {
            MoonfallGrade.Grain(px, recipe.Grain, recipe.Name.Length + 13);
        }

        Lap("paint");
        if (recipe.Palette is { } palette)
        {
            px = Palette(ctx, px, palette);
            Lap("palette");
        }

        var before = check ? MoonfallGrade.Lightness(px) : null;
        var moving = new List<MoonfallShafts>();
        foreach (var layer in recipe.Light)
        {
            ApplyLight(ctx, px, layer, still: false, beams: moving);
        }

        Lap("light");

        foreach (var group in recipe.Framing)
        {
            var mask = new MoonfallPlane(ctx.W, ctx.H);
            foreach (var shape in group.Shapes)
            {
                switch (shape)
                {
                    case MoonfallFrond f:
                        MoonfallDress.Frond(ctx, mask, f);
                        break;
                    case MoonfallTrunk t:
                        MoonfallDress.Trunk(ctx, mask, t);
                        break;
                    case MoonfallPines p:
                        MoonfallDress.Pines(ctx, mask, p);
                        break;
                    case MoonfallOutcrop o:
                        MoonfallDress.Outcrop(ctx, mask, o);
                        break;
                    case MoonfallRock r:
                        MoonfallDress.Rock(ctx, mask, r);
                        break;
                    case MoonfallRope r:
                        MoonfallDress.Rope(ctx, mask, r);
                        break;
                }
            }

            Clamp(ctx, mask);
            MoonfallDress.Silhouette(ctx, px, mask, group);
            foreach (var crystal in group.Shapes.OfType<MoonfallCrystal>())
            {
                MoonfallDress.Crystal(ctx, px, crystal);
            }
        }

        // Whatever drew it (a crystal, a placed trunk), no framing within 6 units of a piece.
        Clamp(ctx, ctx.Cover);
        Lap("framing");
        foreach (var light in recipe.Lights)
        {
            MoonfallDress.Points(ctx, px, light);
        }

        var fireflies = recipe.Fireflies is { } ff ? Fireflies(clearance, ff) : [];
        Lap("lights");
        MoonfallFramingReport? report = null;
        if (check)
        {
            var lights = new List<Vector3>(ctx.Lights);
            lights.AddRange(fireflies.Select(static f => new Vector3(f.X, f.Y, 4.5f * f.Size * 2.2f)));
            report = MoonfallFramingCheck.Check(ctx.Cover, ctx.Rim, lights, clearance, s, before, MoonfallGrade.Lightness(px));
            Lap("check");
        }

        var backdrop = Backdrop(px, s, recipe.Chrome);
        // The veil is not baked: every piece carries its own as sprites drawn under it in play (MoonfallVeil), so it goes
        // with the piece when it clears and moves with it on a mover's path.
        Lap("veil");

        var flickers = new List<MoonfallFlicker>();
        var phase = 0.37f;
        foreach (var l in recipe.Lights)
        {
            if (l.Flicker && ctx.Clear(l.X, l.Y) >= 8 + (l.Halo * l.Size * 0.6f))
            {
                flickers.Add(new MoonfallFlicker(l.X, l.Y, l.Halo * l.Size, l.Colour, phase));
                phase += 1.7f;
            }
        }

        var (beamsA, beamsB, amount) = Beams(moving, clearance, ctx.Cover);
        Lap("beams");
        var baseLayer = Crop(px, s, OpeningRect, alpha: null);
        var sky = SkyMask(ctx);
        var feverMoon = recipe.Moon is { } m0 && ctx.Clear(m0.X, m0.Y) >= m0.R + MoonKeep ? m0 : null;
        var moonFront = feverMoon is { } moon && Covered(ctx, MoonRect(moon)) is { } front ? Crop(px, s, front, ctx.Cover) : null;
        var stars = Stars(px, ctx, recipe);
        var glints = recipe.Motion.Glints.Select(static g => new MoonfallGlintTrack(g)).ToList();
        var mist = Mist(recipe.Motion, clearance);
        Lap("layers");
        timer.Stop();
        return new MoonfallSceneLayers
        {
            Name = recipe.Name,
            Scale = s,
            Base = baseLayer,
            Backdrop = backdrop,
            BeamsA = beamsA,
            BeamsB = beamsB,
            BeamColour = moving.Count > 0 ? moving[0].Colour : Vector3.One,
            BeamAmount = amount,
            SkyMask = sky,
            MoonFront = moonFront,
            Moon = feverMoon,
            VeilK = recipe.Veil,
            Fireflies = fireflies,
            FireflyColour = recipe.Fireflies?.Colour ?? MoonfallColor.Hex("#FFD27A"),
            FireflyHaloColour = recipe.Fireflies?.HaloColour ?? MoonfallColor.Hex("#FFB45E"),
            Glints = glints,
            Stars = stars,
            Flickers = flickers,
            Mist = mist,
            Dust = moving.Count > 0 ? recipe.Motion.Dust : 0,
            Clearance = clearance,
            Chrome = recipe.Chrome,
            Report = report,
            Dropped = ctx.Dropped,
            Cost = timer.Elapsed,
            Timings = timings,
        };
    }

    /// <summary>The moon Fever swells and the framing round it, board units.</summary>
    public static Vector4 MoonRect(MoonfallMoon moon)
    {
        ArgumentNullException.ThrowIfNull(moon);
        var r = moon.R * 3.0f;
        return new Vector4(MathF.Max(OpeningRect.X, moon.X - r), MathF.Max(OpeningRect.Y, moon.Y - r), MathF.Min(OpeningRect.Z, moon.X + r), MathF.Min(OpeningRect.W, moon.Y + r));
    }

    /// <summary>
    /// The part of <paramref name="board"/> (units) the framing covers, to whole pixels; null when it covers none of it. The
    /// moon's front layer is only the framing in front of the moon, so it is cut to that (no texture where nothing stands).
    /// </summary>
    private static Vector4? Covered(MoonfallDressContext ctx, Vector4 board)
    {
        int x0 = (int)MathF.Floor(board.X * ctx.S), y0 = (int)MathF.Floor(board.Y * ctx.S);
        int x1 = (int)MathF.Ceiling(board.Z * ctx.S), y1 = (int)MathF.Ceiling(board.W * ctx.S);
        int lx = int.MaxValue, ly = int.MaxValue, hx = -1, hy = -1;
        for (var y = y0; y < y1; y++)
        {
            for (var x = x0; x < x1; x++)
            {
                if (ctx.Cover.Data[(y * ctx.W) + x] > 0.5f / 255f)
                {
                    (lx, ly, hx, hy) = (Math.Min(lx, x), Math.Min(ly, y), Math.Max(hx, x), Math.Max(hy, y));
                }
            }
        }

        return hx < 0 ? null : new Vector4(lx / ctx.S, ly / ctx.S, (hx + 1) / ctx.S, (hy + 1) / ctx.S);
    }

    /// <summary>The painting mirrored, padded, cropped and resampled to the board (scene_official.build), or taken whole as a fallback.</summary>
    public static MoonfallImage Cut(MoonfallSceneSource source, MoonfallImage painting, int s, bool whole = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(painting);
        int w = 800 * s, h = 600 * s;
        if (whole || source.Crop is null)
        {
            return painting.Width == w && painting.Height == h ? painting.Copy() : MoonfallFilters.Resize(painting, w, h);
        }

        var src = source.Mirror ? painting.MirrorX() : painting;
        if (source.PadTop > 0 || source.PadLeft > 0)
        {
            src = Pad(src, source.PadTop, source.PadLeft, source.PadReflect);
        }

        var (x, y, cw, ch) = source.Crop.Value;
        var cut = MoonfallFilters.Resample(src, x + source.PadLeft, y + source.PadTop, cw, ch, w, h);
        cut.A = null;
        return cut;
    }

    private static MoonfallImage Pad(MoonfallImage src, int top, int left, bool reflect)
    {
        int w = src.Width + left, h = src.Height + top;
        var out_ = new MoonfallImage(w, h);
        foreach (var (from, to) in new[] { (src.R, out_.R), (src.G, out_.G), (src.B, out_.B) })
        {
            for (var y = 0; y < h; y++)
            {
                var sy = y - top;
                sy = sy >= 0 ? sy : reflect ? Math.Min(-sy, src.Height - 1) : 0;
                for (var x = 0; x < w; x++)
                {
                    var sx = x - left;
                    sx = sx >= 0 ? sx : reflect ? Math.Min(-sx, src.Width - 1) : 0;
                    to.Data[(y * w) + x] = from.Data[(sy * src.Width) + sx];
                }
            }
        }

        return out_;
    }

    private static MoonfallImage Palette(MoonfallDressContext ctx, MoonfallImage px, MoonfallPaletteRecipe p)
    {
        MoonfallPlane? lightness = null;
        MoonfallPlane Lightness() => lightness ??= MoonfallGrade.Lightness(px);
        MoonfallPlane? where = p.Where.Count > 0 ? Mask(ctx, p.Where, 1f, Lightness) : null;
        if (p.Spare.Count > 0)
        {
            // The spared places' union (the largest at each pixel), taken out of where the palette applies.
            var union = new MoonfallPlane(ctx.W, ctx.H);
            foreach (var spare in p.Spare)
            {
                var m = Mask(ctx, spare, 1f, Lightness);
                for (var i = 0; i < union.Data.Length; i++)
                {
                    union.Data[i] = MathF.Max(union.Data[i], m.Data[i]);
                }
            }

            where ??= MoonfallPlane.Filled(ctx.W, ctx.H, 1f);
            for (var i = 0; i < where.Data.Length; i++)
            {
                where.Data[i] *= 1 - union.Data[i];
            }
        }

        var grade = new MoonfallJewelGrade
        {
            Bands = p.Bands,
            ValueHues = p.ValueHues,
            Mix = p.Mix,
            Chroma = p.Chroma,
            Floor = p.Floor,
            Keep = p.Keep,
            KeepHigh = p.KeepHigh,
            Regions = p.Regions.Select(r => new MoonfallJewelRegion(Mask(ctx, r.Where, r.Weight, Lightness), r.Hue, r.Chroma)).ToList(),
            Mask = where,
        };
        return MoonfallGrade.Jewel(px, grade, ctx.S);
    }

    private static void ApplyLight(MoonfallDressContext ctx, MoonfallImage px, MoonfallLightRecipe layer, bool still, List<MoonfallShafts>? beams)
    {
        switch (layer)
        {
            case MoonfallShafts sh:
                var moving = sh.Moving && !still && beams is not null;
                if (moving)
                {
                    beams!.Add(sh);
                }

                // Its still share is in a plate already: only the beams are made.
                if (moving && sh.BeamsOnly)
                {
                    break;
                }

                // A moving shaft is baked at the low end of its breath; the beams layer adds the rest in play.
                var amount = MoonfallDress.ShaftAmount(sh, ctx.S, ctx.W, ctx.H, Vector2.Zero);
                MoonfallDress.ScreenScaled(px, sh.Colour, amount, moving ? 1 - BeamBreath : 1);
                break;
            case MoonfallTone t:
                Tone(ctx, px, t);
                break;
            case MoonfallPlate plate:
                Plate(ctx, px, plate);
                break;
            case MoonfallGlow g:
                MoonfallDress.Glow(px, ctx.S, g);
                break;
            case MoonfallMoonGlow mg:
                MoonfallGrade.MoonGlow(px, mg.X * ctx.S, mg.Y * ctx.S, mg.RCore * ctx.S, mg.RWide * ctx.S, mg.KCore, mg.KWide, mg.Colour);
                break;
            case MoonfallMoon m:
                // A moon is the board's brightest face: no piece within its radius and 18 units (F6), else it is left out.
                if (ctx.Clear(m.X, m.Y) < m.R + MoonKeep)
                {
                    ctx.Dropped++;
                    break;
                }

                MoonfallDress.Moon(px, ctx.S, m);
                break;
            case MoonfallAurora a:
                MoonfallDress.Aurora(px, ctx.S, a);
                break;
            case MoonfallNebula n:
                MoonfallDress.Nebula(px, ctx.S, n);
                break;
            case MoonfallCompassRose c:
                MoonfallDress.CompassRose(ctx, px, c);
                break;
            case MoonfallNeatline n:
                MoonfallDress.Neatline(ctx, px, n);
                break;
            case MoonfallRoute r:
                MoonfallDress.Route(ctx, px, r);
                break;
        }
    }

    /// <summary>No framing within 6.5 units of a piece's edge, feathered over the next unit (F3a by construction).</summary>
    private static void Clamp(MoonfallDressContext ctx, MoonfallPlane mask)
    {
        MoonfallFilters.Chunks(mask.Data.Length, (lo, hi) =>
        {
            for (var i = lo; i < hi; i++)
            {
                var d = ctx.ClearanceAtS.Data[i];
                if (d < 7.5f)
                {
                    mask.Data[i] *= MoonfallColor.Smooth(6.5f, 7.5f, d);
                }
            }
        });
    }

    /// <summary>
    /// The moving shafts at two places of their drift, at 1 pixel per 2 units over the opening, masked off every piece and
    /// kept behind the framing (<paramref name="cover"/>, at the scene's own scale): light from the sky never crosses a
    /// silhouette in front of it.
    /// </summary>
    private static (MoonfallRgba? A, MoonfallRgba? B, MoonfallPlane? Amount) Beams(List<MoonfallShafts> moving, MoonfallClearance clearance, MoonfallPlane cover)
    {
        if (moving.Count == 0)
        {
            return (null, null, null);
        }

        var full = (int)(800 * LowScale);
        var tall = (int)(600 * LowScale);
        var a = new MoonfallPlane(full, tall);
        var b = new MoonfallPlane(full, tall);
        foreach (var sh in moving)
        {
            var pa = MoonfallDress.ShaftAmount(sh, LowScale, full, tall, Vector2.Zero);
            var pb = MoonfallDress.ShaftAmount(sh, LowScale, full, tall, new Vector2(-10, 0));
            for (var i = 0; i < a.Data.Length; i++)
            {
                a.Data[i] += pa.Data[i];
                b.Data[i] += pb.Data[i];
            }
        }

        // Nothing moves within 2.5 units of a piece: the mask is zero out to 2.5 units plus a texel and the filter's reach.
        var mask = MotionMask(clearance, LowScale);
        var behind = Behind(cover, full, tall);
        for (var i = 0; i < a.Data.Length; i++)
        {
            a.Data[i] *= mask.Data[i] * behind.Data[i];
            b.Data[i] *= mask.Data[i] * behind.Data[i];
        }

        var amount = a.Copy();
        return (WhiteAlpha(a, 2 * BeamBreath, LowScale), WhiteAlpha(b, 2 * BeamBreath, LowScale), amount);
    }

    /// <summary>One less the framing's coverage at the beams' scale (the most of it in each cell), so no beam shows over a silhouette.</summary>
    private static MoonfallPlane Behind(MoonfallPlane cover, int width, int height)
    {
        var step = cover.Width / width;
        var plane = new MoonfallPlane(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var c = 0f;
                for (var dy = 0; dy < step; dy++)
                {
                    for (var dx = 0; dx < step; dx++)
                    {
                        c = MathF.Max(c, cover.Data[(((y * step) + dy) * cover.Width) + (x * step) + dx]);
                    }
                }

                plane.Data[(y * width) + x] = 1 - c;
            }
        }

        return plane;
    }

    /// <summary>
    /// Where ambient motion may show, at <paramref name="s"/> pixels a unit: 0 within <see cref="MoonfallMotion.PegKeepOut"/>
    /// of a piece plus two texels (so a bilinear sample never reaches in), rising to 1 three units further.
    /// </summary>
    public static MoonfallPlane MotionMask(MoonfallClearance clearance, float s)
    {
        ArgumentNullException.ThrowIfNull(clearance);
        var plane = clearance.ToPlane(s);
        var inner = MoonfallMotion.PegKeepOut + (2f / s);
        for (var i = 0; i < plane.Data.Length; i++)
        {
            plane.Data[i] = MoonfallColor.Smooth(inner, inner + 3f, plane.Data[i]);
        }

        return plane;
    }

    private static MoonfallRgba WhiteAlpha(MoonfallPlane alpha, float scale, float s)
    {
        var x0 = (int)(OpeningRect.X * s);
        var y0 = (int)(OpeningRect.Y * s);
        var x1 = (int)MathF.Ceiling(OpeningRect.Z * s);
        var y1 = (int)MathF.Ceiling(OpeningRect.W * s);
        int w = x1 - x0, h = y1 - y0;
        var bytes = new byte[w * h * 4];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var o = ((y * w) + x) * 4;
                bytes[o] = bytes[o + 1] = bytes[o + 2] = 255;
                bytes[o + 3] = MoonfallImage.ToByte(alpha.Data[((y0 + y) * alpha.Width) + x0 + x] * scale);
            }
        }

        return new MoonfallRgba(w, h, bytes, new Vector4(x0 / s, y0 / s, x1 / s, y1 / s));
    }

    private static MoonfallRgba SkyMask(MoonfallDressContext ctx)
    {
        var w = (int)(800 * LowScale);
        var h = (int)(600 * LowScale);
        var sky = new MoonfallPlane(w, h);
        var step = (int)(ctx.S / LowScale);
        for (var y = 0; y < h; y++)
        {
            var Y = (y + 0.5f) / LowScale;
            for (var x = 0; x < w; x++)
            {
                var cover = 0f;
                for (var dy = 0; dy < step; dy++)
                {
                    for (var dx = 0; dx < step; dx++)
                    {
                        cover = MathF.Max(cover, ctx.Cover.Data[(((y * step) + dy) * ctx.W) + (x * step) + dx]);
                    }
                }

                sky.Data[(y * w) + x] = MoonfallColor.Smooth(360f, 220f, Y) * (1 - cover);
            }
        }

        return WhiteAlpha(sky, 1f, LowScale);
    }

    private static MoonfallRgba Crop(MoonfallImage px, int s, Vector4 board, MoonfallPlane? alpha)
    {
        var x0 = (int)MathF.Floor(board.X * s);
        var y0 = (int)MathF.Floor(board.Y * s);
        var x1 = (int)MathF.Ceiling(board.Z * s);
        var y1 = (int)MathF.Ceiling(board.W * s);
        int w = x1 - x0, h = y1 - y0;
        var bytes = new byte[w * h * 4];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var i = ((y0 + y) * px.Width) + x0 + x;
                var o = ((y * w) + x) * 4;
                bytes[o] = MoonfallImage.ToByte(px.R.Data[i]);
                bytes[o + 1] = MoonfallImage.ToByte(px.G.Data[i]);
                bytes[o + 2] = MoonfallImage.ToByte(px.B.Data[i]);
                bytes[o + 3] = alpha is null ? (byte)255 : MoonfallImage.ToByte(alpha.Data[i]);
            }
        }

        return new MoonfallRgba(w, h, bytes, new Vector4(x0 / (float)s, y0 / (float)s, x1 / (float)s, y1 / (float)s));
    }

    /// <summary>The whole board, small (100 × 75) and softened: drawn stretched, it is the blurred scene in the margins.</summary>
    private static MoonfallRgba Backdrop(MoonfallImage px, int s, MoonfallChromePalette chrome)
    {
        // play2.window: the margins carry the whole board blurred (the scene in its opening, the enamel rails round it),
        // so the rails' lapis is painted in where the frame stands, lit from above as the plugin lights it.
        const int W = 100, H = 75;
        var step = 8 * s;
        var small = new MoonfallImage(W, H);
        // The rails as the eye averages them: the lapis enamel and the gilt of the frame and its bands (play2.window blurs
        // the whole composite, chrome and all, so the margins read warm grey-blue, not black).
        var gilt = MoonfallColor.Hex("#C9A04A");
        var channels = new[] { (px.R, small.R, chrome.Sky.X, gilt.X), (px.G, small.G, chrome.Sky.Y, gilt.Y), (px.B, small.B, chrome.Sky.Z, gilt.Z) };
        foreach (var (from, to, enamel, gold) in channels)
        {
            for (var y = 0; y < H; y++)
            {
                for (var x = 0; x < W; x++)
                {
                    var sum = 0f;
                    for (var dy = 0; dy < step; dy++)
                    {
                        var by = ((y * step) + dy + 0.5f) / s;
                        var light = 1.12f * (1f - (0.268f * by / 600f));
                        for (var dx = 0; dx < step; dx++)
                        {
                            var bx = ((x * step) + dx + 0.5f) / s;
                            var inRail = bx < MoonfallFramingCheck.WallL || bx > MoonfallFramingCheck.WallR || by < MoonfallFramingCheck.Top || by > MoonfallFramingCheck.Foot;
                            sum += inRail ? (0.6f * enamel * 1.4f * light) + (0.4f * gold) : from.Data[(((y * step) + dy) * px.Width) + (x * step) + dx];
                        }
                    }

                    to.Data[(y * W) + x] = sum / (step * step);
                }
            }
        }

        small = MoonfallFilters.Blur(small, 1.2f);
        return new MoonfallRgba(W, H, small.ToRgba(), new Vector4(0, 0, 800, 600));
    }

    /// <summary>
    /// dress2.firefly_spots: fireflies over <see cref="MoonfallFireflies.Region"/> where their whole wander (9 units across,
    /// 4 up and down) and their halo keep 8 units from every piece (F5 in motion), never a row: at most three in any
    /// 40-unit band of height, 30 or more apart, picked from a shuffled (not ranked) list of the clear spots.
    /// </summary>
    public static IReadOnlyList<MoonfallFirefly> Fireflies(MoonfallClearance clearance, MoonfallFireflies f)
    {
        ArgumentNullException.ThrowIfNull(clearance);
        ArgumentNullException.ThrowIfNull(f);
        var rng = new MoonfallNoise.Random(f.Seed);
        var cands = new List<(float X, float Y, float S)>();
        for (var y = (int)f.Region.Y; y < (int)f.Region.W; y += 5)
        {
            for (var x = (int)f.Region.X; x < (int)f.Region.Z; x += 5)
            {
                var size = 0.7f + (0.3f * rng.Next());
                // Clear of the pieces round their whole wander, and of the launcher's swing.
                if (MoonfallMotion.FireflyClearance(clearance, x, y, size, 12) >= 0 && Vector2.Distance(new Vector2(x, y), MoonfallFramingCheck.Pivot) >= MoonfallRibbons.SwingReach)
                {
                    cands.Add((x, y, size));
                }
            }
        }

        // Shuffled, not ranked: a ranked pick lines up on the clearest row (round 2, GD N1).
        for (var i = cands.Count - 1; i > 0; i--)
        {
            var j = rng.Int(0, i + 1);
            (cands[i], cands[j]) = (cands[j], cands[i]);
        }

        var picked = new List<MoonfallFirefly>();
        foreach (var (x, y, size) in cands)
        {
            if (picked.Count >= f.Count)
            {
                break;
            }

            var band = picked.Count(p => MathF.Abs(p.Y - y) < 20);
            if (band >= 3 || picked.Any(p => ((p.X - x) * (p.X - x)) + ((p.Y - y) * (p.Y - y)) < 30 * 30))
            {
                continue;
            }

            var fx = x + rng.Range(-2, 2);
            var fy = y + rng.Range(-2, 2);
            // The jittered place is checked again round its whole wander, at 24 points.
            if (MoonfallMotion.FireflyClearance(clearance, fx, fy, size, 24) < 0)
            {
                continue;
            }

            picked.Add(new MoonfallFirefly(fx, fy, size, rng.Range(0, 2 * MathF.PI), rng.Next() < 0.5f ? 2f : 3f));
        }

        return picked;
    }

    /// <summary>
    /// The discs no star may sit in (x, y, radius), board units: every moon the recipe paints or names for Fever, out to
    /// half its radius again or 8 units, whichever is more (game designer runtime round 1, M1).
    /// </summary>
    public static IReadOnlyList<Vector3> MoonDiscs(MoonfallSceneRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        var moons = recipe.Paint.Concat(recipe.Light).OfType<MoonfallMoon>().ToList();
        if (recipe.Moon is { } m)
        {
            moons.Add(m);
        }

        return moons.Select(static m => new Vector3(m.X, m.Y, MathF.Max(m.R * 1.5f, m.R + 8))).Distinct().ToList();
    }

    /// <summary>
    /// A mask (<see cref="MoonfallMaskTerm"/>s) over <paramref name="px"/> at <paramref name="s"/> pixels a unit, against
    /// <paramref name="clearance"/>; a <c>land</c> term finds no land here (the tests read a star mask with it).
    /// </summary>
    public static MoonfallPlane EvaluateMask(IReadOnlyList<MoonfallMaskTerm> terms, MoonfallClearance clearance, MoonfallImage px, int s)
    {
        ArgumentNullException.ThrowIfNull(terms);
        ArgumentNullException.ThrowIfNull(clearance);
        ArgumentNullException.ThrowIfNull(px);
        var ctx = new MoonfallDressContext(s, clearance);
        return Mask(ctx, terms, 1f, () => MoonfallGrade.Lightness(px));
    }

    /// <summary>
    /// The scene's own bright points in the sky (title_loop.star_points): local maxima, clear of framing and 8 units from
    /// pieces, inside the recipe's sky (<see cref="MoonfallMotionRecipe.StarWhere"/>) and out of every moon's disc.
    /// </summary>
    private static IReadOnlyList<MoonfallStar> Stars(MoonfallImage px, MoonfallDressContext ctx, MoonfallSceneRecipe recipe)
    {
        var motion = recipe.Motion;
        if (motion.Stars <= 0)
        {
            return [];
        }

        var sky = motion.StarWhere.Count > 0 ? Mask(ctx, motion.StarWhere, 1f, () => MoonfallGrade.Lightness(px)) : null;
        var moons = MoonDiscs(recipe);

        var luma = new MoonfallPlane(px.Width, px.Height);
        for (var i = 0; i < luma.Data.Length; i++)
        {
            luma.Data[i] = MoonfallColor.Luma(px.R.Data[i], px.G.Data[i], px.B.Data[i]);
        }

        var local = MoonfallFilters.Blur(luma, 1.5f * ctx.S);
        var cands = new List<(float X, float Y, float Loc)>();
        var r = motion.StarRegion;
        for (var y = Math.Max(1, (int)(r.Y * ctx.S)); y < Math.Min(ctx.H - 1, (int)(r.W * ctx.S)); y++)
        {
            for (var x = Math.Max(1, (int)(r.X * ctx.S)); x < Math.Min(ctx.W - 1, (int)(r.Z * ctx.S)); x++)
            {
                var i = (y * ctx.W) + x;
                var v = luma.Data[i];
                var loc = v - local.Data[i];
                if (loc < 0.035f || v < 0.12f || ctx.Cover.Data[i] > 0.05f)
                {
                    continue;
                }

                if (v < luma.Data[i - 1] || v < luma.Data[i + 1] || v < luma.Data[i - ctx.W] || v < luma.Data[i + ctx.W])
                {
                    continue;
                }

                float X = (x + 0.5f) / ctx.S, Y = (y + 0.5f) / ctx.S;
                if (ctx.Clear(X, Y) < MoonfallMotion.ParticleKeepOut + 2 || (sky is not null && sky.Data[i] < StarMaskLeast)
                    || moons.Any(m => Vector2.Distance(new Vector2(X, Y), new Vector2(m.X, m.Y)) < m.Z))
                {
                    continue;
                }

                cands.Add((X, Y, loc));
            }
        }

        var rng = new MoonfallNoise.Random(ctx.W + motion.Stars);
        var stars = new List<MoonfallStar>();
        while (stars.Count < motion.Stars && cands.Count > 0)
        {
            var k = rng.Int(0, cands.Count);
            var (x, y, _) = cands[k];
            cands.RemoveAt(k);
            if (stars.Any(s => MathF.Abs(s.X - x) < 6 && MathF.Abs(s.Y - y) < 6))
            {
                continue;
            }

            stars.Add(new MoonfallStar(x, y, 1.1f, rng.Range(0, 2 * MathF.PI), rng.Next() < 0.5f ? 2f : 3f));
        }

        return stars;
    }

    /// <summary>The mist layers whose band keeps clear of every piece (a band near one is left out: nothing moves near a peg).</summary>
    private static IReadOnlyList<MoonfallMistLayer> Mist(MoonfallMotionRecipe motion, MoonfallClearance clearance)
    {
        var layers = new List<MoonfallMistLayer>();
        foreach (var m in motion.Mist)
        {
            if (!MoonfallMotion.MistBandClear(clearance, m.Y0, m.Y1))
            {
                continue;
            }

            var tileW = (int)(650 * LowScale);
            var tileH = Math.Max(2, (int)MathF.Ceiling((m.Y1 - m.Y0) * LowScale));
            var noise = MoonfallNoise.FbmPeriodic(tileW, tileH, m.Cell * LowScale, 4, m.Seed);
            var bytes = new byte[tileW * tileH * 4];
            for (var y = 0; y < tileH; y++)
            {
                // Feathered at the band's top and foot.
                var edge = MathF.Min(MoonfallColor.Smooth(0, 3, y), MoonfallColor.Smooth(tileH, tileH - 3, y + 1));
                for (var x = 0; x < tileW; x++)
                {
                    var o = ((y * tileW) + x) * 4;
                    bytes[o] = bytes[o + 1] = bytes[o + 2] = 255;
                    bytes[o + 3] = MoonfallImage.ToByte(MoonfallColor.Smooth(0.45f, 0.8f, noise.Data[(y * tileW) + x]) * edge);
                }
            }

            layers.Add(new MoonfallMistLayer(new MoonfallRgba(tileW, tileH, bytes, new Vector4(75, m.Y0, 725, m.Y1)), m.Speed, m.Alpha, m.Colour));
        }

        return layers;
    }
}
