using System.Numerics;
using Tsukimichi.Core.Evaluation;

namespace Tsukimichi.Core.Ui;

/// <summary>
/// A star's depth (docs/design/v7/ui/spec.md §3.1). There is no parallax (the layout is static): depth is size, alpha
/// and halo alone.
/// </summary>
public enum StarLayer : byte
{
    /// <summary>60 %: a 1 × 1 px dot at .10–.16 (a soft r .6 disc while the sky drifts), never twinkling.</summary>
    Far,

    /// <summary>32 %: a disc r .95 at .20–.28; one in three twinkles ×1.18 / ×.82.</summary>
    Mid,

    /// <summary>8 %: a disc r 1.35 at .52–.60 with a fixed 5 px cross at .4 and a soft r 3.4 halo; every one twinkles ×1.28 / ×.72.</summary>
    Near,
}

/// <summary>A star's colour (spec §3.2). Far stars are cool or moon white only.</summary>
public enum StarTemperature : byte
{
    /// <summary><c>#DCE5FF</c>, 64 %.</summary>
    Cool,

    /// <summary><c>#F4F2EA</c>, 24 %.</summary>
    Moon,

    /// <summary><c>#FFE2A8</c>, 9 % (mid and near only).</summary>
    Gold,

    /// <summary><c>#FFC9AE</c>, 3 % (mid and near only).</summary>
    Ember,
}

/// <summary>
/// A star in its field's unit square: <paramref name="U"/> across (0 = left, wrapping at 1), <paramref name="V"/> down
/// (0 = top), its <paramref name="Layer"/> and <paramref name="Temperature"/>, its base <paramref name="Alpha"/>, and its
/// twinkle: a <paramref name="Period"/> of 7–13 s (0 for a star that never twinkles) at <paramref name="Phase"/> (0..1).
/// </summary>
public readonly record struct Star(float U, float V, StarLayer Layer, StarTemperature Temperature, float Alpha, float Period, float Phase)
{
    /// <summary>Whether the star breathes (spec §3.3): every near star and one mid star in three.</summary>
    public bool Twinkles => Period > 0f;
}

/// <summary>One expansion's sky band on a path: its rows (header first) and its pre-seeded stars.</summary>
/// <param name="Expansion">The band's expansion.</param>
/// <param name="FirstRow">Index of its first row in the path's rows.</param>
/// <param name="RowCount">Rows in the band.</param>
/// <param name="Seed"><see cref="StarField.Seed"/> of the band.</param>
/// <param name="Stars">Its stars, in the band's unit square.</param>
public sealed record StarBand(byte Expansion, int FirstRow, int RowCount, int Seed, IReadOnlyList<Star> Stars);

/// <summary>A star placed for this frame (<see cref="StarField.Place"/>): where, how bright (twinkle and edge fade included), and how it draws.</summary>
public readonly record struct PlacedStar(Vector2 Position, float Alpha, StarLayer Layer, StarTemperature Temperature);

/// <summary>
/// What a sky rect needs to place its stars this frame, in screen px. The field is laid over <paramref name="CanvasMin"/>..
/// <paramref name="CanvasMax"/> (the pane it belongs to, so a sky that grows or shrinks uncovers stars that were always
/// there instead of stretching them) and shows only inside <paramref name="SkyMin"/>..<paramref name="SkyMax"/> (empty
/// sky, never under text) less <paramref name="Inset"/> on every side. <paramref name="Offset"/> is the drift: the whole
/// field moves left by it, wrapping on a tile as wide as the canvas. Stars fade over <paramref name="Fade"/> px at the
/// sky's left and right edges, so nothing pops in or out as it drifts. <paramref name="Time"/> is the twinkle clock, and
/// <paramref name="Twinkle"/> whether it breathes at all. While <paramref name="Drifting"/>, x is left unrounded so the
/// sky glides instead of stepping a pixel at a time.
/// </summary>
public readonly record struct SkyView(
    Vector2 CanvasMin,
    Vector2 CanvasMax,
    Vector2 SkyMin,
    Vector2 SkyMax,
    float Inset,
    float Offset,
    float Fade,
    double Time,
    bool Twinkle,
    bool Drifting)
{
    /// <summary>The left edge stars may sit on: the sky's, inset.</summary>
    public float Left => SkyMin.X + Inset;

    /// <summary>The right edge stars may sit on.</summary>
    public float Right => SkyMax.X - Inset;

    /// <summary>The top edge stars may sit on.</summary>
    public float Top => SkyMin.Y + Inset;

    /// <summary>The bottom edge stars may sit on.</summary>
    public float Bottom => SkyMax.Y - Inset;

    /// <summary>Whether any star can show: the inset sky and the canvas both have area.</summary>
    public bool IsOpen => Right > Left && Bottom > Top && CanvasMax.X > CanvasMin.X && CanvasMax.Y > CanvasMin.Y;
}

