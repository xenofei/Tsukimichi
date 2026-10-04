using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>UI strings for 1.20.0's Before Evercold card (feature plan v7, N7). English only; localization is frozen.</summary>
static partial class Strings
{
    public static string PrepTitle => Loc.Get("PrepTitle");
    /// <summary>{0} = the expected early-access date.</summary>
    public static string PrepExpectedFormat => Loc.Get("PrepExpectedFormat");
    public static string PrepExpectedTooltip => Loc.Get("PrepExpectedTooltip");
    public static string PrepSectionTooltip => Loc.Get("PrepSectionTooltip");
    public static string PrepHeaderDone => Loc.Get("PrepHeaderDone");
    /// <summary>{0} = lines left.</summary>
    public static string PrepHeaderLeftFormat => Loc.Get("PrepHeaderLeftFormat");
    public static string PrepDoneTooltip => Loc.Get("PrepDoneTooltip");
    public static string PrepLeftTooltip => Loc.Get("PrepLeftTooltip");
    public static string PrepActionSelect => Loc.Get("PrepActionSelect");
    public static string PrepActionMakeRoom => Loc.Get("PrepActionMakeRoom");

    public static string PrepHide => Loc.Get("PrepHide");
    public static string PrepHideTooltip => Loc.Get("PrepHideTooltip");
    public static string PrepHideFromTonight => Loc.Get("PrepHideFromTonight");
    public static string PrepShowOnTonight => Loc.Get("PrepShowOnTonight");
    public static string PrepShowOnTonightTooltip => Loc.Get("PrepShowOnTonightTooltip");
    /// <summary>{0} = the character's name.</summary>
    public static string PrepHiddenToastFormat => Loc.Get("PrepHiddenToastFormat");
    /// <summary>{0} = the character's name.</summary>
    public static string PrepShownToastFormat => Loc.Get("PrepShownToastFormat");

    /// <summary>{0} = the patch of the game data's last main scenario quest ("7.56").</summary>
    public static string PrepMsqTitleFormat => Loc.Get("PrepMsqTitleFormat");
    public static string PrepMsqTitle => Loc.Get("PrepMsqTitle");
    /// <summary>{0} = main scenario quests left, {1} = the next one (through the spoiler shield).</summary>
    public static string PrepMsqLeftFormat => Loc.Get("PrepMsqLeftFormat");
    /// <summary>{0} = the last quest left (through the spoiler shield).</summary>
    public static string PrepMsqLeftOneFormat => Loc.Get("PrepMsqLeftOneFormat");
    public static string PrepMsqDone => Loc.Get("PrepMsqDone");
    public static string PrepMsqTooltip => Loc.Get("PrepMsqTooltip");

    /// <summary>{0} = the level cap.</summary>
    public static string PrepJobsTitleFormat => Loc.Get("PrepJobsTitleFormat");
    /// <summary>{0} = a job or "Tank role quests", {1} = quests left.</summary>
    public static string PrepJobLeftFormat => Loc.Get("PrepJobLeftFormat");
    /// <summary>{0} = the level cap.</summary>
    public static string PrepJobsDoneFormat => Loc.Get("PrepJobsDoneFormat");
    public static string PrepJobsTooltip => Loc.Get("PrepJobsTooltip");

    public static string PrepJournalTitle => Loc.Get("PrepJournalTitle");
    /// <summary>{0} = "2 journal slots left".</summary>
    public static string PrepJournalLeftFormat => Loc.Get("PrepJournalLeftFormat");
    public static string PrepJournalTooltip => Loc.Get("PrepJournalTooltip");

    /// <summary>{0} = event, {1} = its end date.</summary>
    public static string PrepEventTitleFormat => Loc.Get("PrepEventTitleFormat");
    public static string PrepEventLeftOne => Loc.Get("PrepEventLeftOne");
    /// <summary>{0} = event quests left (in the journal or to take).</summary>
    public static string PrepEventLeftFormat => Loc.Get("PrepEventLeftFormat");
    public static string PrepEventDone => Loc.Get("PrepEventDone");
    /// <summary>{0} = event, {1} = its end and source ("announced to end Dec 31 (Lodestone)").</summary>
    public static string PrepEventTooltipFormat => Loc.Get("PrepEventTooltipFormat");
}
