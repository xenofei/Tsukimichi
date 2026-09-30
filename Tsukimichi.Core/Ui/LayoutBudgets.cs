namespace Tsukimichi.Core.Ui;

/// <summary>
/// The Journal tree's width tiers (feature plan v4 L3, UI audit §4.3, design v4 §8.2), widest first. Each row is then
/// fitted by <see cref="RowFit"/> within its tier, so a long name can still push a part out of its own row.
/// </summary>
public enum TreeTier
{
    /// <summary>At least <see cref="LayoutBudgets.TreeFullLogical"/>: name, expansion pill, Ready pill, mini bar and count.</summary>
    Full = 0,

    /// <summary>The expansion pill goes; the mini bar goes too where the name needs its room.</summary>
    Trim = 1,

    /// <summary>The mini bar goes and the count becomes a percentage; complete nodes show no count.</summary>
    Compact = 2,

    /// <summary>No count (the ring alone carries progress); the Ready pill becomes a gold dot on the glyph.</summary>
    Slim = 3,
}

/// <summary>How the rail fills its height this frame (<see cref="LayoutBudgets.FitRail"/>), in logical pixels.</summary>
/// <param name="Crest">The crest's size; 0 hides it.</param>
/// <param name="Station">Each station's height.</param>
/// <param name="Percent">Whether the gauge's percentage shows under it.</param>
/// <param name="FootAnchored">Whether the foot sits at the bottom; false when the rail is taller than its pane and scrolls.</param>
public readonly record struct RailFit(float Crest, float Station, bool Percent, bool FootAnchored);

/// <summary>
/// The fixed-width places a translated label has to fit (V2-19), in logical pixels at UI scale 1: the tab rail's
/// label room, the quest table's fixed columns and the quick-view segments. The plugin draws with these numbers
/// (<c>TabStrip</c>, <c>TablePane</c>, <c>Chrome.SegmentedControl</c>) and the layout tests measure every language's
/// labels against them, so a translation that would be cut shows up in a test rather than in game. Where a label is
/// wider than the default room the room grows to fit it, up to the caps here; a label beyond a cap fails the test.
/// </summary>
public static class LayoutBudgets
{
    // ---- Tab rail (TabStrip; feature plan v4 L7, design v4 §7.1) ----

    /// <summary>A station of the labelled rail: a 22 px icon with the tab's label under it.</summary>
    public const float StationLogical = 54f;

    /// <summary>The shortest a labelled station gets when the window is too short for the whole rail.</summary>
    public const float StationMinLogical = 44f;

    /// <summary>A station of the compact rail: the icon alone.</summary>
    public const float CompactStationLogical = 40f;

    /// <summary>The shortest a compact station gets (still at least the 26 px click target).</summary>
    public const float CompactStationMinLogical = 30f;

    /// <summary>A station's icon box.</summary>
    public const float StationIconLogical = 22f;

    /// <summary>Between a station's icon and its label.</summary>
    public const float StationGapLogical = 4f;

    /// <summary>The rail's labels (and the gauge's percentage) are drawn at this fraction of the body font.</summary>
    public const float RailLabelFraction = 0.7f;

    /// <summary>Space either side of a station's label.</summary>
    public const float RailLabelPadLogical = 2f;

    /// <summary>A station label's room (the rail less its pads); the label is measured at its own, smaller size.</summary>
    public const float RailLabelRoomLogical = ScaleMetrics.RailLogical - (2f * RailLabelPadLogical);

    /// <summary>The crest on top of the rail, and its size when the window is short.</summary>
    public const float CrestLogical = 40f;

    /// <summary>The crest on a short window or the compact rail.</summary>
    public const float CrestSmallLogical = 28f;

    /// <summary>The overall gauge at the rail's foot.</summary>
    public const float RailGaugeLogical = 36f;

    /// <summary>A round button at the rail's foot (Help, Settings): the minimum click target.</summary>
    public const float RailButtonLogical = 26f;

    /// <summary>Between the parts of the rail (crest, stations, foot) and inside the foot.</summary>
    public const float RailGapLogical = 6f;

    /// <summary>Above the crest and below the foot.</summary>
    public const float RailPadLogical = 8f;

