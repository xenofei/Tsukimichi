using System.Numerics;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

public class OverlayGeometryTests
{
    private static readonly ScreenRect Screen = new(new Vector2(0f, 0f), new Vector2(1920f, 1080f));
    private static readonly ScreenRect Area = new(new Vector2(100f, 100f), new Vector2(1100f, 800f));

    // ------------------------------------------------------------------ cutout

    [Fact]
    public void Cutout_without_holes_is_the_area_itself()
    {
        Span<ScreenRect> output = stackalloc ScreenRect[16];
        Span<ScreenRect> scratch = stackalloc ScreenRect[16];
        var count = OverlayGeometry.Cutout(in Area, ReadOnlySpan<ScreenRect>.Empty, output, scratch);
        Assert.Equal(1, count);
        Assert.Equal(Area, output[0]);
    }

    [Fact]
    public void Cutout_of_an_inner_hole_covers_the_area_minus_the_hole_without_overlap()
    {
        var hole = new ScreenRect(new Vector2(400f, 300f), new Vector2(600f, 500f));
        Span<ScreenRect> output = stackalloc ScreenRect[16];
        Span<ScreenRect> scratch = stackalloc ScreenRect[16];
        var count = OverlayGeometry.Cutout(in Area, [hole], output, scratch);

        Assert.Equal(4, count);
        AssertPartition(output[..count], Area, [hole]);
    }

    [Fact]
    public void Cutout_of_two_holes_partitions_the_rest()
    {
        var target = new ScreenRect(new Vector2(150f, 150f), new Vector2(350f, 250f));
        var card = new ScreenRect(new Vector2(364f, 150f), new Vector2(700f, 330f));
        Span<ScreenRect> output = stackalloc ScreenRect[16];
        Span<ScreenRect> scratch = stackalloc ScreenRect[16];
        var count = OverlayGeometry.Cutout(in Area, [target, card], output, scratch);

        Assert.InRange(count, 2, 16);
        AssertPartition(output[..count], Area, [target, card]);
    }

    [Fact]
    public void Cutout_ignores_holes_outside_the_area_and_empty_holes()
    {
        var outside = new ScreenRect(new Vector2(1500f, 900f), new Vector2(1600f, 1000f));
        var empty = new ScreenRect(new Vector2(200f, 200f), new Vector2(200f, 300f));
        Span<ScreenRect> output = stackalloc ScreenRect[16];
        Span<ScreenRect> scratch = stackalloc ScreenRect[16];
        var count = OverlayGeometry.Cutout(in Area, [outside, empty], output, scratch);

        Assert.Equal(1, count);
        Assert.Equal(Area, output[0]);
    }

    [Fact]
    public void Cutout_of_a_hole_crossing_the_edge_clips_it_to_the_area()
    {
        var hole = new ScreenRect(new Vector2(50f, 50f), new Vector2(300f, 300f));
        Span<ScreenRect> output = stackalloc ScreenRect[16];
        Span<ScreenRect> scratch = stackalloc ScreenRect[16];
        var count = OverlayGeometry.Cutout(in Area, [hole], output, scratch);

        Assert.Equal(2, count);
        AssertPartition(output[..count], Area, [hole]);
    }

    [Fact]
    public void Cutout_that_would_overflow_the_buffers_keeps_the_pieces_so_far()
    {
        var a = new ScreenRect(new Vector2(200f, 200f), new Vector2(300f, 300f));
        var b = new ScreenRect(new Vector2(500f, 500f), new Vector2(600f, 600f));
        Span<ScreenRect> output = stackalloc ScreenRect[4];
        Span<ScreenRect> scratch = stackalloc ScreenRect[4];
        var count = OverlayGeometry.Cutout(in Area, [a, b], output, scratch);

        // The first hole fits (four bands); the second would need up to sixteen, so it is left undimmed.
        Assert.Equal(4, count);
        AssertPartition(output[..count], Area, [a]);
    }

    // ------------------------------------------------------------------ card placement

    [Fact]
    public void Card_goes_to_the_right_of_the_target_when_there_is_room()
    {
        var target = new ScreenRect(new Vector2(100f, 100f), new Vector2(300f, 200f));
        var pos = OverlayGeometry.PlaceCard(in target, new Vector2(320f, 160f), in Screen, 10f, out var side);

        Assert.Equal(CardSide.Right, side);
        Assert.Equal(new Vector2(310f, 100f), pos);
    }

    [Fact]
    public void Card_flips_to_the_left_near_the_right_edge()
    {
        var target = new ScreenRect(new Vector2(1700f, 100f), new Vector2(1900f, 200f));
        var pos = OverlayGeometry.PlaceCard(in target, new Vector2(320f, 160f), in Screen, 10f, out var side);

        Assert.Equal(CardSide.Left, side);
        Assert.Equal(new Vector2(1700f - 10f - 320f, 100f), pos);
    }

    [Fact]
    public void Card_goes_below_when_neither_side_has_room()
    {
        var target = new ScreenRect(new Vector2(10f, 100f), new Vector2(1910f, 200f));
        var pos = OverlayGeometry.PlaceCard(in target, new Vector2(320f, 160f), in Screen, 10f, out var side);

        Assert.Equal(CardSide.Below, side);
        Assert.Equal(new Vector2(10f, 210f), pos);
    }

