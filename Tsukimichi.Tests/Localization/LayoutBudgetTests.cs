using System.Globalization;
using System.Text.Json;
using Tsukimichi.Core.Localization;
using Tsukimichi.Core.Ui;
using Xunit.Abstractions;

namespace Tsukimichi.Tests.Localization;

/// <summary>
/// Long-string layout tests (V2-19): every language's labels measured against the fixed-width places of
/// <see cref="LayoutBudgets"/> at UI scale 1, with the advance widths of Dalamud's default UI font
/// (<c>Fixtures/font-advances.json</c>, written by <c>tools/font-advances.py</c>). A label wider than a default budget
/// is reported (it relies on the widening: the rail grows, a column takes its header's width); one wider than a cap
/// fails, and its translation must be shortened. Pseudo-localization ("qps", English stretched by 40 %) runs through
/// the same checks, so the layout holds for a language longer than any shipped one.
/// </summary>
public class LayoutBudgetTests(ITestOutputHelper output)
{
    /// <summary>Header labels are captions: 0.85 of the body size, never under 12 px (Typography.Caption).</summary>
    private const float CaptionPx = LayoutBudgets.BodyFontPx * 0.85f;

    /// <summary>The Text sizes the rail's label fit is checked at (Settings › General › Text size).</summary>
    private static readonly float[] TextSizes = [0.8f, 0.9f, 1f, 1.1f, 1.25f, 1.5f];

    /// <summary>A label's width at a size of one, from the font table: what <see cref="RailLabel.Fit"/> scales.</summary>
    private static readonly EmMeasure FontEm = static text => Width(text.ToString(), 1f);

    /// <summary>The Rewards column's icons at UI scale 1 and the default icon scale: four 17.5 px icons, gaps and margin.</summary>
    private const float RewardsContentLogical = (14f * ScaleMetrics.DefaultIconScale * 4f) + (2f * 3f) + 8f;

    private static readonly string[] TabKeys = ["TabJournal", "TabMoonlit", "TabCharacters", "TabFlight", "PlanTab"];

    private static readonly string[] QuickViewKeys =
    [
        "QuickViewAll", "Core.Filter.FeatureQuests", "Core.Filter.LevelBand", "Core.Filter.Stalled",
        "Core.Filter.StorySidequests", "Core.Filter.Sprout",
    ];

    private static readonly (string Key, float Content, bool Sortable)[] FixedColumns =
    [
        ("ColumnLevel", LayoutBudgets.LevelColumnLogical, true),
        ("ColumnJob", LayoutBudgets.JobColumnLogical, false),
        ("ColumnExpansion", LayoutBudgets.ExpansionColumnLogical, true),
        ("ColumnRewards", RewardsContentLogical, false),
    ];

    private static readonly string[] StateKeys =
    [
        "Core.State.Ready", "Core.State.ReadyOnOtherJob", "Core.State.Accepted", "Core.State.Blocked", "Core.State.DoneToday",
        "Core.State.DoneThisWeek", "Core.State.DoneThisCycle", "Core.State.Completed", "Core.State.Foreclosed", "Core.State.Unknown",
    ];

    private static readonly Lazy<Dictionary<int, float>> Advances = new(LoadAdvances);

    public static IEnumerable<object[]> AllLanguages() =>
        new[] { "en", "qps" }.Concat(ResxFiles.Translations).Select(static l => new object[] { l });

    [Fact]
    public void The_font_table_measures_like_the_font()
    {
        // "Characters" at 16 px is 83 px in Noto Sans CJK JP Medium; a full-width character is one em.
        Assert.InRange(Width("Characters", LayoutBudgets.BodyFontPx), 80f, 86f);
        Assert.Equal(16f, Width("月", LayoutBudgets.BodyFontPx), 3);
        Assert.Equal(32f, Width("ジャ", LayoutBudgets.BodyFontPx), 3);
    }

    [Fact]
    public void English_fits_every_default_budget_without_widening()
    {
        var labels = Labels("en");
        foreach (var key in TabKeys)
        {
            // Every English tab label shows on one line under its icon at every level and Text size (plan v7 UI-4):
            // at most tracked or a little smaller, never wrapped or left to the tooltip.
            foreach (var flair in new[] { Flair.Full, Flair.Quiet })
            {
                foreach (var text in TextSizes)
                {
                    var fit = RailFit(labels[key], flair, text, maxLines: 1);
                    Assert.False(fit.IconOnly, $"{key} \"{labels[key]}\" at {flair}, {text:P0} has no room");
                    Assert.Equal(1, fit.Lines);
                }
            }
        }

        foreach (var (key, content, sortable) in FixedColumns)
        {
            // English widens nothing but, on the narrow Level column, room for the sort arrow its pills already give.
            var width = LayoutBudgets.FixedColumnWidth(content, Width(labels[key], CaptionPx), sortable);
            Assert.True(width <= content + LayoutBudgets.SortArrowLogical, $"{key} \"{labels[key]}\" widens its column to {width:0} px");
        }

        var widestState = StateKeys.Max(k => Width(labels[k], LayoutBudgets.BodyFontPx) + (ReasonStates.Contains(k) ? Width(" · …", LayoutBudgets.BodyFontPx) : 0f));
        Assert.Equal(LayoutBudgets.StatusMinLogical, LayoutBudgets.StatusMin(widestState));
    }

