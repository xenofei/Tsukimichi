using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

public class TypeScaleTests
{
    [Theory]
    [InlineData(0.5f, 0)]
    [InlineData(0.9f, 0)]
    [InlineData(1.0f, 1)]
    [InlineData(1.2f, 2)]
    [InlineData(1.3f, 3)]
    [InlineData(1.5f, 4)]
    [InlineData(3f, 4)]
    [InlineData(float.NaN, 1)]
    public void Scale_falls_into_the_nearest_bucket(float uiScale, int bucket)
    {
        Assert.Equal(bucket, TypeScale.Bucket(uiScale));
    }

    [Theory]
    [InlineData(10f, 12f)]
    [InlineData(14f, 12f)]
    [InlineData(20f, 17f)]
    [InlineData(float.NaN, 12f)]
    public void Captions_are_085_of_the_body_with_a_12_px_floor(float body, float caption)
    {
        Assert.Equal(caption, TypeScale.CaptionPx(body), 3);
    }

    [Fact]
    public void Display_is_12_tenths_of_the_body()
    {
        Assert.Equal(20.4f, TypeScale.DisplayPx(17f), 3);
        Assert.Equal(0f, TypeScale.DisplayPx(float.NaN));
    }

    [Theory]
    [InlineData(12f, 0)]    // Axis96
    [InlineData(14.45f, 1)] // caption at 1.0: nearer Axis12 (16) than Axis96 (12.8)
    [InlineData(20.4f, 2)]  // display at 1.0: Axis14
    [InlineData(23.1f, 3)]  // caption at 1.6: Axis18
    [InlineData(100f, 4)]
    public void The_nearest_game_font_is_picked(float px, int index)
    {
        Assert.Equal(index, TypeScale.NearestGameFont(px));
    }

    [Fact]
    public void Every_bucket_gets_a_game_font_near_its_caption_and_display_sizes()
    {
        const float body = 17f;
        for (var bucket = 0; bucket < TypeScale.Buckets.Length; bucket++)
        {
            var caption = TypeScale.CaptionPx(body * TypeScale.Buckets[bucket]);
            var display = TypeScale.DisplayPx(body * TypeScale.Buckets[bucket]);
            var captionFont = TypeScale.GameFontSizesPx[TypeScale.CaptionGameFont(bucket, body)];
            var displayFont = TypeScale.GameFontSizesPx[TypeScale.DisplayGameFont(bucket, body)];

            // The remainder the window scale makes up stays small, so the bitmap is never scaled far.
            Assert.InRange(caption / captionFont, 0.8f, 1.2f);
            Assert.InRange(display / displayFont, 0.8f, 1.4f);
        }
    }

    [Fact]
    public void Heading_game_font_sizes_are_the_game_points_in_pixels()
    {
        // TrumpGothic 18.4 / 23 / 34 / 68, Jupiter 16 / 20 / 23 / 46 (45 and 90 are digits only), MiedingerMid 10–36.
        Assert.Equal([18.4f * 4f / 3f, 23f * 4f / 3f, 34f * 4f / 3f, 68f * 4f / 3f], TypeScale.EyebrowGameFontSizesPx.ToArray());
        Assert.Equal([16f * 4f / 3f, 20f * 4f / 3f, 23f * 4f / 3f, 46f * 4f / 3f], TypeScale.TitleGameFontSizesPx.ToArray());
        Assert.Equal([10f * 4f / 3f, 12f * 4f / 3f, 14f * 4f / 3f, 18f * 4f / 3f, 36f * 4f / 3f], TypeScale.NumeralGameFontSizesPx.ToArray());
    }

    [Theory]
    [InlineData(16f)]
    [InlineData(17f)]
    public void The_default_body_size_lands_on_the_proposal_sizes_at_ui_scale_1(float body)
    {
        var bucket = TypeScale.Bucket(1f);

        // Proposal §4: TrumpGothic 18.4, Jupiter 20, MiedingerMid 12 at the default bucket.
        Assert.Equal(0, TypeScale.EyebrowGameFont(bucket, body));
        Assert.Equal(1, TypeScale.TitleGameFont(bucket, body));
        Assert.Equal(1, TypeScale.NumeralGameFont(bucket, body));
    }