    /// <summary>
    /// Main windows narrower than this (Dalamud-scaled units at the default UI scale, scaling with it) get the compact
    /// rail by themselves.
    /// </summary>
    public const float CompactRailWindowLogical = 1040f;

    /// <summary>
    /// Whether the rail is compact this frame: always when the user chose it, else when the window is narrower than
    /// <see cref="CompactRailWindowLogical"/> (× the UI scale over the default); once compact it widens again only past
    /// that plus <see cref="HysteresisLogical"/>, so a window resting on the threshold does not flicker. An unreadable
    /// width keeps the current state.
    /// </summary>
    /// <param name="windowWidth">The main window's width in Dalamud-scaled units (pixels over the global scale).</param>
    /// <param name="uiScale">The UI scale.</param>
    /// <param name="wasCompact">Whether the rail was compact last frame.</param>
    /// <param name="forced">Settings › Display › Compact rail.</param>
    public static bool CompactRail(float windowWidth, float uiScale, bool wasCompact, bool forced)
    {
        if (forced)
        {
            return true;
        }

        if (!float.IsFinite(windowWidth))
        {
            return wasCompact;
        }

        var factor = ScaleMetrics.ClampUiScale(uiScale) / ScaleMetrics.DefaultUiScale;
        var threshold = CompactRailWindowLogical * factor;
        return wasCompact ? windowWidth < threshold + (HysteresisLogical * factor) : windowWidth < threshold;
    }

    /// <summary>
    /// How the rail fills a pane <paramref name="heightLogical"/> tall with <paramref name="stations"/> stations: the
    /// crest, the stations, and the foot (gauge, its percentage, Help and Settings) held to the bottom. When the
    /// height runs short the rail gives up, in order: the crest's full size, the percentage (the gauge's tooltip still
    /// has it), the stations' spare height, the crest; past that the foot follows the stations and the rail scrolls.
    /// </summary>
    public static RailFit FitRail(float heightLogical, int stations, bool compact)
    {
        var height = float.IsFinite(heightLogical) ? MathF.Max(0f, heightLogical) : 0f;
        var count = Math.Max(0, stations);
        var station = compact ? CompactStationLogical : StationLogical;
        var stationMin = compact ? CompactStationMinLogical : StationMinLogical;
        var crest = compact ? CrestSmallLogical : CrestLogical;

        var fit = new RailFit(crest, station, Percent: true, FootAnchored: true);
        if (RailHeight(fit, count, compact) <= height)
        {
            return fit;
        }

        fit = fit with { Crest = CrestSmallLogical };
        if (RailHeight(fit, count, compact) <= height)
        {
            return fit;
        }

        fit = fit with { Percent = false };
        if (RailHeight(fit, count, compact) <= height)
        {
            return fit;
        }

        // The stations give up their spare height, as far as their minimum.
        var spare = RailHeight(fit, count, compact) - height;
        var give = count > 0 ? MathF.Min(station - stationMin, spare / count) : 0f;
        fit = fit with { Station = station - give };
        if (RailHeight(fit, count, compact) <= height + 0.01f)
        {
            return fit;
        }

        fit = fit with { Crest = 0f };
        return RailHeight(fit, count, compact) <= height + 0.01f ? fit : fit with { FootAnchored = false };
    }

    /// <summary>The height a rail laid out as <paramref name="fit"/> needs, from the top pad to the bottom pad.</summary>
    public static float RailHeight(RailFit fit, int stations, bool compact)
    {
        var crest = fit.Crest > 0f ? fit.Crest + RailGapLogical : 0f;
        return RailPadLogical + crest + (Math.Max(0, stations) * fit.Station) + RailGapLogical + FootHeight(fit.Percent, compact) + RailPadLogical;
    }

    /// <summary>
    /// The foot's height: the gauge, the percentage under it (a line at the label size), and the two buttons side by
    /// side on the labelled rail or one above the other on the compact rail.
    /// </summary>
    public static float FootHeight(bool percent, bool compact)
    {
        var percentLine = percent ? (BodyFontPx * RailLabelFraction) + RailGapLogical : 0f;
        var buttons = compact ? (2f * RailButtonLogical) + RailGapLogical : RailButtonLogical;
        return RailGaugeLogical + RailGapLogical + percentLine + buttons;
    }

    // ---- Quest table (TablePane) ----

