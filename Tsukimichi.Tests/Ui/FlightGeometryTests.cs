using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>Flight's expansion marks (feature plan v6 U6): 32 px in the list and 44 in the banner, hi-res, centred.</summary>
public sealed class FlightGeometryTests
{
    /// <summary>The plugin's Orbit.LowResMaxPx: at or below it a game icon loads its blurry native 32 px texture.</summary>
    private const float LowResMaxPx = 24f;

    [Theory]
    [InlineData(1f, 32f)]
    [InlineData(1.5f, 48f)]
    [InlineData(2f, 64f)]
    [InlineData(0.5f, 28f)]
    [InlineData(0.85f, 28f)]
    [InlineData(float.NaN, 32f)]
    [InlineData(0f, 32f)]
    public void The_list_mark_is_32_px_scaled_and_never_under_28(float iconScale, float expected)
    {
        Assert.Equal(expected, FlightGeometry.MarkSize(iconScale), 3);
    }

    [Theory]
    [InlineData(1f, 44f)]
    [InlineData(1.25f, 55f)]
    [InlineData(0.5f, 28f)]
    public void The_banner_mark_is_44_px_scaled_and_never_under_28(float iconScale, float expected)
    {
        Assert.Equal(expected, FlightGeometry.BannerMarkSize(iconScale), 3);
    }

    [Theory]
    [InlineData(0.1f)]
    [InlineData(0.5f)]
    [InlineData(1f)]
    [InlineData(3f)]
    public void Every_mark_loads_the_hi_res_texture(float iconScale)
    {
        Assert.True(FlightGeometry.MarkSize(iconScale) > LowResMaxPx);
        Assert.True(FlightGeometry.BannerMarkSize(iconScale) > LowResMaxPx);
    }

    [Fact]
    public void The_ring_column_fits_the_mark_and_the_bead_box()
    {
        Assert.Equal(32f, FlightGeometry.ColumnWidth(32f, 24f));
        Assert.Equal(40f, FlightGeometry.ColumnWidth(32f, 40f));
        Assert.Equal(4f, FlightGeometry.Centred(32f, 24f));
        Assert.Equal(0f, FlightGeometry.Centred(24f, 32f));
        Assert.Equal(1f, FlightGeometry.Centred(27f, 24f));
    }

    [Fact]
    public void A_mark_taller_than_its_heading_moves_the_heading_down_to_its_centre()
    {
        // A 32 px mark and a 29 px heading centred 16 px down: the centres already meet, so neither moves.
        var row = FlightGeometry.HeadingRow(32f, 29f, 16f, 0f);
        Assert.Equal(new FlightHeadingRow(0f, 0f, 32f), row);

        // A 48 px mark: its centre (24) is 8 below the title's, so the heading drops 8.
        row = FlightGeometry.HeadingRow(48f, 29f, 16f, 0f);
        Assert.Equal(new FlightHeadingRow(0f, 8f, 48f), row);
        Assert.Equal(row.MarkTop + 24f, row.HeadingTop + 16f);
    }

    [Fact]
    public void A_heading_taller_than_its_mark_moves_the_mark_down_to_the_title()
    {
        // A 28 px mark beside a 40 px heading centred 22 down: the mark drops 8 so its centre (8 + 14) meets 22.
        var row = FlightGeometry.HeadingRow(28f, 40f, 22f, 0f);
        Assert.Equal(new FlightHeadingRow(8f, 0f, 40f), row);
    }

    [Fact]
    public void The_gap_above_a_group_moves_the_whole_row()
    {
        var row = FlightGeometry.HeadingRow(48f, 29f, 16f, 10f);
        Assert.Equal(new FlightHeadingRow(10f, 18f, 58f), row);
    }

    [Fact]
    public void A_group_without_a_mark_keeps_its_heading_at_the_top()
    {
        Assert.Equal(new FlightHeadingRow(16f, 0f, 29f), FlightGeometry.HeadingRow(0f, 29f, 16f, 0f));
        Assert.Equal(new FlightHeadingRow(0f, 0f, 0f), FlightGeometry.HeadingRow(float.NaN, float.NaN, float.NaN, float.NaN));
    }
}
