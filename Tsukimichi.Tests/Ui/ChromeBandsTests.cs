using System.Reflection;
using System.Text.RegularExpressions;
using Tsukimichi.Core.Ui;
using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The fixed window frame (feature plan v6 U2): the window never moves under the player. Every band's height comes from
/// the window's width and the type and control sizes alone, so filters, the scope, chips, notices and the character's
/// name can never change one; the main window draws nothing between its toolbar and its body; the Journal list's title
/// and chip lane come before its empty state. Any new band, chip row or overlay line has to keep these passing.
/// </summary>
public class ChromeBandsTests
{
    private static readonly float[] Scales = [0.75f, 1f, 1.25f, 1.5f, 2f, 3f];

    private static ChromeMetrics Metrics(float width, float scale = 1f) => new(
        AvailableWidth: width,
        Scale: scale,
        LineHeight: 17f * scale,
        ItemSpacingY: 4f * scale,
        MinTarget: MathF.Max(26f * scale, 24f),
        QuickViewsWidth: 520f * scale,
        FiltersWidth: 92f * scale);

    [Fact]
    public void The_frame_is_measured_from_sizes_alone()
    {
        // The rule itself: nothing about the content reaches the bands. A new input naming filters, the scope, chips,
        // notices, the character or the selection would let one of them move the panes again.
        var banned = new Regex("filter(?!swidth)|scope|chip|notice|banner|character|selected|selection|search(?!width)|count|text", RegexOptions.IgnoreCase);
        var parameters = typeof(ChromeMetrics).GetConstructors().Single().GetParameters();
        Assert.All(parameters, p =>
        {
            Assert.Equal(typeof(float), p.ParameterType);
            Assert.False(banned.IsMatch(p.Name!), $"ChromeMetrics.{p.Name} names content; band heights must not depend on it");
        });

        var layout = typeof(ChromeBands).GetMethod(nameof(ChromeBands.Layout), BindingFlags.Public | BindingFlags.Static)!;
        Assert.Single(layout.GetParameters());
    }

    [Fact]
    public void Nothing_the_player_does_moves_the_body()
    {
        // The body starts under the toolbar: with 0, 1 or 5 notices waiting and any number of chips the toolbar's height
        // is the same, because neither is an input. The notices float (FloatingSlots) and the chips stay on one line
        // (ChipLane), so neither takes a line of the layout either.
        var bands = ChromeBands.Layout(Metrics(1400f));
        foreach (var notices in new[] { 0, 1, 5 })
        {
            var queue = new NoticeQueue();
            foreach (var kind in Enum.GetValues<NoticeKind>().Take(notices))
            {
                queue.Set(kind, true);
            }

            for (var chips = 0; chips <= 14; chips++)
            {
                var widths = Enumerable.Range(0, chips).Select(i => 60f + (i * 9f)).ToArray();
                var shown = ChipLane.Fit(widths, 600f, 8f, 40f);
                Assert.True(ChipLane.Width(widths, shown, 8f) + (shown < chips ? 48f : 0f) <= 600f);
                Assert.Equal(bands, ChromeBands.Layout(Metrics(1400f)));
            }
        }
    }

    [Theory]
    [InlineData(1600f, 1)]
    [InlineData(1450f, 1)]
    [InlineData(999f, 2)]
    [InlineData(700f, 2)]
    [InlineData(560f, 3)]
    public void The_toolbar_s_rows_follow_the_width(float width, int rows)
    {
        var bands = ChromeBands.Layout(Metrics(width));
        Assert.Equal(rows, bands.ToolbarRows);
        Assert.Equal(rows == 3, bands.QuickViewsOwnRow);
        Assert.Equal(rows * bands.ToolbarRow, bands.Toolbar, 3);
    }

    [Fact]
    public void A_wider_window_never_gains_a_toolbar_row()
    {
        foreach (var scale in Scales)
        {
            var previous = int.MaxValue;
            for (var width = 300f; width <= 4000f; width += 10f)
            {
                var rows = ChromeBands.Layout(Metrics(width, scale)).ToolbarRows;
                Assert.True(rows <= previous, $"{rows} rows at {width} px after {previous} (scale {scale})");
                previous = rows;
            }
        }
    }

    [Fact]
    public void The_search_fits_its_row()
    {
        foreach (var width in new[] { 400f, 700f, 999f, 1600f })
        {
            var m = Metrics(width);
            var bands = ChromeBands.Layout(m);
            Assert.True(bands.SearchWidth <= width);
            if (bands.ToolbarRows == 2)
            {
                Assert.True(bands.SearchWidth + (ChromeBands.ToolbarGapLogical * m.Scale) + m.QuickViewsWidth <= width + 0.01f);
            }
        }
    }

    [Fact]
    public void The_lane_is_one_chip_tall_at_every_scale()
    {
        foreach (var scale in Scales)
        {
            var m = Metrics(1200f, scale);
            var bands = ChromeBands.Layout(m);
            Assert.Equal(ChromeBands.ChipHeight(scale, m.LineHeight, m.MinTarget), bands.Lane);
            Assert.True(bands.Lane >= m.MinTarget);
            Assert.True(bands.Lane >= m.LineHeight + (4f * scale));
            Assert.True(bands.ToolbarRow >= m.MinTarget + (6f * scale));
            Assert.Equal(m.LineHeight + (3f * m.ItemSpacingY), bands.StatusBar, 3);
        }
    }

    [Fact]
    public void The_main_window_draws_nothing_between_its_toolbar_and_its_body()
    {
        var source = Source("Tsukimichi", "Ui", "MainWindow.cs");
        var content = MethodBody(source, "private void DrawContent(");
        var toolbar = content.IndexOf("DrawToolbar(", StringComparison.Ordinal);
        var body = content.IndexOf("DrawBody(", StringComparison.Ordinal);
        var status = content.IndexOf("DrawStatusBar(", StringComparison.Ordinal);
        Assert.True(toolbar > 0 && body > toolbar && status > body, "DrawContent draws the toolbar, the body, then the status bar");

        var between = content[(content.IndexOf(';', toolbar) + 1)..body];
        Assert.DoesNotMatch(new Regex(@"\w+\s*\("), StripComments(between));
    }

    [Fact]
    public void The_banners_and_the_wrapping_chip_row_are_gone()
    {
        var plugin = Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi");
        foreach (var file in Directory.GetFiles(plugin, "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("DrawChipRow(", text, StringComparison.Ordinal);
            Assert.DoesNotContain("DrawBanners(", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_list_s_title_and_lane_come_before_its_empty_state()
    {
        var draw = MethodBody(Source("Tsukimichi", "Ui", "TablePane.cs"), "public void Draw(bool hasSnapshot)");
        var header = draw.IndexOf("DrawJournalHeader(", StringComparison.Ordinal);
        var empty = draw.IndexOf("runner.Empty", StringComparison.Ordinal);
        Assert.True(header > 0 && empty > header, "TablePane draws its fixed band before the empty state can return");
    }

    private static string Source(params string[] parts) =>
        File.ReadAllText(Path.Combine([ResxFiles.RepositoryRoot(), .. parts]));

    /// <summary>The body of the method whose signature starts with <paramref name="signature"/>, braces balanced.</summary>
    private static string MethodBody(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"{signature} not found");
        var open = source.IndexOf('{', start);
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            depth += source[i] switch { '{' => 1, '}' => -1, _ => 0 };
            if (depth == 0)
            {
                return source[open..(i + 1)];
            }
        }

        return source[open..];
    }

    private static string StripComments(string code) => Regex.Replace(code, @"//[^\n]*", string.Empty);
}