    /// <summary>The level column's least width (its pills set the rest).</summary>
    public const float LevelColumnLogical = 34f;

    /// <summary>The job column: an icon and a short name.</summary>
    public const float JobColumnLogical = 76f;

    /// <summary>The expansion column: a four-letter pill.</summary>
    public const float ExpansionColumnLogical = 40f;

    /// <summary>The room a status line needs for the longest English state name with " · …" after it (the translation budget).</summary>
    public const float StatusMinLogical = 170f;

    /// <summary>The widest the status column's minimum may grow for a long state name.</summary>
    public const float MaxStatusMinLogical = 260f;

    /// <summary>
    /// The status column's least content width for a language: <see cref="StatusMinLogical"/>, or the widest state name
    /// (a state with a reason already followed by " · …") when that is wider, so the state word, which the column never
    /// cuts, always shows whole; at most <see cref="MaxStatusMinLogical"/>. This is the translation budget the layout
    /// tests check; the quest table sizes its status column from the measured state word
    /// (<see cref="TableGeometry.QuestColumnSpecs"/>).
    /// </summary>
    public static float StatusMin(float widestStateWord) =>
        float.IsNaN(widestStateWord) ? StatusMinLogical : Math.Clamp(MathF.Ceiling(widestStateWord), StatusMinLogical, MaxStatusMinLogical);

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
    /// window padding: the rail, the panes' floors and the gutters): the quick views, on a row of their own when they
    /// must be, fit in it.
    /// </summary>
    public const float MinToolbarLogical = ScaleMetrics.RailCompactLogical + PaneLayout.MinContentLogical;

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

    // ---- Responsive breakpoints (feature plan v4 L2; UI audit §4, design v4 §8.2) ----
    // Logical pixels of the pane's own width. The pane floors themselves live in PaneLayout; the lowest tier of each
    // pane starts at its floor, which the layout tests check.

    /// <summary>
    /// How far past a breakpoint a width must move back before a hidden part (a table column, a row part) returns,
    /// so a width resting on a threshold does not make it flicker.
    /// </summary>
    public const float HysteresisLogical = 16f;

    /// <summary>The least room a row keeps for its name before a row part is dropped (RowFit).</summary>
    public const float RowNameMinLogical = 48f;

    /// <summary>Tree: every part of a row shows (name, patch caption, expansion pill, Ready pill, count, bar).</summary>
    public const float TreeFullLogical = 300f;

    /// <summary>Tree: the expansion pill, then the mini bar, go.</summary>
    public const float TreeTrimLogical = 240f;

    /// <summary>Tree: the count becomes a percentage; complete nodes show no count.</summary>
    public const float TreeCompactLogical = 200f;

    /// <summary>Tree: the ring alone carries progress and the Ready pill becomes a dot; the tree's floor.</summary>
    public const float TreeSlimLogical = PaneLayout.TreeFloorLogical;

    /// <summary>
    /// The tree's tier for a pane <paramref name="widthLogical"/> wide: it narrows as soon as the width goes under a
    /// breakpoint and widens again only at the breakpoint plus <see cref="HysteresisLogical"/>, so a pane resting on a
    /// threshold does not flicker. An unreadable width keeps <paramref name="previous"/>.
    /// </summary>
    public static TreeTier TreeTierFor(float widthLogical, TreeTier previous)
    {
        if (!float.IsFinite(widthLogical))
        {
            return previous;
        }

        var tier = previous is < TreeTier.Full or > TreeTier.Slim ? TreeTier.Full : previous;
        while (tier > TreeTier.Full && widthLogical >= TreeTierFloor(tier - 1) + HysteresisLogical)
        {
            tier--;
        }

        while (tier < TreeTier.Slim && widthLogical < TreeTierFloor(tier))
        {
            tier++;
        }

        return tier;
    }

    /// <summary>The narrowest pane a tier holds at (its breakpoint); the last tier has none.</summary>
    public static float TreeTierFloor(TreeTier tier) => tier switch
    {
        TreeTier.Full => TreeFullLogical,
        TreeTier.Trim => TreeTrimLogical,
        TreeTier.Compact => TreeCompactLogical,
        _ => 0f,
    };

