using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The rail labels' fit (plan v7 UI-4, spec Revision 3 R3.1) with a monospaced measure (every glyph half an em, so
/// widths are easy to reckon): as is, tracked, shrunk to no less than the least size, wrapped at a space, or icon only;
/// never wider than the room, never an ellipsis.
/// </summary>
public class RailLabelTests
{
    private static readonly EmMeasure HalfEm = static text => text.Length * 0.5f;

    [Fact]
    public void A_label_that_fits_is_drawn_as_is()
    {
        // 8 glyphs at 12 px: 48 px in 60.
        var fit = RailLabel.Fit("Moonlit!", 12f, 10f, 60f, 1, HalfEm);
        Assert.True(fit.AsIs);
        Assert.Equal(12f, fit.Size);
        Assert.Equal(48f, fit.Width, 3);
    }

    [Fact]
    public void A_label_a_little_too_wide_is_tracked_at_minus_two_hundredths_of_an_em()
    {
        // 10 glyphs at 12 px: 60 px in 59; tracked, 9 gaps of -0.24 px: 57.84.
        var fit = RailLabel.Fit("Characters", 12f, 10f, 59f, 1, HalfEm);
        Assert.False(fit.IconOnly);
        Assert.Equal(12f, fit.Size);
        Assert.Equal(-0.24f, fit.Tracking, 4);
        Assert.Equal(57.84f, fit.Width, 3);
    }

    [Fact]
    public void A_label_still_too_wide_shrinks_but_never_under_the_least_size()
    {
        // Tracked, 10 glyphs take 4.82 em: 52 px of room is 10.78 px.
        var fit = RailLabel.Fit("Characters", 12f, 10f, 52f, 1, HalfEm);
        Assert.InRange(fit.Size, 10.7f, 10.79f);
        Assert.True(fit.Width <= 52f);
        Assert.True(fit.Tracking < 0f);

        // 40 px would need 8.3 px: under 10, so on one line it has no fit.
        Assert.True(RailLabel.Fit("Characters", 12f, 10f, 40f, 1, HalfEm).IconOnly);
    }

    [Fact]
    public void A_long_label_wraps_at_the_space_that_balances_its_lines()
    {
        // "Clear my blues" (14 glyphs, 84 px) in 50: "Clear my" and "blues", or "Clear" and "my blues"; the second
        // pair's widest line is 8 glyphs, the first's 8 too; either way each line fits at 12 px (48 px).
        var fit = RailLabel.Fit("Clear my blues", 12f, 10f, 50f, 2, HalfEm);
        Assert.Equal(2, fit.Lines);
        Assert.True(fit.Width <= 50f);
        Assert.Equal(' ', "Clear my blues"[fit.Break]);
        Assert.Equal(12f, fit.Size);
    }

    [Fact]
    public void Every_split_is_tried_before_the_icon_alone()
    {
        // 'W' is an em, '.' takes no width (so tracking pulls a run of them in), the rest half an em. The most balanced
        // split untracked, "WWW" | ".... W", leaves "WWW" needing 29.6 px even tracked at the least size; the other,
        // "WWW ...." | "W", is wider untracked but has 43 gaps to track and fits 28 px at about 10.6 px.
        EmMeasure measure = static text =>
        {
            var em = 0f;
            foreach (var c in text)
            {
                em += c switch { 'W' => 1f, '.' => 0f, _ => 0.5f };
            }

            return em;
        };
        var label = "WWW " + new string('.', 40) + " W";

        var fit = RailLabel.Fit(label, 12f, 10f, 28f, 2, measure);

        Assert.False(fit.IconOnly);
        Assert.Equal(2, fit.Lines);
        Assert.Equal(label.LastIndexOf(' '), fit.Break);
        Assert.True(fit.Width <= 28f);
        Assert.InRange(fit.Size, 10f, 12f);
    }

    [Fact]
    public void A_run_of_spaces_wraps_once_and_the_second_line_starts_at_its_word()
    {
        const string label = "  Clear   my blues ";
        var fit = RailLabel.Fit(label, 12f, 10f, 50f, 2, HalfEm);

        Assert.Equal(2, fit.Lines);
        Assert.Equal(' ', label[fit.Break]);

        // The lines as the rail draws them (trimmed either side of the break) are the lines measured.
        var text = label.AsSpan().Trim();
        var at = fit.Break - (label.Length - label.AsSpan().TrimStart().Length);
        var first = text[..at].TrimEnd().ToString();
        var second = text[(at + 1)..].TrimStart().ToString();
        // "Clear" | "my blues" keeps 12 px; "Clear   my" | "blues" would have to shrink.
        Assert.Equal("Clear", first);
        Assert.Equal("my blues", second);
        Assert.Equal(12f, fit.Size);
        var widest = MathF.Max(RailLabel.Width(first, fit.Size, fit.Tracking / fit.Size, HalfEm), RailLabel.Width(second, fit.Size, fit.Tracking / fit.Size, HalfEm));
        Assert.Equal(widest, fit.Width, 3);
        Assert.True(fit.Width <= 50f);
    }

    [Fact]
    public void A_label_with_no_room_for_two_lines_does_not_wrap()
    {
        Assert.True(RailLabel.Fit("Clear my blues", 12f, 10f, 50f, 1, HalfEm).IconOnly);
    }

    [Fact]
    public void One_long_word_with_no_room_is_left_to_the_tooltip()
    {
        var fit = RailLabel.Fit("Abenteuertagebuch", 12f, 10f, 56f, 2, HalfEm);
        Assert.True(fit.IconOnly);
        Assert.Equal(0, fit.Lines);
    }

    [Fact]
    public void An_empty_label_or_no_room_is_icon_only()
    {
        Assert.True(RailLabel.Fit("   ", 12f, 10f, 60f, 2, HalfEm).IconOnly);
        Assert.True(RailLabel.Fit("Flight", 12f, 10f, 0f, 2, HalfEm).IconOnly);
    }

    [Fact]
    public void A_label_never_ends_wider_than_its_room()
    {
        var labels = new[] { "Journal", "Moonlit", "Characters", "Flight", "My blues", "Reisetagebuch", "Personnages", "冒険者の日誌", "A B C D E F G H" };
        foreach (var label in labels)
        {
            for (var room = 20f; room <= 80f; room += 0.5f)
            {
                foreach (var size in new[] { 10f, 11f, 12f })
                {
                    foreach (var lines in new[] { 1, 2 })
                    {
                        var fit = RailLabel.Fit(label, size, 10f, room, lines, HalfEm);
                        if (fit.IconOnly)
                        {
                            continue;
                        }

                        Assert.True(fit.Width <= room + 0.001f, $"\"{label}\" {size} px in {room}: {fit.Width}");
                        Assert.InRange(fit.Size, 10f, size);
                        Assert.InRange(fit.Lines, 1, lines);
                    }
                }
            }
        }
    }
}
