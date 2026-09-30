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
            var room = key == "TabJournal" ? LayoutBudgets.TabLabelRoomLogical - LayoutBudgets.TabBadgeReserveLogical : LayoutBudgets.TabLabelRoomLogical;
            Assert.True(Width(labels[key], LayoutBudgets.BodyFontPx) <= room, $"{key} \"{labels[key]}\" is wider than its {room} px");
        }

        foreach (var (key, content, sortable) in FixedColumns)
        {
            // English widens nothing but, on the narrow Level column, room for the sort arrow its pills already give.
            var width = LayoutBudgets.FixedColumnWidth(content, Width(labels[key], CaptionPx), sortable);
            Assert.True(width <= content + LayoutBudgets.SortArrowLogical, $"{key} \"{labels[key]}\" widens its column to {width:0} px");
        }

        var widestState = StateKeys.Max(k => Width(labels[k], LayoutBudgets.BodyFontPx) + (ReasonStates.Contains(k) ? Width(" · …", LayoutBudgets.BodyFontPx) : 0f));
        Assert.Equal(LayoutBudgets.StatusMinLogical, LayoutBudgets.StatusMin(widestState));

        var railNeed = LayoutBudgets.RailWidth(TabKeys.Max(k => Width(labels[k], LayoutBudgets.BodyFontPx)), Width(labels["TabJournal"], LayoutBudgets.BodyFontPx));
        Assert.Equal(ScaleMetrics.RailLogical, railNeed);
    }

    [Theory]
    [MemberData(nameof(AllLanguages))]
    public void The_tab_rail_shows_every_label_whole(string language)
    {
        var labels = Labels(language);
        var widest = TabKeys.Max(k => Width(labels[k], LayoutBudgets.BodyFontPx));
        var journal = Width(labels["TabJournal"], LayoutBudgets.BodyFontPx);
        var need = MathF.Max(widest, journal + LayoutBudgets.TabBadgeReserveLogical) + LayoutBudgets.TabChromeLogical;
        var rail = LayoutBudgets.RailWidth(widest, journal);
        foreach (var key in TabKeys)
        {
            var room = key == "TabJournal" ? LayoutBudgets.TabLabelRoomLogical - LayoutBudgets.TabBadgeReserveLogical : LayoutBudgets.TabLabelRoomLogical;
            var width = Width(labels[key], LayoutBudgets.BodyFontPx);
            if (width > room)
            {
                output.WriteLine($"{language}: tab {key} \"{labels[key]}\" is {width:0} px, {room:0} px at the default rail; the rail widens to {rail:0}");
            }
        }

        Assert.True(need <= LayoutBudgets.MaxRailLogical, $"{language}: the tab labels need a {need:0} px rail, more than {LayoutBudgets.MaxRailLogical}; shorten the widest");
        Assert.True(rail >= need - 0.5f, $"{language}: the rail is {rail} px for {need} px");
    }

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
