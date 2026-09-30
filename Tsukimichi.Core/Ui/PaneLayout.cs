namespace Tsukimichi.Core.Ui;

/// <summary>
/// The widths of the main window's four panes for one frame, in pixels: the tab rail, the left pane (the Journal
/// tree, or another tab's list), the centre and the detail pane, with the gutter between each pair. The panes sit
/// left to right as rail · gutter · tree · gutter · centre · gutter · detail; the second and third gutters hold the
/// drag handles. <see cref="TreeStrip"/> says the tree is drawn as its icon strip.
/// </summary>
/// <param name="Rail">The rail's width.</param>
/// <param name="Tree">The left pane's width.</param>
/// <param name="Centre">The centre pane's width.</param>
/// <param name="Detail">The detail pane's width.</param>
/// <param name="Gutter">The width of each of the three gutters.</param>
/// <param name="TreeStrip">Whether the tree is the icon strip (<see cref="PaneLayout.TreeStripLogical"/> wide).</param>
public readonly record struct PaneWidths(float Rail, float Tree, float Centre, float Detail, float Gutter, bool TreeStrip)
{
    /// <summary>Everything the layout takes: the four panes and the three gutters.</summary>
    public float Total => Rail + Tree + Centre + Detail + (PaneLayout.GutterCount * Gutter);
}

/// <summary>
/// The pure arithmetic of the main window's pane splitter (feature plan v4 L1, UI audit §4): each pane has a floor in
/// logical pixels that no drag and no window size goes under, the side panes keep the width the user dragged them to
/// in logical units (so they follow the UI scale), and when the window runs out of room the detail pane gives way
/// first, then the tree, each down to its floor; the centre gives up its own floor only when both sides are at theirs.
/// Dragged below <see cref="StripSnapLogical"/> the tree snaps to a <see cref="TreeStripLogical"/> strip of icons, and
/// it comes back only past the snap plus <see cref="StripHysteresisLogical"/>, so a hand resting on the threshold
/// does not make it flicker. The plugin's <c>PaneSplit</c> draws with these numbers.
/// </summary>
public static class PaneLayout
{
    /// <summary>The narrowest the labelled tree (or another tab's list) may be.</summary>
    public const float TreeFloorLogical = 180f;

    /// <summary>The narrowest the centre pane (the quest table) may be.</summary>
    public const float CentreFloorLogical = 320f;

    /// <summary>The narrowest the detail pane may be.</summary>
    public const float DetailFloorLogical = 260f;

    /// <summary>The tree's width on a new install and after a double-click on its handle.</summary>
    public const float TreeDefaultLogical = 300f;

    /// <summary>The detail pane's width on a new install and after a double-click on its handle.</summary>
    public const float DetailDefaultLogical = 360f;

    /// <summary>The tree's width as a strip of icons.</summary>
    public const float TreeStripLogical = 56f;

    /// <summary>The tree dragged narrower than this snaps to the strip.</summary>
    public const float StripSnapLogical = 150f;

    /// <summary>How far past <see cref="StripSnapLogical"/> the strip must be dragged before it opens again.</summary>
    public const float StripHysteresisLogical = 16f;

    /// <summary>The widest a stored side pane width is taken to be; a larger value (a hand-edited file) is clamped.</summary>
    public const float MaxSideLogical = 1600f;

    /// <summary>A gutter between two panes: the drag handle's hit area, with a 1 px line down its middle.</summary>
    public const float GutterLogical = 6f;

    /// <summary>The gutters: rail | tree (a plain line), tree | centre and centre | detail (the handles).</summary>
    public const int GutterCount = 3;

    /// <summary>The three panes' floors together.</summary>
    public const float FloorsLogical = TreeFloorLogical + CentreFloorLogical + DetailFloorLogical;

    /// <summary>The floors and the gutters: what the window needs beside the rail so that every floor holds.</summary>
    public const float MinContentLogical = FloorsLogical + (GutterCount * GutterLogical);

    /// <summary>
    /// The most <see cref="MinContentPx"/> exceeds <see cref="MinContentLogical"/> times the scale: the side floors are
    /// whole pixels rounded up (under 1 px each) and each gutter is rounded to a whole pixel (at most 0.5 px each).
    /// <c>ScaleMetrics.MinWindowSize</c> reserves it, so the floors hold at the smallest window.
    /// </summary>
    public const float RoundingReservePx = 4f;

    /// <summary>A pane floor in whole pixels at <paramref name="scale"/>: rounded up, so the floor always holds (180 at 1.3 is 234, never 233).</summary>
    public static float FloorPx(float floorLogical, float scale) => MathF.Ceiling((floorLogical * SafeScale(scale)) - 1e-3f);

    /// <summary>A gutter in whole pixels at <paramref name="scale"/>.</summary>
    public static float GutterPx(float scale) => MathF.Round(GutterLogical * SafeScale(scale));

    /// <summary>
    /// The pixels the panes need beside the rail at <paramref name="scale"/> so that every floor holds as
    /// <see cref="Solve"/> rounds it: the tree's and the detail pane's floors in whole pixels, the centre's floor and the
    /// three gutters. At most <see cref="MinContentLogical"/> × scale + <see cref="RoundingReservePx"/>.
    /// </summary>
    public static float MinContentPx(float scale) =>
        FloorPx(TreeFloorLogical, scale) + FloorPx(DetailFloorLogical, scale) + (CentreFloorLogical * SafeScale(scale)) + (GutterCount * GutterPx(scale));