/// <summary>
/// The Full sky's stars (docs/design/v7/ui/spec.md §3 and Revision 3; path-section proposal §4.3 for the Path chart's
/// bands). A field is seeded (a 31-bit linear congruential generator, as the mockup's), so the same seed always yields
/// the same sky and it never reshuffles: position and depth come from the first stream, exactly as 1.13 drew them, and
/// colour, brightness and twinkle from a second stream of the same seed, so a longer field keeps every star of a
/// shorter one. Placement per frame (<see cref="Place"/>) is pure arithmetic over a cached array and allocates nothing.
/// </summary>
public static class StarField
{
    /// <summary>The seed multiplier (a prime).</summary>
    public const int SeedPrime = 7919;

    /// <summary>Logical px² of sky per star.</summary>
    public const float AreaPerStar = 1400f;

    public const int MinStars = 6;
    public const int MaxStars = 48;

    /// <summary>The chart width the density is reckoned at (the card's inner width at the 360 px column).</summary>
    public const float NominalWidth = 324f;

    /// <summary>The layers' shares (spec §3.1): far 60 %, mid 32 %, near the remaining 8 %.</summary>
    public const float FarShare = 0.60f;

    public const float MidShare = 0.32f;

    /// <summary>The twinkle's period range in seconds (spec §3.3): slower than any interface motion.</summary>
    public const float MinPeriod = 7f;

    public const float MaxPeriod = 13f;

    /// <summary>The twinkle's swing: a near star breathes ×1.28 / ×.72, a mid star ×1.18 / ×.82.</summary>
    public const float NearSwing = 0.28f;

    public const float MidSwing = 0.18f;

    /// <summary>The share of mid stars that twinkle (one in three).</summary>
    public const float MidTwinkleShare = 1f / 3f;

    /// <summary>A band's seed: <c>expansionId * 7919 + rowCount</c>.</summary>
    public static int Seed(byte expansion, int rowCount) => (expansion * SeedPrime) + rowCount;

    /// <summary>Stars for a band of <paramref name="width"/> × <paramref name="height"/> logical px.</summary>
    public static int CountFor(float width, float height) => CountFor(width, height, MinStars, MaxStars);

    /// <summary>Stars for a sky of <paramref name="width"/> × <paramref name="height"/> logical px, from <paramref name="min"/> to <paramref name="max"/>.</summary>
    public static int CountFor(float width, float height, int min, int max)
    {
        var area = width * height;
        if (!float.IsFinite(area) || area <= 0f)
        {
            return min;
        }

        return Math.Clamp((int)MathF.Round(area / AreaPerStar), min, max);
    }

    /// <summary>
    /// <paramref name="count"/> stars from <paramref name="seed"/>: the same seed always yields the same stars, and a
    /// longer field starts with every star of a shorter one. U covers the whole width (the field wraps as it drifts); V
    /// stays 3 % inside the unit square.
    /// </summary>
    public static Star[] Generate(int seed, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        var stars = new Star[count];
        Fill(seed, stars);
        return stars;
    }

    /// <summary><see cref="Generate"/> into <paramref name="into"/>, without allocating.</summary>
    public static void Fill(int seed, Span<Star> into)
    {
        var state = (uint)seed & 0x7FFFFFFFu;
        var extra = SecondStream(seed);
        for (var i = 0; i < into.Length; i++)
        {
            var u = Next(ref state);
            var v = 0.03f + (0.94f * Next(ref state));
            var m = Next(ref state);
            var layer = m < FarShare ? StarLayer.Far : m < FarShare + MidShare ? StarLayer.Mid : StarLayer.Near;
            into[i] = Dress(u >= 1f ? 0f : u, v, layer, ref extra);
        }
    }

    /// <summary>
    /// The Milky Way's extra stars (spec §3.4, about 40 % more along its axis), seeded apart from the field: far stars
    /// with U along the band (0..1) and V across it (-1..1, most near the axis).
    /// </summary>
    public static Star[] Band(int seed, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        var stars = new Star[count];
        var state = SecondStream(seed ^ 0x2B0F_1D3);
        var extra = SecondStream(seed + SeedPrime);
        for (var i = 0; i < count; i++)
        {
            var u = Next(ref state);
            var across = (Next(ref state) + Next(ref state) + Next(ref state) - 1.5f) / 1.5f;
            stars[i] = Dress(u >= 1f ? 0f : u, Math.Clamp(across, -1f, 1f), StarLayer.Far, ref extra);
        }

        return stars;
    }

