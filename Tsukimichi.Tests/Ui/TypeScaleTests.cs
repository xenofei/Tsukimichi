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

            // TrumpGothic jumps from 23 to 34 pt and Jupiter from 23 to 46 pt, so at the largest bucket the remainder is
            // wider than the Axis roles'; the window scale still makes up the difference.
            Assert.InRange(TypeScale.EyebrowPxFor(scaled) / eyebrow, 0.8f, 1.25f);
            Assert.InRange(TypeScale.TitlePxFor(scaled) / title, 0.7f, 1.45f);
            Assert.InRange(TypeScale.NumeralPxFor(scaled) / numeral, 0.8f, 1.2f);
        }
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
}
