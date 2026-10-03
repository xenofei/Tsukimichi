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
    /// <summary>
    /// Header labels at Quiet and Plain, and at Full where the game face cannot draw them, are at the body size (plan v7
    /// §5; they were captions, 0.85×, before 1.14). Full's TrumpGothic capitals are narrower than the body face's.
    /// </summary>
    private const float HeaderPx = LayoutBudgets.BodyFontPx;

    /// <summary>The rail's station labels are drawn at <see cref="LayoutBudgets.RailLabelFraction"/> of the body size.</summary>
    private const float RailLabelPx = LayoutBudgets.BodyFontPx * LayoutBudgets.RailLabelFraction;

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
            // Every English tab label shows whole under its icon on the 64 px rail (feature plan v4 L7).
            var width = Width(labels[key], RailLabelPx);
            Assert.True(width <= LayoutBudgets.RailLabelRoomLogical, $"{key} \"{labels[key]}\" is {width:0.0} px, wider than the rail's {LayoutBudgets.RailLabelRoomLogical} px of label room");
        }

        foreach (var (key, content, sortable) in FixedColumns)
        {
            // English widens nothing but, on the narrow Level column, room for the sort arrow its pills already give.
            var width = LayoutBudgets.FixedColumnWidth(content, Width(labels[key], HeaderPx), sortable);
            Assert.True(width <= content + LayoutBudgets.SortArrowLogical, $"{key} \"{labels[key]}\" widens its column to {width:0} px");
        }

        var widestState = StateKeys.Max(k => Width(labels[k], LayoutBudgets.BodyFontPx) + (ReasonStates.Contains(k) ? Width(" · …", LayoutBudgets.BodyFontPx) : 0f));
        Assert.Equal(LayoutBudgets.StatusMinLogical, LayoutBudgets.StatusMin(widestState));
    }

    [Theory]
    [MemberData(nameof(AllLanguages))]
    public void The_tab_rail_keeps_its_width_and_ellipsises_a_long_label(string language)
    {
        // The rail no longer widens for a translation (feature plan v4 L7): a label wider than its station ends in
        // an ellipsis and the station's tooltip names the tab. Listed here so a translator can see what is cut.
        var labels = Labels(language);
        foreach (var key in TabKeys)
        {
            var width = Width(labels[key], RailLabelPx);
            if (width > LayoutBudgets.RailLabelRoomLogical)
            {
                output.WriteLine($"{language}: tab {key} \"{labels[key]}\" is {width:0} px of the rail's {LayoutBudgets.RailLabelRoomLogical:0}; it ends in an ellipsis");
            }

            Assert.False(string.IsNullOrWhiteSpace(labels[key]), $"{language}: {key} is empty, so a cut station would have no name in its tooltip");
        }

        Assert.Equal(ScaleMetrics.RailLogical - (2f * LayoutBudgets.RailLabelPadLogical), LayoutBudgets.RailLabelRoomLogical);
    }

    [Theory]
    [MemberData(nameof(AllLanguages))]
    public void Fixed_table_columns_hold_their_headers(string language)
    {
        var labels = Labels(language);
        foreach (var (key, content, sortable) in FixedColumns)
        {
            var header = Width(labels[key], HeaderPx);
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