    /// <summary>
    /// A star's alpha at <paramref name="time"/> (seconds on the sky's clock): its base, breathing along
    /// <see cref="TwinkleCurve"/> when <paramref name="twinkle"/> is on and the star twinkles. The base alone otherwise
    /// (Reduce motion, or a level other than Full).
    /// </summary>
    public static float Alpha(in Star star, double time, bool twinkle)
    {
        if (!twinkle || !star.Twinkles || !double.IsFinite(time))
        {
            return star.Alpha;
        }

        var turns = (time / star.Period) + star.Phase;
        var f = (float)(turns - Math.Floor(turns));
        var swing = star.Layer == StarLayer.Near ? NearSwing : MidSwing;
        return star.Alpha * (1f + (swing * TwinkleCurve(f)));
    }

    /// <summary>
    /// The twinkle over one period (spec §3.3), -1..1: up to the peak at 30 %, down to the trough at 70 %, back at 100 %,
    /// each leg eased in and out. A breath, not a flicker.
    /// </summary>
    public static float TwinkleCurve(float f)
    {
        if (!float.IsFinite(f))
        {
            return 0f;
        }

        f -= MathF.Floor(f);
        return f < 0.3f ? Smooth(f / 0.3f)
            : f < 0.7f ? 1f - (2f * Smooth((f - 0.3f) / 0.4f))
            : -1f + Smooth((f - 0.7f) / 0.3f);
    }

    /// <summary><paramref name="x"/> wrapped into 0..<paramref name="period"/> (0 for no period).</summary>
    public static float Wrap(float x, float period)
    {
        if (!(period > 0f) || !float.IsFinite(x))
        {
            return 0f;
        }

        var r = x % period;
        if (r < 0f)
        {
            r += period;
        }

        return r >= period ? 0f : r;
    }

    /// <summary>
    /// A star's x on a tile <paramref name="tileWidth"/> wide with the sky drifted left by <paramref name="offset"/>
    /// px (R3.3): its own place less the drift, wrapping, so the field moves as one rigid sky.
    /// </summary>
    public static float Position(in Star star, float tileWidth, float offset) => Wrap((star.U * tileWidth) - offset, tileWidth);

    /// <summary>
    /// How much of a mark at <paramref name="x"/> shows between <paramref name="left"/> and <paramref name="right"/>:
    /// 1 inside, falling to 0 over <paramref name="fade"/> px at either edge (R3.3), 0 outside.
    /// </summary>
    public static float EdgeFade(float x, float left, float right, float fade)
    {
        if (x < left || x > right)
        {
            return 0f;
        }

        if (!(fade > 0f))
        {
            return 1f;
        }

        return Math.Clamp(MathF.Min(x - left, right - x) / fade, 0f, 1f);
    }

    /// <summary>
    /// Places <paramref name="stars"/> for this frame into <paramref name="into"/> (at most its length) and returns how
    /// many show: each mapped onto the canvas and drifted, kept only inside the inset sky, faded at its left and right
    /// edges, and breathing on the twinkle clock. Allocation-free; the caller only draws.
    /// </summary>
    public static int Place(ReadOnlySpan<Star> stars, in SkyView view, Span<PlacedStar> into)
    {
        if (!view.IsOpen)
        {
            return 0;
        }

        var width = view.CanvasMax.X - view.CanvasMin.X;
        var height = view.CanvasMax.Y - view.CanvasMin.Y;
        var (left, right, top, bottom) = (view.Left, view.Right, view.Top, view.Bottom);
        var n = 0;
        for (var i = 0; i < stars.Length && n < into.Length; i++)
        {
            ref readonly var star = ref stars[i];
            var x = view.CanvasMin.X + Position(star, width, view.Offset);
            var y = MathF.Round(view.CanvasMin.Y + (star.V * height));
            if (!view.Drifting)
            {
                x = MathF.Round(x);
            }

            if (y < top || y > bottom)
            {
                continue;
            }

            var fade = EdgeFade(x, left, right, view.Fade);
            if (fade <= 0f)
            {
                continue;
            }

            into[n++] = new PlacedStar(new Vector2(x, y), Alpha(star, view.Time, view.Twinkle) * fade, star.Layer, star.Temperature);
        }

        return n;
    }

