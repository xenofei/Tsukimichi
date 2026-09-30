using Tsukimichi.Core.Query;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for the patch of origin (P8): the "Added in" filter and its chip, and the Unlocks quick view's "New in
/// 7.5x" group. The detail pane's "Added in" line lives with the hero's strings (<c>DetailAddedInFormat</c>).
/// </summary>
static partial class Strings
{
    public const string AddedIn = FilterNames.AddedIn;
    public const string AddedInTooltip = "Keep only the quests one patch series added: 7.5x is 7.5, 7.51, 7.55 and the rest of 7.5. Quests whose patch is not known are hidden while it is set";
    public const string AddedInAny = "Any patch";

    /// <summary>A series in the combo: {0} = series ("7.5"), {1} = quests it added.</summary>
    public const string AddedInOptionFormat = "{0}x  ({1})";

    /// <summary>The chip: {0} = series ("7.5").</summary>
    public const string AddedInChipFormat = "Added in {0}x";

    /// <summary>Above the Unlocks quick view: {0} = the newest patch series in the data ("7.5"), {1} = its unlock quests in the table.</summary>
    public const string NewThisPatchCaptionFormat = "New in {0}x: {1} unlock quests, listed first";
    public const string NewThisPatchCaptionOneFormat = "New in {0}x: 1 unlock quest, listed first";
}
