using System.Numerics;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

public class ImageCoverTests
{
    [Fact]
    public void Matching_aspect_shows_the_whole_image()
    {
        var (uv0, uv1) = ImageCover.Uv(300f, 100f, 900f, 300f);

        Assert.Equal(Vector2.Zero, uv0);
        Assert.Equal(Vector2.One, uv1);
    }

    [Fact]
    public void Wide_image_in_a_square_box_keeps_the_middle_third()
    {
        var (uv0, uv1) = ImageCover.Uv(100f, 100f, 300f, 100f);

        Assert.Equal(1f / 3f, uv0.X, 5);
        Assert.Equal(2f / 3f, uv1.X, 5);
        Assert.Equal(0f, uv0.Y);
        Assert.Equal(1f, uv1.Y);
    }

    [Fact]
    public void Tall_image_in_a_hero_strip_keeps_the_middle_band()
    {
        // A 1024 × 384 journal banner in a 360 × 96 hero (ui-revamp §2.5, T16's ≤ 96 px hero).
        var (uv0, uv1) = ImageCover.Uv(360f, 96f, 1024f, 384f);

        Assert.Equal(0f, uv0.X);
        Assert.Equal(1f, uv1.X);
        var shown = (uv1.Y - uv0.Y) * 384f;
        Assert.Equal(360f / 96f, 1024f / shown, 3);
        Assert.Equal(uv0.Y, 1f - uv1.Y, 5);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(1f, 0.5f)]
    [InlineData(-3f, 0f)]
    [InlineData(7f, 0.5f)]
    public void Focus_moves_the_window_along_the_cropped_axis(float focusY, float expectedStart)
    {
        // A 2:1 image in a 4:1 box shows half its height; the focus picks which half.
        var (uv0, uv1) = ImageCover.Uv(400f, 100f, 800f, 400f, focusY: focusY);

        Assert.Equal(expectedStart, uv0.Y, 5);
        Assert.Equal(expectedStart + 0.5f, uv1.Y, 5);
    }

    [Fact]
    public void A_non_finite_focus_reads_as_the_centre()
    {
        var (uv0, _) = ImageCover.Uv(400f, 100f, 800f, 400f, focusY: float.NaN);

        Assert.Equal(0.25f, uv0.Y, 5);
    }

    [Theory]
    [InlineData(0f, 100f, 100f, 100f)]
    [InlineData(100f, -1f, 100f, 100f)]
    [InlineData(100f, 100f, 0f, 100f)]
    [InlineData(100f, 100f, 100f, float.NaN)]
    [InlineData(float.PositiveInfinity, 100f, 100f, 100f)]
    public void Degenerate_sizes_show_the_whole_image(float boxW, float boxH, float texW, float texH)
    {
        var (uv0, uv1) = ImageCover.Uv(boxW, boxH, texW, texH);

        Assert.Equal(Vector2.Zero, uv0);
        Assert.Equal(Vector2.One, uv1);
    }

    [Fact]
    public void Scale_metrics_centre_crop_is_the_centred_cover()
    {
        Assert.Equal(ImageCover.Uv(344f, 200f, 1024f, 384f), ScaleMetrics.CenterCropUv(344f, 200f, 1024f, 384f));
    }

    [Theory]
    [InlineData(10f, 0.42f, 16.6667f)]
    [InlineData(30f, 0.42f, 30f)]
    [InlineData(float.NaN, 0.42f, 16.6667f)]
    [InlineData(10f, 0f, 10f)]
    public void Playing_glyph_box_never_holds_a_moon_under_14_px(float box, float fraction, float expected)
    {
        Assert.Equal(expected, ScaleMetrics.GlyphBoxWithFloor(box, fraction), 3);
    }
}