    /// <summary>A stored tree width as the layout reads it: the default when unreadable, else within the floor and the cap.</summary>
    public static float SanitizeTree(float logical) => Sanitize(logical, TreeFloorLogical, TreeDefaultLogical);

    /// <summary>A stored detail width as the layout reads it: the default when unreadable, else within the floor and the cap.</summary>
    public static float SanitizeDetail(float logical) => Sanitize(logical, DetailFloorLogical, DetailDefaultLogical);

    /// <summary>
    /// Whether a tree the user is dragging to <paramref name="wantedLogical"/> is the strip: under
    /// <see cref="StripSnapLogical"/> it snaps shut; once shut it opens again only at the snap plus
    /// <see cref="StripHysteresisLogical"/>. An unreadable width keeps the current state.
    /// </summary>
    /// <param name="wantedLogical">Where the drag puts the tree's right edge, in logical pixels from its left.</param>
    /// <param name="wasStrip">Whether the tree is the strip now.</param>
    public static bool Strip(float wantedLogical, bool wasStrip)
    {
        if (float.IsNaN(wantedLogical))
        {
            return wasStrip;
        }

        return wasStrip ? wantedLogical < StripSnapLogical + StripHysteresisLogical : wantedLogical < StripSnapLogical;
    }

    /// <summary>
    /// The panes' widths for a body <paramref name="total"/> pixels wide. The side panes start from what the user asked
    /// for (never under their floors); when they and the centre's floor do not fit, the detail pane shrinks to its floor,
    /// then the tree to its floor (the strip cannot shrink). Only then does the centre go under its floor, and when
    /// even the centre is gone the detail pane, then the tree, give up the rest: no width is ever negative. Side widths
    /// are whole pixels; the centre takes the remainder, so the panes and gutters add up to the body exactly (unless
    /// the body is narrower than the rail and the gutters).
    /// </summary>
    /// <param name="total">The body's width in pixels (the window's content width).</param>
    /// <param name="rail">The rail's width in pixels.</param>
    /// <param name="leftWanted">The tree's width the user chose, in logical pixels.</param>
    /// <param name="rightWanted">The detail pane's width the user chose, in logical pixels.</param>
    /// <param name="scale">Pixels per logical unit (global scale × UI scale).</param>
    /// <param name="treeStrip">Whether the tree is the icon strip.</param>
    public static PaneWidths Solve(float total, float rail, float leftWanted, float rightWanted, float scale, bool treeStrip = false)
    {
        var s = SafeScale(scale);
        var railPx = float.IsFinite(rail) ? MathF.Max(0f, rail) : 0f;
        var gutter = GutterPx(s);
        var content = MathF.Max(0f, (float.IsFinite(total) ? total : 0f) - railPx - (GutterCount * gutter));

        // Floors round up so they hold; an asked width rounds down but never under its floor.
        var treeMin = FloorPx(treeStrip ? TreeStripLogical : TreeFloorLogical, s);
        var detailMin = FloorPx(DetailFloorLogical, s);
        var tree = treeStrip ? treeMin : MathF.Max(treeMin, MathF.Floor(SanitizeTree(leftWanted) * s));
        var detail = MathF.Max(detailMin, MathF.Floor(SanitizeDetail(rightWanted) * s));
        var centreFloor = CentreFloorLogical * s;

        // Out of room: the detail pane first, then the tree, each down to its floor.
        var excess = tree + detail + centreFloor - content;
        if (excess > 0f)
        {
            var give = MathF.Min(excess, detail - detailMin);
            detail -= MathF.Ceiling(give);
            excess -= give;
        }

        if (excess > 0f)
        {
            var give = MathF.Min(excess, tree - treeMin);
            tree -= MathF.Ceiling(give);
        }

        // Still too narrow: the centre goes under its floor; past nothing, the sides give up the rest.
        var centre = content - tree - detail;
        if (centre < 0f)
        {
            var deficit = -centre;
            centre = 0f;
            var give = MathF.Min(deficit, detail);
            detail -= give;
            deficit -= give;
            tree = MathF.Max(0f, tree - deficit);
        }

        return new PaneWidths(railPx, tree, centre, detail, gutter, treeStrip);
    }

    /// <summary>
    /// The logical width a side pane is dragged to: its width when the drag began plus how far the mouse has moved
    /// since, in logical units. <paramref name="deltaPx"/> is signed so that positive widens the pane (the caller
    /// negates the mouse's movement for the detail pane, whose handle is on its left).
    /// </summary>
    public static float Dragged(float startLogical, float deltaPx, float scale)
    {
        var s = float.IsFinite(scale) && scale > 0f ? scale : 1f;
        var delta = float.IsFinite(deltaPx) ? deltaPx : 0f;
        return (float.IsFinite(startLogical) ? startLogical : 0f) + (delta / s);
    }

    private static float SafeScale(float scale) => float.IsFinite(scale) && scale > 0f ? scale : 1f;

    private static float Sanitize(float logical, float floor, float fallback) =>
        float.IsFinite(logical) && logical > 0f ? Math.Clamp(logical, floor, MaxSideLogical) : fallback;
}
