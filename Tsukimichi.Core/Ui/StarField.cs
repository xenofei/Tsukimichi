using Tsukimichi.Core.Evaluation;

namespace Tsukimichi.Core.Ui;

/// <summary>A star's size class (path-section proposal §4.3): 70 % faint dots, 25 % small discs, 5 % sparkles.</summary>
public enum StarMagnitude : byte
{
    /// <summary>r 0.6 at 0.14 alpha, a 1 × 1 rect.</summary>
    Faint,

    /// <summary>r 1.0 at 0.22 alpha.</summary>
    Small,

    /// <summary>r 1.5 at 0.32 alpha with a two-line cross (the FFXIV sparkle).</summary>
    Bright,
}

/// <summary>A star in its band's unit square: <paramref name="U"/> across (0 = left), <paramref name="V"/> down (0 = top).</summary>
public readonly record struct Star(float U, float V, StarMagnitude Magnitude);

/// <summary>One expansion's sky band on a path: its rows (header first) and its pre-seeded stars.</summary>
/// <param name="Expansion">The band's expansion.</param>
/// <param name="FirstRow">Index of its first row in the path's rows.</param>
/// <param name="RowCount">Rows in the band.</param>
/// <param name="Seed"><see cref="StarField.Seed"/> of the band.</param>
/// <param name="Stars">Its stars, in the band's unit square.</param>
public sealed record StarBand(byte Expansion, int FirstRow, int RowCount, int Seed, IReadOnlyList<Star> Stars);

/// <summary>
/// The Path chart's star field (path-section proposal §4.3): a few faint stars per expansion band at one per
/// <see cref="AreaPerStar"/> logical px² (at least <see cref="MinStars"/>, at most <see cref="MaxStars"/>), seeded
/// by <c>expansionId * 7919 + rowCount</c> so the sky is identical on every frame and whenever a quest of the same
/// shape is selected again. Built once per path; the caller maps the unit coordinates into the band and skips stars
/// that would sit on a node, the thread or a label.
/// </summary>
public static class StarField
{
    /// <summary>The seed multiplier (a prime).</summary>
    public const int SeedPrime = 7919;

    /// <summary>Logical px² of band per star.</summary>
    public const float AreaPerStar = 1400f;

    public const int MinStars = 6;
    public const int MaxStars = 48;

    /// <summary>The chart width the density is reckoned at (the card's inner width at the 360 px column).</summary>
    public const float NominalWidth = 324f;

    /// <summary>A band's seed: <c>expansionId * 7919 + rowCount</c>.</summary>
    public static int Seed(byte expansion, int rowCount) => (expansion * SeedPrime) + rowCount;

    /// <summary>Stars for a band of <paramref name="width"/> × <paramref name="height"/> logical px.</summary>
    public static int CountFor(float width, float height)
    {
        var area = width * height;
        if (!float.IsFinite(area) || area <= 0f)
        {
            return MinStars;
        }

        return Math.Clamp((int)MathF.Round(area / AreaPerStar), MinStars, MaxStars);
    }

    /// <summary>
    /// <paramref name="count"/> stars from <paramref name="seed"/> (a 31-bit linear congruential generator, as the
    /// mockup's): the same seed always yields the same stars. U and V stay 3 % inside the unit square.
    /// </summary>
    public static Star[] Generate(int seed, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        var stars = new Star[count];
        var state = (uint)seed & 0x7FFFFFFFu;
        for (var i = 0; i < count; i++)
        {
            var u = 0.03f + (0.94f * Next(ref state));
            var v = 0.03f + (0.94f * Next(ref state));
            var m = Next(ref state);
            var magnitude = m < 0.70f ? StarMagnitude.Faint : m < 0.95f ? StarMagnitude.Small : StarMagnitude.Bright;
            stars[i] = new Star(u, v, magnitude);
        }

        return stars;
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
            bands.Add(Band(rows, 0, rows.Count, width));
            return bands;
        }

        for (var i = 1; i <= rows.Count; i++)
        {
            if (i == rows.Count || rows[i].Kind == PathRowKind.Band)
            {
                bands.Add(Band(rows, start, i - start, width));
                start = i;
            }
        }

        return bands;
    }

    private static StarBand Band(IReadOnlyList<PathRow> rows, int first, int count, float width)
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

    private static float Next(ref uint state)
    {
        state = unchecked((state * 1103515245u) + 12345u) & 0x7FFFFFFFu;
        return state / (float)0x7FFFFFFF;
    }
}
