using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>The Journal's one-line chip lane (feature plan v6 U2): never wraps; what does not fit goes behind "+N".</summary>
public class ChipLaneTests
{
    private const float Gap = 8f;
    private const float More = 40f;

    [Fact]
    public void No_chips_show_nothing()
    {
        Assert.Equal(0, ChipLane.Fit([], 500f, Gap, More));
        Assert.Equal(0f, ChipLane.Width([], 3, Gap));
    }

    [Fact]
    public void Chips_that_fit_all_show_without_a_more_chip()
    {
        // 100 + 8 + 100 + 8 + 100 = 316: exactly the room, no "+N" needed.
        Assert.Equal(3, ChipLane.Fit([100f, 100f, 100f], 316f, Gap, More));
    }

    [Fact]
    public void Chips_that_overflow_leave_room_for_the_more_chip()
    {
        // Two chips (208) plus a gap and "+N" (48) is 256; a third would need 364.
        var widths = new[] { 100f, 100f, 100f, 100f };
        Assert.Equal(2, ChipLane.Fit(widths, 300f, Gap, More));
        Assert.True(ChipLane.Width(widths, 2, Gap) + Gap + More <= 300f);
    }

    [Fact]
    public void Order_is_kept_even_when_a_later_chip_is_narrower()
    {
        // The second chip does not fit; the narrow third is not pulled ahead of it.
        Assert.Equal(1, ChipLane.Fit([100f, 300f, 20f], 250f, Gap, More));
    }

    [Fact]
    public void A_lane_too_narrow_for_any_chip_shows_only_the_more_chip()
    {
        Assert.Equal(0, ChipLane.Fit([200f, 200f], 120f, Gap, More));
    }

    [Fact]
    public void Every_count_fits_one_line()
    {
        for (var count = 0; count <= 16; count++)
        {
            var widths = Enumerable.Range(0, count).Select(i => 50f + (i * 13f % 90f)).ToArray();
            foreach (var room in new[] { 80f, 200f, 480f, 900f })
            {
                var shown = ChipLane.Fit(widths, room, Gap, More);
                Assert.InRange(shown, 0, count);
                var used = ChipLane.Width(widths, shown, Gap) + (shown < count ? (shown > 0 ? Gap : 0f) + More : 0f);
                Assert.True(used <= room || shown == 0, $"{count} chips in {room}: {used}");
            }
        }
    }
}