    [Theory]
    [InlineData(16f)]
    [InlineData(17f)]
    public void Every_bucket_gets_a_heading_font_near_its_role_size(float body)
    {
        for (var bucket = 0; bucket < TypeScale.Buckets.Length; bucket++)
        {
            var scaled = body * TypeScale.Buckets[bucket];
            var eyebrow = TypeScale.EyebrowGameFontSizesPx[TypeScale.EyebrowGameFont(bucket, body)];
            var title = TypeScale.TitleGameFontSizesPx[TypeScale.TitleGameFont(bucket, body)];
            var numeral = TypeScale.NumeralGameFontSizesPx[TypeScale.NumeralGameFont(bucket, body)];

            // A bitmap face is never stretched by more than MaxUpscale: past it the next larger size is drawn smaller.
            // TrumpGothic jumps from 23 to 34 pt and Jupiter from 23 to 46 pt, so the shrink can reach about half.
            Assert.InRange(TypeScale.EyebrowPxFor(scaled) / eyebrow, 0.55f, TypeScale.MaxUpscale);
            Assert.InRange(TypeScale.TitlePxFor(scaled) / title, 0.55f, TypeScale.MaxUpscale);
            Assert.InRange(TypeScale.NumeralPxFor(scaled) / numeral, 0.55f, TypeScale.MaxUpscale);
        }
    }

    [Fact]
    public void A_heading_font_goes_one_size_up_rather_than_stretch_past_a_tenth()
    {
        // Jupiter 23 is 30.67 px. Body 17 at bucket 1.3 asks the title for 35.4 px: the nearest (23) would be stretched
        // 1.15×, so Jupiter 46 is drawn at 0.58× instead.
        Assert.Equal(3, TypeScale.TitleGameFont(3, 17f));
        // Body 16 at bucket 1.3 asks for 33.3 px: 1.085× Jupiter 23 is within the limit.
        Assert.Equal(2, TypeScale.TitleGameFont(3, 16f));
        // MiedingerMid 18 (24 px) at body 17 × 1.6 = 27.2 px would be 1.13×: MiedingerMid 36.
        Assert.Equal(4, TypeScale.NumeralGameFont(4, 17f));
    }

    [Fact]
    public void Heading_sizes_follow_the_body_and_reject_nonsense()
    {
        Assert.Equal(17f * TypeScale.EyebrowFactor, TypeScale.EyebrowPxFor(17f), 3);
        Assert.Equal(17f * TypeScale.TitleFactor, TypeScale.TitlePxFor(17f), 3);
        Assert.Equal(17f, TypeScale.NumeralPxFor(17f), 3);
        Assert.Equal(0f, TypeScale.EyebrowPxFor(float.NaN));
        Assert.Equal(1, TypeScale.NumeralGameFont(1, float.NaN));
    }

    // ------------------------------------------------------------------ plan v7: Section, Header, hero title

    /// <summary>The Text sizes Settings offers (80–150 % in 10 % steps) and the spec table's 125 %.</summary>
    public static readonly float[] TextSizes = [0.8f, 0.9f, 1.0f, 1.1f, 1.2f, 1.25f, 1.3f, 1.4f, 1.5f];

    [Theory]
    [InlineData(0.8f, 1.80f)]
    [InlineData(1.0f, 1.80f)]
    [InlineData(1.1f, 1.80f)]
    [InlineData(1.25f, 1.725f)]
    [InlineData(1.5f, 1.60f)]
    [InlineData(2.0f, 1.60f)]
    [InlineData(float.NaN, 1.80f)]
    public void The_section_role_is_180_tapering_to_160_at_150_percent(float textScale, float factor)
    {
        Assert.Equal(factor, TypeScale.SectionFactorFor(textScale), 3);
    }