    [Fact]
    public void Card_goes_above_when_the_target_fills_the_bottom()
    {
        var target = new ScreenRect(new Vector2(10f, 900f), new Vector2(1910f, 1070f));
        var pos = OverlayGeometry.PlaceCard(in target, new Vector2(320f, 160f), in Screen, 10f, out var side);

        Assert.Equal(CardSide.Above, side);
        Assert.Equal(new Vector2(10f, 900f - 10f - 160f), pos);
    }

    [Fact]
    public void Card_is_clamped_into_the_screen_when_nothing_fits()
    {
        var target = new ScreenRect(new Vector2(0f, 0f), new Vector2(1920f, 1080f));
        var pos = OverlayGeometry.PlaceCard(in target, new Vector2(320f, 160f), in Screen, 10f, out _);

        Assert.True(pos.X >= 0f && pos.X + 320f <= 1920f);
        Assert.True(pos.Y >= 0f && pos.Y + 160f <= 1080f);
    }

    [Fact]
    public void Card_beside_a_target_near_the_bottom_edge_moves_above_it()
    {
        // Right and left would hang below the screen, below has no room, so the card goes above.
        var target = new ScreenRect(new Vector2(100f, 1000f), new Vector2(300f, 1060f));
        var pos = OverlayGeometry.PlaceCard(in target, new Vector2(320f, 160f), in Screen, 10f, out var side);

        Assert.Equal(CardSide.Above, side);
        Assert.Equal(new Vector2(100f, 1000f - 10f - 160f), pos);
    }

    [Fact]
    public void Card_to_the_right_is_clamped_up_when_only_slightly_too_low()
    {
        // Right fits horizontally but not vertically; left and below do not fit either; above has no room: fall
        // back to the right side, clamped into the screen.
        var target = new ScreenRect(new Vector2(100f, 20f), new Vector2(300f, 1070f));
        var pos = OverlayGeometry.PlaceCard(in target, new Vector2(320f, 160f), in Screen, 10f, out var side);

        Assert.Equal(CardSide.Right, side);
        Assert.Equal(new Vector2(310f, 20f), pos);
    }

    [Fact]
    public void Centre_placement_centres_and_clamps()
    {
        var centred = OverlayGeometry.CenterIn(in Area, new Vector2(300f, 100f));
        Assert.Equal(new Vector2(450f, 400f), centred);

        var clamped = OverlayGeometry.CenterIn(in Area, new Vector2(2000f, 100f));
        Assert.Equal(new Vector2(100f, 400f), clamped);
    }

    // ------------------------------------------------------------------ rect helpers

    [Fact]
    public void Union_ignores_empty_sides_and_intersect_is_empty_when_apart()
    {
        var a = new ScreenRect(new Vector2(0f, 0f), new Vector2(10f, 10f));
        var b = new ScreenRect(new Vector2(20f, 20f), new Vector2(30f, 30f));
        var empty = default(ScreenRect);

        Assert.Equal(a, ScreenRect.Union(in a, in empty));
        Assert.Equal(new ScreenRect(new Vector2(0f, 0f), new Vector2(30f, 30f)), ScreenRect.Union(in a, in b));
        Assert.True(ScreenRect.Intersect(in a, in b).IsEmpty);
        Assert.Equal(new ScreenRect(new Vector2(-2f, -2f), new Vector2(12f, 12f)), a.Expand(2f));
    }

    /// <summary>The pieces are inside the area, disjoint from every hole and from each other, and cover the rest exactly (by area and by sampling).</summary>
    private static void AssertPartition(ReadOnlySpan<ScreenRect> pieces, ScreenRect area, ScreenRect[] holes)
    {
        var expected = area.Area;
        foreach (var hole in holes)
        {
            expected -= ScreenRect.Intersect(in area, in hole).Area;
        }

        var covered = 0f;
        for (var i = 0; i < pieces.Length; i++)
        {
            var piece = pieces[i];
            Assert.False(piece.IsEmpty);
            Assert.Equal(piece, ScreenRect.Intersect(in area, in piece));
            foreach (var hole in holes)
            {
                Assert.True(ScreenRect.Intersect(in piece, in hole).IsEmpty, $"piece {i} overlaps a hole");
            }

            for (var j = i + 1; j < pieces.Length; j++)
            {
                Assert.True(ScreenRect.Intersect(in piece, in pieces[j]).IsEmpty, $"pieces {i} and {j} overlap");
            }

            covered += piece.Area;
        }

        Assert.Equal(expected, covered, 0.01f);

        for (var y = area.Min.Y + 0.5f; y < area.Max.Y; y += 25f)
        {
            for (var x = area.Min.X + 0.5f; x < area.Max.X; x += 25f)
            {
                var p = new Vector2(x, y);
                var inHole = false;
                foreach (var hole in holes)
                {
                    inHole |= hole.Contains(p);
                }

                var inPiece = false;
                foreach (var piece in pieces)
                {
                    inPiece |= piece.Contains(p);
                }

                Assert.Equal(!inHole, inPiece);
            }
        }
    }
}
