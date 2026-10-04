using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for the hand-in items (1.6.0): the detail pane's Hand in section, its Artisan and GatherBuddy hand-offs,
/// "Copy missing items", the "Needed for" item hint and menu entry, and their settings. Prefixed <c>HandIn</c> (and
/// <c>Items</c> for the hint) so this half of the partial class never collides with the others.
/// </summary>
static partial class Strings
{
    // ---- Detail pane section ----
    public static string HandInSection => Loc.Get("HandInSection");

    /// <summary>The section's only line while the spoiler shield masks the quest's name.</summary>
    public static string HandInMasked => Loc.Get("HandInMasked");

    /// <summary>{0} = items the character holds enough of, {1} = items the quest asks for.</summary>
    public static string HandInCaptionFormat => Loc.Get("HandInCaptionFormat");

    /// <summary>{0} = how many the quest asks for.</summary>
    public static string HandInAmountFormat => Loc.Get("HandInAmountFormat");

    public static string HandInHq => Loc.Get("HandInHq");

    /// <summary>Between two job categories one supply row is for ("MIN / BTN").</summary>
    public const string HandInJobSeparator = " / ";

    /// <summary>{0} = how many the logged-in character holds.</summary>
    public static string HandInHaveFormat => Loc.Get("HandInHaveFormat");

    /// <summary>{0} = held by the character, {1} = held by its retainers (Allagan Tools).</summary>
    public static string HandInHaveWithRetainersFormat => Loc.Get("HandInHaveWithRetainersFormat");

    /// <summary>{0} = held by the retainers, while the game read is paused.</summary>
    public static string HandInRetainersOnlyFormat => Loc.Get("HandInRetainersOnlyFormat");

    public static string HandInCountUnknown => Loc.Get("HandInCountUnknown");

    /// <summary>{0} = high-quality items the character holds (the game's count), for an item asked for HQ.</summary>
    public static string HandInHaveHqFormat => Loc.Get("HandInHaveHqFormat");

    /// <summary>{0} = high-quality items held, {1} = held by the retainers, NQ and HQ together (Allagan Tools).</summary>
    public static string HandInHaveHqWithRetainersFormat => Loc.Get("HandInHaveHqWithRetainersFormat");

    /// <summary>{0} = held by the character, NQ and HQ together (Allagan Tools while the game read is paused).</summary>
    public static string HandInHaveMixedFormat => Loc.Get("HandInHaveMixedFormat");

    /// <summary>{0} = held by the character, {1} = held by its retainers, each NQ and HQ together (Allagan Tools).</summary>
    public static string HandInHaveMixedWithRetainersFormat => Loc.Get("HandInHaveMixedWithRetainersFormat");

    /// <summary>{0} = held by the retainers, NQ and HQ together, while the game read is paused.</summary>
    public static string HandInRetainersOnlyMixedFormat => Loc.Get("HandInRetainersOnlyMixedFormat");

    /// <summary>Under the rows when an item asked for HQ shows a count that mixes NQ and HQ.</summary>
    public static string HandInHqMixedNote => Loc.Get("HandInHqMixedNote");
    public static string HandInCountsLiveOnly => Loc.Get("HandInCountsLiveOnly");
    public static string HandInCountsUnavailable => Loc.Get("HandInCountsUnavailable");

    /// <summary>Under the rows when the game data gives no amount for some item.</summary>
    public static string HandInAmountUnknownNote => Loc.Get("HandInAmountUnknownNote");

    // ---- Hand-offs (decision 1) ----
    /// <summary>{0} = amount, {1} = item name.</summary>
    public static string HandInCraftTooltipFormat => Loc.Get("HandInCraftTooltipFormat");

    /// <summary>{0} = crafts, {1} = item name, {2} = items one craft makes.</summary>
    public static string HandInCraftYieldTooltipFormat => Loc.Get("HandInCraftYieldTooltipFormat");

    /// <summary>The Craft button's tooltip, disabled, when the character holds enough already.</summary>
    public static string HandInHaveEnough => Loc.Get("HandInHaveEnough");

    public static string HandInNeedsArtisan => Loc.Get("HandInNeedsArtisan");
    public static string HandInArtisanBusy => Loc.Get("HandInArtisanBusy");
    public static string HandInNoRecipe => Loc.Get("HandInNoRecipe");

