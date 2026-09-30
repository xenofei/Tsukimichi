using Tsukimichi.Core.Query;
using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for the patch of origin (P8): the "Added in" filter and its chip, and the Unlocks quick view's "New in
/// 7.5x" group. The detail pane's "Added in" line lives with the hero's strings (<c>DetailAddedInFormat</c>).
/// </summary>
static partial class Strings
{
    public static string AddedIn => FilterNames.Display(FilterNames.AddedIn);
    public static string AddedInTooltip => Loc.Get("AddedInTooltip");
    public static string AddedInAny => Loc.Get("AddedInAny");

    /// <summary>A series in the combo: {0} = series ("7.5"), {1} = quests it added.</summary>
    public static string AddedInOptionFormat => Loc.Get("AddedInOptionFormat");

    /// <summary>The chip: {0} = series ("7.5").</summary>
    public static string AddedInChipFormat => Loc.Get("AddedInChipFormat");

    /// <summary>Above the Unlocks quick view: {0} = the newest patch series in the data ("7.5"), {1} = its unlock quests in the table.</summary>
    public static string NewThisPatchCaptionFormat => Loc.Get("NewThisPatchCaptionFormat");
    public static string NewThisPatchCaptionOneFormat => Loc.Get("NewThisPatchCaptionOneFormat");
}