    // Quest table tiers (design v4 §8.2), each the least width of its tier: where the English table at UI scale 1 takes
    // that step under the column plan (TableGeometry.PlanQuestTable, measured with Dalamud's font; a test keeps them
    // within half the hysteresis of it). The table itself plans from the widths it measures, so another language or
    // scale moves the steps instead of cutting a column.

    /// <summary>Quest table: every column shows, the job with its label; Name and Status stretch 3 : 2. Under this Rewards hides.</summary>
    public const float TableFullLogical = 664f;

    /// <summary>Quest table: Rewards hidden. Under this Expansion hides too.</summary>
    public const float TableNoRewardsLogical = 586f;

    /// <summary>Quest table: Rewards and Expansion hidden. Under this the job shows its icon alone, then hides.</summary>
    public const float TableNoExpansionLogical = 534f;

    /// <summary>Quest table: the job as an icon, then hidden. Under this Level hides too (its level stays in the name's hover card).</summary>
    public const float TableNoJobLogical = 434f;

    /// <summary>Quest table: glyph, name and status only. Under this the rows go two-line, since the status would no longer fit beside the name.</summary>
    public const float TableNoLevelLogical = 386f;

    /// <summary>Quest table: two-line rows (the name and the level, the status under them), down to the centre's floor.</summary>
    public const float TableTwoLineLogical = PaneLayout.CentreFloorLogical;

    /// <summary>
    /// Quest table: rows are two-line under this width however short the state words are (design v4 §8.2); they are
    /// two-line wider than this too wherever the glyph, the name and the status stop fitting one line.
    /// </summary>
    public const float TableTwoLineUnderLogical = 360f;

    // The quest table's column plan (feature plan v4 L4, UI audit §4.3).

    /// <summary>Quest table: the name column's least width; it never hides and takes three shares of the spare room.</summary>
    public const float TableNameMinLogical = 140f;

    /// <summary>Quest table: the room past the widest state word in the status column's least width.</summary>
    public const float TableStatusPadLogical = 24f;

    /// <summary>Quest table: the name column's share of the room the columns' minimums leave.</summary>
    public const float TableNameWeight = 3f;

    /// <summary>Quest table: the status column's share of the room the columns' minimums leave.</summary>
    public const float TableStatusWeight = 2f;

    /// <summary>Quest table: the gap between the two lines of a two-line row.</summary>
    public const float TableTwoLineGapLogical = 2f;

    /// <summary>Detail pane D1: the full hero, requirements as a grid.</summary>
    public const float DetailFullLogical = 340f;

    /// <summary>Detail pane D2: a smaller moon, chips wrap by segment, the action extras fold into "…".</summary>
    public const float DetailMediumLogical = 320f;

    /// <summary>Detail pane D3: the moon above the title, requirements label over value; the pane's floor.</summary>
    public const float DetailNarrowLogical = PaneLayout.DetailFloorLogical;

    /// <summary>My blues: one line per row at or above this.</summary>
    public const float PlanOneLineLogical = 560f;

    /// <summary>My blues: under this Flag and Reveal fold into one "…" menu.</summary>
    public const float PlanMenuLogical = 420f;

    /// <summary>Moonlit: under this the toolbar takes two rows.</summary>
    public const float MoonlitTwoRowToolbarLogical = 560f;

    /// <summary>
    /// Label beside value (<c>Chrome.LabelValue</c>) while the value keeps at least this many ems of room; under it the
    /// label goes above the value.
    /// </summary>
    public const float LabelValueMinEm = 10f;

    /// <summary>
    /// Whether a label and its value stack (label above value) in <paramref name="available"/> pixels: the label takes
    /// <paramref name="labelWidth"/> and <paramref name="gap"/>, and the value would be left under
    /// <see cref="LabelValueMinEm"/> ems of <paramref name="em"/> pixels. Unreadable widths stack.
    /// </summary>
    public static bool StackLabelValue(float available, float labelWidth, float gap, float em)
    {
        if (!float.IsFinite(available) || !float.IsFinite(labelWidth))
        {
            return true;
        }

        var room = available - MathF.Max(0f, labelWidth) - (float.IsFinite(gap) ? MathF.Max(0f, gap) : 0f);
        var need = LabelValueMinEm * (float.IsFinite(em) && em > 0f ? em : BodyFontPx);
        return room < need;
    }
}
