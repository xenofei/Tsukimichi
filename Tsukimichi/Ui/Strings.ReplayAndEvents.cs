using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for 1.19.0's New Game+ session (C4), allied society allowances (C5) and seasonal events ending soon (C10)
/// (feature plan v7; spec-1.19). English only; localization is frozen.
/// </summary>
static partial class Strings
{
    // ---- C4: New Game+ ----
    /// <summary>{0} = chapter ("Shadowbringers - Part 2"), {1} = the quest's place in it, {2} = the chapter's quests.</summary>
    public static string NewGamePlusStatusFormat => Loc.Get("NewGamePlusStatusFormat");
    public static string NewGamePlusStatus => Loc.Get("NewGamePlusStatus");
    public static string NewGamePlusKept => Loc.Get("NewGamePlusKept");
    public static string NewGamePlusTooltip => Loc.Get("NewGamePlusTooltip");
    public static string NewGamePlusTooltipReplay => Loc.Get("NewGamePlusTooltipReplay");
    public static string NewGamePlusEnd => Loc.Get("NewGamePlusEnd");
    public static string NewGamePlusEndTooltip => Loc.Get("NewGamePlusEndTooltip");
    public static string NewGamePlusEndBody => Loc.Get("NewGamePlusEndBody");
    public static string NewGamePlusEndConfirm => Loc.Get("NewGamePlusEndConfirm");
    public static string NewGamePlusEndKeep => Loc.Get("NewGamePlusEndKeep");
    public static string ReplayingChip => Loc.Get("ReplayingChip");
    public static string ReplayingChipTooltip => Loc.Get("ReplayingChipTooltip");

    // ---- C5: allied societies ----
    /// <summary>{0} = the daily's name (through the spoiler shield).</summary>
    public static string AlliedCarriedFormat => Loc.Get("AlliedCarriedFormat");
    public static string AlliedCarriedTooltip => Loc.Get("AlliedCarriedTooltip");
    public static string AlliedRankUpReady => Loc.Get("AlliedRankUpReady");
    public static string AlliedRankUpReadyTooltip => Loc.Get("AlliedRankUpReadyTooltip");
    /// <summary>{0} = how long ago the character was last captured ("2 d ago").</summary>
    public static string AlliedStoredHoldsFormat => Loc.Get("AlliedStoredHoldsFormat");
    /// <summary>{0} = allowances projected, {1} = how long ago the character was last captured.</summary>
    public static string AlliedStoredProjectedFormat => Loc.Get("AlliedStoredProjectedFormat");
    /// <summary>{0} = the stored character's name.</summary>
    public static string AlliedStoredAltFormat => Loc.Get("AlliedStoredAltFormat");
    public static string AlliedFlag => Loc.Get("AlliedFlag");
    public static string AlliedFlagTooltip => Loc.Get("AlliedFlagTooltip");

    // ---- C10: events ending soon ----
    public static string EventEndsToday => Loc.Get("EventEndsToday");
    public static string EventEndsTomorrow => Loc.Get("EventEndsTomorrow");
    /// <summary>{0} = days left.</summary>
    public static string EventEndsInDaysFormat => Loc.Get("EventEndsInDaysFormat");
    public static string EventEndsChipTooltip => Loc.Get("EventEndsChipTooltip");
    /// <summary>{0} = event, {1} = days left.</summary>
    public static string EventCardTitleFormat => Loc.Get("EventCardTitleFormat");
    /// <summary>{0} = event.</summary>
    public static string EventCardTitleTomorrowFormat => Loc.Get("EventCardTitleTomorrowFormat");
    /// <summary>{0} = event, {1} = the local time it ends.</summary>
    public static string EventCardTitleTodayFormat => Loc.Get("EventCardTitleTodayFormat");
    public static string EventCardJournalOne => Loc.Get("EventCardJournalOne");
    /// <summary>{0} = quests in the journal.</summary>
    public static string EventCardJournalFormat => Loc.Get("EventCardJournalFormat");
    public static string EventCardLeftOne => Loc.Get("EventCardLeftOne");
    /// <summary>{0} = quests left to take.</summary>
    public static string EventCardLeftFormat => Loc.Get("EventCardLeftFormat");
    public static string EventCardRewardsOne => Loc.Get("EventCardRewardsOne");
    /// <summary>{0} = rewards the character lacks.</summary>
    public static string EventCardRewardsFormat => Loc.Get("EventCardRewardsFormat");
    /// <summary>{0} = what is left, joined by <see cref="EventCardListSeparator"/>.</summary>
    public static string EventCardWhyFormat => Loc.Get("EventCardWhyFormat");
    public static string EventCardListSeparator => Loc.Get("EventCardListSeparator");
    /// <summary>{0} = the local date, {1} = the local time.</summary>
    public static string EventCardEndsFormat => Loc.Get("EventCardEndsFormat");
    public static string EventCardYouEntered => Loc.Get("EventCardYouEntered");
    /// <summary>{0} = month name.</summary>
    public static string EventCardUsuallyFormat => Loc.Get("EventCardUsuallyFormat");
    public static string EventCardShow => Loc.Get("EventCardShow");
    public static string EventCardShowTooltip => Loc.Get("EventCardShowTooltip");

