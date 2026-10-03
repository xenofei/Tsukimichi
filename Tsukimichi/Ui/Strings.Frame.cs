using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for the fixed window frame (feature plan v6 U2, U3): the chip lane, the notice dock and the filter drawer.
/// </summary>
public static partial class Strings
{
    // Chip lane
    public static string ChipLaneMoreTooltip => Loc.Get("ChipLaneMoreTooltip");
    public static string SelectedHiddenChip => Loc.Get("SelectedHiddenChip");
    public static string SelectedHiddenChipTooltip => Loc.Get("SelectedHiddenChipTooltip");

    // Notice dock
    /// <summary>{0} = the waiting card's title ("What's new in 1.12.0", "Since you were away").</summary>
    public static string DockCardWaitingFormat => Loc.Get("DockCardWaitingFormat");

    public static string DockCardShow => Loc.Get("DockCardShow");
    public static string DockCardShowTooltip => Loc.Get("DockCardShowTooltip");
    public static string DockPreviousTooltip => Loc.Get("DockPreviousTooltip");
    public static string DockNextTooltip => Loc.Get("DockNextTooltip");
    public static string DockCloseTooltip => Loc.Get("DockCloseTooltip");
    public static string DockFoldTooltip => Loc.Get("DockFoldTooltip");
    public static string DockUnfoldTooltip => Loc.Get("DockUnfoldTooltip");
    public static string DockChipRebuildFailed => Loc.Get("DockChipRebuildFailed");
    public static string DockChipFreshness => Loc.Get("DockChipFreshness");

    // Filter drawer
    public static string FilterDrawerPinTooltip => Loc.Get("FilterDrawerPinTooltip");
    public static string FilterDrawerUnpinTooltip => Loc.Get("FilterDrawerUnpinTooltip");
    public static string FilterDrawerCloseTooltip => Loc.Get("FilterDrawerCloseTooltip");
}
