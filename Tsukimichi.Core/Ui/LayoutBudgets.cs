namespace Tsukimichi.Core.Ui;

/// <summary>
/// The fixed-width places a translated label has to fit (V2-19), in logical pixels at UI scale 1: the tab rail's
/// label room, the quest table's fixed columns and the quick-view segments. The plugin draws with these numbers
/// (<c>TabStrip</c>, <c>TablePane</c>, <c>Chrome.SegmentedControl</c>) and the layout tests measure every language's
/// labels against them, so a translation that would be cut shows up in a test rather than in game. Where a label is
/// wider than the default room the room grows to fit it, up to the caps here; a label beyond a cap fails the test.
/// </summary>
public static class LayoutBudgets
{
    // ---- Tab rail (TabStrip) ----

    /// <summary>Height of one tab row.</summary>
    public const float TabRowLogical = 30f;

    /// <summary>Space right of a tab's label.</summary>
    public const float TabPadLogical = 6f;

    /// <summary>Space left of a tab's icon.</summary>
    public const float TabInsetLogical = 10f;

    /// <summary>A tab's icon.</summary>
    public const float TabIconLogical = 16f;

    /// <summary>Between a tab's icon and its label.</summary>
    public const float TabGapLogical = 8f;

    /// <summary>What the Journal tab's Ready badge takes from its label's room (a three-digit badge and its gap).</summary>
    public const float TabBadgeReserveLogical = 34f;

    /// <summary>The widest the rail may grow for a long label: beyond this a translation must be shortened.</summary>
    public const float MaxRailLogical = 200f;

    /// <summary>Everything in a tab row that is not its label.</summary>
    public const float TabChromeLogical = TabInsetLogical + TabIconLogical + TabGapLogical + TabPadLogical;

    /// <summary>The label room of a tab at the default rail width (<see cref="ScaleMetrics.RailLogical"/>).</summary>
    public const float TabLabelRoomLogical = ScaleMetrics.RailLogical - TabChromeLogical;

    /// <summary>
    /// The rail width that shows every label whole: <see cref="ScaleMetrics.RailLogical"/>, or wider when the widest
    /// label (or the Journal label beside its badge) needs it, never beyond <see cref="MaxRailLogical"/>.
    /// </summary>
    /// <param name="widestLabel">The widest tab label, in logical pixels.</param>
    /// <param name="journalLabel">The Journal tab's label, which shares its row with the Ready badge.</param>
    public static float RailWidth(float widestLabel, float journalLabel)
    {
        var need = MathF.Max(widestLabel, journalLabel + TabBadgeReserveLogical) + TabChromeLogical;
        return Math.Clamp(MathF.Ceiling(need), ScaleMetrics.RailLogical, MaxRailLogical);
    }

    // ---- Quest table (TablePane) ----

    /// <summary>The level column's least width (its pills set the rest).</summary>
    public const float LevelColumnLogical = 34f;

    /// <summary>The job column: an icon and a short name.</summary>
    public const float JobColumnLogical = 76f;

    /// <summary>The expansion column: a four-letter pill.</summary>
    public const float ExpansionColumnLogical = 40f;

    /// <summary>The status column's least width before Rewards, then Expansion, are hidden to make room.</summary>
    public const float StatusMinLogical = 170f;

    /// <summary>The widest the status column's minimum may grow for a long state name.</summary>
    public const float MaxStatusMinLogical = 260f;

    /// <summary>
    /// The status column's least content width for a language: <see cref="StatusMinLogical"/>, or the widest state name
    /// (a state with a reason already followed by " · …") when that is wider, so the state word, which the column never
    /// cuts, always shows whole; at most <see cref="MaxStatusMinLogical"/>.
    /// </summary>
    public static float StatusMin(float widestStateWord) =>
        Math.Clamp(MathF.Ceiling(widestStateWord), StatusMinLogical, MaxStatusMinLogical);

    /// <summary>ImGui's cell padding either side of a header label (Dalamud's style).</summary>
    public const float CellPaddingLogical = 4f;

    /// <summary>The sort arrow a sorted header draws after its label, with its gap (0.65 of a 16 px line and 4 px).</summary>
    public const float SortArrowLogical = 15f;

    /// <summary>The widest a fixed column may grow for its header: beyond this a translation must be shortened.</summary>
    public const float MaxFixedColumnLogical = 120f;

    /// <summary>
    /// A fixed column's width: what its content needs, or its header label's width with the cell padding (and the sort
    /// arrow when the column sorts) when that is wider, so a translated header is never cut.
    /// </summary>
    public static float FixedColumnWidth(float content, float headerLabel, bool sortable) =>
        MathF.Max(content, headerLabel + 2f * CellPaddingLogical + (sortable ? SortArrowLogical : 0f));

    // ---- Toolbar (Chrome.SegmentedControl) ----

    /// <summary>Padding either side of a quick-view segment's label.</summary>
    public const float SegmentPadLogical = 10f;

    /// <summary>
    /// The toolbar's width at the smallest main window (<see cref="ScaleMetrics.MinWindowSize(float, float)"/> less the
    /// window padding): the quick views, on a row of their own when they must be, fit in it.
    /// </summary>
    public const float MinToolbarLogical = ScaleMetrics.RailLogical + ScaleMetrics.LeftColumnLogical + ScaleMetrics.RightColumnLogical + ScaleMetrics.CentreFloorLogical - 16f;

    /// <summary>The quick views' width: "All" and each view, every segment padded.</summary>
    public static float SegmentsWidth(IEnumerable<float> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        var width = 0f;
        foreach (var label in labels)
        {
            width += label + 2f * SegmentPadLogical;
        }

        return width;
    }

    // ---- Text ----

    /// <summary>The body font's size at UI scale 1 and Dalamud's global scale 1 (Dalamud's 12 pt default).</summary>
    public const float BodyFontPx = 16f;
}
