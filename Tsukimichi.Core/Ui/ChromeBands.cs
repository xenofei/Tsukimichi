namespace Tsukimichi.Core.Ui;

/// <summary>
/// What the window frame is measured from (feature plan v6 U2): the window's width and the type and control sizes, in
/// pixels. It deliberately carries nothing about the content: no filters, scope, notices, chips, character or
/// selection, so no band can change height when any of those change. A test keeps it that way.
/// </summary>
/// <param name="AvailableWidth">The content width the toolbar has.</param>
/// <param name="Scale">Pixels per logical pixel (Dalamud's global scale times the UI scale).</param>
/// <param name="LineHeight">The body text's line height.</param>
/// <param name="ItemSpacingY">ImGui's vertical item spacing.</param>
/// <param name="MinTarget">The least side of a click target.</param>
/// <param name="QuickViewsWidth">The Quick views control's width (its labels at the current font).</param>
/// <param name="FiltersWidth">The Filters button's width.</param>
public readonly record struct ChromeMetrics(
    float AvailableWidth,
    float Scale,
    float LineHeight,
    float ItemSpacingY,
    float MinTarget,
    float QuickViewsWidth,
    float FiltersWidth);

/// <summary>The window frame's bands for one frame (<see cref="ChromeBands.Layout"/>), in pixels.</summary>
/// <param name="ToolbarRows">1, 2 or 3 toolbar rows, from the width alone.</param>
/// <param name="QuickViewsOwnRow">Whether the Quick views take a row of their own (three rows).</param>
/// <param name="SearchWidth">The search pill's width.</param>
/// <param name="ToolbarRow">One toolbar row's height.</param>
/// <param name="Toolbar">The toolbar strip's height.</param>
/// <param name="Lane">The Journal chip lane's height: one chip, never more.</param>
/// <param name="StatusBar">The status bar's height.</param>
public readonly record struct ChromeBandLayout(
    int ToolbarRows,
    bool QuickViewsOwnRow,
    float SearchWidth,
    float ToolbarRow,
    float Toolbar,
    float Lane,
    float StatusBar);

/// <summary>
/// The fixed window frame (feature plan v6 U2): the window never moves under the player. From top to bottom it is the
/// toolbar, the body (rail, tree, centre and detail panes) and the status bar, and nothing is drawn between them; on
/// the Journal the centre pane opens with a title line (the scope, with an × to clear it) and a one-line chip lane.
/// Every band's height comes from <see cref="ChromeMetrics"/>, which knows only the window's width and the sizes of
/// type and controls, so a filter, a scope, a chip, a notice or the character's name can never change one. Notices,
/// the Undo toast and hints float over the body instead (<see cref="FloatingSlots"/>), and filters that overflow the
/// lane go behind a "+N" (<see cref="ChipLane"/>). Pure, so the rule is tested.
/// </summary>
public static class ChromeBands
{
    /// <summary>One toolbar row, logical.</summary>
    public const float ToolbarRowLogical = 36f;

    /// <summary>Below this much available width (pixels) the toolbar always takes two rows (accessibility B6).</summary>
    public const float ToolbarReflowPx = 1000f;

    /// <summary>The gap between toolbar controls, logical.</summary>
    public const float ToolbarGapLogical = 8f;

    /// <summary>The search pill's width, and the least it shrinks to before the Quick views take their own row, logical.</summary>
    public const float SearchLogical = 280f;

    public const float SearchMinLogical = 160f;

    /// <summary>
    /// The room the character chip is given when deciding the rows, logical. A fixed slot, not the chip's natural width,
    /// so logging in as a character with a longer name never reflows the toolbar; a longer name is clipped in the chip.
    /// </summary>
    public const float CharacterSlotLogical = 220f;

    /// <summary>The character chip's floor on a two-row toolbar, logical.</summary>
    public const float CharacterMinLogical = 120f;

    /// <summary>The gap between the Back and Forward buttons (feature plan v7 N1), logical.</summary>
    public const float HistoryGapLogical = 2f;

    /// <summary>
    /// The Back and Forward buttons' width (feature plan v7 N1): two round buttons of the minimum target with
    /// <see cref="HistoryGapLogical"/> between them. They lead the row that holds the Filters button: the only row on a
    /// one-row toolbar, so they sit before the search as a browser's do, or the last row's start when it reflows.
    /// </summary>
    public static float HistoryWidth(float scale, float minTarget) => (2f * minTarget) + (HistoryGapLogical * scale);

    /// <summary>A filter chip's height, logical (it is never under the line plus 4 px, nor under the minimum target).</summary>
    public const float ChipHeightLogical = 22f;

    /// <summary>The chip height at these sizes; the chip lane is exactly this tall.</summary>
    public static float ChipHeight(float scale, float lineHeight, float minTarget) =>
        MathF.Max(MathF.Max(ChipHeightLogical * scale, lineHeight + (4f * scale)), minTarget);

    /// <summary>One toolbar row's height: 36 logical, never less than the minimum target plus 6.</summary>
    public static float ToolbarRowHeight(float scale, float minTarget) =>
        MathF.Max(ToolbarRowLogical * scale, minTarget + (6f * scale));

    /// <summary>
    /// The status bar's height: one body line with its spacing, plus the spacing above and below the rule.
    /// </summary>
    public static float StatusBarHeight(float lineHeight, float itemSpacingY) => lineHeight + (3f * itemSpacingY);

    /// <summary>
    /// The status bar's height at a Decoration level's style (docs/design/flair-v13 §1, "Status bar": 30, 26 and 20 px
    /// at the default sizes): the line with three spacings at Full, two at Quiet and one at Plain. Like every band it
    /// follows a setting and the type sizes alone, never the content.
    /// </summary>
    public static float StatusBarHeight(float lineHeight, float itemSpacingY, StatusBarStyle style) => style switch
    {
        StatusBarStyle.Text => lineHeight + itemSpacingY,
        StatusBarStyle.Quiet => lineHeight + (2f * itemSpacingY),
        _ => StatusBarHeight(lineHeight, itemSpacingY),
    };

    /// <summary>The frame's bands at <paramref name="m"/>.</summary>
    public static ChromeBandLayout Layout(in ChromeMetrics m)
    {
        var scale = m.Scale > 0f && float.IsFinite(m.Scale) ? m.Scale : 1f;
        var avail = MathF.Max(1f, m.AvailableWidth);
        var gap = ToolbarGapLogical * scale;
        var search = SearchLogical * scale;
        var character = CharacterSlotLogical * scale;
        var history = HistoryWidth(scale, m.MinTarget);
        var oneRow = history + search + m.QuickViewsWidth + m.FiltersWidth + character + (4f * gap);

        // Row 0 holds the search (and the Quick views when they fit beside it); the last row Back and Forward, the Filters
        // button and the character chip. As in 1.10, but decided from the width alone.
        var twoRows = avail < ToolbarReflowPx || oneRow > avail;
        var quickOwnRow = false;
        if (twoRows)
        {
            search = MathF.Min(search, avail - gap - m.QuickViewsWidth);
            if (search < SearchMinLogical * scale)
            {
                quickOwnRow = true;
                search = MathF.Min(SearchLogical * scale, avail);
            }
        }

        var rows = twoRows ? (quickOwnRow ? 3 : 2) : 1;
        var row = ToolbarRowHeight(scale, m.MinTarget);
        return new ChromeBandLayout(
            rows,
            quickOwnRow,
            MathF.Max(1f, search),
            row,
            rows * row,
            ChipHeight(scale, m.LineHeight, m.MinTarget),
            StatusBarHeight(m.LineHeight, m.ItemSpacingY));
    }
}