    /// <summary>
    /// The bands of a path's rows (<see cref="PathRows.Build"/>): one per <see cref="PathRowKind.Band"/> header,
    /// running to the next header; a path without headers (a single quest) is one band over all its rows. Star counts
    /// come from the band's collapsed logical height at <paramref name="width"/>.
    /// </summary>
    public static IReadOnlyList<StarBand> ForPath(IReadOnlyList<PathRow> rows, float width = NominalWidth)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var bands = new List<StarBand>();
        if (rows.Count == 0)
        {
            return bands;
        }

        var start = rows[0].Kind == PathRowKind.Band ? 0 : -1;
        if (start < 0)
        {
            bands.Add(BandOf(rows, 0, rows.Count, width));
            return bands;
        }

        for (var i = 1; i <= rows.Count; i++)
        {
            if (i == rows.Count || rows[i].Kind == PathRowKind.Band)
            {
                bands.Add(BandOf(rows, start, i - start, width));
                start = i;
            }
        }

        return bands;
    }

    private static StarBand BandOf(IReadOnlyList<PathRow> rows, int first, int count, float width)
    {
        var height = 0f;
        for (var i = first; i < first + count; i++)
        {
            height += PathRows.LogicalHeight(rows[i]);
        }

        var expansion = rows[first].Expansion;
        var seed = Seed(expansion, count);
        return new StarBand(expansion, first, count, seed, Generate(seed, CountFor(width, height)));
    }

    /// <summary>Colour, brightness and twinkle from the second stream: always five draws a star, so the streams stay in step.</summary>
    private static Star Dress(float u, float v, StarLayer layer, ref uint extra)
    {
        var t = Next(ref extra);
        var a = Next(ref extra);
        var twinkle = Next(ref extra);
        var period = Next(ref extra);
        var phase = Next(ref extra);
        var (alpha, alphaSpread) = layer switch
        {
            StarLayer.Far => (0.10f, 0.06f),
            StarLayer.Mid => (0.20f, 0.08f),
            _ => (0.52f, 0.08f),
        };

        var twinkles = layer == StarLayer.Near || (layer == StarLayer.Mid && twinkle < MidTwinkleShare);
        return new Star(
            u,
            v,
            layer,
            TemperatureFor(t, layer),
            alpha + (alphaSpread * a),
            twinkles ? MinPeriod + ((MaxPeriod - MinPeriod) * period) : 0f,
            phase >= 1f ? 0f : phase);
    }

    /// <summary>
    /// Spec §3.2: cool 64 %, moon 24 %, gold 9 %, ember 3 %. A far star is never gold or ember, so it takes cool and moon
    /// in the same 64 : 24 proportion.
    /// </summary>
    private static StarTemperature TemperatureFor(float t, StarLayer layer)
    {
        if (layer == StarLayer.Far)
        {
            return t < 0.64f / 0.88f ? StarTemperature.Cool : StarTemperature.Moon;
        }

        return t < 0.64f ? StarTemperature.Cool : t < 0.88f ? StarTemperature.Moon : t < 0.97f ? StarTemperature.Gold : StarTemperature.Ember;
    }

    private static uint SecondStream(int seed) => unchecked(((uint)seed * 2654435761u) + 0x6A09E667u) & 0x7FFFFFFFu;

    private static float Smooth(float x) => 0.5f - (0.5f * MathF.Cos(MathF.PI * Math.Clamp(x, 0f, 1f)));

    private static float Next(ref uint state)
    {
        state = unchecked((state * 1103515245u) + 12345u) & 0x7FFFFFFFu;
        return state / (float)0x7FFFFFFF;
    }
}

/// <summary>
/// One sky's cached field (Revision 3, "Cost"): its stars are generated once, at <paramref name="maxStars"/>, and each
/// frame takes the prefix its area calls for, so a resize shows more or fewer of the same stars and nothing is built
/// or allocated per frame.
/// </summary>
/// <param name="seed">The field's seed.</param>
/// <param name="maxStars">The most the field ever shows.</param>
/// <param name="minStars">The fewest it shows on a small canvas.</param>
public sealed class SkyField(int seed, int maxStars, int minStars = 4)
{
    private Star[]? stars;

    /// <summary>The field's seed.</summary>
    public int Seed => seed;

    /// <summary>The stars for a canvas of <paramref name="logicalWidth"/> × <paramref name="logicalHeight"/> logical px, at one per <see cref="StarField.AreaPerStar"/>.</summary>
    public ReadOnlySpan<Star> For(float logicalWidth, float logicalHeight)
    {
        stars ??= StarField.Generate(seed, Math.Max(0, maxStars));
        var count = StarField.CountFor(logicalWidth, logicalHeight, Math.Min(minStars, stars.Length), stars.Length);
        return stars.AsSpan(0, count);
    }
}
