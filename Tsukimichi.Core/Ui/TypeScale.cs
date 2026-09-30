namespace Tsukimichi.Core.Ui;

/// <summary>
/// The type roles of ui-revamp §4.2 as numbers, so they are tested without ImGui: Caption (table headers, pills, the
/// status bar, card titles, the provenance line) at 0.85× the body size with a 12 px absolute floor, Display (the hero
/// title, the empty-state and tour headings) at 1.2×, and
/// the game font picked for each. The game's fonts are pre-baked bitmaps at fixed sizes (Axis 9.6 / 12 / 14 / 18 / 36
/// pt), and scaling one bilinearly blurs it (dalamud-developer panel §5), so <c>Ui.Typography</c> keeps one handle per
/// UI-scale bucket, each the game size nearest to what that bucket draws, and scales the small remainder.
/// </summary>
public static class TypeScale
{
    /// <summary>Caption size as a fraction of the body size.</summary>
    public const float CaptionFactor = 0.85f;

    /// <summary>Display size as a multiple of the body size.</summary>
    public const float DisplayFactor = 1.2f;

    /// <summary>No caption is drawn smaller than this many pixels, whatever the scales (accessibility B6).</summary>
    public const float CaptionFloorPx = 12f;

    /// <summary>The UI-scale buckets a font handle is built for.</summary>
    private static readonly float[] BucketScales = [0.9f, 1.0f, 1.15f, 1.3f, 1.6f];

    /// <summary>The game's Axis sizes in pixels at global scale 1 (points × 4/3): 9.6, 12, 14, 18 and 36 pt.</summary>
    private static readonly float[] GameSizesPx = [12.8f, 16f, 56f / 3f, 24f, 48f];

    /// <summary>The UI-scale buckets, smallest first.</summary>
    public static ReadOnlySpan<float> Buckets => BucketScales;

    /// <summary>The game font sizes <see cref="NearestGameFont"/> picks from, smallest first (Axis96, Axis12, Axis14, Axis18, Axis36).</summary>
    public static ReadOnlySpan<float> GameFontSizesPx => GameSizesPx;

    /// <summary>The bucket nearest <paramref name="uiScale"/>; a non-finite scale reads as 1.0.</summary>
    public static int Bucket(float uiScale)
    {
        if (!float.IsFinite(uiScale))
        {
            uiScale = 1f;
        }

        return Nearest(BucketScales, uiScale);
    }

    /// <summary>The caption size for a body size: 0.85×, never under <see cref="CaptionFloorPx"/>.</summary>
    public static float CaptionPx(float bodyPx) =>
        float.IsFinite(bodyPx) ? MathF.Max(CaptionFloorPx, bodyPx * CaptionFactor) : CaptionFloorPx;

    /// <summary>The display size for a body size: 1.2×.</summary>
    public static float DisplayPx(float bodyPx) => float.IsFinite(bodyPx) && bodyPx > 0f ? bodyPx * DisplayFactor : 0f;

    /// <summary>Index into <see cref="GameFontSizesPx"/> of the size nearest <paramref name="px"/> (the smaller on a tie).</summary>
    public static int NearestGameFont(float px) => Nearest(GameSizesPx, float.IsFinite(px) ? px : GameSizesPx[1]);

    /// <summary>The game font for captions in <paramref name="bucket"/>, given the body size at UI scale 1 and global scale 1.</summary>
    public static int CaptionGameFont(int bucket, float basePx) => NearestGameFont(CaptionPx(basePx * BucketScale(bucket)));

    /// <summary>The game font for display text in <paramref name="bucket"/>, given the body size at UI scale 1 and global scale 1.</summary>
    public static int DisplayGameFont(int bucket, float basePx) => NearestGameFont(DisplayPx(basePx * BucketScale(bucket)));

    // ------------------------------------------------------------------ Moon Road roles (proposal §4, plan v4 V3)

    /// <summary>
    /// Eyebrow (TrumpGothic, section headings) as a multiple of the body size: the game's narrow title face is drawn
    /// large for its size, so 1.45× lands on TrumpGothic 18.4 at the default body size (16–17 px) and UI scale 1.
    /// </summary>
    public const float EyebrowFactor = 1.45f;

