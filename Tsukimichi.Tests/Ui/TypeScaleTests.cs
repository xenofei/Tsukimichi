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
}