    /// <summary>The ending-soon card's date: "3 Nov".</summary>
    public const string EventCardDateFormat = "d MMM";

    /// <summary>{0} = the card's title, {1} = what is left.</summary>
    public static string EventChatFormat => Loc.Get("EventChatFormat");
    public static string EventWarnConfig => Loc.Get("EventWarnConfig");
    public static string EventWarnConfigHint => Loc.Get("EventWarnConfigHint");
    public static string EventWarnChatConfig => Loc.Get("EventWarnChatConfig");
    public static string EventWarnChatConfigHint => Loc.Get("EventWarnChatConfigHint");
    public static string EventWarnOffReason => Loc.Get("EventWarnOffReason");

    // ---- C10: the Seasonal events list ----
    public static string SeasonalEndNotAnnounced => Loc.Get("SeasonalEndNotAnnounced");
    public static string SeasonalEnteredTooltip => Loc.Get("SeasonalEnteredTooltip");
    public static string SeasonalSetEndDate => Loc.Get("SeasonalSetEndDate");
    public static string SeasonalChangeEndDate => Loc.Get("SeasonalChangeEndDate");
    public static string SeasonalSetEndDateTooltip => Loc.Get("SeasonalSetEndDateTooltip");
    public static string SeasonalEndDateLabel => Loc.Get("SeasonalEndDateLabel");
    public static string SeasonalEndDateHint => Loc.Get("SeasonalEndDateHint");
    public static string SeasonalEndDateInvalid => Loc.Get("SeasonalEndDateInvalid");
    public static string SeasonalEndDateSave => Loc.Get("SeasonalEndDateSave");
    public static string SeasonalEndDateRemove => Loc.Get("SeasonalEndDateRemove");
    public static string SeasonalEndDateCancel => Loc.Get("SeasonalEndDateCancel");
    public static string SeasonalCalendarLabel => Loc.Get("SeasonalCalendarLabel");
    public static string SeasonalCalendarTooltip => Loc.Get("SeasonalCalendarTooltip");
    /// <summary>{0} = month name, {1} = year.</summary>
    public static string SeasonalEndedFormat => Loc.Get("SeasonalEndedFormat");
    /// <summary>{0} = month name, {1} = year.</summary>
    public static string SeasonalUsuallyFormat => Loc.Get("SeasonalUsuallyFormat");
    /// <summary>{0} = month name.</summary>
    public static string SeasonalUsuallyOnlyFormat => Loc.Get("SeasonalUsuallyOnlyFormat");
    public static string SeasonalRunsTooltipHeading => Loc.Get("SeasonalRunsTooltipHeading");

    /// <summary>{0} = start, {1} = end, each <see cref="SeasonalRunDateFormat"/> (the announcement's dates, UTC).</summary>
    public const string SeasonalRunFormat = "{0} – {1}";

    /// <summary>A dated run's day: "12 Aug 2025".</summary>
    public const string SeasonalRunDateFormat = "d MMM yyyy";
}