    /// <summary>Title (Jupiter, one title per pane) as a multiple of the body size: Jupiter 20 at the default body size and UI scale 1.</summary>
    public const float TitleFactor = 1.6f;

    /// <summary>Numeral (MiedingerMid, counts and percentages) at the body size, so a count keeps its line height with or without game fonts: MiedingerMid 12 at the default.</summary>
    public const float NumeralFactor = 1f;

    /// <summary>TrumpGothic's sizes in points (18.4 / 23 / 34 / 68); <c>Ui.Typography</c> lists the matching game fonts in this order.</summary>
    private static readonly float[] EyebrowPoints = [18.4f, 23f, 34f, 68f];

    /// <summary>
    /// Jupiter's sizes in points with Latin letters (16 / 20 / 23 / 46). Jupiter 45 and 90 hold digits only (the game's
    /// flying text; its font files carry 11 glyphs), so they are never a title.
    /// </summary>
    private static readonly float[] TitlePoints = [16f, 20f, 23f, 46f];

    /// <summary>MiedingerMid's sizes in points (10 / 12 / 14 / 18 / 36).</summary>
    private static readonly float[] NumeralPoints = [10f, 12f, 14f, 18f, 36f];

    private static readonly float[] EyebrowPx = Pixels(EyebrowPoints);
    private static readonly float[] TitlePx = Pixels(TitlePoints);
    private static readonly float[] NumeralPx = Pixels(NumeralPoints);

    /// <summary>The Eyebrow game font sizes in pixels at global scale 1 (points × 4/3), smallest first.</summary>
    public static ReadOnlySpan<float> EyebrowGameFontSizesPx => EyebrowPx;

    /// <summary>The Title game font sizes in pixels at global scale 1, smallest first.</summary>
    public static ReadOnlySpan<float> TitleGameFontSizesPx => TitlePx;

    /// <summary>The Numeral game font sizes in pixels at global scale 1, smallest first.</summary>
    public static ReadOnlySpan<float> NumeralGameFontSizesPx => NumeralPx;

    /// <summary>The Eyebrow size for a body size.</summary>
    public static float EyebrowPxFor(float bodyPx) => Times(bodyPx, EyebrowFactor);

    /// <summary>The Title size for a body size.</summary>
    public static float TitlePxFor(float bodyPx) => Times(bodyPx, TitleFactor);

    /// <summary>The Numeral size for a body size.</summary>
    public static float NumeralPxFor(float bodyPx) => Times(bodyPx, NumeralFactor);

    /// <summary>Index into <see cref="EyebrowGameFontSizesPx"/> of the TrumpGothic for <paramref name="bucket"/>, given the body size at UI scale 1 and global scale 1.</summary>
    public static int EyebrowGameFont(int bucket, float basePx) => NearestOf(EyebrowPx, EyebrowPxFor(basePx * BucketScale(bucket)));

    /// <summary>Index into <see cref="TitleGameFontSizesPx"/> of the Jupiter for <paramref name="bucket"/>.</summary>
    public static int TitleGameFont(int bucket, float basePx) => NearestOf(TitlePx, TitlePxFor(basePx * BucketScale(bucket)));

    /// <summary>Index into <see cref="NumeralGameFontSizesPx"/> of the MiedingerMid for <paramref name="bucket"/>.</summary>
    public static int NumeralGameFont(int bucket, float basePx) => NearestOf(NumeralPx, NumeralPxFor(basePx * BucketScale(bucket)));

    private static float Times(float bodyPx, float factor) => float.IsFinite(bodyPx) && bodyPx > 0f ? bodyPx * factor : 0f;

    private static int NearestOf(float[] sizes, float px) => Nearest(sizes, float.IsFinite(px) && px > 0f ? px : sizes[1]);

    private static float[] Pixels(float[] points)
    {
        var px = new float[points.Length];
        for (var i = 0; i < points.Length; i++)
        {
            px[i] = points[i] * 4f / 3f;
        }

        return px;
    }

    private static float BucketScale(int bucket) => BucketScales[Math.Clamp(bucket, 0, BucketScales.Length - 1)];

    private static int Nearest(float[] values, float target)
    {
        var best = 0;
        for (var i = 1; i < values.Length; i++)
        {
            if (MathF.Abs(values[i] - target) < MathF.Abs(values[best] - target))
            {
                best = i;
            }
        }

        return best;
    }
}