    [Theory]
    // Spec §1's table at a 16 px body and UI scale 1: the size and the TrumpGothic it is drawn from.
    [InlineData(0.8f, 23.04f, 0)]  // TG 18.4 × 0.94
    [InlineData(0.9f, 25.92f, 0)]  // TG 18.4 × 1.06
    [InlineData(1.0f, 28.8f, 1)]   // TG 23 × 0.94
    [InlineData(1.1f, 31.68f, 1)]  // TG 23 × 1.03
    [InlineData(1.25f, 34.5f, 2)]  // TG 34 × 0.76
    [InlineData(1.5f, 38.4f, 2)]   // TG 34 × 0.85
    public void The_section_role_follows_the_spec_table(float textScale, float px, int font)
    {
        var body = 16f * textScale;
        Assert.Equal(px, TypeScale.SectionPxFor(body, textScale), 2);
        Assert.Equal(font, TypeScale.SectionGameFont(TypeScale.Bucket(1f), body, textScale));
    }

    [Theory]
    [InlineData(1.0f, 0)]  // 24.8 px: TG 18.4 × 1.01
    [InlineData(1.25f, 1)] // 31.0 px: TG 23 × 1.01
    [InlineData(1.5f, 2)]  // 37.2 px: TG 23 would be 1.21×, so TG 34 × 0.82
    public void Column_headers_are_155_of_the_body_in_trumpgothic(float textScale, int font)
    {
        var body = 16f * textScale;
        Assert.Equal(body * 1.55f, TypeScale.HeaderPxFor(body), 3);
        Assert.Equal(font, TypeScale.HeaderGameFont(TypeScale.Bucket(1f), body));
    }

    [Fact]
    public void Column_headers_never_outrank_the_section_headings()
    {
        // Metadata under structure (spec §5): never larger, and never tracked wider, than the Section role.
        foreach (var textScale in TextSizes)
        {
            Assert.True(TypeScale.HeaderFactor < TypeScale.SectionFactorFor(textScale), $"{textScale}");
        }

        Assert.True(TypeScale.HeaderTrackingEm <= TypeScale.SectionTrackingEm);
        Assert.True(TypeScale.HeaderFactor > TypeScale.EyebrowFactor, "a step larger than 1.13's headers");
    }

    [Fact]
    public void No_heading_role_is_ever_smaller_than_the_body()
    {
        foreach (var textScale in TextSizes)
        {
            Assert.True(TypeScale.SectionFactorFor(textScale) >= 1f);
            Assert.True(TypeScale.HeroTitleFactorFor(textScale) > TypeScale.SectionFactorFor(textScale));
        }

        Assert.True(TypeScale.LeadFactor > 1f);
        Assert.True(TypeScale.HeaderFactor > 1f);

        // The title of each level stays above that level's section headings: Quiet's Lead headings, Plain's body bands.
        Assert.True(TypeScale.QuietTitleFactor > TypeScale.LeadFactor);
        Assert.True(TypeScale.PlainTitleFactor > 1f);
    }

    [Theory]
    [InlineData(16f)]
    [InlineData(17f)]
    [InlineData(18f)]
    public void The_hero_title_keeps_its_caps_above_the_section_caps_at_every_text_size(float defaultBody)
    {
        // Spec §1 "Hierarchy": the quest title's cap height is at least 1.2× the Section role's, in the faces each is
        // actually drawn from (Jupiter and TrumpGothic, measured cap heights), at every Text size and UI-scale bucket.
        foreach (var textScale in TextSizes)
        {
            var basePx = defaultBody * textScale;
            for (var bucket = 0; bucket < TypeScale.Buckets.Length; bucket++)
            {
                var body = basePx * TypeScale.Buckets[bucket];
                var section = TypeScale.SectionPxFor(body, textScale);
                var hero = TypeScale.HeroTitlePxFor(body, textScale);
                var sectionCaps = TypeScale.EyebrowCapHeight(TypeScale.SectionGameFont(bucket, basePx, textScale), section);
                var heroCaps = TypeScale.TitleCapHeight(TypeScale.HeroTitleGameFont(bucket, basePx, textScale), hero);
                Assert.True(
                    heroCaps >= TypeScale.HeroTitleCapLead * sectionCaps,
                    $"body {defaultBody} at {textScale:P0}, bucket {TypeScale.Buckets[bucket]}: title caps {heroCaps:0.0} px, section caps {sectionCaps:0.0} px");
            }
        }
    }

