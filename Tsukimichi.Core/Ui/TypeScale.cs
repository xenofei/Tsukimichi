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
