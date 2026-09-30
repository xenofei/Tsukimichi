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
    public const string WelcomeBackTitle = "Since you were away";

    /// <summary>{0} = character name.</summary>
    public const string WelcomeBackTitleFormat = "Since you were away · {0}";

    public const string WelcomeBackClose = "Close";
    public const string WelcomeBackDontShow = "Don't show again";
    public const string WelcomeBackDontShowTooltip = "Never open this on its own for this character. Since you were away… on the Characters tab still opens it; Settings › Notices turns it off for every character.";
    public const string WelcomeBackReading = "Reading your last capture…";
    public const string WelcomeBackShowInJournal = "Show in Journal";

    /// <summary>{0} = whole days, {1} = the capture's date.</summary>
    public const string WelcomeBackAwayFormat = "Last capture {0:N0} days ago ({1:yyyy-MM-dd}).";
    public const string WelcomeBackAwayOneDayFormat = "Last capture 1 day ago ({0:yyyy-MM-dd}).";
    public const string WelcomeBackAwayToday = "Last capture earlier today.";

    /// <summary>{0} = the patch the capture was taken on.</summary>
    public const string WelcomeBackSinceRecordedFormat = "It was taken on patch {0}.";

    /// <summary>{0} = the newest patch among the quests the capture had done.</summary>
    public const string WelcomeBackSinceInferredFormat = "It was taken on patch {0} or later (the newest content it had done).";

    /// <summary>{0} = the answer's series.</summary>
    public const string WelcomeBackSinceAnswerFormat = "You last played in {0}x.";
    public const string WelcomeBackChange = "Change";
    public const string WelcomeBackChangeTooltip = "Pick another patch; the answer is kept for this character.";

    /// <summary>{0} = the newest patch the data knows.</summary>
    public const string WelcomeBackNewestFormat = "The game is now at {0}.";

    // ---- Question ----
    public const string WelcomeBackAskTitle = "When did you last play?";
    public const string WelcomeBackAskBody = "Tsukimichi has no earlier capture of this character to compare with. Pick the patch you last played and it lists what the game added since; the answer is kept for this character.";
    public const string WelcomeBackAskPick = "Pick a patch";
    public const string WelcomeBackAskNew = "I'm new";
    public const string WelcomeBackAskNewTooltip = "Nothing to catch up on: the card closes and does not ask again for this character.";
    public const string WelcomeBackAskShow = "Show what's new";

    /// <summary>{0} = series ("7.2"), {1} = quests it added.</summary>
    public const string WelcomeBackAskSeriesFormat = "{0}x  ({1:N0} quests)";

    // ---- Journal section ----
    public const string WelcomeBackJournalTitle = "In your journal then";
    public const string WelcomeBackJournalNowTitle = "In your journal";
    public const string WelcomeBackJournalNone = "Nothing was in your journal.";
    public const string WelcomeBackJournalNowNone = "Nothing is in your journal.";

    /// <summary>{0} = step then.</summary>
    public const string WelcomeBackStillAtFormat = "still at {0}";
    public const string WelcomeBackStill = "still in your journal";

    /// <summary>{0} = step then, {1} = step now.</summary>
    public const string WelcomeBackMovedFormat = "{0} → {1}";
    public const string WelcomeBackMoved = "moved on";
    public const string WelcomeBackCompleted = "completed since";
    public const string WelcomeBackDropped = "no longer in your journal";
    public const string WelcomeBackInJournal = "in your journal";

    /// <summary>{0} = rows not shown.</summary>
    public const string WelcomeBackMoreFormat = "…and {0:N0} more";
    public const string WelcomeBackRowTooltip = "Click to show it in the Journal.";

    // ---- Main scenario section ----
    public const string WelcomeBackMsqTitle = "Main scenario";

    /// <summary>{0} = expansion, {1} = next quest (through the shield), {2} = done, {3} = total.</summary>
    public const string WelcomeBackMsqAtFormat = "{0} · next: {1} ({2:N0} of {3:N0})";

    /// <summary>{0} = expansion, {1} = the routes ("route A 3 of 9 · route B not started").</summary>
    public const string WelcomeBackMsqRoutesFormat = "{0} · {1}";
    public const string WelcomeBackMsqComplete = "complete";

    /// <summary>{0} = the position then.</summary>
    public const string WelcomeBackMsqThenFormat = "Then: {0}";

    /// <summary>{0} = the position now.</summary>
    public const string WelcomeBackMsqNowFormat = "Now: {0}";

    /// <summary>{0} = main scenario quests completed since the capture.</summary>
    public const string WelcomeBackMsqDoneSinceFormat = "{0:N0} main scenario quests completed since.";
    public const string WelcomeBackMsqDoneSinceOne = "1 main scenario quest completed since.";
    public const string WelcomeBackMsqShowTooltip = "Select the next main scenario quest in the Journal.";

    // ---- New quests section ----
    /// <summary>{0} = the patch counted from.</summary>
    public const string WelcomeBackNewTitleFormat = "Added since {0}";

    /// <summary>{0} = the answer's series.</summary>
    public const string WelcomeBackNewTitleSeriesFormat = "Added after {0}x";

    /// <summary>{0} = all, {1} = main scenario, {2} = unlock, {3} = side.</summary>
    public const string WelcomeBackNewTotalFormat = "{0:N0} quests: {1:N0} main scenario, {2:N0} unlock, {3:N0} side.";

    /// <summary>{0} = series, {1} = all, {2} = main scenario, {3} = unlock, {4} = side.</summary>
    public const string WelcomeBackNewSeriesFormat = "{0}x · {1:N0} ({2:N0} main scenario, {3:N0} unlock, {4:N0} side)";

    /// <summary>{0} = series.</summary>
    public const string WelcomeBackNewShowTooltipFormat = "Open the Journal with Added in set to {0}x.";
    public const string WelcomeBackNewNone = "Nothing was added since then.";
    public const string WelcomeBackNewUnknown = "Tsukimichi cannot tell which patch this capture was taken on, so it cannot say what is new.";
    public const string WelcomeBackNewUnknownPick = "Tell it when you last played";
    public const string WelcomeBackNewNote = "Seasonal event quests return every year and are left out here; the ones running now are listed below.";

    // ---- Events and levels ----
    public const string WelcomeBackEventsTitle = "Events running now";

    /// <summary>{0} = event, {1} = quests ready.</summary>
    public const string WelcomeBackEventReadyFormat = "{0} · {1:N0} ready";
    public const string WelcomeBackLevelsTitle = "Levels since";

    /// <summary>{0} = job abbreviation, {1} = level then, {2} = level now.</summary>
    public const string WelcomeBackLevelFormat = "{0} {1} → {2}";

    // ---- Characters dashboard ----
    public const string WelcomeBackOpen = "Since you were away…";
    public const string WelcomeBackOpenTooltip = "What this character was doing, where the main scenario stands and what the game added since: from the capture before your last long break, or since the patch you say you last played.";

    // ---- Settings › Notices ----
    public const string WelcomeBackConfigDays = "Show \"Since you were away\" after";
    public const string WelcomeBackConfigDaysFormat = "%d days";
    public const string WelcomeBackConfigOff = "off";
    public const string WelcomeBackConfigHint = "At a login after this many days away, a card above the detail pane says what you were doing, where the main scenario stands and what the game added since. It waits until every character's last capture is this old, so an alt left idle while you play another stays quiet, and it opens at most once per session. 0 turns it off; Since you were away… on the Characters tab opens it at any time.";
}