    [Fact]
    public void The_section_caps_grow_from_113s_eyebrow()
    {
        // The owner's point: 1.13's headings were TrumpGothic 18.4 at 1.45× (about 13 px caps at a 16 px body).
        var bucket = TypeScale.Bucket(1f);
        var before = TypeScale.EyebrowCapHeight(TypeScale.EyebrowGameFont(bucket, 16f), TypeScale.EyebrowPxFor(16f));
        var after = TypeScale.EyebrowCapHeight(TypeScale.SectionGameFont(bucket, 16f, 1f), TypeScale.SectionPxFor(16f, 1f));
        Assert.InRange(before, 12.5f, 13.5f);
        Assert.InRange(after, 16.5f, 17.5f);
    }

    [Fact]
    public void Every_bucket_draws_the_new_roles_without_stretching_a_bitmap_past_a_tenth()
    {
        foreach (var textScale in TextSizes)
        {
            for (var bucket = 0; bucket < TypeScale.Buckets.Length; bucket++)
            {
                var basePx = 17f * textScale;
                var body = basePx * TypeScale.Buckets[bucket];
                Assert.InRange(TypeScale.SectionPxFor(body, textScale) / TypeScale.EyebrowGameFontSizesPx[TypeScale.SectionGameFont(bucket, basePx, textScale)], 0.55f, TypeScale.MaxUpscale);
                Assert.InRange(TypeScale.HeaderPxFor(body) / TypeScale.EyebrowGameFontSizesPx[TypeScale.HeaderGameFont(bucket, basePx)], 0.55f, TypeScale.MaxUpscale);

                // Jupiter 46 is the largest title face with letters, so past it the hero title is scaled up as it must be.
                var hero = TypeScale.HeroTitleGameFont(bucket, basePx, textScale);
                var heroScale = TypeScale.HeroTitlePxFor(body, textScale) / TypeScale.TitleGameFontSizesPx[hero];
                Assert.InRange(heroScale, 0.5f, hero == TypeScale.TitleGameFontSizesPx.Length - 1 ? 1.4f : TypeScale.MaxUpscale);
            }
        }
    }

    [Fact]
    public void The_lead_face_and_quiet_title_are_built_near_their_sizes()
    {
        Assert.Equal(18f, TypeScale.LeadFontPx(16f));
        Assert.Equal(20f, TypeScale.LeadFontPx(17f));
        Assert.Equal(0f, TypeScale.LeadFontPx(float.NaN));
        Assert.Equal(16f * 1.15f, TypeScale.LeadPxFor(16f), 3);

        // Quiet's title at 1.40× a 16 px body is 22.4 px: Axis 18 (24 px).
        Assert.Equal(3, TypeScale.QuietTitleGameFont(TypeScale.Bucket(1f), 16f));
    }

    [Theory]
    [InlineData(28.8f, 0.08f, 2f)]
    [InlineData(38.4f, 0.08f, 3f)]
    [InlineData(20f, 0f, 0f)]
    [InlineData(float.NaN, 0.08f, 0f)]
    public void Tracking_is_whole_pixels(float fontPx, float em, float px)
    {
        Assert.Equal(px, TypeScale.TrackingPx(fontPx, em));
    }

    [Fact]
    public void Tracking_goes_between_glyphs_only()
    {
        Assert.Equal(108f, TypeScale.TrackedWidth(100f, 5, 2f));
        Assert.Equal(40f, TypeScale.TrackedWidth(40f, 1, 2f));
        Assert.Equal(40f, TypeScale.TrackedWidth(40f, 5, -2f));
        Assert.Equal(4f, TypeScale.TrackedWidth(float.NaN, 3, 2f));
    }
}
