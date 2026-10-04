using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for 1.21.0 in My blues (feature plan v7): the Do first sort, its tiers and Set aside (P4, <c>Blues</c>
/// prefix), and Your story on one page with the pace line (N9, <c>Story</c> prefix).
/// </summary>
static partial class Strings
{
    // ---- The switch and the sort ----
    public static string BluesViewClear => Loc.Get("BluesViewClear");
    public static string BluesViewStory => Loc.Get("BluesViewStory");
    public static string BluesViewClearTooltip => Loc.Get("BluesViewClearTooltip");
    public static string BluesViewStoryTooltip => Loc.Get("BluesViewStoryTooltip");
    public static string BluesSort => Loc.Get("BluesSort");
    public static string BluesSortStory => Loc.Get("BluesSortStory");
    public static string BluesSortDoFirst => Loc.Get("BluesSortDoFirst");
    public static string BluesSortStoryTooltip => Loc.Get("BluesSortStoryTooltip");
    public static string BluesSortDoFirstTooltip => Loc.Get("BluesSortDoFirstTooltip");

    // ---- Tiers ----
    public static string BluesTierWhyStory => Loc.Get("BluesTierWhyStory");
    public static string BluesTierWhyContent => Loc.Get("BluesTierWhyContent");
    public static string BluesTierWhySystems => Loc.Get("BluesTierWhySystems");
    public static string BluesTierWhyHighEnd => Loc.Get("BluesTierWhyHighEnd");
    public static string BluesTierWhyAnotherJob => Loc.Get("BluesTierWhyAnotherJob");

    /// <summary>The five tiers explained, one per line (the tier word's hover).</summary>
    public static string BluesTierTooltip => Loc.Get("BluesTierTooltip");

    /// <summary>{0} = quests left.</summary>
    public static string BluesTierLeftFormat => Loc.Get("BluesTierLeftFormat");

    /// <summary>{0} = the tier word, {1} = quests left.</summary>
    public static string BluesTierFoldedFormat => Loc.Get("BluesTierFoldedFormat");
    public static string BluesTierFoldedTooltip => Loc.Get("BluesTierFoldedTooltip");

    /// <summary>{0} = rows not shown.</summary>
    public static string BluesMoreFormat => Loc.Get("BluesMoreFormat");
    public static string BluesFewer => Loc.Get("BluesFewer");

    /// <summary>{0} = the main scenario quest that needs it (through the shield).</summary>
    public static string BluesNeedsItFormat => Loc.Get("BluesNeedsItFormat");

    /// <summary>{0} = level text ("Lv 50"), {1} = the tier word.</summary>
    public static string BluesLevelTierFormat => Loc.Get("BluesLevelTierFormat");

    // ---- Set aside ----
    public static string BluesSetAsideForLater => Loc.Get("BluesSetAsideForLater");
    public static string BluesSetAsideForLaterTooltip => Loc.Get("BluesSetAsideForLaterTooltip");
    public static string BluesNotForMe => Loc.Get("BluesNotForMe");
    public static string BluesNotForMeTooltip => Loc.Get("BluesNotForMeTooltip");
    public static string BluesSetAsideLine => Loc.Get("BluesSetAsideLine");
    public static string BluesNotForMeLine => Loc.Get("BluesNotForMeLine");
    public static string BluesBroughtBackLine => Loc.Get("BluesBroughtBackLine");
    public static string BluesUndo => Loc.Get("BluesUndo");
    public static string BluesUndoTooltip => Loc.Get("BluesUndoTooltip");
    public static string BluesBringBack => Loc.Get("BluesBringBack");
    public static string BluesBringBackTooltip => Loc.Get("BluesBringBackTooltip");
    public static string BluesSetAsideChip => Loc.Get("BluesSetAsideChip");
    public static string BluesSetAsideChipTooltip => Loc.Get("BluesSetAsideChipTooltip");

    /// <summary>{0} = quests set aside.</summary>
    public static string BluesSetAsideLinkFormat => Loc.Get("BluesSetAsideLinkFormat");
    public static string BluesSetAsideLinkTooltip => Loc.Get("BluesSetAsideLinkTooltip");

    /// <summary>{0} = quests left, {1} = Ready.</summary>
    public static string BluesSummaryFormat => Loc.Get("BluesSummaryFormat");

    /// <summary>{0} = quests set aside.</summary>
    public static string BluesSetAsideSummaryFormat => Loc.Get("BluesSetAsideSummaryFormat");
    public static string BluesSetAsideEmpty => Loc.Get("BluesSetAsideEmpty");
    public static string BluesSetAsideEmptyBody => Loc.Get("BluesSetAsideEmptyBody");

    /// <summary>{0} = quests in the group.</summary>
    public static string BluesSetAsideGroupFormat => Loc.Get("BluesSetAsideGroupFormat");
    public static string BluesGroupMenuTooltip => Loc.Get("BluesGroupMenuTooltip");

    /// <summary>{0} = quests in the group.</summary>
    public static string BluesSetAsideConfirmTitleFormat => Loc.Get("BluesSetAsideConfirmTitleFormat");

    /// <summary>{0} = the group ("Mor Dhona", "Another job or society").</summary>
    public static string BluesSetAsideConfirmBodyFormat => Loc.Get("BluesSetAsideConfirmBodyFormat");

    /// <summary>{0} = quests in the group.</summary>
    public static string BluesSetAsideConfirmButtonFormat => Loc.Get("BluesSetAsideConfirmButtonFormat");

    /// <summary>{0} = the quest.</summary>
    public static string BluesSetAsideToastFormat => Loc.Get("BluesSetAsideToastFormat");

    /// <summary>{0} = the quest.</summary>
    public static string BluesNotForMeToastFormat => Loc.Get("BluesNotForMeToastFormat");

    /// <summary>{0} = quests set aside.</summary>
    public static string BluesSetAsideGroupToastFormat => Loc.Get("BluesSetAsideGroupToastFormat");

    /// <summary>{0} = the quest.</summary>
    public static string BluesBroughtBackToastFormat => Loc.Get("BluesBroughtBackToastFormat");

    // ---- Your story (N9) ----
    public static string StoryTitle => Loc.Get("StoryTitle");
    public static string StoryLoading => Loc.Get("StoryLoading");

    /// <summary>{0} = main scenario quests left, {1} = lowest level, {2} = highest level, {3} = evenings.</summary>
    public static string StoryPaceFormat => Loc.Get("StoryPaceFormat");

    /// <summary>{0} = main scenario quests left, {1} = lowest level, {2} = highest level.</summary>
    public static string StoryPaceOneEveningFormat => Loc.Get("StoryPaceOneEveningFormat");

    /// <summary>{0} = main scenario quests left, {1} = lowest level, {2} = highest level, {3} = the quests needed, {4} = dated quests.</summary>
    public static string StoryPaceThinFormat => Loc.Get("StoryPaceThinFormat");

    /// <summary>{0} = main scenario quests left, {1} = lowest level, {2} = highest level.</summary>
    public static string StoryPaceNoCharacterFormat => Loc.Get("StoryPaceNoCharacterFormat");
    public static string StoryPaceTooltip => Loc.Get("StoryPaceTooltip");
    public static string StoryCaughtUp => Loc.Get("StoryCaughtUp");

    /// <summary>{0} = band name ("A Realm Reborn", "2.1 – 2.5"), {1} = its second part ("2.0", "Seventh Astral Era").</summary>
    public static string StoryBandTitleFormat => Loc.Get("StoryBandTitleFormat");

    /// <summary>{0} = main scenario quests left, {1} = the next one.</summary>
    public static string StoryBandLeftFormat => Loc.Get("StoryBandLeftFormat");
    public static string StoryBandDone => Loc.Get("StoryBandDone");

    /// <summary>{0} = optional lines with quests left.</summary>
    public static string StoryBandDoneLinesFormat => Loc.Get("StoryBandDoneLinesFormat");
    public static string StoryBandDoneTooltip => Loc.Get("StoryBandDoneTooltip");

    /// <summary>{0} = main scenario quests, {1} = optional lines.</summary>
    public static string StoryBandPastFormat => Loc.Get("StoryBandPastFormat");
    public static string StoryNamesHidden => Loc.Get("StoryNamesHidden");

    /// <summary>{0} = the main scenario quest (through the shield).</summary>
    public static string StoryOpensAfterFormat => Loc.Get("StoryOpensAfterFormat");
    public static string StoryOpenFromStart => Loc.Get("StoryOpenFromStart");

    /// <summary>{0} = quests left, {1} = the next one.</summary>
    public static string StoryLineLeftFormat => Loc.Get("StoryLineLeftFormat");

    /// <summary>{0} = quests left.</summary>
    public static string StoryLineInJournalFormat => Loc.Get("StoryLineInJournalFormat");
    public static string StoryLineDone => Loc.Get("StoryLineDone");
    public static string StoryHiddenLineOne => Loc.Get("StoryHiddenLineOne");

    /// <summary>{0} = optional lines.</summary>
    public static string StoryHiddenLinesFormat => Loc.Get("StoryHiddenLinesFormat");
    public static string StoryCopyTooltip => Loc.Get("StoryCopyTooltip");
    public static string StoryCopied => Loc.Get("StoryCopied");
}
