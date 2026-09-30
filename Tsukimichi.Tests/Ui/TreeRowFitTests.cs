using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The Journal tree's row fit (feature plan v4 L3): the parts go in their order, and a section's expansion suffix
/// ("Main Scenario · DT"), which is all that tells two sections apart, is never cut: it is the pill, or it follows the
/// head whole while the head is ellipsised. Measured with Dalamud's font advances the way TreePane measures its rows.
/// </summary>
public class TreeRowFitTests
{
    private const float NameMin = LayoutBudgets.RowNameMinLogical;

    [Fact]
    public void A_roomy_row_shows_every_part_and_the_pill()
    {
        var fit = TreeRowFit.Fit(600f, NameMin, new TreeRowWidths(110f, 40f, 70f, 30f, 56f, 45f));
        Assert.True(fit.Count && fit.Ready && fit.Bar && fit.Pill);
        Assert.False(fit.SuffixInLabel);
        Assert.Equal(600f - 70f - 30f - 56f - 45f, fit.HeadRoom, 3);
    }

    [Fact]
    public void Without_its_pill_the_label_carries_the_suffix_whole_and_the_head_is_cut_instead()
    {
        // 240 px: the pill would leave the head under its minimum, so it goes; the label is then refitted with the
        // suffix on top of the head's minimum, which costs the bar too.
        var widths = new TreeRowWidths(110f, 40f, 70f, 30f, 56f, 45f);
        var fit = TreeRowFit.Fit(240f, NameMin, widths);
        Assert.False(fit.Pill);
        Assert.True(fit.SuffixInLabel);
        Assert.False(fit.Bar);
        Assert.True(fit.Count && fit.Ready);
        Assert.Equal(240f - 70f - 30f, fit.LabelRoom, 3);
        Assert.Equal(fit.LabelRoom - widths.Suffix, fit.HeadRoom, 3);
        Assert.True(fit.HeadRoom >= NameMin);
    }

    [Fact]
    public void Where_the_tier_offers_no_pill_the_suffix_is_in_the_label()
    {
        var fit = TreeRowFit.Fit(400f, NameMin, new TreeRowWidths(110f, 40f, 70f, 30f, 56f, 0f));
        Assert.False(fit.Pill);
        Assert.True(fit.SuffixInLabel);
        Assert.True(fit.Bar);
        Assert.Equal(fit.LabelRoom - 40f, fit.HeadRoom, 3);
    }

    [Fact]
    public void A_node_without_a_suffix_fits_as_a_plain_row()
    {
        // An expansion pill that is not a suffix ("Hildibrand" with ShB) goes first and leaves nothing in the label.
        Span<float> parts = [70f, 30f, 56f, 45f];
        Span<bool> visible = stackalloc bool[4];
        foreach (var available in new[] { 120f, 180f, 230f, 260f, 400f })
        {
            var fit = TreeRowFit.Fit(available, NameMin, new TreeRowWidths(150f, 0f, 70f, 30f, 56f, 45f));
            var plain = RowFit.Fit(available, 150f, NameMin, parts, visible);
            Assert.False(fit.SuffixInLabel);
            Assert.Equal(visible[3], fit.Pill);
            Assert.Equal(visible[2], fit.Bar);
            Assert.Equal(visible[1], fit.Ready);
            Assert.Equal(visible[0], fit.Count);
            Assert.Equal(plain.NameRoom, fit.HeadRoom, 3);
            Assert.Equal(fit.HeadRoom, fit.LabelRoom, 3);
        }
    }

    /// <summary>The sections that differ only by their suffix, as <c>JournalNames.Short</c> names them, with their counts.</summary>
    private static readonly (string Label, string Count, string Ready)[] Twins =
    [
        ("Main Scenario · ARR–EW", "612 / 915", "1"),
        ("Main Scenario · DT", "12 / 104", "1"),
        ("Allied Societies · ARR–EW", "140 / 402", "12"),
        ("Allied Societies · DT", "0 / 48", string.Empty),
    ];

    public static IEnumerable<object[]> Scales()
    {
        for (var step = 0; step <= 14; step++)
        {
            foreach (var iconScale in new[] { ScaleMetrics.DefaultIconScale, ScaleMetrics.MaxIconScale })
            {
                yield return [ScaleMetrics.MinUiScale + (step * 0.05f), iconScale];
            }
        }
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void The_twin_sections_stay_apart_at_every_tree_width(float uiScale, float iconScale)
    {
        // TreePane's arithmetic at global scale 1: a 16 px font times the UI scale, the row from the halo past the
        // chevron's slot to the pane's edge less a 16 px scrollbar, each part with its 6 px gap.
        var s = ScaleMetrics.LayoutFactor(1f, uiScale);
        var font = LayoutBudgets.BodyFontPx * s;
        float Text(string text) => FontAdvances.Width(text, font);
        var pad = 6f * s;
        var bold = MathF.Max(1f, s);
        var radius = ScaleMetrics.TreeGlyphRadius(font, iconScale);
        var namePos = font + (2f * 4f) + (2f * radius) + pad;
        const float scrollbar = 16f;

        for (var paneLogical = PaneLayout.TreeFloorLogical; paneLogical <= 340f; paneLogical += 2f)
        {
            var pane = MathF.Floor(paneLogical * s);
            var available = pane - scrollbar - pad - namePos;
            // Hysteresis can hold the tier a step narrower than the width's own; both must keep the twins apart.
            foreach (var tier in new[] { LayoutBudgets.TreeTierForPane(pane, s, TreeTier.Full), LayoutBudgets.TreeTierForPane(pane, s, TreeTier.Slim) })
            {
                foreach (var (label, count, ready) in Twins)
                {
                    var (head, suffix) = JournalNames.SplitExpansion(label);
                    Assert.NotEqual(string.Empty, suffix);
                    var suffixText = JournalNames.SuffixSeparator + suffix;
                    var countText = tier <= TreeTier.Trim ? count : tier == TreeTier.Compact ? "66 %" : string.Empty;
                    var widths = new TreeRowWidths(
                        Text(head) + bold,
                        Text(suffixText),
                        countText.Length > 0 ? Text(countText) + pad : 0f,
                        ready.Length > 0 && tier != TreeTier.Slim ? Text(ready) + (10f * s) + pad : 0f,
                        tier <= TreeTier.Trim ? (56f * s) : 0f,
                        tier == TreeTier.Full ? (Text(suffix) * 0.72f) + (10f * s) + pad : 0f);
                    var fit = TreeRowFit.Fit(available, NameMin * s, widths);
                    var where = $"{label} at {paneLogical} logical, UI {uiScale}, icons {iconScale}, {tier}";

                    // Apart: the pill shows (it is never cut), or the suffix is drawn whole after the head (TreePane draws
                    // it at the end of the head's room, so what is left of the label's room must hold it).
                    Assert.True(fit.Pill || fit.SuffixInLabel, where);
                    var suffixRoom = fit.Pill ? 0f : widths.Suffix;
                    if (!fit.Pill)
                    {
                        Assert.True(fit.LabelRoom - bold >= widths.Suffix + MathF.Min(widths.Head, fit.HeadRoom) - bold - 0.01f, $"{where}: suffix cut");
                    }

                    // The head keeps its minimum wherever the row has room for it beside the suffix; past that (the
                    // narrowest tree at a large scale, where no part is left to drop) the suffix wins over the head.
                    var headMin = MathF.Min(MathF.Min(widths.Head, NameMin * s), available - suffixRoom);
                    Assert.True(fit.HeadRoom >= headMin - 0.01f, $"{where}: head {fit.HeadRoom}");
                }
            }
        }
    }
}
