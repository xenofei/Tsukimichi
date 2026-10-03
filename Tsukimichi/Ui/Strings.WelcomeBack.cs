using Tsukimichi.Localization;

namespace Tsukimichi.Ui;

/// <summary>
/// UI strings for "Since you were away" (P7): the card above the detail pane, its "When did you last play?" question,
/// the Characters dashboard button and the Settings › Notices slider. Constant names carry the <c>WelcomeBack</c>
/// prefix so this part of the partial class never collides with the others. Main scenario names reach these formats
/// only through the spoiler shield.
/// </summary>
static partial class Strings
{
    // ---- Card ----
    public static string WelcomeBackTitle => Loc.Get("WelcomeBackTitle");

    /// <summary>{0} = character name.</summary>
    public static string WelcomeBackTitleFormat => Loc.Get("WelcomeBackTitleFormat");

    public static string WelcomeBackClose => Loc.Get("WelcomeBackClose");
    public static string WelcomeBackDontShow => Loc.Get("WelcomeBackDontShow");
    public static string WelcomeBackDontShowTooltip => Loc.Get("WelcomeBackDontShowTooltip");
    public static string WelcomeBackReading => Loc.Get("WelcomeBackReading");
    public static string WelcomeBackShowInJournal => Loc.Get("WelcomeBackShowInJournal");

    /// <summary>{0} = whole days, {1} = the capture's date.</summary>
    public static string WelcomeBackAwayFormat => Loc.Get("WelcomeBackAwayFormat");
    public static string WelcomeBackAwayOneDayFormat => Loc.Get("WelcomeBackAwayOneDayFormat");
    public static string WelcomeBackAwayToday => Loc.Get("WelcomeBackAwayToday");

    /// <summary>{0} = the patch the capture was taken on.</summary>
    public static string WelcomeBackSinceRecordedFormat => Loc.Get("WelcomeBackSinceRecordedFormat");

    /// <summary>{0} = the newest patch among the quests the capture had done.</summary>
    public static string WelcomeBackSinceInferredFormat => Loc.Get("WelcomeBackSinceInferredFormat");

    /// <summary>{0} = the answer's series.</summary>
    public static string WelcomeBackSinceAnswerFormat => Loc.Get("WelcomeBackSinceAnswerFormat");
    public static string WelcomeBackChange => Loc.Get("WelcomeBackChange");
    public static string WelcomeBackChangeTooltip => Loc.Get("WelcomeBackChangeTooltip");

    /// <summary>{0} = the newest patch the data knows.</summary>
    public static string WelcomeBackNewestFormat => Loc.Get("WelcomeBackNewestFormat");

    // ---- Question ----
    public static string WelcomeBackAskTitle => Loc.Get("WelcomeBackAskTitle");
    public static string WelcomeBackAskBody => Loc.Get("WelcomeBackAskBody");
    public static string WelcomeBackAskPick => Loc.Get("WelcomeBackAskPick");
    public static string WelcomeBackAskNew => Loc.Get("WelcomeBackAskNew");
    public static string WelcomeBackAskNewTooltip => Loc.Get("WelcomeBackAskNewTooltip");
    public static string WelcomeBackAskShow => Loc.Get("WelcomeBackAskShow");

    /// <summary>{0} = series ("7.2"), {1} = quests it added.</summary>
    public static string WelcomeBackAskSeriesFormat => Loc.Get("WelcomeBackAskSeriesFormat");

    // ---- Journal section ----
    public static string WelcomeBackJournalTitle => Loc.Get("WelcomeBackJournalTitle");
    public static string WelcomeBackJournalNowTitle => Loc.Get("WelcomeBackJournalNowTitle");
    public static string WelcomeBackJournalNone => Loc.Get("WelcomeBackJournalNone");
    public static string WelcomeBackJournalNowNone => Loc.Get("WelcomeBackJournalNowNone");

    /// <summary>{0} = step then.</summary>
    public static string WelcomeBackStillAtFormat => Loc.Get("WelcomeBackStillAtFormat");
    public static string WelcomeBackStill => Loc.Get("WelcomeBackStill");

    /// <summary>{0} = step then, {1} = step now.</summary>
    public const string WelcomeBackMovedFormat = "{0} → {1}";
    public static string WelcomeBackMoved => Loc.Get("WelcomeBackMoved");
    public static string WelcomeBackCompleted => Loc.Get("WelcomeBackCompleted");
    public static string WelcomeBackDropped => Loc.Get("WelcomeBackDropped");
    public static string WelcomeBackInJournal => Loc.Get("WelcomeBackInJournal");