    [Theory]
    [MemberData(nameof(AllLanguages))]
    public void The_tab_rail_keeps_its_width_and_fits_every_label_inside_its_plate(string language)
    {
        // The rail does not widen for a translation, and since plan v7 UI-4 (spec Revision 3) nothing is cut: every
        // label ends no wider than its plate's room at every level, Text size and line count, or the station shows its
        // icon alone and the tooltip names the tab. Listed here so a translator can see what is squeezed.
        var labels = Labels(language);
        foreach (var key in TabKeys)
        {
            var label = labels[key];
            Assert.False(string.IsNullOrWhiteSpace(label), $"{language}: {key} is empty, so an icon-only station would have no name in its tooltip");
            foreach (var flair in new[] { Flair.Full, Flair.Quiet })
            {
                var room = LayoutBudgets.RailLabelRoom(flair);
                foreach (var text in TextSizes)
                {
                    foreach (var lines in new[] { 1, 2 })
                    {
                        var fit = RailFit(label, flair, text, lines);
                        if (fit.IconOnly)
                        {
                            output.WriteLine($"{language}: tab {key} \"{label}\" at {flair}, {text:P0}, {lines} line(s): icon only");
                            continue;
                        }

                        Assert.True(fit.Width <= room + 0.01f, $"{language}: {key} \"{label}\" is {fit.Width:0.0} px of {room} at {flair}, {text:P0}");
                        Assert.True(fit.Size >= LayoutBudgets.RailLabelMinLogical - 0.001f, $"{language}: {key} shrank to {fit.Size}");
                        Assert.InRange(fit.Lines, 1, lines);
                        if (!fit.AsIs && lines == 1)
                        {
                            output.WriteLine($"{language}: tab {key} \"{label}\" at {flair}, {text:P0}: {fit.Size:0.0} px, tracking {fit.Tracking:0.00}");
                        }
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData(Flair.Full, 1f, 12f, true)]
    [InlineData(Flair.Full, 1.5f, 12f, true)]
    [InlineData(Flair.Full, 0.8f, 10f, false)]
    [InlineData(Flair.Quiet, 0.8f, 10f, false)]
    public void Characters_fits_its_plate_as_the_spec_measured(Flair flair, float text, float size, bool tracked)
    {
        // Spec Revision 3 R3.1: "Characters" at 12 px is about 62 px against Full's 60 of room, so it is tracked at
        // -0.02 em; at 80 % Text size it is 10 px and fits as is. The owner saw it escape its plate at 64 px.
        var fit = RailFit("Characters", flair, text, maxLines: 1);
        Assert.False(fit.IconOnly);
        Assert.Equal(1, fit.Lines);
        // The font table measures it 62.3 wide (the spec's live table 62.0), so tracked it is a hair over 60 and takes
        // 11.96 px: within a tenth of the label size either way.
        Assert.InRange(fit.Size, size - 0.1f, size);
        Assert.Equal(tracked, fit.Tracking < 0f);
        Assert.True(fit.Width <= LayoutBudgets.RailLabelRoom(flair));
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void Characters_at_quiet_is_tracked_then_shrunk_but_never_under_10_px(float text)
    {
        // 56 px of room at Quiet: tracked is still about 60, so it shrinks to about 11.2 px.
        var fit = RailFit("Characters", Flair.Quiet, text, maxLines: 1);
        Assert.False(fit.IconOnly);
        Assert.InRange(fit.Size, 10.8f, 11.6f);
        Assert.True(fit.Tracking < 0f);
        Assert.True(fit.Width <= 56f);
    }

    /// <summary>The fit the rail draws a label with at a level and a Text size (UI scale moves rail, plate and label together, so it is logical).</summary>
    private static RailLabelFit RailFit(string label, Flair flair, float textSize, int maxLines) =>
        RailLabel.Fit(
            label,
            LayoutBudgets.RailLabelLogical(LayoutBudgets.BodyFontPx * textSize),
            LayoutBudgets.RailLabelMinLogical,
            LayoutBudgets.RailLabelRoom(flair),
            maxLines,
            FontEm);

    [Theory]
    [MemberData(nameof(AllLanguages))]
    public void Fixed_table_columns_hold_their_headers(string language)
    {
        var labels = Labels(language);
        foreach (var (key, content, sortable) in FixedColumns)
        {
            var header = Width(labels[key], CaptionPx);
            var width = LayoutBudgets.FixedColumnWidth(content, header, sortable);
            var need = header + (2f * LayoutBudgets.CellPaddingLogical) + (sortable ? LayoutBudgets.SortArrowLogical : 0f);
            if (need > content)
            {
                output.WriteLine($"{language}: column {key} \"{labels[key]}\" needs {need:0} px, the content {content:0} px; the column widens to {width:0}");
            }

            Assert.True(width >= need - 0.5f, $"{language}: {key} is {width} px for a {need} px header");
            Assert.True(width <= LayoutBudgets.MaxFixedColumnLogical, $"{language}: {key} \"{labels[key]}\" makes the column {width:0} px, more than {LayoutBudgets.MaxFixedColumnLogical}; shorten it");
        }
    }

    [Theory]
    [MemberData(nameof(AllLanguages))]
    public void The_quick_views_fit_the_smallest_toolbar(string language)
    {
        var labels = Labels(language);
        var width = LayoutBudgets.SegmentsWidth(QuickViewKeys.Select(k => Width(labels[k], LayoutBudgets.BodyFontPx)));
        output.WriteLine($"{language}: quick views {width:0} px of {LayoutBudgets.MinToolbarLogical:0}");
        Assert.True(width <= LayoutBudgets.MinToolbarLogical, $"{language}: the quick views take {width:0} px, the smallest toolbar {LayoutBudgets.MinToolbarLogical:0}");
    }

    [Theory]
    [MemberData(nameof(AllLanguages))]
    public void Every_state_name_fits_the_narrowest_status_column(string language)
    {
        // The status line is "<state> · <reason>"; the reason is cut with an ellipsis, the state word never. The
        // column's minimum grows to the widest state word (LayoutBudgets.StatusMin), within its cap.
        var labels = Labels(language);
        var ellipsis = Width(" · …", LayoutBudgets.BodyFontPx);
        var widest = 0f;
        foreach (var key in StateKeys)
        {
            var reason = ReasonStates.Contains(key) ? ellipsis : 0f;
            var width = Width(labels[key], LayoutBudgets.BodyFontPx) + reason;
            widest = MathF.Max(widest, width);
            if (width > LayoutBudgets.StatusMinLogical)
            {
                output.WriteLine($"{language}: {key} \"{labels[key]}\" needs {width:0} px of the status column's {LayoutBudgets.StatusMinLogical:0}; its minimum grows");
            }
        }

        var min = LayoutBudgets.StatusMin(widest);
        Assert.True(widest <= LayoutBudgets.MaxStatusMinLogical, $"{language}: the widest state name needs {widest:0} px, more than {LayoutBudgets.MaxStatusMinLogical}; shorten it");
        Assert.True(min >= widest - 0.5f);
    }

    private static readonly HashSet<string> ReasonStates = new(StringComparer.Ordinal)
    {
        "Core.State.Blocked", "Core.State.Foreclosed", "Core.State.Unknown", "Core.State.Accepted",
    };

    /// <summary>A language's labels: the resource file over English (a missing key reads in English), or English stretched for "qps".</summary>
    private static Dictionary<string, string> Labels(string language)
    {
        var english = ResxFiles.Load(string.Empty);
        if (language == "en")
        {
            return english;
        }

        if (language == "qps")
        {
            return english.ToDictionary(static e => e.Key, static e => PseudoText.IsMachineKey(e.Key) ? e.Value : PseudoText.Stretch(e.Value), StringComparer.Ordinal);
        }

        var merged = new Dictionary<string, string>(english, StringComparer.Ordinal);
        foreach (var (key, value) in ResxFiles.Load(language))
        {
            merged[key] = value;
        }

        return merged;
    }

    /// <summary>The text's advance width at <paramref name="px"/>: the font table's widths, one em for CJK, 0.6 em otherwise.</summary>
    private static float Width(string text, float px)
    {
        var hidden = text.IndexOf("##", StringComparison.Ordinal);
        if (hidden >= 0)
        {
            text = text[..hidden];
        }

        var em = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            em += Advances.Value.TryGetValue(rune.Value, out var advance) ? advance : rune.Value >= 0x2E80 ? 1f : 0.6f;
        }

        return em * px;
    }

    private static Dictionary<int, float> LoadAdvances()
    {
        var path = Path.Combine(ResxFiles.RepositoryRoot(), "Tsukimichi.Tests", "Fixtures", "font-advances.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var result = new Dictionary<int, float>();
        foreach (var entry in doc.RootElement.GetProperty("advances").EnumerateObject())
        {
            result[int.Parse(entry.Name, NumberStyles.HexNumber, CultureInfo.InvariantCulture)] = entry.Value.GetSingle();
        }

        return result;
    }
}
