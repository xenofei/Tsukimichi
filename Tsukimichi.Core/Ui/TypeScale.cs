namespace Tsukimichi.Core.Ui;

/// <summary>
/// The type roles of ui-revamp §4.2 as numbers, so they are tested without ImGui: Caption (table headers, pills, the
/// status bar, card titles, the provenance line) at 0.85× the body size with a 12 px absolute floor, Display (the hero
/// title, the empty-state and tour headings) at 1.2×, and
/// the game font picked for each. The game's fonts are pre-baked bitmaps at fixed sizes (Axis 9.6 / 12 / 14 / 18 / 36
/// pt), and scaling one bilinearly blurs it (dalamud-developer panel §5), so <c>Ui.Typography</c> keeps one handle per
/// UI-scale bucket, each the game size nearest to what that bucket draws, and scales the small remainder. The Moon
/// Road heading faces go one size up rather than stretch a smaller one by more than <see cref="MaxUpscale"/>.
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

    /// <summary>The scale of <paramref name="bucket"/> (clamped to the buckets).</summary>
    public static float BucketScale(int bucket) => BucketScales[Math.Clamp(bucket, 0, BucketScales.Length - 1)];

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

    // ------------------------------------------------------------------ plan v7 roles (docs/design/v7/ui/spec.md §1, §5)

    /// <summary>
    /// Section (the detail pane's card headings, the filter drawer's section heads) at Full: TrumpGothic at 1.80× the
    /// body up to <see cref="SectionTaperFrom"/> Text size, so "Requirements" reads as a heading and not as a caption.
    /// </summary>
    public const float SectionFactor = 1.80f;

    /// <summary>The Section factor at 150 % Text size: the role tapers so a heading never takes over a narrow pane.</summary>
    public const float SectionFactorAt150 = 1.60f;

    /// <summary>The Text size the Section taper starts from (110 %).</summary>
    public const float SectionTaperFrom = 1.10f;

    /// <summary>The Text size the Section taper ends at (150 %, the largest Settings offers).</summary>
    public const float SectionTaperTo = 1.50f;

    /// <summary>
    /// Lead (Quiet's section headings, the Section role's fallback, Plain's quest title) as a multiple of the body: the
    /// body face one step up. The atlas has no bold, so a heading in the body face grows by size, never by fake weight.
    /// </summary>
    public const float LeadFactor = 1.15f;

    /// <summary>
    /// How much larger than the Section role the Full hero quest title is drawn, so its capitals stay at least
    /// <see cref="HeroTitleCapLead"/> × the Section capitals with the game's real faces (Jupiter's capitals are no taller
    /// for their size than TrumpGothic's: see <see cref="EyebrowCapHeight"/>). The order is title, state, section, body.
    /// </summary>
    public const float HeroTitleLead = 1.25f;

    /// <summary>The Full hero quest title as a multiple of the body at the default Text size (Section × <see cref="HeroTitleLead"/>).</summary>
    public const float HeroTitleFactor = SectionFactor * HeroTitleLead;

    /// <summary>The least ratio of the hero title's cap height to the Section role's (spec §1, "Hierarchy").</summary>
    public const float HeroTitleCapLead = 1.2f;

    /// <summary>Quiet's quest title block (the display face) as a multiple of the body; also the hero title's fallback.</summary>
    public const float QuietTitleFactor = 1.40f;

    /// <summary>Plain's quest title as a multiple of the body: the <see cref="LeadFactor"/> face.</summary>
    public const float PlainTitleFactor = LeadFactor;

    /// <summary>
    /// The Journal's column headers at Full (TrumpGothic) as a multiple of the body: a step over the Eyebrow, held under
    /// the Section role so metadata never outranks structure. Quiet and Plain draw them at the body size.
    /// </summary>
    public const float HeaderFactor = 1.55f;

    /// <summary>The Section role's letter spacing at Full, in ems (ImGui has none, so it is drawn glyph by glyph).</summary>
    public const float SectionTrackingEm = 0.08f;

    /// <summary>The column headers' letter spacing at Full, in ems.</summary>
    public const float HeaderTrackingEm = 0.08f;

    /// <summary>
    /// The Section factor at <paramref name="textScale"/> (Settings › Text size): <see cref="SectionFactor"/> up to
    /// <see cref="SectionTaperFrom"/>, then linearly down to <see cref="SectionFactorAt150"/> at
    /// <see cref="SectionTaperTo"/> and held there; a non-finite scale reads as 100 %.
    /// </summary>
    public static float SectionFactorFor(float textScale)
    {
        if (!float.IsFinite(textScale) || textScale <= SectionTaperFrom)
        {
            return SectionFactor;
        }

        var t = MathF.Min(1f, (textScale - SectionTaperFrom) / (SectionTaperTo - SectionTaperFrom));
        return SectionFactor + ((SectionFactorAt150 - SectionFactor) * t);
    }

    /// <summary>The Section size for a body size at <paramref name="textScale"/>.</summary>
    public static float SectionPxFor(float bodyPx, float textScale) => Times(bodyPx, SectionFactorFor(textScale));

    /// <summary>The hero title factor at <paramref name="textScale"/>: the Section factor's taper times <see cref="HeroTitleLead"/>.</summary>
    public static float HeroTitleFactorFor(float textScale) => SectionFactorFor(textScale) * HeroTitleLead;

    /// <summary>The Full hero title size for a body size at <paramref name="textScale"/>.</summary>
    public static float HeroTitlePxFor(float bodyPx, float textScale) => Times(bodyPx, HeroTitleFactorFor(textScale));

    /// <summary>The column header size for a body size at Full.</summary>
    public static float HeaderPxFor(float bodyPx) => Times(bodyPx, HeaderFactor);

    /// <summary>The Lead size for a body size.</summary>
    public static float LeadPxFor(float bodyPx) => Times(bodyPx, LeadFactor);

    /// <summary>Quiet's title size for a body size (the hero title's fallback too).</summary>
    public static float QuietTitlePxFor(float bodyPx) => Times(bodyPx, QuietTitleFactor);

    /// <summary>
    /// The pixel size the Lead font is built at from a body of <paramref name="basePx"/> (Dalamud's font at the Text
    /// size, global scale 1), rounded to a whole pixel so nearby bodies share one build; 0 when the base is unknown.
    /// </summary>
    public static float LeadFontPx(float basePx) => MathF.Round(LeadPxFor(basePx));

    /// <summary>
    /// Letter spacing in pixels for a face drawn at <paramref name="fontPx"/>: <paramref name="em"/> ems, rounded to
    /// a whole pixel (ImGui places glyphs on whole pixels) and never negative.
    /// </summary>
    public static float TrackingPx(float fontPx, float em) =>
        float.IsFinite(fontPx) && fontPx > 0f && float.IsFinite(em) && em > 0f ? MathF.Round(fontPx * em) : 0f;

    /// <summary>
    /// The width of <paramref name="glyphs"/> glyphs whose advances sum to <paramref name="advance"/>, tracked by
    /// <paramref name="trackingPx"/>: the spacing goes between glyphs, never after the last.
    /// </summary>
    public static float TrackedWidth(float advance, int glyphs, float trackingPx) =>
        (float.IsFinite(advance) ? MathF.Max(0f, advance) : 0f) + (glyphs > 1 && float.IsFinite(trackingPx) ? MathF.Max(0f, trackingPx) * (glyphs - 1) : 0f);

    /// <summary>Index into <see cref="EyebrowGameFontSizesPx"/> of the TrumpGothic the Section role draws in for <paramref name="bucket"/>.</summary>
    public static int SectionGameFont(int bucket, float basePx, float textScale) =>
        NearestOf(EyebrowPx, SectionPxFor(basePx * BucketScale(bucket), textScale));

    /// <summary>Index into <see cref="TitleGameFontSizesPx"/> of the Jupiter the Full hero title draws in for <paramref name="bucket"/>.</summary>
    public static int HeroTitleGameFont(int bucket, float basePx, float textScale) =>
        NearestOf(TitlePx, HeroTitlePxFor(basePx * BucketScale(bucket), textScale));

    /// <summary>Index into <see cref="EyebrowGameFontSizesPx"/> of the TrumpGothic the column headers draw in for <paramref name="bucket"/>.</summary>
    public static int HeaderGameFont(int bucket, float basePx) => NearestOf(EyebrowPx, HeaderPxFor(basePx * BucketScale(bucket)));

    /// <summary>Index into <see cref="GameFontSizesPx"/> of the Axis Quiet's title block draws in for <paramref name="bucket"/>.</summary>
    public static int QuietTitleGameFont(int bucket, float basePx) => NearestGameFont(QuietTitlePxFor(basePx * BucketScale(bucket)));

    /// <summary>
    /// Cap height ("H") of each TrumpGothic size in pixels, in the order of <see cref="EyebrowGameFontSizesPx"/>, and of
    /// each Jupiter size, in the order of <see cref="TitleGameFontSizesPx"/>: measured from the 2026.09 client's
    /// <c>common/font/*.fdt</c> glyph boxes and <c>font*.tex</c> ink (rows over half coverage). TrumpGothic is narrow,
    /// not short: its capitals are as tall for their size as Jupiter's.
    /// </summary>
    private static readonly float[] EyebrowCapsPx = [14f, 18f, 26f, 52f];

    private static readonly float[] TitleCapsPx = [13f, 15f, 18f, 35f];

    /// <summary>The cap height of TrumpGothic size <paramref name="index"/> drawn at <paramref name="drawnPx"/>.</summary>
    public static float EyebrowCapHeight(int index, float drawnPx) => CapHeight(EyebrowCapsPx, EyebrowPx, index, drawnPx);

    /// <summary>The cap height of Jupiter size <paramref name="index"/> drawn at <paramref name="drawnPx"/>.</summary>
    public static float TitleCapHeight(int index, float drawnPx) => CapHeight(TitleCapsPx, TitlePx, index, drawnPx);

    private static float CapHeight(float[] caps, float[] sizes, int index, float drawnPx) =>
        float.IsFinite(drawnPx) && drawnPx > 0f ? caps[Math.Clamp(index, 0, caps.Length - 1)] / sizes[Math.Clamp(index, 0, sizes.Length - 1)] * drawnPx : 0f;

    /// <summary>
    /// A bitmap face scaled up blurs far more than one scaled down, so a heading font is never stretched by more than
    /// this: when the nearest size would be, the next larger one is taken and drawn a little smaller instead.
    /// </summary>
    public const float MaxUpscale = 1.1f;

    /// <summary>
    /// The heading size for <paramref name="px"/>: the nearest of <paramref name="sizes"/>, or the next larger one when
    /// the nearest would be scaled up by more than <see cref="MaxUpscale"/> (the largest size is scaled up as far as it must).
    /// </summary>
    private static int NearestOf(float[] sizes, float px)
    {
        var target = float.IsFinite(px) && px > 0f ? px : sizes[1];
        var best = Nearest(sizes, target);
        return target / sizes[best] > MaxUpscale && best + 1 < sizes.Length ? best + 1 : best;
    }

    private static float[] Pixels(float[] points)
    {
        var px = new float[points.Length];
        for (var i = 0; i < points.Length; i++)
        {
            px[i] = points[i] * 4f / 3f;
        }

        return px;
    }

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