    /// <summary>{0} = rows not shown.</summary>
    public static string WelcomeBackMoreFormat => Loc.Get("WelcomeBackMoreFormat");
    public static string WelcomeBackRowTooltip => Loc.Get("WelcomeBackRowTooltip");

    // ---- Main scenario section ----
    public static string WelcomeBackMsqTitle => Loc.Get("WelcomeBackMsqTitle");

    /// <summary>{0} = expansion, {1} = next quest (through the shield), {2} = main scenario quests left, the next one included.</summary>
    public static string WelcomeBackMsqAtFormat => Loc.Get("WelcomeBackMsqAtFormat");

    /// <summary>{0} = expansion, {1} = the routes ("route A 3 of 9 · route B not started").</summary>
    public const string WelcomeBackMsqRoutesFormat = "{0} · {1}";
    public static string WelcomeBackMsqComplete => Loc.Get("WelcomeBackMsqComplete");

    /// <summary>{0} = the position then.</summary>
    public static string WelcomeBackMsqThenFormat => Loc.Get("WelcomeBackMsqThenFormat");

    /// <summary>{0} = the position now.</summary>
    public static string WelcomeBackMsqNowFormat => Loc.Get("WelcomeBackMsqNowFormat");

    /// <summary>{0} = main scenario quests completed since the capture.</summary>
    public static string WelcomeBackMsqDoneSinceFormat => Loc.Get("WelcomeBackMsqDoneSinceFormat");
    public static string WelcomeBackMsqDoneSinceOne => Loc.Get("WelcomeBackMsqDoneSinceOne");
    public static string WelcomeBackMsqShowTooltip => Loc.Get("WelcomeBackMsqShowTooltip");

    // ---- New quests section ----
    /// <summary>{0} = the patch counted from.</summary>
    public static string WelcomeBackNewTitleFormat => Loc.Get("WelcomeBackNewTitleFormat");

    /// <summary>{0} = the answer's series.</summary>
    public static string WelcomeBackNewTitleSeriesFormat => Loc.Get("WelcomeBackNewTitleSeriesFormat");

    /// <summary>{0} = all, {1} = main scenario, {2} = unlock, {3} = side.</summary>
    public static string WelcomeBackNewTotalFormat => Loc.Get("WelcomeBackNewTotalFormat");

    /// <summary>{0} = series, {1} = all, {2} = main scenario, {3} = unlock, {4} = side.</summary>
    public static string WelcomeBackNewSeriesFormat => Loc.Get("WelcomeBackNewSeriesFormat");

    /// <summary>{0} = series.</summary>
    public static string WelcomeBackNewShowTooltipFormat => Loc.Get("WelcomeBackNewShowTooltipFormat");
    public static string WelcomeBackNewNone => Loc.Get("WelcomeBackNewNone");
    public static string WelcomeBackNewUnknown => Loc.Get("WelcomeBackNewUnknown");
    public static string WelcomeBackNewUnknownPick => Loc.Get("WelcomeBackNewUnknownPick");
    public static string WelcomeBackNewNote => Loc.Get("WelcomeBackNewNote");

    // ---- Events and levels ----
    public static string WelcomeBackEventsTitle => Loc.Get("WelcomeBackEventsTitle");

    /// <summary>{0} = event, {1} = quests ready.</summary>
    public static string WelcomeBackEventReadyFormat => Loc.Get("WelcomeBackEventReadyFormat");
    public static string WelcomeBackLevelsTitle => Loc.Get("WelcomeBackLevelsTitle");

    /// <summary>{0} = job abbreviation, {1} = level then, {2} = level now.</summary>
    public const string WelcomeBackLevelFormat = "{0} {1} → {2}";

    // ---- Characters dashboard ----
    public static string WelcomeBackOpen => Loc.Get("WelcomeBackOpen");
    public static string WelcomeBackOpenTooltip => Loc.Get("WelcomeBackOpenTooltip");

    // ---- Settings › Notices ----
    public static string WelcomeBackConfigDays => Loc.Get("WelcomeBackConfigDays");
    public static string WelcomeBackConfigDaysFormat => Loc.Get("WelcomeBackConfigDaysFormat");
    public static string WelcomeBackConfigOff => Loc.Get("WelcomeBackConfigOff");
    public static string WelcomeBackConfigHint => Loc.Get("WelcomeBackConfigHint");
}
