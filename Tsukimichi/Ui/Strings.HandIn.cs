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
    public static string HandInCountsLiveOnly => Loc.Get("HandInCountsLiveOnly");
    public static string HandInCountsUnavailable => Loc.Get("HandInCountsUnavailable");

    /// <summary>Under the rows when the game data gives no amount for some item.</summary>
    public static string HandInAmountUnknownNote => Loc.Get("HandInAmountUnknownNote");

    // ---- Hand-offs (decision 1) ----
    /// <summary>{0} = amount, {1} = item name.</summary>
    public static string HandInCraftTooltipFormat => Loc.Get("HandInCraftTooltipFormat");

    public static string HandInNeedsArtisan => Loc.Get("HandInNeedsArtisan");
    public static string HandInArtisanBusy => Loc.Get("HandInArtisanBusy");
    public static string HandInNoRecipe => Loc.Get("HandInNoRecipe");

    /// <summary>{0} = item name.</summary>
    public static string HandInSentToArtisanFormat => Loc.Get("HandInSentToArtisanFormat");

    public static string HandInArtisanFailed => Loc.Get("HandInArtisanFailed");

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

    /// <summary>{0} = Allagan Tools' state, {1} = Artisan's, {2} = GatherBuddy's (each "loaded" or "not loaded").</summary>
    public static string ConfigHandInPluginsFormat => Loc.Get("ConfigHandInPluginsFormat");

    public static string ConfigHandInLoaded => Loc.Get("ConfigHandInLoaded");
    public static string ConfigHandInNotLoaded => Loc.Get("ConfigHandInNotLoaded");
}