    /// <summary>{0} = item name.</summary>
    public static string HandInSentToArtisanFormat => Loc.Get("HandInSentToArtisanFormat");

    public static string HandInArtisanFailed => Loc.Get("HandInArtisanFailed");

    /// <summary>The status bar's line while an Artisan craft Tsukimichi handed off runs (1.12.0, U4).</summary>
    public static string ArtisanCraftingStatus => Loc.Get("ArtisanCraftingStatus");

    /// <summary>The status bar's Stop for that craft.</summary>
    public static string ArtisanStopTooltip => Loc.Get("ArtisanStopTooltip");

    /// <summary>The status bar's note when that Stop could not reach Artisan.</summary>
    public static string ArtisanStopFailed => Loc.Get("ArtisanStopFailed");

    /// <summary>{0} = the chat command that will run ("/gather Maple Log").</summary>
    public static string HandInGatherTooltipFormat => Loc.Get("HandInGatherTooltipFormat");

    public static string HandInNeedsGatherBuddy => Loc.Get("HandInNeedsGatherBuddy");

    /// <summary>{0} = item name.</summary>
    public static string HandInSentToGatherBuddyFormat => Loc.Get("HandInSentToGatherBuddyFormat");

    public static string HandInGatherBuddyFailed => Loc.Get("HandInGatherBuddyFailed");

    // ---- Copy missing items ----
    public static string HandInCopyTooltip => Loc.Get("HandInCopyTooltip");
    public static string HandInCopyLinkThis => Loc.Get("HandInCopyLinkThis");
    public static string HandInCopyListThis => Loc.Get("HandInCopyListThis");
    public static string HandInCopyLinkPinned => Loc.Get("HandInCopyLinkPinned");
    public static string HandInCopyListPinned => Loc.Get("HandInCopyListPinned");
    public static string HandInCopyLinkTooltip => Loc.Get("HandInCopyLinkTooltip");
    public static string HandInCopyListTooltip => Loc.Get("HandInCopyListTooltip");
    public static string HandInNothingMissing => Loc.Get("HandInNothingMissing");
    public static string HandInCopied => Loc.Get("HandInCopied");

    // ---- Where to get it (1.19, N5; the lines themselves are Core's WhereToGet) ----
    /// <summary>The last line of a placed "Where" line's tooltip.</summary>
    public static string HandInWhereClickHint => Loc.Get("HandInWhereClickHint");

    /// <summary>Status note when the map could not be opened on a "Where" line's place.</summary>
    public static string HandInWhereFlagFailed => Loc.Get("HandInWhereFlagFailed");

    // ---- Moonlit ----
    /// <summary>The obtained line of a relic or special weapon Allagan Tools counts.</summary>
    public static string HandInOwnedPerAllagan => Loc.Get("HandInOwnedPerAllagan");

    // ---- Item hint and menu ----
    /// <summary>{0} = quest name; one line of the hover hint.</summary>
    public static string ItemsNeededForFormat => Loc.Get("ItemsNeededForFormat");

    /// <summary>{0} = quest name.</summary>
    public static string ItemsMenuNeededFormat => Loc.Get("ItemsMenuNeededFormat");

    /// <summary>{0} = number of quests; the entry opens a submenu with one line per quest.</summary>
    public static string ItemsMenuNeededManyFormat => Loc.Get("ItemsMenuNeededManyFormat");

    // ---- Settings ----
    public static string ConfigSectionHandIn => Loc.Get("ConfigSectionHandIn");
    public static string ConfigHandInAllagan => Loc.Get("ConfigHandInAllagan");
    public static string ConfigHandInAllaganHint => Loc.Get("ConfigHandInAllaganHint");
    public static string ConfigItemNeededFor => Loc.Get("ConfigItemNeededFor");
    public static string ConfigItemNeededForHint => Loc.Get("ConfigItemNeededForHint");

    public static string HandInCraftShort => Loc.Get("HandInCraftShort");
    public static string HandInGatherShort => Loc.Get("HandInGatherShort");
    public static string HandInFishShort => Loc.Get("HandInFishShort");
}
